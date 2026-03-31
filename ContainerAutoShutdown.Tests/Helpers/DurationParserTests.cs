using ContainerAutoShutdown.Helpers;

namespace ContainerAutoShutdown.Tests.Helpers;

public class DurationParserTests
{
    [Theory]
    [InlineData("30s", 30)]
    [InlineData("5m", 300)]
    [InlineData("2h", 7200)]
    [InlineData("1d", 86400)]
    [InlineData("1.5h", 5400)]
    [InlineData("0.5d", 43200)]
    [InlineData("90s", 90)]
    [InlineData("  2h  ", 7200)]
    [InlineData("2H", 7200)]
    public void TryParse_ValidInput_ReturnsTrueAndExpectedDuration(string input, double expectedSeconds)
    {
        var result = DurationParser.TryParse(input, out var duration);

        Assert.True(result);
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), duration);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0h")]
    [InlineData("-1h")]
    [InlineData("h")]
    [InlineData("2x")]
    [InlineData("2")]
    [InlineData("-5m")]
    [InlineData("0s")]
    public void TryParse_InvalidInput_ReturnsFalseAndZeroDuration(string? input)
    {
        var result = DurationParser.TryParse(input, out var duration);

        Assert.False(result);
        Assert.Equal(TimeSpan.Zero, duration);
    }
}
