using System.Text.Json;
var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
var app = builder.Build();
string node = builder.Configuration["NodeId"] ?? "api";
app.MapGet("/health", () => Results.Ok());
app.MapGet("/api/server", () => new { NodeId = node, Component = "api", DataClass = "synthetic" });
app.Run();
