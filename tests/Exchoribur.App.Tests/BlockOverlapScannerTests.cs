using Exchoribur.App.TimelineUi;
using Exchoribur.Core.Models;

namespace Exchoribur.App.Tests;

public sealed class BlockOverlapScannerTests
{
    [Fact]
    public void Unsorted_nested_and_touching_blocks_produce_the_same_masks_as_all_pairs()
    {
        var random = new Random(992370);
        var blocks = Enumerable.Range(0, 120).Select(index => new Block(Guid.NewGuid(), "块", index % 10,
            TimeSpan.FromSeconds(random.Next(-20, 20)), TimeSpan.FromSeconds(random.Next(1, 30)),
            [new BlockFrame(TimeSpan.Zero, default)])).ToArray();
        var expected = new List<(int Channel, TimeSpan Start, TimeSpan End)>();
        for (var first = 0; first < blocks.Length; first++)
        {
            for (var second = first + 1; second < blocks.Length; second++)
            {
                if (blocks[first].Channel != blocks[second].Channel) continue;
                var start = blocks[first].Start > blocks[second].Start ? blocks[first].Start : blocks[second].Start;
                var end = blocks[first].End < blocks[second].End ? blocks[first].End : blocks[second].End;
                if (start < end) expected.Add((blocks[first].Channel, start, end));
            }
        }
        var actual = new List<(int Channel, TimeSpan Start, TimeSpan End)>();
        new BlockOverlapScanner(blocks).Visit((channel, start, end) => actual.Add((channel, start, end)));
        Assert.Equal(expected.Order(), actual.Order());
    }
}
