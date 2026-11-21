var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var instanceId = builder.Configuration["Sample:InstanceId"] ?? Environment.MachineName;
app.MapGet("/health", () => Results.Ok());
app.MapGet("/api/server", () => new { instanceId, runtime = System.Runtime.InteropServices.RuntimeInformation.OSDescription });
app.MapGet("/", () => Results.Content("""
<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>Webhosting lab</title><style>body{font:18px system-ui;max-width:760px;margin:70px auto;padding:24px;background:#101820;color:#eaf2f8}code{color:#76d7c4}pre{padding:24px;background:#1c2c38;white-space:pre-wrap}small{color:#b0c2ce}</style>
<h1>Webhosting lab</h1><p>This page uses one URL while Linux gateway and application replicas change.</p>
<pre id="status">Connecting…</pre><small>Prototype: crash recovery may interrupt requests. This is not the heat simulation.</small>
<script>async function refresh(){try{const r=await fetch('/api/server',{cache:'no-store'});if(!r.ok)throw Error('HTTP '+r.status);const data=await r.json();document.getElementById('status').textContent='Gateway: '+r.headers.get('X-Webhosting-Gateway')+'\nApplication: '+data.instanceId+'\nRuntime: '+data.runtime;}catch(e){document.getElementById('status').textContent='Request failed; retrying. '+e.message;}}refresh();setInterval(refresh,1000);</script></html>
""", "text/html"));
app.Run();
