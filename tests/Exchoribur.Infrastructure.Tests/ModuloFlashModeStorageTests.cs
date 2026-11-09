using System.Text.Json.Nodes;
using Exchoribur.Core.Models;
using Exchoribur.Infrastructure.Storage;

namespace Exchoribur.Infrastructure.Tests;

public sealed class ModuloFlashModeStorageTests
{
    [Fact]
    public void Json_import_normalizes_raw_frame_and_effect_codes_and_saves_only_standard_values()
    {
        for (byte raw = 0; raw < 16; raw++)
        {
            var mode = (FlashMode)(raw % 4);
            var block = new Block(Guid.NewGuid(), "raw", 0, TimeSpan.Zero, TimeSpan.FromSeconds(3),
                [new BlockFrame(TimeSpan.Zero, new ChannelState(new LightColor(1, 2, 3), mode))],
                effects: raw % 4 == 0 ? [] : [new FlashEffect(Guid.NewGuid(), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), mode)]);
            var input = JsonNode.Parse(TimelineJson.Write(new Timeline([block], [])))!;
            var data = input["Blocks"]![0]!;
            data["Frames"]![0]!["Mode"] = raw;
            if (raw % 4 != 0) data["Effects"]![0]!["Mode"] = raw;
            var restored = Assert.Single(TimelineJson.Read(input.ToJsonString()).Blocks);
            Assert.Equal(mode, restored.Frames[0].State.Mode);
            Assert.All(restored.Effects, effect => Assert.Equal(mode, effect.Mode));
            var saved = TimelineJson.Write(new Timeline([restored], []));
            var reopened = Assert.Single(TimelineJson.Read(saved).Blocks);
            Assert.Equal(restored.Frames, reopened.Frames);
            Assert.Equal(restored.Effects, reopened.Effects);
        }
    }

    [Fact]
    public void Csv_import_uses_the_same_four_standard_modes()
    {
        for (byte raw = 0; raw < 16; raw++)
        {
            var csv = $"frame_time_ms,ch0_function,ch0_red,ch0_green,ch0_blue\n0,{raw},15,0,0\n500,{raw},15,0,0\n";
            var block = Assert.Single(TimelineCsvReader.Read(CsvTable.Parse(csv)).Blocks);
            Assert.Equal((FlashMode)(raw % 4), block.Frames[0].State.Mode);
            Assert.Equal((FlashMode)(raw % 4), block.GetStateAt(TimeSpan.Zero).Mode);
        }
    }
}
