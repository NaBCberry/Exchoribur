namespace LightFlow.App.Services;

/// <summary>
/// 播放用的节拍器:每隔一小会儿回调一次,告诉调用方"距上次过了多久"。
/// 抽成接口是为了让播放逻辑能测——测试里塞一个假的节拍器,
/// 想推进多少时间就喂多少,不用真等。
/// </summary>
public interface IPlaybackClock
{
    /// <summary>开始走时;每过一帧回调一次,参数是距上次回调经过的时间。</summary>
    void Start(Action<TimeSpan> onTick);

    /// <summary>停下来。再 Start 会重新计时。</summary>
    void Stop();
}
