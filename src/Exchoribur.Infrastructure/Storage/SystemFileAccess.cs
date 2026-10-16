using Exchoribur.Application.Abstractions;

namespace Exchoribur.Infrastructure.Storage;

public sealed class SystemFileAccess : IFileAccess
{
    public bool Exists(string path) => File.Exists(path);
    public Task EnsureDirectoryAsync(string path) => Task.Run(() => Directory.CreateDirectory(path));
}
