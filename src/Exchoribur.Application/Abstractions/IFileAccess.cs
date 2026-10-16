namespace Exchoribur.Application.Abstractions;

/// <summary>查询文件及准备外部打开所需目录,不向界面泄漏磁盘实现。</summary>
public interface IFileAccess
{
    bool Exists(string path);
    Task EnsureDirectoryAsync(string path);
}
