using Exchoribur.Core.Models;

namespace Exchoribur.Core.Tests;

/// <summary>工程模型:名字兜底、改动标记、换时间轴。</summary>
public class TimelineDocumentTests
{
    [Fact]
    public void A_new_document_is_not_marked_as_modified()
    {
        var document = new TimelineDocument("乐鸣东方", Timeline.Empty, "video.mp4");

        Assert.Equal("乐鸣东方", document.Name);
        Assert.Equal("video.mp4", document.MediaPath);
        Assert.False(document.IsModified);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_names_fall_back_to_the_default(string name)
    {
        var document = new TimelineDocument(name, Timeline.Empty);

        Assert.Equal(TimelineDocument.DefaultName, document.Name);
    }

    [Fact]
    public void Renaming_trims_the_name_and_marks_the_document()
    {
        var document = new TimelineDocument("旧名字", Timeline.Empty);

        document.Rename("  新名字  ");

        Assert.Equal("新名字", document.Name);
        Assert.True(document.IsModified);
    }

    [Fact]
    public void Saving_clears_the_modified_flag()
    {
        var document = new TimelineDocument("演出", Timeline.Empty);
        document.AttachMedia("video.mp4");

        document.MarkSaved();

        Assert.False(document.IsModified);
        Assert.Equal("video.mp4", document.MediaPath);
    }
}
