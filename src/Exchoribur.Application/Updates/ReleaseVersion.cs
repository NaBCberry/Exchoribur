namespace Exchoribur.Application.Updates;

/// <summary>
/// 一个发布版本号:<c>1.2.3</c> 或 <c>1.2.3-beta.1</c>(GitHub 标签上多一个 v 前缀)。
/// 用来判断 Release 上那个版本比现在跑的新不新。
/// </summary>
/// <remarks>
/// 比较规则跟语义化版本一致:先比三段数字;数字相同时,正式版高于预发布版,
/// 两个预发布之间按点分段比较(数字段按数值,其余按 ASCII 顺序),短的更低。
/// </remarks>
public readonly record struct ReleaseVersion(int Major, int Minor, int Patch, string? Prerelease)
    : IComparable<ReleaseVersion>
{
    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();

        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
        {
            trimmed = trimmed[1..];
        }

        var sections = trimmed.Split('-', 2);
        var numbers = sections[0].Split('.');

        if (numbers.Length != 3
            || !TryReadNumber(numbers[0], out var major)
            || !TryReadNumber(numbers[1], out var minor)
            || !TryReadNumber(numbers[2], out var patch))
        {
            return false;
        }

        var prerelease = sections.Length > 1 ? sections[1] : null;

        // 预发布段里空一段(1.0.0-beta.)或整个空着都算写错
        if (prerelease is not null
            && (prerelease.Length == 0 || prerelease.Split('.').Any(segment => segment.Length == 0)))
        {
            return false;
        }

        version = new ReleaseVersion(major, minor, patch, prerelease);
        return true;
    }

    public int CompareTo(ReleaseVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
        {
            return result;
        }

        result = Patch.CompareTo(other.Patch);
        if (result != 0)
        {
            return result;
        }

        if (Prerelease is null && other.Prerelease is null)
        {
            return 0;
        }

        // 同号时正式版更高:0.2.0 > 0.2.0-beta.2
        if (Prerelease is null)
        {
            return 1;
        }

        return other.Prerelease is null ? -1 : ComparePrerelease(Prerelease, other.Prerelease);
    }

    public override string ToString() => Prerelease is null
        ? $"{Major}.{Minor}.{Patch}"
        : $"{Major}.{Minor}.{Patch}-{Prerelease}";

    private static bool TryReadNumber(string text, out int value) =>
        int.TryParse(text, out value) && value >= 0;

    private static int ComparePrerelease(string left, string right)
    {
        var leftSegments = left.Split('.');
        var rightSegments = right.Split('.');

        for (var index = 0; index < Math.Max(leftSegments.Length, rightSegments.Length); index++)
        {
            if (index >= leftSegments.Length)
            {
                return -1;
            }

            if (index >= rightSegments.Length)
            {
                return 1;
            }

            var leftIsNumber = int.TryParse(leftSegments[index], out var leftNumber);
            var rightIsNumber = int.TryParse(rightSegments[index], out var rightNumber);

            var result = (leftIsNumber, rightIsNumber) switch
            {
                // 数字段比字母段低:beta.1 < beta.x
                (true, true) => leftNumber.CompareTo(rightNumber),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(leftSegments[index], rightSegments[index]),
            };

            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }
}
