using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;

namespace DigitalHeat.Gaming;

// Trusted operator configuration, never a tenant-supplied command or security sandbox.
public sealed record ApplicationProfile(string Id, string Executable, string WorkingDirectory, string User,
    string LogPath, string[] Arguments, Dictionary<string, string[]> Variants,
    Dictionary<string, string> Environment, string[] RequiredCapabilities);
public sealed record NodeProfile(string Id, string DockerContext, string ComposeProject, string ComposeService,
    string BrowserUrl, string HardwareProfileId);
public sealed record RuntimeHardwareProfile(string Id, string[] Capabilities, string Source);
public sealed record RuntimeProfiles(ApplicationProfile[] Applications, NodeProfile[] Nodes,
    RuntimeHardwareProfile[] HardwareProfiles)
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };
    public static RuntimeProfiles Load(string path)
    {
        var profiles = JsonSerializer.Deserialize<RuntimeProfiles>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Missing runtime profiles.");
        profiles.Validate();
        return profiles;
    }
    public void Validate()
    {
        static void Require([DoesNotReturnIf(false)] bool valid, string field)
        { if (!valid) throw new InvalidDataException($"Invalid runtime profile: {field}."); }
        static bool Token(string? value) => value is not null && Regex.IsMatch(value, "^[a-zA-Z0-9][a-zA-Z0-9_.-]*$");
        static bool Path(string? value) => !string.IsNullOrWhiteSpace(value) && value.StartsWith('/') && !value.Contains('\0');
        static bool Text(string? value) => value is not null && !value.Contains('\0');
        Require(Applications is not null && Nodes is not null && HardwareProfiles is not null, "collections");
        Require(Applications!.All(a => a is not null) && Nodes!.All(n => n is not null) && HardwareProfiles!.All(h => h is not null), "entries");
        Require(Applications.Select(a => a.Id).Distinct().Count() == Applications.Length && Nodes.Select(n => n.Id).Distinct().Count() == Nodes.Length &&
            HardwareProfiles.Select(h => h.Id).Distinct().Count() == HardwareProfiles.Length, "unique IDs");
        foreach (var hardware in HardwareProfiles)
            Require(Token(hardware.Id) && hardware.Capabilities is not null && hardware.Capabilities.All(Token) && !string.IsNullOrWhiteSpace(hardware.Source), "hardware declaration and source");
        foreach (var node in Nodes)
        {
            Require(Token(node.Id) && Token(node.DockerContext) && Token(node.ComposeProject) && Token(node.ComposeService), "node identity/context");
            Require(Uri.TryCreate(node.BrowserUrl, UriKind.Absolute, out var url) && (url.Scheme == "http" || url.Scheme == "https") && string.IsNullOrEmpty(url.UserInfo), "browser URL without credentials");
            Require(HardwareProfiles.Any(h => h.Id == node.HardwareProfileId), "node hardware reference");
        }
        foreach (var app in Applications)
        {
            Require(Token(app.Id) && Token(app.User) && app.User != "root" && app.User != "0", "application ID and non-root user");
            Require(Path(app.Executable) && Path(app.WorkingDirectory) && Path(app.LogPath), "absolute Linux paths");
            Require(app.Arguments is not null && app.Arguments.All(Text) && app.Variants is not null && app.Variants.All(v => Token(v.Key) && v.Value is not null && v.Value.All(Text)), "argument lists");
            Require(app.Environment is not null && app.Environment.All(e => Regex.IsMatch(e.Key, "^[A-Za-z_][A-Za-z0-9_]*$") && Text(e.Value)), "environment");
            Require(app.RequiredCapabilities is not null && app.RequiredCapabilities.All(Token), "capabilities");
        }
    }
}
