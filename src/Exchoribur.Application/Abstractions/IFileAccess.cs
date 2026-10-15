namespace Exchoribur.Application.Abstractions;

/// <summary>查询外部文件是否存在,不向界面泄漏磁盘实现。</summary>
public interface IFileAccess
{
    bool Exists(string path);
}
