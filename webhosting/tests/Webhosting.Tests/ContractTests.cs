using System.Text.Json;
using Webhosting.Contracts;

namespace Webhosting.Tests;

public class ContractTests
{
    private static ComponentSpec Component(string id, string path) => new(id, "demo:local", 8080, "/health", 2, 1, 128, path);

    [Fact]
    public void FrontendAndApiUseNestedDistinctPrefixes()
    {
        var spec = new DeploymentSpec("demo", [Component("frontend", "/"), Component("api", "/api")]);
        Assert.Empty(DeploymentValidation.Validate("demo", spec));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(spec, options);
        Assert.Contains("\"cpuLimitCores\"", json);
        var parsed = JsonSerializer.Deserialize<DeploymentSpec>(json, options)!;
        Assert.Equal(spec.Components, parsed.Components);
    }

    [Theory]
    [InlineData("api")]
    [InlineData("//api")]
    [InlineData("/api/")]
    [InlineData("/../api")]
    [InlineData("/api?x=1")]
    public void InvalidRoutePrefixesAreRejected(string prefix)
    {
        Assert.NotEmpty(DeploymentValidation.Validate("demo", new("demo", [Component("api", prefix)])));
    }

    [Fact]
    public void DuplicateRoutesAndMismatchedIdentityAreRejected()
    {
        Assert.NotEmpty(DeploymentValidation.Validate("other", new("demo", [Component("api", "/api")])));
        Assert.NotEmpty(DeploymentValidation.Validate("demo", new("demo", [Component("one", "/api"), Component("two", "/api")])));
    }

    [Fact]
    public void InvalidResourcesAndNullBodiesAreRejected()
    {
        Assert.NotEmpty(DeploymentValidation.Validate("demo", null));
        Assert.NotEmpty(DeploymentValidation.Validate("demo", new("demo", [Component("api", "/api") with { CpuLimitCores = double.PositiveInfinity }])));
        Assert.NotEmpty(DeploymentValidation.Validate("demo", new("demo", [Component("api", "/api") with { MemoryLimitMiB = 0 }])));
    }
}
