namespace Exchoribur.Core.Storage;

/// <summary>
/// 原子的文件写入:先写同目录的临时文件,写完整了才改名顶替正式文件。
/// 直接往目标文件写的话,中途失败(磁盘满、被杀进程、断电)之后原文件就只剩一个空壳。
/// </summary>
internal static class AtomicFile
{
    /// <summary>
    /// 写一个文件。suffix 是临时文件的后缀,临时文件就放在目标旁边;
    /// 目录不存在会自动建。中途出错时临时文件会被收拾掉,正式文件保持不动。
    /// </summary>
    public static void Write(string path, string suffix, Action<string> write)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + suffix;

        try
        {
            write(temporary);

            // 同一个目录里改名,系统只改目录项,不会把数据再搬一遍。
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>收拾没写完的临时文件。收拾不掉也不算错,真正的失败原因由调用方抛出。</summary>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 忽略:临时文件留着不影响正式文件,下次写会覆盖它。
        }
    }
}
