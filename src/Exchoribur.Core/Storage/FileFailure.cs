namespace Exchoribur.Core.Storage;

/// <summary>文件操作失败的原因,用来给用户一句能照着做的提示。</summary>
public enum FileFailureReason
{
    /// <summary>认不出来的错误,只能原样显示系统给的消息。</summary>
    Unknown,

    /// <summary>文件被别的程序占着。解压工具、播放器、网盘同步都可能干这事。</summary>
    InUse,

    /// <summary>没有写入权限:文件或目录是只读的,也可能被别的程序拦着。</summary>
    AccessDenied,

    /// <summary>磁盘写满了。</summary>
    DiskFull,
}

/// <summary>
/// 给文件操作异常归类。Windows 上这类异常的 HRESULT 低 16 位就是 Win32 错误码,
/// 而 .NET 抛的是 IOException 还是 UnauthorizedAccessException 并不稳定,
/// 所以这里只看错误码,不看类型。
/// </summary>
public static class FileFailure
{
    private const int ErrorAccessDenied = 5;
    private const int ErrorSharingViolation = 32;
    private const int ErrorLockViolation = 33;
    private const int ErrorDiskFull = 112;

    /// <summary>Win32 错误码那一族(0x8007xxxx)。别的来源的 HRESULT 低 16 位没有这个含义。</summary>
    private const int Win32Facility = unchecked((int)0x80070000);
    private const int FacilityMask = unchecked((int)0xFFFF0000);

    public static FileFailureReason Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if ((exception.HResult & FacilityMask) != Win32Facility)
        {
            return FileFailureReason.Unknown;
        }

        return (exception.HResult & 0xFFFF) switch
        {
            ErrorSharingViolation or ErrorLockViolation => FileFailureReason.InUse,
            ErrorAccessDenied => FileFailureReason.AccessDenied,
            ErrorDiskFull => FileFailureReason.DiskFull,
            _ => FileFailureReason.Unknown,
        };
    }
}
