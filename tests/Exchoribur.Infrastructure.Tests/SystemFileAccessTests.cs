using Exchoribur.Infrastructure.Storage;

namespace Exchoribur.Infrastructure.Tests;

public sealed class SystemFileAccessTests
{
    [Fact]
    public async Task Preparing_a_directory_creates_missing_parents_and_can_be_repeated()
    {
        var root = Path.Combine(Path.GetTempPath(), $"exchoribur-directory-{Guid.NewGuid():N}");
        var path = Path.Combine(root, "中文路径", "settings");
        try
        {
            var files = new SystemFileAccess();
            await files.EnsureDirectoryAsync(path);
            await files.EnsureDirectoryAsync(path);
            Assert.True(Directory.Exists(path));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
