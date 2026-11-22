using System.Text.Json;
using System.Text.Json.Nodes;

internal static class RenderFrameStore
{
    private const int MaximumImageBytes=16*1024*1024;
    internal static string PathFor(string assets,string hash)
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(hash,"^[a-f0-9]{64}$"))throw new InvalidDataException("Invalid frame hash");
        return Path.Combine(assets,"frame-"+hash+".png");
    }
    internal static JsonElement Publish(string assets,string source,JsonElement metadata)
    {
        byte[] bytes=File.ReadAllBytes(source);
        if(bytes.Length==0||bytes.Length>MaximumImageBytes)throw new InvalidDataException("Invalid frame size");
        string hash=NativeRenderRuntime.Hash(bytes),target=PathFor(assets,hash);
        if(!File.Exists(target)){
            string temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";
            using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(bytes);f.Flush(true);}
            try{File.Move(temp,target);}catch(IOException)when(File.Exists(target)){File.Delete(temp);}
        }
        if(NativeRenderRuntime.Hash(File.ReadAllBytes(target))!=hash)throw new InvalidDataException("Shared frame changed");
        var node=JsonNode.Parse(metadata.GetRawText())!.AsObject();node["image_sha256"]=hash;
        return JsonSerializer.SerializeToElement(node,RenderNodeHost.Json);
    }
    internal static string VerifiedPath(string assets,JsonElement metadata)
    {
        string hash=metadata.GetProperty("image_sha256").GetString()!,path=PathFor(assets,hash);
        var info=new FileInfo(path);
        if(!info.Exists||info.Length>MaximumImageBytes||NativeRenderRuntime.Hash(File.ReadAllBytes(path))!=hash)
            throw new InvalidDataException("Shared frame absent or corrupted");
        return path;
    }
    internal static RenderArtifact[] Recover(LocalNodeIdentity identity,string assets,IReadOnlySet<string> ids,PreparedScene scene)
    {
        var results=new List<RenderArtifact>();
        foreach(string path in Directory.GetFiles(Path.Combine(identity.StateDirectory,"native-attempts"),"owned.json",SearchOption.AllDirectories)){
            string dir=Path.GetDirectoryName(path)!;
            var own=JsonSerializer.Deserialize<NativeOwnership>(File.ReadAllText(path),RenderNodeHost.Json)!;
            if(own.FleetId!="landscape."+identity.PeerId||own.AttemptId!=Path.GetFileName(dir))throw new InvalidDataException("Fenced ownership identity mismatch");
            if(!own.Released||own.ExitCode!=0)continue;
            string job=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(dir,"job.json"))).GetProperty("job_id").GetString()!;
            if(!ids.Contains(job))continue;
            var meta=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(dir,"result.json")));
            if(meta.GetProperty("stage").GetString()!="frame"||meta.GetProperty("scene_sha256").GetString()!=scene.SceneSha256||
               meta.GetProperty("recipe_id").GetString()!=scene.RecipeId||meta.GetProperty("build_hash").GetString()!=scene.BuildHash)
                throw new InvalidDataException("Fenced frame identity mismatch");
            if(!job.EndsWith("-"+meta.GetProperty("frame").GetInt32().ToString("D3"),StringComparison.Ordinal))throw new InvalidDataException("Frame number mismatch");
            VerifiedPath(assets,meta);results.Add(new(job,identity.PeerId,meta));
        }
        return results.ToArray();
    }
}
