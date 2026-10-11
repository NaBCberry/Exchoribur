namespace Exchoribur.App.ViewModels;

/// <summary>链接图标该显示成什么样子。</summary>
public enum LinkIndicator
{
    /// <summary>没选中块:常态白色。</summary>
    Idle,

    /// <summary>选中的块全都已经有链接:亮黄色,再点一下解除。</summary>
    Linked,

    /// <summary>选中的块里既有已链接的也有没链接的:紫色,点一下把它们重新链接成一伙。</summary>
    Mixed,
}
