using CommunityToolkit.Mvvm.ComponentModel;

namespace Exchoribur.App.TimelineUi;

/// <summary>
/// 时间轴的取景框:现在看的是哪一段时间、放大到什么程度。
/// 时间轴色块和播放头两层共用同一个实例,所以两边画出来的位置永远对得上;
/// 以后音频波形也接进来,波形就能跟时间轴严丝合缝地对齐。
/// </summary>
/// <remarks>
/// 这里只处理"轨道内部的像素",不算左边留给通道名的留白;
/// 控件自己减掉 <see cref="TimelineLayout.TrackLeft"/> 再传进来。
/// </remarks>
public sealed class TimelineViewport : ObservableObject
{
    /// <summary>放大上限:每秒钟占 2000 像素(1 像素 ≈ 0.5 毫秒),再放大也没有信息可看。</summary>
    private const double MaxPixelsPerSecond = 2000;

    /// <summary>
    /// 播放时自动翻页后,播放头落在视口左侧这个比例的位置。
    /// 取小值 = 每次翻一整页;取大值 = 每次只往前挪一点点。
    /// </summary>
    private const double PageLeadingRatio = 0.1;

    private TimeSpan _duration;
    private TimeSpan _contentStart;
    private TimeSpan _start;
    private double _trackWidth;
    private double _scale = 1;

    /// <summary>还没手动缩放时保持"整条铺满",窗口变大变小都自动跟着重算。</summary>
    private bool _isFitToWidth = true;

    /// <summary>整条时间轴的总长。</summary>
    public TimeSpan Duration => _duration;

    /// <summary>可见范围的左边缘时间。</summary>
    public TimeSpan Start => _start;

    /// <summary>可见范围的右边缘时间。</summary>
    public TimeSpan End => _start + VisibleDuration;

    /// <summary>放大倍率:每秒钟占多少像素。数值越大越"放大"。</summary>
    public double Scale => _scale;

    /// <summary>可见范围有多长。完全缩小到铺满时,它就等于总时长。</summary>
    public TimeSpan VisibleDuration
        => _scale > 0 ? TimeSpan.FromSeconds(_trackWidth / _scale) : _duration;

    /// <summary>缩到最小(整条铺满)时的倍率;不能比它更小,否则两边会露出空白。</summary>
    private double MinScale
        => _duration > TimeSpan.Zero && _trackWidth > 0 ? _trackWidth / _duration.TotalSeconds : 1;

    /// <summary>内容的起点。工程里可能有 0 之前的预备片段,所以可能是负数。</summary>
    public TimeSpan ContentStart => _contentStart;

    /// <summary>换了一份从 0 开始的数据。</summary>
    public void SetContent(TimeSpan duration) => SetContent(TimeSpan.Zero, duration);

    /// <summary>
    /// 换了一份数据:内容范围是 [contentStart, contentStart + contentLength],
    /// 视口回到"整条铺满"。起点允许是负数,用来显示 0 之前的预备片段。
    /// </summary>
    public void SetContent(TimeSpan contentStart, TimeSpan contentLength)
    {
        _contentStart = contentStart;
        _duration = contentLength > TimeSpan.Zero ? contentLength : TimeSpan.Zero;
        _isFitToWidth = true;
        _start = _contentStart;
        _scale = MinScale;
        RaiseChanged();
    }

    /// <summary>轨道区域的像素宽度变了(窗口缩放)。没手动缩放过就继续保持铺满。</summary>
    public void SetTrackWidth(double width)
    {
        if (width <= 0 || Math.Abs(width - _trackWidth) < 0.5)
        {
            return;
        }

        _trackWidth = width;

        if (_isFitToWidth)
        {
            _scale = MinScale;
            _start = _contentStart;
        }
        else
        {
            _scale = Math.Clamp(_scale, MinScale, MaxPixelsPerSecond);
            ClampStart();
        }

        RaiseChanged();
    }

    /// <summary>缩放到整条时间轴都看得见,并把左边缘对到 0。</summary>
    public void FitAll()
    {
        _isFitToWidth = true;
        _scale = MinScale;
        _start = _contentStart;
        RaiseChanged();
    }

    /// <summary>
    /// 让画面正好显示 [start, start + length) 这一段(内容和总长不变)。
    /// 块编辑器打开时用它:内容仍是整条轴,只是先把镜头对到要编辑的那一小段。
    /// </summary>
    public void ShowRange(TimeSpan start, TimeSpan length)
    {
        if (_duration <= TimeSpan.Zero || _trackWidth <= 0 || length <= TimeSpan.Zero)
        {
            return;
        }

        _isFitToWidth = false;
        _scale = Math.Clamp(_trackWidth / length.TotalSeconds, MinScale, MaxPixelsPerSecond);
        _start = start;
        ClampStart();
        RaiseChanged();
    }

    /// <summary>
    /// 以某个像素位置为锚点缩放:锚点下面的那一瞬间在屏幕上不动,
    /// 这是"用滚轮放大想看某处"时最符合直觉的行为。
    /// </summary>
    /// <param name="factor">大于 1 放大,小于 1 缩小。</param>
    /// <param name="anchorX">锚点的轨道内横坐标。</param>
    public void Zoom(double factor, double anchorX)
    {
        if (_duration <= TimeSpan.Zero || _trackWidth <= 0 || factor <= 0)
        {
            return;
        }

        var anchorTime = MapX(anchorX);
        var scale = Math.Clamp(_scale * factor, MinScale, MaxPixelsPerSecond);
        if (scale == _scale)
        {
            return;
        }

        _scale = scale;
        _isFitToWidth = false;
        _start = anchorTime - TimeSpan.FromSeconds(anchorX / _scale);
        ClampStart();
        RaiseChanged();
    }

    /// <summary>以可见范围的中心为锚点缩放,给工具栏上的放大/缩小按钮用。</summary>
    public void ZoomBy(double factor) => Zoom(factor, _trackWidth / 2);

    /// <summary>平移:正数表示内容向左挪(看更晚的时间)。</summary>
    public void PanByPixels(double deltaX)
    {
        if (_duration <= TimeSpan.Zero || _trackWidth <= 0 || _scale <= 0)
        {
            return;
        }

        var previousStart = _start;
        _start -= TimeSpan.FromSeconds(deltaX / _scale);
        ClampStart();

        // 已经顶到两端时位置没变,就不用再叫醒界面重画一遍。
        if (_start != previousStart)
        {
            RaiseChanged();
        }
    }

    /// <summary>
    /// 把某个时间点带进可见范围:已经在画面里就不动,在右边就让它落在
    /// 靠右的位置(而不是贴着边)。
    /// 放大以后点"跳到结尾"时用得上,否则播放头会跑到屏幕外看不见。
    /// </summary>
    public void EnsureVisible(TimeSpan time)
    {
        if (_duration <= TimeSpan.Zero || _trackWidth <= 0 || _scale <= 0)
        {
            return;
        }

        var visible = VisibleDuration;

        if (time < _start)
        {
            _start = time;
        }
        else if (time >= _start + visible)
        {
            _start = time - TimeSpan.FromSeconds(visible.TotalSeconds * 0.9);
        }
        else
        {
            return;
        }

        ClampStart();
        RaiseChanged();
    }

    /// <summary>
    /// 播放时用:播放头跑出画面就整页翻过去,翻完它落在视口左边靠右一点的位置
    /// (而不是留在右边只往前挪一点点)。还在画面里就什么都不做。
    /// </summary>
    public void PageTo(TimeSpan time)
    {
        if (_duration <= TimeSpan.Zero || _trackWidth <= 0 || _scale <= 0)
        {
            return;
        }

        var visible = VisibleDuration;
        if (time >= _start && time < _start + visible)
        {
            return;
        }

        _start = time - TimeSpan.FromSeconds(visible.TotalSeconds * PageLeadingRatio);
        ClampStart();
        RaiseChanged();
    }

    /// <summary>时间 → 轨道内横坐标。</summary>
    public double MapTime(TimeSpan time)
        => (time - _start).TotalSeconds * _scale;

    /// <summary>轨道内横坐标 → 时间;超出可见范围时夹在两端。</summary>
    public TimeSpan MapX(double x)
    {
        if (_scale <= 0)
        {
            return _start;
        }

        var visible = VisibleDuration;
        var seconds = Math.Clamp(x / _scale, 0, visible.TotalSeconds);
        return _start + TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// 把左边缘拉回合法范围:不能小于 0,也不能让它右边露出时间轴之外。
    /// 一直缩到铺满时左边缘只能是 0。
    /// </summary>
    private void ClampStart()
    {
        var maxStart = _contentStart + _duration - VisibleDuration;
        if (maxStart <= _contentStart)
        {
            _start = _contentStart;
            return;
        }

        if (_start < _contentStart)
        {
            _start = _contentStart;
        }
        else if (_start > maxStart)
        {
            _start = maxStart;
        }
    }

    private void RaiseChanged()
    {
        OnPropertyChanged(nameof(Start));
        OnPropertyChanged(nameof(Scale));
    }
}
