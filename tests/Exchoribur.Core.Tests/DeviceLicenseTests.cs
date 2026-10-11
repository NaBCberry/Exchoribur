using Exchoribur.Core.Devices;

namespace Exchoribur.Core.Tests;

public class DeviceLicenseTests
{
    private const string Valid = "AA:BB:CC:DD:EE:FF|1893456000|0A1B2C";

    [Fact]
    public void A_well_formed_license_parses()
    {
        Assert.True(DeviceLicense.TryParse(Valid, out var license, out var error));

        Assert.Null(error);
        Assert.NotNull(license);
        Assert.Equal("AA:BB:CC:DD:EE:FF", license.MacText);
        Assert.Equal(1_893_456_000L, license.ExpireUnixTime);
        Assert.Equal<byte>([0x0A, 0x1B, 0x2C], license.Signature);
    }

    [Fact]
    public void Mac_may_be_written_without_separators()
    {
        Assert.True(DeviceLicense.TryParse("aabbccddeeff|1893456000|FF", out var license, out _));

        Assert.Equal("AA:BB:CC:DD:EE:FF", license!.MacText);
    }

    [Theory]
    [InlineData("", "空")]
    [InlineData("AA:BB:CC:DD:EE:FF|1893456000", "三段")]
    [InlineData("AA:BB:CC|1893456000|FF", "十二位")]
    [InlineData("AA:BB:CC:DD:EE:GG|1893456000|FF", "十二位")]
    [InlineData("AA:BB:CC:DD:EE:FF|abc|FF", "unix")]
    [InlineData("AA:BB:CC:DD:EE:FF|1893456000|0A1", "偶数位")]
    [InlineData("AA:BB:CC:DD:EE:FF|1893456000|", "偶数位")]
    public void A_bad_license_explains_what_is_wrong(string text, string expectedFragment)
    {
        Assert.False(DeviceLicense.TryParse(text, out var license, out var error));

        Assert.Null(license);
        Assert.Contains(expectedFragment, error!);
    }

    [Fact]
    public void A_signature_longer_than_the_protocol_allows_is_rejected()
    {
        var tooLong = new string('A', (DeviceLicense.MaxSignatureBytes + 1) * 2);

        Assert.False(DeviceLicense.TryParse($"AA:BB:CC:DD:EE:FF|1893456000|{tooLong}", out _, out var error));

        Assert.Contains("72", error!);
    }

    [Fact]
    public void Remaining_time_counts_down_to_the_expiry()
    {
        DeviceLicense.TryParse(Valid, out var license, out _);
        var now = DateTimeOffset.FromUnixTimeSeconds(1_893_456_000 - 90);

        Assert.False(license!.IsExpired(now));
        Assert.Equal(TimeSpan.FromSeconds(90), license.Remaining(now));

        // 过期之后是负数,界面照实说"过期多久了"。
        Assert.True(license.IsExpired(now.AddSeconds(120)));
        Assert.Equal(TimeSpan.FromSeconds(-30), license.Remaining(now.AddSeconds(120)));
    }
}
