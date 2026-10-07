using Exchoribur.Core.Models;

namespace Exchoribur.Core.Editing;

/// <summary>
/// 一步编辑。命令自己带着"改之前"和"改之后"的数据,所以撤销就是把命令
/// 反着再应用一次,不需要回滚现场。
/// </summary>
public interface ITimelineEdit
{
    /// <summary>操作名,菜单显示"撤销 设置颜色"用的就是它。</summary>
    string Name { get; }

    /// <summary>正向应用,返回新的时间轴。</summary>
    Timeline Apply(Timeline timeline);

    /// <summary>反过来应用,返回撤销后的时间轴。</summary>
    Timeline Revert(Timeline timeline);
}
