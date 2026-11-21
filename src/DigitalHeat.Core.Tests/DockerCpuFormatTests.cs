using DigitalHeat.JobRuntime;
using Xunit;

namespace DigitalHeat.Core.Tests;

public class DockerCpuFormatTests
{
    [Theory]
    [InlineData(0.10000000000000002, "0.1")]   // 0.1 x 1 core from a slider: Docker rejected the raw value
    [InlineData(0.6, "0.6")]
    [InlineData(1.0, "1")]
    [InlineData(0.333333, "0.33")]
    [InlineData(0.001, "0.01")]                // Docker's minimum granularity
    public void CpuLimitHasAtMostTwoDecimals(double cores, string expected) =>
        Assert.Equal(expected, DockerCliContainerControl.FormatCpus(cores));
}
