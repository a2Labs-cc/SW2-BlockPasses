using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlockPasses;

public class ModelPreset
{
    public string Name { get; set; } = string.Empty;
    public string ModelPath { get; set; } = string.Empty;
}

public sealed class ModelPresetJsonConverter : JsonConverter<ModelPreset>
{
    public override ModelPreset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected StartObject token, got {reader.TokenType}");
        }

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var name = root.TryGetProperty("Name", out var nameEl) ? (nameEl.GetString() ?? string.Empty) : string.Empty;
        var modelPath = root.TryGetProperty("ModelPath", out var pathEl) ? (pathEl.GetString() ?? string.Empty) : string.Empty;

        return new ModelPreset
        {
            Name = name,
            ModelPath = modelPath
        };
    }

    public override void Write(Utf8JsonWriter writer, ModelPreset value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Name", value.Name ?? string.Empty);
        writer.WriteString("ModelPath", value.ModelPath ?? string.Empty);
        writer.WriteEndObject();
    }
}

public class BlockPassesConfig
{
    public int Players { get; set; } = 6;

    public bool SpawnBlocksOnWarmup { get; set; } = false;

    public bool Debug { get; set; } = false;

    public string ChatPrefix { get; set; } = "BlockPasses |";

    public string ChatPrefixColor { get; set; } = "green";
    public List<ModelPreset> ModelPresets { get; set; } = new()
    {
        new() { Name = "Dust Rollupdoor", ModelPath = "models/props/de_dust/hr_dust/dust_windows/dust_rollupdoor_96x128_surface_lod.vmdl" },
        new() { Name = "Mirage Small Door", ModelPath = "models/props/de_mirage/small_door_b.vmdl" },
        new() { Name = "Mirage Large Door", ModelPath = "models/props/de_mirage/large_door_c.vmdl" },
        new() { Name = "Nuke Fence", ModelPath = "models/props/de_nuke/hr_nuke/chainlink_fence_001/chainlink_fence_001_256.vmdl" }
    };
}

public class BlockPassEntityConfig
{
    public string Id { get; set; } = string.Empty;
    public string ModelPath { get; set; } = string.Empty;
    public int[] Color { get; set; } = { 255, 255, 255 };
    public string Origin { get; set; } = string.Empty;
    public string Angles { get; set; } = string.Empty;
    public float? Scale { get; set; }
}
