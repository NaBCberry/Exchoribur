namespace Exchoribur.App.ViewModels.Settings;

/// <summary>只在持久化偏好变化时通知父模型;普通界面刷新不会请求保存。</summary>
public abstract class SettingsSectionViewModel : ViewModelBase, IDisposable
{
    protected bool IsDisposed { get; private set; }
    public event Action? PreferencesChanged;
    protected void NotifyPreferencesChanged()
    {
        if (!IsDisposed) PreferencesChanged?.Invoke();
    }

    public virtual void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        PreferencesChanged = null;
    }
}
