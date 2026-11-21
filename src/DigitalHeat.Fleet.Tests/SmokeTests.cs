using Xunit;

namespace DigitalHeat.Fleet.Tests;

public class SmokeTests
{
    [Fact]
    public void RngIsDeterministic()
    {
        var a = new Rng(42); var b = new Rng(42);
        for (int i = 0; i < 100; i++) Assert.Equal(a.NextDouble(), b.NextDouble());
    }
}
