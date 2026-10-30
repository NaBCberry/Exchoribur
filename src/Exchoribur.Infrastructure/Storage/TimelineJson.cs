using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Exchoribur.Application.Projects;
using Exchoribur.Core.Models;

namespace Exchoribur.Infrastructure.Storage;

/// <summary>
/// 时间轴的 JSON 表示:块 + 标记。块是编排的唯一存储形态,这里不再有扁平帧数组。
/// 块里的每一帧写一行,文件打开能直接看。
/// </summary>
public static class TimelineJson
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>写字符串用:中文保持原样,不转成 \uXXXX。</summary>
    private static readonly JsonSerializerOptions TextOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Write(Timeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        var builder = new StringBuilder();
        builder.Append("{\n");
        builder.Append("  \"Version\": ").Append(ProjectFileFormat.FormatVersion).Append(",\n");

        AppendBlocks(builder, timeline.Blocks);
        AppendMarkers(builder, timeline.Markers);

        builder.Append("}\n");
        return builder.ToString();
    }

    public static Timeline Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        TimelineDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<TimelineDto>(json, ReadOptions);
        }
        catch (JsonException exception)
        {
            throw new FormatException($"时间轴 JSON 解析失败:{exception.Message}", exception);
        }

        if (dto is null)
        {
            throw new FormatException("时间轴 JSON 是空的。");
        }

        if (dto.Version != ProjectFileFormat.FormatVersion)
        {
            throw new FormatException(
                $"时间轴版本是 {dto.Version},这个版本的程序只认 {ProjectFileFormat.FormatVersion}。");
        }

        var blocks = new List<Block>(dto.Blocks.Count);

        foreach (var block in dto.Blocks)
        {
            blocks.Add(ReadBlock(block));
        }

        var markers = dto.Markers
            .Select(marker => new TimelineMarker(
                TimeSpan.FromMilliseconds(marker.Time),
                marker.Name ?? string.Empty))
            .ToList();

        return new Timeline(blocks, markers);
    }

    private static void AppendBlocks(StringBuilder builder, IReadOnlyList<Block> blocks)
    {
        builder.Append("  \"Blocks\": ");

        if (blocks.Count == 0)
        {
            builder.Append("[],\n");
            return;
        }

        builder.Append("[\n");

        for (var index = 0; index < blocks.Count; index++)
        {
            AppendBlock(builder, blocks[index]);
            builder.Append(index < blocks.Count - 1 ? "," : string.Empty).Append('\n');
        }

        builder.Append("  ],\n");
    }

    private static void AppendBlock(StringBuilder builder, Block block)
    {
        builder.Append("    {\n");
        builder.Append("      \"Id\": ").Append(Quote(block.Id.ToString())).Append(",\n");
        builder.Append("      \"Name\": ").Append(Quote(block.Name)).Append(",\n");
        builder.Append("      \"Channel\": ").Append(block.Channel.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        builder.Append("      \"Start\": ").Append(FormatNumber(block.Start.TotalMilliseconds)).Append(",\n");
        builder.Append("      \"Length\": ").Append(FormatNumber(block.Length.TotalMilliseconds)).Append(",\n");
        builder.Append("      \"LinkGroup\": ")
            .Append(block.LinkGroupId is { } group ? Quote(group.ToString()) : "null")
            .Append(",\n");


        builder.Append("      \"Effects\": ").Append(JsonSerializer.Serialize(block.Effects.Select(effect => new
        {
            Id = effect.Id.ToString(), Offset = effect.Offset.TotalMilliseconds,
            Length = effect.Length.TotalMilliseconds, Mode = (byte)effect.Mode
        }))).Append(",\n");
        builder.Append("      \"Frames\": ");

        if (block.Frames.Count == 0)
        {
            builder.Append("[]\n");
        }
        else
        {
            builder.Append("[\n");

            for (var index = 0; index < block.Frames.Count; index++)
            {
                var frame = block.Frames[index];

                builder.Append("        { \"Offset\": ").Append(FormatNumber(frame.Offset.TotalMilliseconds))
                    .Append(", \"Mode\": ").Append(((byte)frame.State.Mode).ToString(CultureInfo.InvariantCulture))
                    .Append(", \"Red\": ").Append(frame.State.Color.Red.ToString(CultureInfo.InvariantCulture))
                    .Append(", \"Green\": ").Append(frame.State.Color.Green.ToString(CultureInfo.InvariantCulture))
                    .Append(", \"Blue\": ").Append(frame.State.Color.Blue.ToString(CultureInfo.InvariantCulture))
                    .Append(" }");

                builder.Append(index < block.Frames.Count - 1 ? "," : string.Empty).Append('\n');
            }

            builder.Append("      ]\n");
        }

        builder.Append("    }");
    }

    private static void AppendMarkers(StringBuilder builder, IReadOnlyList<TimelineMarker> markers)
    {
        builder.Append("  \"Markers\": ");

        if (markers.Count == 0)
        {
            builder.Append("[]\n");
            return;
        }

        builder.Append("[\n");

        for (var index = 0; index < markers.Count; index++)
        {
            var marker = markers[index];

            builder.Append("    { \"Time\": ").Append(FormatNumber(marker.Time.TotalMilliseconds))
                .Append(", \"Name\": ").Append(Quote(marker.Name))
                .Append('}');

            builder.Append(index < markers.Count - 1 ? "," : string.Empty).Append('\n');
        }

        builder.Append("  ]\n");
    }

    private static Block ReadBlock(BlockDto dto)
    {
        if (dto.Channel < 0 || dto.Channel >= Frame.ChannelCount)
        {
            throw new FormatException($"块「{dto.Name}」的通道号 {dto.Channel} 超出范围。");
        }

        var frames = new List<BlockFrame>(dto.Frames.Count);

        foreach (var frame in dto.Frames)
        {
            if (!FlashModes.TryFromRawValue(frame.Mode, out var mode))
            {
                throw new FormatException($"块「{dto.Name}」里有不认识的闪烁模式 {frame.Mode}。");
            }

            if (frame.Red > LightColor.MaxComponentValue
                || frame.Green > LightColor.MaxComponentValue
                || frame.Blue > LightColor.MaxComponentValue)
            {
                throw new FormatException($"块「{dto.Name}」里有超出 0-15 的颜色分量。");
            }

            frames.Add(new BlockFrame(
                TimeSpan.FromMilliseconds(frame.Offset),
                new ChannelState(new LightColor(frame.Red, frame.Green, frame.Blue), mode)));
        }

        var id = Guid.TryParse(dto.Id, out var parsed) ? parsed : Block.NewId();
        var linkGroup = Guid.TryParse(dto.LinkGroup, out var parsedGroup) ? parsedGroup : (Guid?)null;

        try
        {
            var block = new Block(
                id,
                dto.Name ?? Block.DefaultName,
                dto.Channel,
                TimeSpan.FromMilliseconds(dto.Start),
                TimeSpan.FromMilliseconds(dto.Length),
                frames,
                linkGroup,
                dto.Effects?.Select(ReadEffect).ToArray());
            return dto.Effects is not null && dto.Frames.Any(frame => frame.Mode >= 4)
                ? CompleteImportedEffects(dto, block) : block;
        }
        catch (ArgumentException exception)
        {
            throw new FormatException($"块「{dto.Name}」读不出来:{exception.Message}", exception);
        }
    }

    private static FlashEffect ReadEffect(EffectDto dto)
    {
        if (!FlashModes.TryFromRawValue(dto.Mode, out var mode))
            throw new FormatException($"不认识的闪烁效果模式 {dto.Mode}。");
        return new FlashEffect(Guid.TryParse(dto.Id, out var id) ? id : Guid.NewGuid(),
            TimeSpan.FromMilliseconds(dto.Offset), TimeSpan.FromMilliseconds(dto.Length), mode);
    }

    // 只在导入扩展帧码时补齐旧效果列表。保存后的标准帧码不会再次触发补齐。
    private static Block CompleteImportedEffects(BlockDto dto, Block block)
    {
        var legacy = new Block(block.Id, block.Name, block.Channel, block.Start, block.Length,
            dto.Frames.Select(frame => new BlockFrame(TimeSpan.FromMilliseconds(frame.Offset),
                new ChannelState(default, frame.Mode >= 4 ? (FlashMode)(frame.Mode % 4) : FlashMode.Solid))).ToArray());
        var added = new List<FlashEffect>();
        var covered = block.Effects.OrderBy(effect => effect.Offset).ToArray();
        foreach (var effect in legacy.Effects)
        {
            var cursor = effect.Offset;
            foreach (var existing in covered)
            {
                if (existing.End <= cursor) continue;
                if (existing.Offset >= effect.End) break;
                if (existing.Offset > cursor)
                    added.Add(new FlashEffect(Guid.NewGuid(), cursor, existing.Offset - cursor, effect.Mode));
                if (existing.End > cursor) cursor = existing.End;
                if (cursor >= effect.End) break;
            }
            if (cursor < effect.End)
                added.Add(new FlashEffect(Guid.NewGuid(), cursor, effect.End - cursor, effect.Mode));
        }
        return added.Count == 0 ? block : block.WithEffects(added.Concat(block.Effects).ToArray());
    }

    private static string Quote(string text) => JsonSerializer.Serialize(text, TextOptions);

    private static string FormatNumber(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new InvalidOperationException($"数值 {value} 不是有限数,写不进 JSON。");
        }

        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private sealed class TimelineDto
    {
        public int Version { get; set; }
        public List<BlockDto> Blocks { get; set; } = [];
        public List<MarkerDto> Markers { get; set; } = [];
    }

    private sealed class BlockDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int Channel { get; set; }
        public double Start { get; set; }
        public double Length { get; set; }
        public string? LinkGroup { get; set; }
        public List<FrameDto> Frames { get; set; } = [];
        public List<EffectDto>? Effects { get; set; }
    }

    private sealed class EffectDto
    {
        public string? Id { get; set; }
        public double Offset { get; set; }
        public double Length { get; set; }
        public byte Mode { get; set; }
    }

    private sealed class FrameDto
    {
        public double Offset { get; set; }
        public byte Mode { get; set; }
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
    }

    private sealed class MarkerDto
    {
        public double Time { get; set; }
        public string? Name { get; set; }
    }
}
