namespace DigitalHeat.Core;

/// <summary>Customer- or contract-specified policy; Source identifies its authority.</summary>
public sealed record JobPlacementPolicy(string Mode, string Source, string[]? AllowedCountryCodes)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Source)
            || Source.Contains("TODO(source)", StringComparison.OrdinalIgnoreCase)
            || Mode is not ("anywhere" or "origin-site" or "country-allowlist"))
            throw new InvalidDataException("Invalid or unsourced job placement policy.");

        if (Mode == "country-allowlist")
        {
            if (AllowedCountryCodes is not { Length: > 0 }
                || AllowedCountryCodes.Any(code => !ValidCountryCode(code))
                || AllowedCountryCodes.Distinct(StringComparer.Ordinal).Count() != AllowedCountryCodes.Length)
                throw new InvalidDataException("Country allowlist needs unique two-letter country codes.");
        }
        else if (AllowedCountryCodes is not null)
            throw new InvalidDataException("Only a country-allowlist policy may name countries.");
    }

    private static bool ValidCountryCode(string? code) => code is { Length: 2 }
        && code.All(character => character is >= 'A' and <= 'Z');
}

/// <summary>Execution metadata; production callers must obtain it from a trusted inventory.</summary>
public sealed record JobPlacementContext(string? DataOriginSiteId, string? ExecutionSiteId,
    string? ExecutionCountryCode);

public static class JobPlacement
{
    /// <summary>Fail closed when a restricted job lacks matching execution metadata.</summary>
    public static bool IsEligible(JobPlacementPolicy policy, JobPlacementContext context)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(context);
        policy.Validate();
        return policy.Mode switch
        {
            "anywhere" => true,
            "origin-site" => !string.IsNullOrWhiteSpace(context.DataOriginSiteId)
                && !string.IsNullOrWhiteSpace(context.ExecutionSiteId)
                && string.Equals(context.DataOriginSiteId, context.ExecutionSiteId,
                    StringComparison.Ordinal),
            "country-allowlist" => context.ExecutionCountryCode is { Length: 2 } country
                && policy.AllowedCountryCodes!.Contains(country, StringComparer.Ordinal),
            _ => false
        };
    }
}
