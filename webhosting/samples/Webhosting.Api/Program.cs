using System.Text.Json;
var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
var app = builder.Build();
string node = builder.Configuration["NodeId"] ?? "api";
app.MapGet("/health", () => new { NodeId = node, Component = "api" });
app.MapGet("/api/slow", async (CancellationToken token) =>
{
    await Task.Delay(TimeSpan.FromSeconds(builder.Configuration.GetValue<double>("Demo:SlowRequestSeconds")), token);
    return new { NodeId = node, Component = "api" };
});
app.MapGet("/api/server", () => new { NodeId = node, Component = "api", DataClass = "synthetic" });
app.Run();
