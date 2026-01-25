using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace BlockPasses;

public sealed class BlockPassMapDataService
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;

    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private string? _loadedMapName;
    private List<BlockPassEntityConfig> _blocks = new();

    public BlockPassMapDataService(ISwiftlyCore core, ILogger logger)
    {
        _core = core;
        _logger = logger;
    }

    private List<BlockPassEntityConfig> ReadBlocksFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<BlockPassEntityConfig>();

        var trimmed = json.TrimStart();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            _logger.LogInformation("BlockPasses: Loading legacy array format, will save as object format");
            return JsonSerializer.Deserialize<List<BlockPassEntityConfig>>(json, _jsonOptions) ?? new List<BlockPassEntityConfig>();
        }

        var data = JsonSerializer.Deserialize<MapBlocksFile>(json, _jsonOptions);
        return data?.Blocks ?? new List<BlockPassEntityConfig>();
    }

    public string? LoadedMapName => _loadedMapName;

    public string GetMapFilePathFor(string mapName)
    {
        return GetMapFilePath(mapName);
    }

    public IReadOnlyList<BlockPassEntityConfig> Blocks => _blocks;

    public List<BlockPassEntityConfig> GetBlocks(string mapName)
    {
        if (!string.Equals(_loadedMapName, mapName, StringComparison.OrdinalIgnoreCase))
        {
            return Load(mapName);
        }

        return _blocks;
    }

    public List<BlockPassEntityConfig> Reload(string mapName)
    {
        _loadedMapName = null;
        _blocks = new List<BlockPassEntityConfig>();
        return Load(mapName);
    }

    public List<BlockPassEntityConfig> Load(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return new List<BlockPassEntityConfig>();
        }

        if (string.Equals(_loadedMapName, mapName, StringComparison.OrdinalIgnoreCase))
        {
            return _blocks;
        }

        _blocks = new List<BlockPassEntityConfig>();

        var path = GetMapFilePath(mapName);
        EnsureDirectory(path);

        try
        {
            if (!File.Exists(path))
            {
                _loadedMapName = mapName;
                return _blocks;
            }

            var json = File.ReadAllText(path);
            _blocks = ReadBlocksFromJson(json);
            EnsureIds(_blocks);

            _loadedMapName = mapName;
            _logger.LogInformation("BlockPasses: Loaded {Count} blocks for {Map}", _blocks.Count, mapName);
            return _blocks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BlockPasses: Failed to load map blocks for {Map} from {Path}. Starting with empty block list.", mapName, path);
            _blocks = new List<BlockPassEntityConfig>();
            _loadedMapName = mapName;
            return _blocks;
        }
    }

    public bool Save()
    {
        if (string.IsNullOrWhiteSpace(_loadedMapName)) return false;
        return Save(_loadedMapName, _blocks);
    }

    public bool Save(string mapName, List<BlockPassEntityConfig> blocks)
    {
        if (string.IsNullOrWhiteSpace(mapName)) return false;

        EnsureIds(blocks);

        var path = GetMapFilePath(mapName);
        EnsureDirectory(path);

        var backupPath = path + ".backup";
        var tempPath = path + ".tmp";

        try
        {
            if (File.Exists(path))
            {
                try
                {
                    File.Copy(path, backupPath, overwrite: true);
                    _logger.LogDebug("BlockPasses: Created backup at {BackupPath}", backupPath);
                }
                catch (Exception backupEx)
                {
                    _logger.LogWarning(backupEx, "BlockPasses: Failed to create backup, continuing with save");
                }
            }

            var payload = new MapBlocksFile { Blocks = blocks };
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            
            File.WriteAllText(tempPath, json);
            
            File.Move(tempPath, path, overwrite: true);

            _loadedMapName = mapName;
            _blocks = blocks;
            
            _logger.LogInformation("BlockPasses: Saved {Count} blocks for {Map}", blocks.Count, mapName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BlockPasses: Failed to save map blocks for {Map} to {Path}", mapName, path);
            
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
            
            return false;
        }
    }

    private static void EnsureIds(List<BlockPassEntityConfig>? blocks)
    {
        if (blocks == null || blocks.Count == 0) return;

        var assigned = new HashSet<int>();
        var next = 1;
        foreach (var cfg in blocks)
        {
            if (cfg is null) continue;

            var raw = (cfg.Id ?? string.Empty).Trim();
            if (int.TryParse(raw, out var n) && n > 0 && !assigned.Contains(n))
            {
                assigned.Add(n);
                cfg.Id = n.ToString();
                continue;
            }

            while (assigned.Contains(next)) next++;
            cfg.Id = next.ToString();
            assigned.Add(next);
            next++;
        }
    }

    private string GetMapFilePath(string mapName)
    {
        return Path.Combine(
            _core.CSGODirectory,
            "addons",
            "swiftlys2",
            "data",
            "BlockPasses",
            $"{mapName}.json");
    }

    private static void EnsureDirectory(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private sealed class MapBlocksFile
    {
        public List<BlockPassEntityConfig> Blocks { get; set; } = new();
    }
}
