using Exchoribur.Core.Models;
using Exchoribur.Infrastructure.Storage;

namespace Exchoribur.Infrastructure.Tests;

public sealed class LegacyFrameEffectImportTests
{
    private const string LegacyJson = """
        {"Version":1,"Blocks":[{"Name":"legacy","Channel":0,"Start":0,"Length":5000,
        "Frames":[{"Offset":0,"Mode":0,"Red":15},{"Offset":1000,"Mode":11,"Green":15},
        {"Offset":2000,"Mode":11,"Blue":15},{"Offset":3000,"Mode":11,"Red":15},
        {"Offset":4000,"Mode":0,"Red":15}],
        "Effects":[{"Offset":2000,"Length":1000,"Mode":1}]}],"Markers":[]}
        """;

    [Fact]
    public void Old_effect_lists_are_completed_without_covering_existing_effects()
    {
        var block = Load();
        Assert.Equal(3, block.Effects.Count);
        var imported = block.Effects.Where(effect => effect.Mode == FlashMode.Blink4Hz).OrderBy(effect => effect.Offset).ToArray();
        Assert.Equal([1, 3], imported.Select(effect => effect.Offset.TotalSeconds));
        Assert.All(imported, effect => Assert.Equal(TimeSpan.FromSeconds(1), effect.Length));
        Assert.Equal(FlashMode.Blink4Hz, block.GetStateAt(TimeSpan.FromSeconds(1.5)).Mode);
        Assert.Equal(FlashMode.Blink1Hz, block.GetStateAt(TimeSpan.FromSeconds(2.5)).Mode);
        Assert.Equal(FlashMode.Blink4Hz, block.GetStateAt(TimeSpan.FromSeconds(3.5)).Mode);
        Assert.Equal(FlashMode.Blink4Hz, block.Frames[1].State.Mode);
    }

    [Fact]
    public void Deleting_imported_effect_stays_deleted_after_save_and_reopen()
    {
        var block = Load();
        var removed = block.Effects.Single(effect => effect.Offset == TimeSpan.FromSeconds(1));
        var changed = block.WithEffects(block.Effects.Where(effect => effect.Id != removed.Id).ToArray());
        var reopened = RoundTrip(changed);
        Assert.Equal(FlashMode.Solid, changed.GetStateAt(TimeSpan.FromSeconds(1.5)).Mode);
        Assert.Equal(FlashMode.Solid, reopened.GetStateAt(TimeSpan.FromSeconds(1.5)).Mode);
        Assert.Equal(changed.Effects, reopened.Effects);
        Assert.Equal(FlashMode.Blink4Hz, reopened.Frames[1].State.Mode);
    }

    [Fact]
    public void Moving_and_resizing_imported_effect_do_not_leave_blinking_at_old_positions()
    {
        var block = Load();
        var original = block.Effects.Single(effect => effect.Offset == TimeSpan.FromSeconds(1));
        var moved = original.Drag(TimeSpan.FromSeconds(-.5), block.Length, 0);
        var changed = block.WithEffects(block.Effects.Where(effect => effect.Id != original.Id).Append(moved).ToArray());
        Assert.Equal(FlashMode.Blink4Hz, changed.GetStateAt(TimeSpan.FromSeconds(.75)).Mode);
        Assert.Equal(FlashMode.Solid, RoundTrip(changed).GetStateAt(TimeSpan.FromSeconds(1.75)).Mode);
        var shortened = original.Drag(TimeSpan.FromSeconds(-.5), block.Length, 1);
        changed = block.WithEffects(block.Effects.Where(effect => effect.Id != original.Id).Append(shortened).ToArray());
        Assert.Equal(FlashMode.Solid, RoundTrip(changed).GetStateAt(TimeSpan.FromSeconds(1.75)).Mode);
        var parentMoved = changed.MovedTo(TimeSpan.FromSeconds(20), 3);
        Assert.Equal(shortened.Offset, parentMoved.Effects.Single(effect => effect.Id == shortened.Id).Offset);
    }

    [Fact]
    public void Missing_effect_list_infers_one_continuous_effect_across_color_changes()
    {
        var json = LegacyJson.Replace(
            "\"Effects\":[{\"Offset\":2000,\"Length\":1000,\"Mode\":1}]", "\"Effects\":null");
        var block = Assert.Single(TimelineJson.Read(json).Blocks);
        var effect = Assert.Single(block.Effects);
        Assert.Equal(TimeSpan.FromSeconds(1), effect.Offset);
        Assert.Equal(TimeSpan.FromSeconds(3), effect.Length);
        Assert.Equal(FlashMode.Blink4Hz, effect.Mode);
        Assert.Equal(block.Effects, RoundTrip(block).Effects);
    }

    private static Block Load() => Assert.Single(TimelineJson.Read(LegacyJson).Blocks);
    private static Block RoundTrip(Block block)
        => Assert.Single(TimelineJson.Read(TimelineJson.Write(new Timeline([block], []))).Blocks);
}
