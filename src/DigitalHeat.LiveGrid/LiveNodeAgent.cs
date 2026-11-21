using DigitalHeat.Core;
using DigitalHeat.JobRuntime;

/// <summary>Four physical Docker workers on laptop B. It never runs a scheduler or advances model time.</summary>
static class LiveNodeAgent
{
    public static void Run(string root, DemoConfig config, int port, string token)
    {
        var nodes = config.Nodes.Where(n => n.Device == "peer").ToArray();
        if (nodes.Length != 4) throw new InvalidDataException("The peer agent requires four peer nodes.");
        string work = Path.Combine(root, "data", "external", "research-sim", "live-demo-work");
        var docker = new CachedDockerControl(new DockerCliContainerControl());
        var executors = nodes.ToDictionary(n => n.Id, n => (IExecutor)new DockerJobExecutor(
            docker, n.Container, config.AllowedCpuCoresPerContainer.Value, supportsThrottle: true));
        foreach (var e in executors.Values) e.ApplyTargetLoad(0);

        var builder = WebApplication.CreateBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        var app = builder.Build();
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Headers["X-DigitalHeat-Token"] != token) { ctx.Response.StatusCode = 401; return; }
            await next();
        });

        bool Known(string node) => nodes.Any(n => n.Id == node);
        static bool Name(string value) => value.Length is > 5 and < 100
            && value.EndsWith(".json", StringComparison.Ordinal)
            && value[..^5].All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
        string? Dir(string kind, string node) => kind switch
        {
            "done" => Path.Combine(work, "done"),
            "queue" or "running" or "progress" or "outbox" when Known(node) => Path.Combine(work, kind, node),
            _ => null,
        };
        string? PathFor(string kind, string node, string name) => Name(name) && Dir(kind, node) is { } d
            ? Path.Combine(d, name) : null;
        string? Read(string kind, string node, string name)
        {
            string? path = PathFor(kind, node, name);
            if (path is null || !File.Exists(path)) return null;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(fs);
            return reader.ReadToEnd();
        }
        void Put(string node, string name, string content)
        {
            string path = PathFor("queue", node, name) ?? throw new InvalidDataException("Invalid task path.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path))
            {
                if (File.ReadAllText(path) == content) return; // transfer retry after an unacknowledged reply
                throw new IOException("Task already exists with different content.");
            }
            File.WriteAllText(path + ".tmp", content);
            File.Move(path + ".tmp", path);
        }

        app.MapGet("/agent/health", () => nodes.Select(n => n.Id).ToArray());
        app.MapGet("/agent/files/{kind}/{node}", (string kind, string node) =>
        {
            string? path = Dir(kind, node);
            if (path is null) return Results.BadRequest();
            if (!Directory.Exists(path)) return Results.Json(Array.Empty<string>());
            return Results.Json(Directory.GetFiles(path, "*.json", kind == "running"
                ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Select(Path.GetFileName).ToArray());
        });
        app.MapGet("/agent/files/{kind}/{node}/{name}", (string kind, string node, string name) =>
            Read(kind, node, name) is { } content ? Results.Text(content) : Results.NotFound());
        app.MapDelete("/agent/files/{kind}/{node}/{name}", (string kind, string node, string name) =>
        {
            if (kind is not ("queue" or "done") || PathFor(kind, node, name) is not { } path) return Results.BadRequest();
            try { File.Delete(path); return Results.NoContent(); }
            catch (IOException) { return Results.Conflict(); }
        });
        app.MapPut("/agent/queue/{node}/{name}", async (string node, string name, HttpRequest request) =>
        {
            if (PathFor("queue", node, name) is null) return Results.BadRequest();
            using var reader = new StreamReader(request.Body);
            try { Put(node, name, await reader.ReadToEndAsync()); return Results.NoContent(); }
            catch (IOException) { return Results.Conflict(); }
        });
        app.MapPost("/agent/move/{from}/{to}/{name}", (string from, string to, string name) =>
        {
            if (PathFor("queue", from, name) is not { } source || PathFor("queue", to, name) is not { } dest)
                return Results.BadRequest();
            try { File.Move(source, dest); return Results.NoContent(); }
            catch (IOException) { return Results.Conflict(); }
        });
        app.MapPost("/agent/take/{node}/{name}", (string node, string name) =>
        {
            if (PathFor("queue", node, name) is not { } source || PathFor("outbox", node, name) is not { } dest)
                return Results.BadRequest();
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            try { File.Move(source, dest); return Results.Text(File.ReadAllText(dest)); }
            catch (IOException) { return Results.Conflict(); }
        });
        app.MapPost("/agent/ack/{node}/{name}", (string node, string name) =>
        {
            if (PathFor("outbox", node, name) is not { } path) return Results.BadRequest();
            File.Delete(path); return Results.NoContent();
        });
        app.MapPost("/agent/restore/{node}/{name}", (string node, string name) =>
        {
            if (PathFor("outbox", node, name) is not { } source || PathFor("queue", node, name) is not { } dest)
                return Results.BadRequest();
            try { File.Move(source, dest); return Results.NoContent(); }
            catch (IOException) { return Results.Conflict(); }
        });
        app.MapPost("/agent/reset", () =>
        {
            foreach (string kind in new[] { "queue", "running", "progress", "outbox" })
                foreach (var node in nodes)
                {
                    string path = Dir(kind, node.Id)!;
                    Directory.CreateDirectory(path);
                    foreach (string file in Directory.GetFiles(path, "*.json", SearchOption.AllDirectories)) File.Delete(file);
                }
            string done = Path.Combine(work, "done");
            Directory.CreateDirectory(done);
            foreach (string file in Directory.GetFiles(done, "*.json")) File.Delete(file);
            return Results.NoContent();
        });
        app.MapGet("/agent/cpu/{node}", (string node) =>
        {
            var n = nodes.FirstOrDefault(n => n.Id == node);
            return n is null ? Results.NotFound() : Results.Json(docker.Load(n.Container, config.AllowedCpuCoresPerContainer.Value));
        });
        app.MapPost("/agent/load/{node}", (string node, LoadRequest request) =>
        {
            if (!executors.TryGetValue(node, out var executor) || !double.IsFinite(request.Load)
                || request.Load is < 0 or > 1) return Results.BadRequest();
            return Results.Json(executor.ApplyTargetLoad(request.Load));
        });
        using var stop = new CancellationTokenSource();
        app.Lifetime.ApplicationStopping.Register(() => stop.Cancel());
        _ = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                try { docker.Observe(nodes.Select(n => n.Container).ToArray()); } catch (Exception) { }
                try { await Task.Delay(1000, stop.Token); } catch (OperationCanceledException) { break; }
            }
        });
        Console.WriteLine($"Node agent listening on port {port}: {string.Join(", ", nodes.Select(n => n.Id))}");
        app.Run();
    }

    private sealed record LoadRequest(double Load);
}
