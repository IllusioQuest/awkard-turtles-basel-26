namespace Webhosting.Gateway;

public interface IHostingReplicaControl
{
    Task<bool> ReadyAsync(HostingNode node, CancellationToken token);
    Task ResumeAsync(HostingNode node, CancellationToken token);
    Task PauseAsync(HostingNode node, CancellationToken token);
}

public sealed class HostingLifecycle(HostingPlanReader plans, HostingAdmission admission,
    IHostingReplicaControl control, TimeSpan interval, TimeSpan drainTimeout)
{
    private bool restore = true;

    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try { await ReconcileAsync(token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception e) { admission.Phase = "retry"; Console.WriteLine($"HOSTING_RETRY {e.GetType().Name}: {e.Message}"); }
            try { await Task.Delay(interval, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
        }
    }

    public async Task ReconcileAsync(CancellationToken token)
    {
        var plan = plans.Read();
        if (plan is null) { admission.Phase = "invalid_plan"; return; }
        bool restoredAll = true;
        foreach (var service in plan.Services)
        {
            var nodes = plan.Nodes.Where(n => n.Component == service.Component && plan.Eligible(n)).ToArray();
            var scores = plan.Scores(nodes, DateTimeOffset.UtcNow);
            var preferred = scores is null ? nodes : nodes.OrderByDescending(n => scores[n.Id])
                .ThenBy(n => n.Id, StringComparer.Ordinal).Take(plan.MinimumServingReplicas).ToArray();
            foreach (var node in nodes)
                admission.SetReady(node.Id, await control.ReadyAsync(node, token));

            // Recover capacity before optimizing heat; start/resume is idempotent and never removes capacity.
            bool shortage = nodes.Count(n => admission.Ready(n.Id)) < plan.MinimumServingReplicas;
            foreach (var node in (restore || shortage || scores is null ? nodes : preferred))
            {
                if (!admission.Ready(node.Id))
                {
                    admission.Phase = $"resume:{node.Id}";
                    try { await control.ResumeAsync(node, token); }
                    catch (Exception e) when (e is not OperationCanceledException)
                    { restoredAll = false; Console.WriteLine($"HOSTING_RESUME_FAILED {node.Id}: {e.Message}"); }
                    admission.SetReady(node.Id, await control.ReadyAsync(node, token));
                }
                if (!admission.Ready(node.Id)) restoredAll = false;
            }
            if (restore || scores is null || !preferred.All(n => admission.Ready(n.Id))) continue;

            foreach (var old in nodes.Where(n => !preferred.Contains(n) && admission.Ready(n.Id)))
            {
                admission.Phase = $"drain:{old.Id}";
                admission.Drain(old.Id, true);
                try
                {
                    var started = System.Diagnostics.Stopwatch.StartNew();
                    while (admission.InFlight(old.Id) != 0 && started.Elapsed < drainTimeout)
                        await Task.Delay(interval, token);
                    if (admission.InFlight(old.Id) != 0) continue;
                    var current = plans.Read();
                    if (current is null || current.Fingerprint != plan.Fingerprint
                        || current.Scores(nodes, DateTimeOffset.UtcNow) is null) continue;
                    int others = 0;
                    foreach (var other in preferred)
                    {
                        bool ready = current.Eligible(other) && await control.ReadyAsync(other, token);
                        admission.SetReady(other.Id, ready);
                        if (ready) others++;
                    }
                    if (others < plan.MinimumServingReplicas || admission.InFlight(old.Id) != 0) continue;
                    var latest = plans.Read();
                    if (latest is null || latest.Fingerprint != plan.Fingerprint
                        || latest.Scores(nodes, DateTimeOffset.UtcNow) is null) continue;
                    token.ThrowIfCancellationRequested();
                    admission.Phase = $"pause:{old.Id}";
                    await control.PauseAsync(old, token);
                    admission.SetReady(old.Id, false);
                    Console.WriteLine($"HOSTING_PAUSED {old.Id} revision={plan.Revision}");
                }
                finally { admission.Drain(old.Id, false); }
            }
        }
        if (restore && restoredAll) restore = false;
        admission.Phase = restore ? "restore" : "serving";
    }
}
