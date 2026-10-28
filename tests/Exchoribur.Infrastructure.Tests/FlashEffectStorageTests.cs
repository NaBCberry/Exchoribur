using Exchoribur.Core.Models;
using Exchoribur.Infrastructure.Storage;

namespace Exchoribur.Infrastructure.Tests;

public sealed class FlashEffectStorageTests
{
    [Fact]
    public void Json_and_csv_keep_effect_edges_modes_and_colors()
    {
        var effect = new FlashEffect(Guid.NewGuid(), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500), FlashMode.Blink4Hz);
        var block = new Block(Guid.NewGuid(), "块", 0, TimeSpan.Zero, TimeSpan.FromSeconds(1),
            [new BlockFrame(TimeSpan.Zero, new ChannelState(new LightColor(15, 0, 0), FlashMode.Solid))], effects: [effect]);
        var restored = TimelineJson.Read(TimelineJson.Write(new Timeline([block], [])));
        Assert.Equal(effect, Assert.Single(Assert.Single(restored.Blocks).Effects));
        var csv = TimelineCsvWriter.Write(restored);
        Assert.Contains("250,3,15,0,0", csv);
        Assert.Contains("750,0,15,0,0", csv);
        Assert.Empty(TimelineJson.Read(TimelineJson.Write(new Timeline([block.WithEffects([])], []))).Blocks[0].Effects);
    }

    [Fact]
    public void Older_json_without_effect_field_is_migrated()
    {
        var json = """
            {"Version":1,"Blocks":[{"Name":"旧块","Channel":0,"Start":0,"Length":1000,
            "Frames":[{"Offset":0,"Mode":2,"Red":15,"Green":0,"Blue":0}]}],"Markers":[]}
            """;
        Assert.Equal(FlashMode.Blink2Hz, Assert.Single(TimelineJson.Read(json).Blocks[0].Effects).Mode);
    }
}
