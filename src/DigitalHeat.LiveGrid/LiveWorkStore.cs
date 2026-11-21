using System.IO.Enumeration;

/// <summary>Physical task files, local or on the peer laptop. A task is never scheduled from a mirror.</summary>
sealed class LiveWorkStore(string root, DemoNodeConfig[] nodes, PeerClient? peer)
{
    private readonly string[] ids = nodes.Select(n => n.Id).ToArray();
    private readonly Dictionary<string, (int From, int To)> pendingTransfers = [];
    private bool IsPeer(int node) => nodes[node].Device == "peer";
    public bool IsOnline(int node) => !IsPeer(node) || peer?.Online == true;
    private string Dir(string kind, int node) => Path.Combine(root, kind, ids[node]);
    private string DoneDir => Path.Combine(root, "done");
    private string OutboxDir(int node) => Dir("outbox", node);

    public string[] List(string kind, int node, string pattern = "*.json")
    {
        if (IsPeer(node))
        {
            if (!IsOnline(node)) return [];
            try { return peer!.List(kind, ids[node]).Where(n => FileSystemName.MatchesSimpleExpression(pattern, n)).ToArray(); }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return []; }
        }
        string path = kind == "done" ? DoneDir : Dir(kind, node);
        if (!Directory.Exists(path)) return [];
        return Directory.GetFiles(path, pattern, kind == "running" ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName).Where(n => n is not null).Select(n => n!).ToArray();
    }

    private string[] StrictList(string kind, int node, string pattern) => IsPeer(node)
        ? peer!.List(kind, ids[node]).Where(n => FileSystemName.MatchesSimpleExpression(pattern, n)).ToArray()
        : List(kind, node, pattern);

    public string Read(string kind, int node, string name)
    {
        if (IsPeer(node)) return (peer ?? throw new InvalidOperationException("Peer is not configured."))
            .Read(kind, ids[node], name);
        string path = kind == "done" ? Path.Combine(DoneDir, name) : Path.Combine(Dir(kind, node), name);
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(fs);
        return reader.ReadToEnd();
    }

    public void Put(int node, string name, string content)
    {
        if (IsPeer(node)) { (peer ?? throw new InvalidOperationException("Peer is not configured.")).Put(ids[node], name, content); return; }
        string path = Path.Combine(Dir("queue", node), name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) throw new IOException($"Queue file already exists: {name}");
        File.WriteAllText(path + ".tmp", content);
        File.Move(path + ".tmp", path);
    }

    public bool Delete(string kind, int node, string name)
    {
        if (IsPeer(node))
        {
            if (!IsOnline(node)) return false;
            try { return peer!.Delete(kind, ids[node], name); }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return false; }
        }
        string path = kind == "done" ? Path.Combine(DoneDir, name) : Path.Combine(Dir(kind, node), name);
        try { File.Delete(path); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    public bool MoveQueue(int from, int to, string name)
    {
        if (pendingTransfers.ContainsKey(name) || !IsOnline(from) || !IsOnline(to)) return false;
        if (IsPeer(from) && IsPeer(to))
        {
            try { return peer!.Move(ids[from], ids[to], name); }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return false; }
        }
        if (!IsPeer(from) && !IsPeer(to))
        {
            try { File.Move(Path.Combine(Dir("queue", from), name), Path.Combine(Dir("queue", to), name)); return true; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
        }

        // An atomic claim to outbox wins or loses the race against the worker. Only waiting files move.
        string? content;
        try { content = IsPeer(from) ? peer!.Take(ids[from], name) : TakeLocal(from, name); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            pendingTransfers[name] = (from, to); // take may have succeeded before the reply was lost
            return false;
        }
        if (content is null) return false;
        try
        {
            Put(to, name, content);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            pendingTransfers[name] = (from, to); // destination may already own it: never restore blindly
            return false;
        }
        // Once the destination acknowledged the queue file, an outbox ack failure cannot duplicate work.
        try { if (IsPeer(from)) peer!.Ack(ids[from], name); else File.Delete(Path.Combine(OutboxDir(from), name)); }
        catch (Exception e)
        {
            pendingTransfers[name] = (from, to);
            Console.Error.WriteLine($"Outbox cleanup pending for {name}: {e.Message}");
        }
        return true;
    }

    /// <summary>Resolve uncertain HTTP outcomes from physical files before another move can use the task.</summary>
    public void RecoverPending()
    {
        foreach (var (name, transfer) in pendingTransfers.ToArray())
        {
            var (from, to) = transfer;
            if (!IsOnline(from) || !IsOnline(to)) continue;
            try
            {
                if (!StrictList("outbox", from, name).Contains(name))
                {
                    // The take never happened, or an ack already completed.
                    pendingTransfers.Remove(name);
                    continue;
                }
                bool destinationOwns = new[] { "queue", "running", "done" }
                    .Any(kind => StrictList(kind, to, name).Contains(name));
                if (destinationOwns)
                {
                    if (IsPeer(from)) peer!.Ack(ids[from], name);
                    else File.Delete(Path.Combine(OutboxDir(from), name));
                }
                else if (IsPeer(from)) peer!.Restore(ids[from], name);
                else RestoreLocal(from, name);
                pendingTransfers.Remove(name);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
            {
                // The outbox is inert. Retry after the next heartbeat rather than risk a duplicate.
                Console.Error.WriteLine($"Transfer {name} still pending: {e.Message}");
            }
        }
    }

    private string? TakeLocal(int node, string name)
    {
        string from = Path.Combine(Dir("queue", node), name), to = Path.Combine(OutboxDir(node), name);
        Directory.CreateDirectory(OutboxDir(node));
        try { File.Move(from, to); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
        return File.ReadAllText(to);
    }
    private void RestoreLocal(int node, string name) =>
        File.Move(Path.Combine(OutboxDir(node), name), Path.Combine(Dir("queue", node), name));

    public (int Node, string Name)[] DoneFiles(string pattern)
    {
        var locations = new List<int>();
        int local = Array.FindIndex(nodes, n => n.Device != "peer");
        int remote = Array.FindIndex(nodes, n => n.Device == "peer");
        if (local >= 0) locations.Add(local);
        if (remote >= 0) locations.Add(remote);
        return locations.SelectMany(i => List("done", i, pattern).Select(name => (i, name))).ToArray();
    }

    public void Reset()
    {
        foreach (int i in Enumerable.Range(0, nodes.Length).Where(i => !IsPeer(i)))
            foreach (string kind in new[] { "queue", "running", "progress", "outbox" })
            {
                Directory.CreateDirectory(Dir(kind, i));
                foreach (string f in Directory.GetFiles(Dir(kind, i), "*.json", SearchOption.AllDirectories))
                    File.Delete(f);
            }
        Directory.CreateDirectory(DoneDir);
        foreach (string f in Directory.GetFiles(DoneDir, "*.json")) File.Delete(f);
        if (nodes.Any(n => n.Device == "peer")) (peer ?? throw new InvalidOperationException("Peer is not configured.")).Reset();
    }
}
