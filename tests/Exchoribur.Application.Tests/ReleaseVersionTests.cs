using Exchoribur.Application.Updates;

namespace Exchoribur.Application.Tests;

public class ReleaseVersionTests
{
    [Theory]
    [InlineData("0.1.1", "0.1.1", 0)]
    [InlineData("v0.1.1", "0.1.1", 0)]
    [InlineData("0.2.0", "0.1.9", 1)]
    [InlineData("0.1.10", "0.1.9", 1)]
    [InlineData("1.0.0", "0.99.99", 1)]
    // 同号时正式版比预发布高
    [InlineData("0.2.0", "0.2.0-beta.1", 1)]
    [InlineData("0.2.0-beta.2", "0.2.0-beta.1", 1)]
    [InlineData("0.2.0-beta.10", "0.2.0-beta.9", 1)]
    [InlineData("0.2.0-beta.1", "0.2.0-alpha.9", 1)]
    [InlineData("0.2.0-beta", "0.2.0-beta.1", -1)]
    public void Versions_compare_as_expected(string left, string right, int expectedSign)
    {
        Assert.True(ReleaseVersion.TryParse(left, out var a));
        Assert.True(ReleaseVersion.TryParse(right, out var b));

        Assert.Equal(expectedSign, Math.Sign(a.CompareTo(b)));
        Assert.Equal(-expectedSign, Math.Sign(b.CompareTo(a)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.x")]
    [InlineData("-1.2.3")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-beta.")]
    [InlineData("v")]
    public void Bad_versions_are_rejected(string text)
    {
        Assert.False(ReleaseVersion.TryParse(text, out _));
    }

    [Fact]
    public void The_text_round_trips()
    {
        Assert.True(ReleaseVersion.TryParse("v0.1.1-beta.1", out var version));

        Assert.Equal("0.1.1-beta.1", version.ToString());
        Assert.Equal(0, version.Major);
        Assert.Equal(1, version.Minor);
        Assert.Equal(1, version.Patch);
        Assert.Equal("beta.1", version.Prerelease);
    }
}
