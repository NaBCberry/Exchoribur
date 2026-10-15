using Avalonia.Media.Imaging;
using Exchoribur.Application.Abstractions;

namespace Exchoribur.App.Services;

/// <summary>媒体播放控制加上界面位图和界面线程刷新通知。</summary>
public interface IVideoService : IMediaPlayback
{
    WriteableBitmap? Frame { get; }
    event EventHandler? FrameUpdated;
}
