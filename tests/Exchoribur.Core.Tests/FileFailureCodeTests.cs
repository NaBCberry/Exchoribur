using Exchoribur.Core.Storage;

namespace Exchoribur.Core.Tests;

/// <summary>
/// 错误码归类是分平台的:Windows 认 Win32 错误码,Unix 认 errno。
/// 两套数字有重合(5、32、33 含义完全不同),所以两张表都要钉住。
/// </summary>
public sealed class FileFailureCodeTests
{
    [Theory]
    [InlineData(5, FileFailureReason.AccessDenied)]     // ERROR_ACCESS_DENIED
    [InlineData(32, FileFailureReason.InUse)]           // ERROR_SHARING_VIOLATION
    [InlineData(33, FileFailureReason.InUse)]           // ERROR_LOCK_VIOLATION
    [InlineData(112, FileFailureReason.DiskFull)]       // ERROR_DISK_FULL
    [InlineData(13, FileFailureReason.Unknown)]         // Windows 上 13 不是"没权限"
    public void Windows_codes_are_read_as_win32_errors(int code, FileFailureReason expected)
        => Assert.Equal(expected, FileFailure.ClassifyCode(code, windows: true));

    [Theory]
    [InlineData(13, FileFailureReason.AccessDenied)]        // EACCES
    [InlineData(30, FileFailureReason.AccessDenied)]        // EROFS
    [InlineData(16, FileFailureReason.InUse)]               // EBUSY
    [InlineData(26, FileFailureReason.InUse)]               // ETXTBSY
    [InlineData(28, FileFailureReason.DiskFull)]            // ENOSPC
    [InlineData(5, FileFailureReason.Unknown)]              // Unix 上 5 是 EIO,不当作"没权限"
    public void Unix_codes_are_read_as_errno(int code, FileFailureReason expected)
        => Assert.Equal(expected, FileFailure.ClassifyCode(code, windows: false));

    [Fact]
    public void Unauthorized_access_is_denied_on_every_platform()
    {
        // 这个异常的 HResult 在任何平台上都是 0x80070005;按 errno 查表会把它当成 EIO,
        // 所以类型优先。
        Assert.Equal(FileFailureReason.AccessDenied, FileFailure.Classify(new UnauthorizedAccessException()));
    }
}
