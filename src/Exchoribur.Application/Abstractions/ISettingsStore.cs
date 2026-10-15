using Exchoribur.Application.Settings;

namespace Exchoribur.Application.Abstractions;

public interface ISettingsStore
{
    string DefaultPath { get; }
    AppSettings Load(string path);
    void Save(string path, AppSettings settings);
}
