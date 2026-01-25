using System.IO;
using System.Text.Json;
using BlockPasses;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace BlockPasses.Configuration;

public class ConfigService
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private const string PluginFolderName = "BlockPasses";
    private const string ConfigFileName = "config.json";

    public ConfigService(ISwiftlyCore core, ILogger logger)
    {
        _core = core;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new ModelPresetJsonConverter() }
        };
    }

    public BlockPassesConfig LoadConfig()
    {
        var configPath = GetConfigPath();
        EnsureDirectory(configPath);

        if (!File.Exists(configPath))
        {
            var config = new BlockPassesConfig();
            SaveConfig(config);
            return config;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<BlockPassesConfig>(json, _jsonOptions);
            return config ?? new BlockPassesConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BlockPasses: Failed to load config from {Path}, using defaults", configPath);
            return new BlockPassesConfig();
        }
    }

    public void SaveConfig(BlockPassesConfig config)
    {
        var configPath = GetConfigPath();
        EnsureDirectory(configPath);

        try
        {
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(configPath, json);
            _logger.LogInformation("BlockPasses: Config saved to {Path}", configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BlockPasses: Failed to save config to {Path}", configPath);
        }
    }

    private string GetConfigPath()
    {
        return Path.Combine(
            _core.CSGODirectory,
            "addons",
            "swiftlys2",
            "configs",
            "plugins",
            PluginFolderName,
            ConfigFileName);
    }

    private static void EnsureDirectory(string configPath)
    {
        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

}
