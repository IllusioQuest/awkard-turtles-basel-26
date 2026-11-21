using System.Text.Json;
using DigitalHeat.Gaming;
try
{
    if (args.Length < 3 || args.Skip(3).Any(a => a != "--dispatch" && !a.StartsWith("--variant=", StringComparison.Ordinal)))
        throw new ArgumentException("Usage: GamingRuntime <profiles.json> <application-id> <node-id> [--variant=match] [--dispatch]. Default: read-only plan.");
    var runtime = new DockerDesktopRuntime(new DockerCommandRunner());
    var variant = args.Skip(3).Where(a => a.StartsWith("--variant=", StringComparison.Ordinal)).Select(a => a[10..]).SingleOrDefault();
    var plan = runtime.Plan(RuntimeProfiles.Load(args[0]), args[1], args[2], variant);
    if (args.Contains("--dispatch")) runtime.Dispatch(plan);
    Console.WriteLine(JsonSerializer.Serialize(new { plan.ApplicationId, plan.NodeId, plan.ContainerId, plan.BrowserUrl,
        status = args.Contains("--dispatch") ? "dispatch_acknowledged_not_readiness" : "plan_only", docker_arguments = plan.DockerArguments }, RuntimeProfiles.JsonOptions));
}
catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
return 0;
