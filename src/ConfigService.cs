using System.IO;
using System.Text.Json;
using BlockPasses;
using Microsoft.Extensions.Logging;

namespace BlockPasses.Configuration;

public class ConfigService
{
    private readonly ILogger _logger;
    private const string PluginFolderName = "BlockPasses";
    private const string ConfigFileName = "config.json";

    public ConfigService(ILogger logger)
    {
        _logger = logger;
    }

    public BlockPassesConfig LoadConfig()
    {
        var configPath = GetConfigPath();
        EnsureDirectory(configPath);

        if (!File.Exists(configPath))
        {
            var config = CreateDefaultConfig();
            Persist(configPath, config);
            return config;
        }

        return ReadConfig(configPath);
    }

    public BlockPassesConfig ReloadConfig()
    {
        var configPath = GetConfigPath();
        EnsureDirectory(configPath);

        if (!File.Exists(configPath))
        {
            var config = CreateDefaultConfig();
            Persist(configPath, config);
            return config;
        }

        return ReadConfig(configPath);
    }

    private static string GetConfigPath()
    {
        // AppContext.BaseDirectory points to ...\addons\swiftlys2\bin\managed\dotnet\
        // We want ...\addons\swiftlys2\configs\plugins\BlockPasses\config.json
        var addonRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        return Path.Combine(
            addonRoot,
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

    private void Persist(string path, BlockPassesConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        _logger.LogInformation("BlockPasses config written to {Path}", path);
    }

    private BlockPassesConfig ReadConfig(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<BlockPassesConfig>(text);
            return loaded ?? CreateDefaultConfig();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read BlockPasses config, using defaults.");
            return CreateDefaultConfig();
        }
    }

    private static BlockPassesConfig CreateDefaultConfig()
    {
        return new BlockPassesConfig
        {
            Players = 6,
            Message = "[{BLUE} BlockerPasses {DEFAULT}] Some passageways are blocked. Unblocking requires {RED}{MINPLAYERS}{DEFAULT} players",
            Maps = new Dictionary<string, List<BlockPassesEntity>>
            {
                {
                    "de_mirage", new List<BlockPassesEntity>
                    {
                        new()
                        {
                            ModelPath = "models/props/de_dust/hr_dust/dust_windows/dust_rollupdoor_96x128_surface_lod.vmdl",
                            Color = new[] { 30, 144, 255 },
                            Origin = "-1600.46 -741.124 -172.965",
                            Angles = "0 180 0",
                            Scale = 0.0f
                        },
                        new()
                        {
                            ModelPath = "models/props/de_mirage/small_door_b.vmdl",
                            Color = new[] { 255, 255, 255 },
                            Origin = "588.428 704.941 -136.517",
                            Angles = "0 270.256 0",
                            Scale = 0.0f
                        },
                        new()
                        {
                            ModelPath = "models/props/de_mirage/large_door_c.vmdl",
                            Color = new[] { 255, 255, 255 },
                            Origin = "-1007.87 -359.812 -323.64",
                            Angles = "0 270.106 0",
                            Scale = 0.0f
                        },
                        new()
                        {
                            ModelPath = "models/props/de_nuke/hr_nuke/chainlink_fence_001/chainlink_fence_001_256.vmdl",
                            Color = new[] { 255, 255, 255 },
                            Origin = "-961.146 -14.2419 -169.489",
                            Angles = "0 269.966 0",
                            Scale = 0.0f
                        },
                        new()
                        {
                            ModelPath = "models/props/de_nuke/hr_nuke/chainlink_fence_001/chainlink_fence_001_256.vmdl",
                            Color = new[] { 255, 255, 255 },
                            Origin = "-961.146 -14.2419 -43.0083",
                            Angles = "0 269.966 0",
                            Scale = 0.0f
                        }
                    }
                }
            }
        };
    }
}
