using System.Text.Json;

namespace DigitalHeat.VoiceDemo;

/// <summary>config/voice-demo.json: every number with unit and source.</summary>
internal sealed class DemoConfig
{
    private readonly JsonElement root;
    public DemoConfig(string path)
    {
        root = JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
        Source = Text("source"); Image = Text("image"); DockerVolume = Text("docker_volume"); DateLocal = Text("date_local");
        Language = Text("language"); SystemPrompt = Text("system_prompt"); WhisperModel = Text("whisper_model"); LlmModel = Text("llm_model");
        ReplayStartLocalHour = Value("replay_start_local_hour"); ReplayEndLocalHour = Value("replay_end_local_hour");
        SecondsPerSimulatedHour = Value("seconds_per_simulated_hour"); ControlIntervalSeconds = Value("control_interval_seconds");
        HotWaterProfile = Text("hot_water_profile"); RequestDeclaredHeatKwhPerHour = Value("request_declared_heat_kwh_per_h");
        MinimumFreezeSeconds = Value("minimum_freeze_seconds");
        RequestCpuCores = Value("request_cpu_cores"); RequestRamMib = Value("request_ram_mib");
        RequestDeadlineSeconds = Value("request_deadline_seconds"); MaxNewTokens = Value("max_new_tokens");
        if (ReplayStartLocalHour < 0 || ReplayEndLocalHour > 24 || ReplayEndLocalHour <= ReplayStartLocalHour
            || SecondsPerSimulatedHour <= 0 || ControlIntervalSeconds <= 0 || RequestDeclaredHeatKwhPerHour <= 0
            || MinimumFreezeSeconds < 0 || RequestCpuCores < 1 || RequestRamMib < 1)
            throw new InvalidDataException("Invalid config/voice-demo.json values.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(Language, "^[a-z]{2}$|^auto$")
            || new[] { WhisperModel, LlmModel }.Any(m => !System.Text.RegularExpressions.Regex.IsMatch(m, "^[A-Za-z0-9._-]+$")))
            throw new InvalidDataException("Invalid language or model file name in config/voice-demo.json.");
    }

    public string Source { get; }
    public string Image { get; }
    public string DockerVolume { get; }
    public string DateLocal { get; }
    public string Language { get; }
    public string WhisperModel { get; }
    public string LlmModel { get; }
    public string SystemPrompt { get; }
    public double ReplayStartLocalHour { get; }
    public double ReplayEndLocalHour { get; }
    public double SecondsPerSimulatedHour { get; }
    public double ControlIntervalSeconds { get; }
    public string HotWaterProfile { get; }
    public double RequestDeclaredHeatKwhPerHour { get; }
    public double MinimumFreezeSeconds { get; }
    public double RequestCpuCores { get; }
    public double RequestRamMib { get; }
    public double RequestDeadlineSeconds { get; }
    public double MaxNewTokens { get; }
    public string ModelSource(string name) =>
        root.GetProperty("model_sources").TryGetProperty(name, out var v) ? v.GetString() ?? "" : "config/voice-demo.json";

    private string Text(string key) => root.GetProperty(key).GetString() ?? throw new InvalidDataException("Missing " + key);
    private double Value(string key) => root.GetProperty(key).GetProperty("value").GetDouble();
}
