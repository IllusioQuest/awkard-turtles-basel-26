using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Webhosting.Contracts;

public static partial class DeploymentValidation
{
    // Identifier restrictions keep names usable in Docker labels, keys and API routes.
    [GeneratedRegex("^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();

    public static IReadOnlyList<string> Validate(string pathId, DeploymentSpec? spec)
    {
        var errors = new List<string>();
        if (spec is null) return ["A deployment specification is required."];
        if (pathId != spec.DeploymentId) errors.Add("Path and deploymentId must match.");
        if (!ValidId(spec.DeploymentId)) errors.Add("deploymentId must be a lowercase identifier.");
        if (spec.Components is null || spec.Components.Count == 0)
        {
            errors.Add("At least one component is required.");
            return errors;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in spec.Components)
        {
            if (component is null) { errors.Add("Components cannot be null."); continue; }
            var label = component.ComponentId ?? "<missing>";
            if (!ValidId(component.ComponentId) || !ids.Add(component.ComponentId))
                errors.Add($"{label}: componentId must be valid and unique.");
            if (string.IsNullOrWhiteSpace(component.Image) || component.Image.Any(char.IsWhiteSpace))
                errors.Add($"{label}: a container image reference is required.");
            if (component.ContainerPort is < 1 or > 65535)
                errors.Add($"{label}: containerPort must be a valid TCP port.");
            if (component.Replicas < 1) errors.Add($"{label}: replicas must be positive.");
            if (!double.IsFinite(component.CpuLimitCores) || component.CpuLimitCores <= 0)
                errors.Add($"{label}: cpuLimitCores must be finite and positive.");
            if (component.MemoryLimitMiB <= 0) errors.Add($"{label}: memoryLimitMiB must be positive.");
            if (!ValidPath(component.HealthPath)) errors.Add($"{label}: healthPath must be an absolute URL path.");
            if (!ValidPath(component.RoutePrefix) ||
                (component.RoutePrefix.Length > 1 && component.RoutePrefix.EndsWith('/')) ||
                !routes.Add(component.RoutePrefix))
                errors.Add($"{label}: routePrefix must be unique and canonical (no trailing slash except /).");
        }
        // Nested routes (e.g. / and /api) are intentional; the gateway must prefer the longest prefix.
        return errors;
    }

    private static bool ValidId([NotNullWhen(true)] string? value) => value is not null && IdentifierPattern().IsMatch(value);

    private static bool ValidPath(string? value) =>
        !string.IsNullOrEmpty(value) && value.StartsWith('/') &&
        !value.Contains("//", StringComparison.Ordinal) && !value.Contains("..", StringComparison.Ordinal) &&
        !value.Any(c => char.IsWhiteSpace(c) || c is '?' or '#' or '\\' or '%' or '{' or '}');
}
