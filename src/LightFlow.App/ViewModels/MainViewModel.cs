using CommunityToolkit.Mvvm.ComponentModel;
using LightFlow.Core.Models;

namespace LightFlow.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        // 先用生成的样例数据把界面搭起来,接入真实工程文件是下一步。
        Timeline = new Timeline(CreateSampleFrames(), []);
        PlayheadTime = TimeSpan.FromMilliseconds(1320);
    }

    public Timeline Timeline { get; }

    public IReadOnlyList<Frame> Frames => Timeline.Frames;

    [ObservableProperty]
    public partial TimeSpan PlayheadTime { get; set; }

    [ObservableProperty]
    public partial Frame? CurrentFrame { get; set; }

    partial void OnPlayheadTimeChanged(TimeSpan value)
    {
        // 用领域里的阶跃语义取帧:播放头落在两帧之间时,沿用前一帧的状态。
        CurrentFrame = Timeline.GetFrameAt(value);
    }

    private static IReadOnlyList<Frame> CreateSampleFrames()
    {
        var frames = new List<Frame>();

        for (var index = 0; index < 48; index++)
        {
            var channels = new ChannelState[Frame.ChannelCount];
            for (var channel = 0; channel < Frame.ChannelCount; channel++)
            {
                var red = (byte)((index + channel) % 16);
                var green = (byte)((index * 2 + channel * 3) % 16);
                var blue = (byte)((index * 3 + channel * 5) % 16);
                channels[channel] = new ChannelState(new LightColor(red, green, blue), FlashMode.Solid);
            }

            frames.Add(new Frame(TimeSpan.FromMilliseconds(index * 180), channels));
        }

        return frames;
    }
}
