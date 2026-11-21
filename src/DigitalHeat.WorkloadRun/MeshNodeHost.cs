using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using DigitalHeat.Core;
using DigitalHeat.Orchestration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Trusted local lab adapter. The actor owns MeshPeer; HTTP never enters it concurrently.
internal sealed record MeshNodeConfig(string MeshId, string Id, string StateDirectory, string JobImage,
    Dictionary<string, string> Peers, double InitialDemandKwh, double CpuCores, long RamMib, int Slots,
    double JobCpuCores, long JobRamMib, double JobHeatKwhPerHour, double ServerHeatKwhPerHour,
    double OfferLifetimeSeconds, double HeartbeatSeconds, double TickSeconds, double RequestTimeoutSeconds,
    int QueueCapacity, int MaxRequestBytes, double DeadlineSeconds, string Source);
internal sealed record MeshEnvelope(string Sender, PeerMessage Message);
internal sealed record MeshSubmission(string JobId);
internal sealed record MeshHeat(double DemandKwh, bool AcceptingWork);

internal static class MeshNodeHost
{
    public static async Task Run(string configPath)
    {
        var cfg = JsonSerializer.Deserialize<MeshNodeConfig>(File.ReadAllText(configPath), WorkloadJson.Options)
            ?? throw new InvalidDataException("Empty node configuration.");
        string token = Environment.GetEnvironmentVariable("MESH_TOKEN") ?? throw new InvalidDataException("MESH_TOKEN required.");
        if (token.Length < 32 || !cfg.Peers.ContainsKey(cfg.Id)) throw new InvalidDataException("Invalid lab credentials/peers.");
        Directory.CreateDirectory(cfg.StateDirectory);
        IDockerCommands docker = new LabDockerCommands(cfg.StateDirectory);
        // Never reconcile against an unavailable or replacement inner engine after a restart.
        string engine = docker.Run(["info", "--format", "{{.ID}} "]).Trim();
        docker.Run(["image", "inspect", cfg.JobImage]);
        string enginePath = Path.Combine(cfg.StateDirectory, "engine-id");
        if (File.Exists(enginePath) && File.ReadAllText(enginePath) != engine)
            throw new InvalidDataException("Inner engine identity changed; recovery requires operator inspection.");
        File.WriteAllText(enginePath, engine);

        var mailbox = Channel.CreateBounded<Action>(new BoundedChannelOptions(cfg.QueueCapacity) { SingleReader = true });
        var outbound = Channel.CreateBounded<(string From, string To, PeerMessage Message)>(cfg.QueueCapacity);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(cfg.RequestTimeoutSeconds) };
        http.DefaultRequestHeaders.Add("X-Mesh-Token", token);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = cfg.MaxRequestBytes);
        builder.Services.ConfigureHttpJsonOptions(o => {
            o.SerializerOptions.PropertyNamingPolicy = WorkloadJson.Options.PropertyNamingPolicy;
        });
        var app = builder.Build();
        var cancel = app.Lifetime.ApplicationStopping;
        var heat = new MeshHeat(cfg.InitialDemandKwh, true);
        long revision = 0;
        WorkloadInventory Inventory()
        {
            var now = DateTimeOffset.UtcNow;
            var start = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
            var resource = new ResourceAmount(cfg.CpuCores, cfg.RamMib, cfg.Slots);
            return new(++revision, start, start.AddHours(1), cfg.Source,
                [new(cfg.Id, "CH", new(heat.DemandKwh, 0, 0, 1, cfg.ServerHeatKwhPerHour), 0)],
                [new(cfg.Id + "-physical", resource, [])],
                [new(cfg.Id + "-worker", cfg.Id, cfg.Id + "-physical", heat.AcceptingWork, true, resource, [], [], [], [])], [])
                { ResourceObservedAtUtc = now, ResourceValidUntilUtc = now.AddSeconds(cfg.OfferLifetimeSeconds) };
        }
        var profile = new ContainerProfile("finite-ising-" + cfg.JobImage.Replace("sha256:", "", StringComparison.Ordinal),
            cfg.JobImage, "65534", [], null, [], [], []) { ShmMib = 16 };
        using var peer = new MeshPeer(cfg.MeshId, cfg.Id, cfg.StateDirectory, Inventory(),
            [new(cfg.Id + "-worker", "default", "none", [], [])],
            new Dictionary<string, ContainerProfile> { [profile.Id] = profile }, cfg.Peers.Keys,
            new QueuedTransport(outbound.Writer), new DockerWorkloadRuntime(docker), TimeProvider.System,
            TimeSpan.FromSeconds(cfg.OfferLifetimeSeconds));
        object snapshot = new { state = "starting" };
        bool decisionRequested = true;
        int tickQueued = 0;
        void Refresh() => Volatile.Write(ref snapshot, new { peer_id = cfg.Id, engine_id = engine,
            observed_at_utc = DateTimeOffset.UtcNow, source = cfg.Source, heat,
            jobs = peer.Jobs(), attempts = peer.Attempts() });
        Refresh();
        app.Use(async (context, next) => {
            if (context.Request.Path != "/healthz" && !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(context.Request.Headers["X-Mesh-Token"].ToString()), Encoding.UTF8.GetBytes(token)))
            { context.Response.StatusCode = 401; return; }
            await next(context);
        });
        app.MapGet("/healthz", () => Results.Ok(new { state = "ready" }));
        app.MapGet("/status", () => Results.Json(Volatile.Read(ref snapshot), WorkloadJson.Options));
        app.MapPost("/messages", (MeshEnvelope packet) => {
            if (!cfg.Peers.ContainsKey(packet.Sender)) return Results.BadRequest();
            return mailbox.Writer.TryWrite(() => {
                peer.Receive(packet.Sender, packet.Message);
                // Unknown replies do not wake a retry loop; the fallback tick retries the same owner.
                if (packet.Message is PeerOffer or PeerReply { Status: "rejected" }) decisionRequested = true;
            }) ? Results.Accepted() : Results.StatusCode(503);
        });
        app.MapPost("/heat", (MeshHeat update) => {
            if (!double.IsFinite(update.DemandKwh) || update.DemandKwh < 0) return Results.BadRequest();
            return mailbox.Writer.TryWrite(() => { heat = update; peer.Update(Inventory()); decisionRequested = true; })
                ? Results.Accepted() : Results.StatusCode(503);
        });
        app.MapPost("/jobs", async (MeshSubmission submission) => {
            if (submission.JobId.Length is < 1 or > 64 || submission.JobId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
                return Results.BadRequest();
            var result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!mailbox.Writer.TryWrite(() => {
                try {
                    var prior = peer.Jobs().SingleOrDefault(j => j.Request.Id == cfg.Id + "." + submission.JobId);
                    var now = DateTimeOffset.UtcNow;
                    var request = prior?.Request ?? new WorkloadRequest(submission.JobId, "batch", cfg.Id,
                        new("anywhere", cfg.Source, null), new(cfg.JobCpuCores, cfg.JobRamMib, 1), cfg.Source,
                        cfg.JobHeatKwhPerHour, cfg.Source, [], null, null, null, [], new(true, true, false))
                        { Deadline = new(now, now.AddSeconds(cfg.DeadlineSeconds), cfg.Source, true) };
                    string id = prior?.Request.Id ?? peer.Submit(request, profile.Id);
                    result.SetResult(id); decisionRequested = true;
                } catch (Exception e) { result.TrySetException(e); }
            })) return Results.StatusCode(503);
            return Results.Json(new { job_id = await result.Task.WaitAsync(cancel) }, WorkloadJson.Options);
        });
        var sender = Task.Run(async () => {
            await foreach (var packet in outbound.Reader.ReadAllAsync(cancel))
            {
                try {
                    using var response = await http.PostAsJsonAsync(cfg.Peers[packet.To] + "/messages",
                        new MeshEnvelope(packet.From, packet.Message), WorkloadJson.Options, cancel);
                    response.EnsureSuccessStatusCode();
                } catch (Exception e) when (e is HttpRequestException or TaskCanceledException) {
                    Console.Error.WriteLine("Peer delivery unavailable; durable origin will retry: " + packet.To);
                }
            }
        }, cancel);
        var actor = Task.Run(async () => {
            var nextHeartbeat = DateTimeOffset.MinValue;
            await foreach (var action in mailbox.Reader.ReadAllAsync(cancel))
            {
                try {
                    action();
                    // Coalesce feedback/ticks already in the mailbox into one reconciliation cycle.
                    while (mailbox.Reader.TryRead(out var more)) more();
                    if (DateTimeOffset.UtcNow >= nextHeartbeat) {
                        peer.Update(Inventory()); nextHeartbeat = DateTimeOffset.UtcNow.AddSeconds(cfg.HeartbeatSeconds);
                    }
                    if (decisionRequested) { decisionRequested = false; peer.Tick(); }
                } catch (Exception e) when (e is InvalidOperationException or IOException or TimeoutException or InvalidDataException) {
                    // An uncertain daemon/proposal never clears ownership or causes reassignment.
                    Console.Error.WriteLine("Mesh action deferred: " + e.Message);
                }
                Refresh();
            }
        }, cancel);
        var timer = Task.Run(async () => {
            using var ticks = new PeriodicTimer(TimeSpan.FromSeconds(cfg.TickSeconds));
            while (await ticks.WaitForNextTickAsync(cancel))
                if (Interlocked.Exchange(ref tickQueued, 1) == 0
                    && !mailbox.Writer.TryWrite(() => { Interlocked.Exchange(ref tickQueued, 0); decisionRequested = true; }))
                    Interlocked.Exchange(ref tickQueued, 0);
        }, cancel);
        try { await app.RunAsync(); }
        finally {
            try { await Task.WhenAll(actor, sender, timer); } catch (OperationCanceledException) { }
        }
    }

    private sealed class QueuedTransport(ChannelWriter<(string From, string To, PeerMessage Message)> queue) : IPeerMeshTransport
    {
        // Dropped notifications are safe: intents precede send; offers refresh; retries keep their owner.
        public void Send(string from, string to, PeerMessage message) => queue.TryWrite((from, to, message));
    }

    // Explicit local fault fixture only; no network endpoint can enable it. Kill this node after
    // the marker appears to exercise the create-before-ack recovery window on the actual engine.
    private sealed class LabDockerCommands(string stateDirectory) : IDockerCommands
    {
        private readonly DockerCommands inner = new();
        public string Run(IReadOnlyList<string> arguments)
        {
            string result = inner.Run(arguments);
            if (arguments.Count > 2 && arguments[2] == "create"
                && File.Exists(Path.Combine(stateDirectory, "inject-after-create")))
            {
                File.WriteAllText(Path.Combine(stateDirectory, "created-before-ack"), result.Trim());
                Thread.Sleep(TimeSpan.FromSeconds(30)); // Bounded test fault, never enabled by default.
            }
            return result;
        }
    }
}
