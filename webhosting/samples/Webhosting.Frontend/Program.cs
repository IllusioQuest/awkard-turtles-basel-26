var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
string node = System.Net.WebUtility.HtmlEncode(builder.Configuration["NodeId"] ?? "frontend");
app.MapGet("/health", () => new { node_id = builder.Configuration["NodeId"] ?? "frontend", component = "frontend" });
app.MapGet("/", () => Results.Content("""
<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>Separated hosting lab</title><style>body{font:18px system-ui;max-width:760px;margin:60px auto;padding:24px;background:#101820;color:#eaf2f8}pre{padding:24px;background:#1c2c38;white-space:pre-wrap}</style>
<h1>One website, separate components</h1><p>Frontend node: FRONTEND_NODE</p>
<p>This node serves the page. API requests go through the gateway to a separate node pool.</p>
<pre id="status">Connecting to API…</pre>
<p>Synthetic data only. Component isolation does not hide data from node operators.</p>
<script>async function refresh(){try{const r=await fetch('/api/server',{cache:'no-store'});if(!r.ok)throw Error('HTTP '+r.status);const data=await r.json();document.getElementById('status').textContent='Gateway: '+r.headers.get('X-Webhosting-Gateway')+'\nAPI node: '+data.node_id+'\nComponent: '+data.component;}catch(e){document.getElementById('status').textContent='Recovering: '+e.message;}}refresh();setInterval(refresh,1000);</script></html>
""".Replace("FRONTEND_NODE", node), "text/html"));
app.Run();
