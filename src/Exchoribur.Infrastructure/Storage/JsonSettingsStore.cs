using Exchoribur.Application.Abstractions;
using Exchoribur.Application.Settings;

namespace Exchoribur.Infrastructure.Storage;

public sealed class JsonSettingsStore : ISettingsStore
{
    public string DefaultPath => SettingsStore.DefaultPath;
    public AppSettings Load(string path) => SettingsStore.Load(path);
    public void Save(string path, AppSettings settings) => SettingsStore.Save(path, settings);
}
