using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
namespace DigitalHeat.Orchestration.Tests;

public class RenderNetworkStatusTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public bool FailB, WrongIdentity;
        public List<string> Requested { get; }=[];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            string id=request.RequestUri!.Host=="a.invalid"?"a":"b";Requested.Add(id);
            if(id=="b"&&FailB)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var status=new RenderPeerStatus(WrongIdentity?"wrong":id,"host-"+id,"recipe",true,0,"CPU",null,[],[],[],null);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(status,options:RenderNodeHost.Json)});
        }
    }
    private static RenderConfig Config(string state)=>new("a","recipe",state,"host-a",
        new(){["a"]="http://a.invalid",["b"]="http://b.invalid"},0,1,1536,.8,2,7200,"Synthetic contract test")
        {PlannedGpuPeers=["planned-gpu-1","planned-gpu-2"]};
    private static JsonElement Snapshot(RenderSession s)=>JsonSerializer.SerializeToElement(s.Snapshot(),RenderNodeHost.Json);

    [Fact] public async Task PlannedServersNeverBecomeRuntimePeersOrReceiveProbes()
    {
        using var handler=new Handler();using var http=new HttpClient(handler);
        var s=new RenderSession(Config(Path.Combine(Path.GetTempPath(),"render-network-"+Guid.NewGuid().ToString("N"))),http);
        await s.RefreshPeers();var result=Snapshot(s);var network=result.GetProperty("network");
        Assert.Equal(2,result.GetProperty("configured_peers").GetInt32());
        Assert.Equal(2,result.GetProperty("peers").GetArrayLength());
        Assert.Equal(2,network.GetProperty("planned_gpu_peers").GetArrayLength());
        Assert.Equal(new[]{"a","b"},handler.Requested.Order().ToArray());
        Assert.All(network.GetProperty("connections").EnumerateArray(),c=>Assert.Equal("reachable",c.GetProperty("state").GetString()));
        Assert.False(network.GetProperty("work_data_stale").GetBoolean());
    }
    [Fact] public async Task PartialFailureRetainsCompleteWorkSnapshotButMarksItsAgeAndReachability()
    {
        using var handler=new Handler();using var http=new HttpClient(handler);
        var s=new RenderSession(Config(Path.Combine(Path.GetTempPath(),"render-network-"+Guid.NewGuid().ToString("N"))),http);
        await s.RefreshPeers();var prior=Snapshot(s).GetProperty("network").GetProperty("peer_snapshot_utc").GetString();
        handler.FailB=true;await s.RefreshPeers();var result=Snapshot(s);var network=result.GetProperty("network");
        Assert.Equal(2,result.GetProperty("peers").GetArrayLength());
        Assert.Equal(prior,network.GetProperty("peer_snapshot_utc").GetString());
        Assert.True(network.GetProperty("work_data_stale").GetBoolean());
        Assert.Equal(new[]{"reachable","unreachable"},network.GetProperty("connections").EnumerateArray().Select(c=>c.GetProperty("state").GetString()).ToArray());
        Assert.Contains("b",result.GetProperty("peer_connection_error").GetString());
        handler.FailB=false;await s.RefreshPeers();Assert.False(Snapshot(s).GetProperty("network").GetProperty("work_data_stale").GetBoolean());
    }
    [Fact] public async Task WrongPeerIdentityIsNotPublishedAsConnectedWork()
    {
        using var handler=new Handler{WrongIdentity=true};using var http=new HttpClient(handler);
        var s=new RenderSession(Config(Path.Combine(Path.GetTempPath(),"render-network-"+Guid.NewGuid().ToString("N"))),http);
        await s.RefreshPeers();var result=Snapshot(s);
        Assert.Empty(result.GetProperty("peers").EnumerateArray());
        Assert.True(result.GetProperty("network").GetProperty("work_data_stale").GetBoolean());
        Assert.Equal(JsonValueKind.Null,result.GetProperty("network").GetProperty("peer_snapshot_utc").ValueKind);
    }
    [Fact] public void RemoteTemplatesUseDistinctGpuIdentitiesAndRemainUnconfigured()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null&&!Directory.Exists(Path.Combine(root.FullName,"rendering","network")))root=root.Parent;
        Assert.NotNull(root);
        var configs=new[]{"gpu-server-1","gpu-server-2"}.Select(id=>JsonSerializer.Deserialize<RenderConfig>(
            File.ReadAllText(Path.Combine(root!.FullName,"rendering","network",id+".example.json")),RenderNodeHost.Json)!).ToArray();
        Assert.Equal(2,configs.Select(c=>c.PhysicalHostId).Distinct().Count());
        Assert.Equal(2,configs.Select(c=>c.Gpu!.DeviceId).Distinct().Count());
        Assert.Single(configs.Select(c=>c.Native).Distinct());
        Assert.All(configs,c=>{
            Assert.True(c.PreparedSceneMode);Assert.True(c.Native!.EnableGpu);Assert.False(c.ServeUi);
            Assert.Equal(0,c.HeatDemandKwh);
            Assert.All(c.Peers.Values,url=>Assert.EndsWith(".invalid",new Uri(url).Host));
        });
    }

}
