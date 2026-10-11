using System.Globalization;
using Exchoribur.App.Resources;
using Exchoribur.Core.Devices;

namespace Exchoribur.App.ViewModels;

/// <summary>把许可串翻成界面上的一句话。面板和设置页共用一份说法。</summary>
internal static class DeviceLicenseSummary
{
    public static string Describe(string? text, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Strings.DeviceLicenseNotConfigured;
        }

        if (!DeviceLicense.TryParse(text, out var license, out var error))
        {
            return string.Format(Strings.DeviceLicenseInvalidFormat, error);
        }

        var expiry = license!.ExpireAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        return license.IsExpired(now)
            ? string.Format(Strings.DeviceLicenseExpiredFormat, expiry)
            : string.Format(Strings.DeviceLicenseValidFormat, expiry);
    }
}
