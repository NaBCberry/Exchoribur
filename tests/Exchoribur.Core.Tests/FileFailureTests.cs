using Exchoribur.Core.Storage;

namespace Exchoribur.Core.Tests;

/// <summary>保存失败的原因能不能认出来:认出来了界面才能给一句能照着做的话。</summary>
public sealed class FileFailureTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"exchoribur-fileerror-{Guid.NewGuid():N}");

    public FileFailureTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [WindowsFact]
    public void A_file_held_open_by_another_program_counts_as_in_use()
    {
        var path = Path.Combine(_directory, "被占用.csv");
        File.WriteAllText(path, "open");

        // 别的程序只允许别人读,这时候写入一定失败。
        using var holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var exception = Assert.ThrowsAny<Exception>(() => File.Create(path).Dispose());

        Assert.Equal(FileFailureReason.InUse, FileFailure.Classify(exception));
    }

    [Fact]
    public void System_error_codes_map_to_reasons()
    {
        // 0x80070020 共享冲突、0x80070021 区域被锁、0x80070005 拒绝访问、0x80070070 磁盘满。
        Assert.Equal(FileFailureReason.InUse, Classify(unchecked((int)0x80070020)));
        Assert.Equal(FileFailureReason.InUse, Classify(unchecked((int)0x80070021)));
        Assert.Equal(FileFailureReason.AccessDenied, Classify(unchecked((int)0x80070005)));
        Assert.Equal(FileFailureReason.DiskFull, Classify(unchecked((int)0x80070070)));
    }

    [Fact]
    public void Unknown_errors_stay_unknown()
    {
        // 托管异常也有 HResult,低 16 位凑巧等于某个错误码时不能乱认。
        Assert.Equal(FileFailureReason.Unknown, FileFailure.Classify(new InvalidOperationException()));
        Assert.Equal(
            FileFailureReason.Unknown,
            FileFailure.Classify(new IOException("随便一个失败", unchecked((int)0x80131500))));
    }

    private static FileFailureReason Classify(int hresult)
        => FileFailure.Classify(new IOException("系统给的失败", hresult));
}
