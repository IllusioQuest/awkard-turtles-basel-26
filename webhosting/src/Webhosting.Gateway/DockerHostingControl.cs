using System.Net.Sockets;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Webhosting.Gateway;

/// <summary>Trusted local lab adapter; Docker socket authority exceeds these label checks.</summary>
public sealed class DockerHostingControl : IHostingReplicaControl, IDisposable
{
    private readonly HttpClient engine;
    private readonly HttpClient probe;
    private readonly string project;
    public DockerHostingControl(string project, TimeSpan timeout)
    {
        this.project = project;
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, token) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try { await socket.ConnectAsync(new UnixDomainSocketEndPoint("/var/run/docker.sock"), token); }
                catch { socket.Dispose(); throw; }
                return new NetworkStream(socket, ownsSocket: true);
            }
        };
        engine = new HttpClient(handler) { BaseAddress = new Uri("http://docker/"), Timeout = timeout };
        probe = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = timeout };
    }

    public async Task<bool> ReadyAsync(HostingNode node, CancellationToken token)
    {
        try
        {
            using var response = await probe.GetAsync(new Uri(new Uri(node.Address), "health"), token);
            if (!response.IsSuccessStatusCode) return false;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            return json.RootElement.GetProperty("node_id").GetString() == node.Id
                && json.RootElement.GetProperty("component").GetString() == node.Component;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or KeyNotFoundException
            || e is OperationCanceledException && !token.IsCancellationRequested) { return false; }
    }

    private async Task<string> EngineId(HostingNode node, CancellationToken token)
    {
        using var list = JsonDocument.Parse(await engine.GetStringAsync("containers/json", token));
        var matches = list.RootElement.EnumerateArray().Where(item =>
        {
            var labels = item.GetProperty("Labels");
            return labels.TryGetProperty("com.docker.compose.project", out var p) && p.GetString() == project
                && labels.TryGetProperty("com.docker.compose.service", out var s) && s.GetString() == node.Id
                && labels.TryGetProperty("webhosting.component", out var c) && c.GetString() == node.Component;
        }).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException($"Expected one running lab engine: {node.Id}");
        string id = matches[0].GetProperty("Id").GetString()!;
        if (id.Length != 64 || !id.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid engine ID");
        return id;
    }

    private async Task<JsonDocument> Inspect(string id, HostingNode node, CancellationToken token)
    {
        var json = JsonDocument.Parse(await Exec(id, ["docker", "inspect", "webhosting-component"], token));
        var labels = json.RootElement[0].GetProperty("Config").GetProperty("Labels");
        if (!labels.TryGetProperty("webhosting.component", out var c) || c.GetString() != node.Component)
        { json.Dispose(); throw new InvalidDataException("Inner component label mismatch"); }
        return json;
    }

    public async Task ResumeAsync(HostingNode node, CancellationToken token)
    {
        string id = await EngineId(node, token);
        using var before = await Inspect(id, node, token);
        var state = before.RootElement[0].GetProperty("State");
        if (state.GetProperty("Paused").GetBoolean())
            await Exec(id, ["docker", "unpause", "webhosting-component"], token);
        else if (!state.GetProperty("Running").GetBoolean())
        {
            string ipv6 = await Exec(id, ["docker", "network", "inspect", "bridge", "--format", "{{.EnableIPv6}}"], token);
            if (ipv6.Trim() != "false") throw new InvalidDataException("Cannot start with unsupported IPv6 bridge");
            await Exec(id, ["sh", "-c", $"export APP_PORT={node.ContainerPort}; tr -d '\\r' < /root/app-firewall.sh | sh && docker start webhosting-component"], token);
        }
        using var after = await Inspect(id, node, token);
        var actual = after.RootElement[0].GetProperty("State");
        if (!actual.GetProperty("Running").GetBoolean() || actual.GetProperty("Paused").GetBoolean())
            throw new InvalidOperationException("Replica did not resume");
    }

    public async Task PauseAsync(HostingNode node, CancellationToken token)
    {
        string id = await EngineId(node, token);
        using var before = await Inspect(id, node, token);
        if (!before.RootElement[0].GetProperty("State").GetProperty("Paused").GetBoolean())
            await Exec(id, ["docker", "pause", "webhosting-component"], token);
        using var after = await Inspect(id, node, token);
        if (!after.RootElement[0].GetProperty("State").GetProperty("Paused").GetBoolean())
            throw new InvalidOperationException("Replica pause not confirmed");
    }

    private async Task<string> Exec(string id, string[] command, CancellationToken token)
    {
        using var created = await engine.PostAsJsonAsync($"containers/{id}/exec",
            new { AttachStdout = true, AttachStderr = true, Cmd = command }, token);
        created.EnsureSuccessStatusCode();
        using var createJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync(token));
        string execId = createJson.RootElement.GetProperty("Id").GetString()!;
        using var started = await engine.PostAsJsonAsync($"exec/{execId}/start", new { Detach = false, Tty = false }, token);
        started.EnsureSuccessStatusCode();
        byte[] bytes = await started.Content.ReadAsByteArrayAsync(token);
        var output = new StringBuilder();
        for (int offset = 0; offset < bytes.Length;)
        {
            if (bytes.Length - offset < 8) throw new InvalidDataException("Truncated Docker output");
            int length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset + 4, 4));
            if (length < 0 || length > bytes.Length - offset - 8) throw new InvalidDataException("Invalid Docker output frame");
            output.Append(Encoding.UTF8.GetString(bytes, offset + 8, length));
            offset += 8 + length;
        }
        using var inspected = JsonDocument.Parse(await engine.GetStringAsync($"exec/{execId}/json", token));
        if (inspected.RootElement.GetProperty("Running").GetBoolean()
            || inspected.RootElement.GetProperty("ExitCode").GetInt32() != 0)
            throw new InvalidOperationException("Nested Docker command failed: " + output);
        return output.ToString();
    }

    public void Dispose() { engine.Dispose(); probe.Dispose(); }
}
