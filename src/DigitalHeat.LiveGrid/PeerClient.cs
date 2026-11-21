using System.Net.Http.Json;
using System.Text.Json;
using DigitalHeat.Core;

/// <summary>Small authenticated LAN client for the four worker nodes on the second laptop.</summary>
sealed class PeerClient(string url, string token) : IDisposable
{
    private readonly HttpClient http = Create(url, token);
    private volatile bool online = true;
    public bool Online => online;

    private static HttpClient Create(string url, string token)
    {
        var client = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.Add("X-DigitalHeat-Token", token);
        return client;
    }

    private static T Await<T>(Task<T> task) => task.GetAwaiter().GetResult();
    private static void Check(HttpResponseMessage response) => response.EnsureSuccessStatusCode();

    public string[] Health() => Await(http.GetFromJsonAsync<string[]>("agent/health")) ?? [];
    public bool RefreshHealth()
    {
        try { online = Health().Length > 0; }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { online = false; }
        return online;
    }
    public string[] List(string kind, string node) =>
        Await(http.GetFromJsonAsync<string[]>($"agent/files/{kind}/{node}")) ?? [];
    public string Read(string kind, string node, string name) => Await(http.GetStringAsync($"agent/files/{kind}/{node}/{name}"));
    public void Put(string node, string name, string content)
    {
        using var response = Await(http.PutAsync($"agent/queue/{node}/{name}", new StringContent(content)));
        Check(response);
    }
    public bool Delete(string kind, string node, string name)
    {
        using var response = Await(http.DeleteAsync($"agent/files/{kind}/{node}/{name}"));
        return response.IsSuccessStatusCode;
    }
    public bool Move(string from, string to, string name)
    {
        using var response = Await(http.PostAsync($"agent/move/{from}/{to}/{name}", null));
        return response.IsSuccessStatusCode;
    }
    public string? Take(string node, string name)
    {
        using var response = Await(http.PostAsync($"agent/take/{node}/{name}", null));
        return response.IsSuccessStatusCode ? Await(response.Content.ReadAsStringAsync()) : null;
    }
    public void Ack(string node, string name)
    {
        using var response = Await(http.PostAsync($"agent/ack/{node}/{name}", null)); Check(response);
    }
    public void Restore(string node, string name)
    {
        using var response = Await(http.PostAsync($"agent/restore/{node}/{name}", null)); Check(response);
    }
    public void Reset()
    {
        using var response = Await(http.PostAsync("agent/reset", null)); Check(response);
    }
    public double? Cpu(string node)
    {
        if (!online) return null;
        try { return Await(http.GetFromJsonAsync<double?>($"agent/cpu/{node}")); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { online = false; return null; }
    }
    public double Apply(string node, double load)
    {
        using var response = Await(http.PostAsJsonAsync($"agent/load/{node}", new { load })); Check(response);
        return Await(response.Content.ReadFromJsonAsync<double>());
    }
    public void Dispose() => http.Dispose();
}

sealed class RemoteExecutor(PeerClient peer, string node) : IExecutor
{
    public double ApplyTargetLoad(double targetLoadPerServer) => peer.Apply(node, targetLoadPerServer);
}
