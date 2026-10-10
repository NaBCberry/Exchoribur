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
/// 给文件操作异常归类。这类异常的 HRESULT 低 16 位就是系统的错误码,
/// 而 .NET 抛的是 IOException 还是 UnauthorizedAccessException 并不稳定,
/// 所以主要看错误码。
/// </summary>
/// <remarks>
/// 错误码的"含义"跟平台绑在一起:Windows 上低 16 位是 Win32 错误码,Unix 上放的是 errno,
/// 两套数字互相冲突(5 在 Windows 是"拒绝访问",在 Unix 是 EIO;32 在 Windows 是"共享冲突",
/// 在 Unix 是 EPIPE)。所以必须按平台分开查表,不能混着认。
/// </remarks>
public static class FileFailure
{
    // Windows(Win32)错误码。
    private const int ErrorAccessDenied = 5;
    private const int ErrorSharingViolation = 32;
    private const int ErrorLockViolation = 33;
    private const int ErrorDiskFull = 112;

    // Unix errno。只列真的会影响用户的那几个。
    private const int ErrnoPermissionDenied = 13;       // EACCES
    private const int ErrnoBusy = 16;                   // EBUSY:文件被挂载/被占用
    private const int ErrnoTextFileBusy = 26;           // ETXTBSY:可执行文件正在运行
    private const int ErrnoNoSpaceLeft = 28;            // ENOSPC:磁盘写满
    private const int ErrnoReadOnlyFileSystem = 30;     // EROFS:只读文件系统

    /// <summary>Win32 错误码那一族(0x8007xxxx)。别的来源的 HRESULT 低 16 位没有这个含义。</summary>
    private const int Win32Facility = unchecked((int)0x80070000);
    private const int FacilityMask = unchecked((int)0xFFFF0000);

    public static FileFailureReason Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // 类型是最可靠的线索:这个异常的 HResult 在任何平台上都是"拒绝访问",
        // 但如果按 Unix 的 errno 去查表,5 会被当成 EIO。
        if (exception is UnauthorizedAccessException)
        {
            return FileFailureReason.AccessDenied;
        }

        if ((exception.HResult & FacilityMask) != Win32Facility)
        {
            return FileFailureReason.Unknown;
        }

        return ClassifyCode(exception.HResult & 0xFFFF, OperatingSystem.IsWindows());
    }

    /// <summary>按错误码归类。windows 为 true 时按 Win32 错误码看,否则按 errno 看。</summary>
    internal static FileFailureReason ClassifyCode(int code, bool windows)
        => windows
            ? code switch
            {
                ErrorSharingViolation or ErrorLockViolation => FileFailureReason.InUse,
                ErrorAccessDenied => FileFailureReason.AccessDenied,
                ErrorDiskFull => FileFailureReason.DiskFull,
                _ => FileFailureReason.Unknown,
            }
            : code switch
            {
                ErrnoBusy or ErrnoTextFileBusy => FileFailureReason.InUse,
                ErrnoPermissionDenied or ErrnoReadOnlyFileSystem => FileFailureReason.AccessDenied,
                ErrnoNoSpaceLeft => FileFailureReason.DiskFull,
                _ => FileFailureReason.Unknown,
            };
}
