using System.IO.Pipes;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DigitalHeat.Gaming.Monitor;

internal sealed class DockerReadException(string category) : Exception(category);

/// <summary>Only local selected transports and fixed GET routes; no redirects, proxies or write API.</summary>
internal sealed class DockerReadOnlyApi : IDisposable
{
    private readonly HttpClient client;
    private readonly DockerObservationOptions options;
    private readonly SemaphoreSlim reads;
    private readonly TimeSpan timeout;
    private string? version;
    private const int BufferBytes = 8192;
    public DockerReadOnlyApi(HttpClient client, DockerObservationOptions options, SemaphoreSlim reads, TimeSpan timeout)
    {
        this.client = client; this.options = options; this.reads = reads; this.timeout = timeout;
        client.BaseAddress = new Uri("http://docker/"); client.Timeout = Timeout.InfiniteTimeSpan;
    }
    internal static bool IsLocalEndpoint(string endpoint) => TryPipe(endpoint, out _) ||
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == "unix" && string.IsNullOrEmpty(uri.Host)
        && Path.IsPathRooted(uri.AbsolutePath) && !uri.AbsolutePath.Contains('\0');
    private static bool TryPipe(string endpoint, out string pipe)
    {
        var match = Regex.Match(endpoint, @"^npipe:/{2,4}\./pipe/([A-Za-z0-9_.-]+)$", RegexOptions.CultureInvariant);
        pipe = match.Success ? match.Groups[1].Value : ""; return match.Success;
    }
    internal static HttpClient LocalClient(string endpoint)
    {
        if (!IsLocalEndpoint(endpoint)) throw new DockerReadException("unsupported_local_transport");
        var handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false };
        handler.ConnectCallback = async (_, cancel) =>
        {
            if (TryPipe(endpoint, out var name))
            {
                if (!OperatingSystem.IsWindows()) throw new DockerReadException("named_pipe_requires_windows");
                var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
                try { await pipe.ConnectAsync(cancel); return pipe; } catch { pipe.Dispose(); throw; }
            }
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try { await socket.ConnectAsync(new UnixDomainSocketEndPoint(new Uri(endpoint).AbsolutePath), cancel); return new NetworkStream(socket, ownsSocket: true); }
            catch { socket.Dispose(); throw; }
        };
        return new HttpClient(handler, disposeHandler: true);
    }
    public async Task Initialize(CancellationToken cancel)
    {
        using var doc = await Get("/version", cancel);
        string? text = doc.RootElement.TryGetProperty("ApiVersion", out var v) ? v.GetString() : null;
        string? minimum = doc.RootElement.TryGetProperty("MinAPIVersion", out var m) ? m.GetString() : options.MinApiVersion;
        if (!Version.TryParse(text, out var server) || !Version.TryParse(minimum, out var serverMin)) throw new DockerReadException("invalid_api_version");
        var max = Version.Parse(options.MaxApiVersion); var selected = server < max ? server : max;
        if (selected < Version.Parse(options.MinApiVersion) || selected < serverMin) throw new DockerReadException("unsupported_api_version");
        version = selected.ToString(2);
    }
    public Task<JsonDocument> Containers(CancellationToken cancel) => Get(Prefix + "/containers/json?all=true", cancel);
    public Task<JsonDocument> Stats(string id, CancellationToken cancel) => Get(Prefix + "/containers/" + Id(id) + "/stats?stream=false&one-shot=true", cancel);
    public Task<JsonDocument> Processes(string id, CancellationToken cancel) => Get(Prefix + "/containers/" + Id(id) + "/top?ps_args=-eo%20pid,args", cancel);
    private string Prefix => "/v" + (version ?? throw new InvalidOperationException("Negotiate the Docker API first."));
    private static string Id(string id) => Regex.IsMatch(id, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant) ? id : throw new DockerReadException("invalid_container_id");
    private async Task<JsonDocument> Get(string path, CancellationToken cancel)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel); deadline.CancelAfter(timeout);
        bool acquired = false;
        try
        {
            await reads.WaitAsync(deadline.Token); acquired = true;
            using var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode) throw new DockerReadException("engine_http_" + (int)response.StatusCode);
            if (response.Content.Headers.ContentLength > options.MaxResponseBytes) throw new DockerReadException("engine_response_too_large");
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var memory = new MemoryStream(); var buffer = new byte[BufferBytes]; int bytes;
            while ((bytes = await stream.ReadAsync(buffer, deadline.Token)) > 0)
            {
                if (memory.Length + bytes > options.MaxResponseBytes) throw new DockerReadException("engine_response_too_large");
                memory.Write(buffer, 0, bytes);
            }
            return JsonDocument.Parse(memory.ToArray());
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested) { throw new DockerReadException("engine_timeout"); }
        catch (Exception e) when (e is HttpRequestException or IOException or JsonException) { throw new DockerReadException("engine_response_unavailable"); }
        finally { if (acquired) reads.Release(); }
    }
    public void Dispose() => client.Dispose();
}
