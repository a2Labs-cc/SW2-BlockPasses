using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.EntitySystem;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;

using BlockPasses.Configuration;
using ChatColors = SwiftlyS2.Shared.Helper.ChatColors;

namespace BlockPasses;

[PluginMetadata(Id = "BlockPasses", Version = "1.0.0", Name = "BlockPasses", Author = "aga", Description = "No description.")]
public partial class BlockPasses : BasePlugin
{
    private BlockPassesConfig _config = null!;
    private PrecachingService? _precachingService;
    private ConfigService? _configService;

    public BlockPasses(ISwiftlyCore core) : base(core)
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void Load(bool hotReload)
    {
        _configService = new ConfigService(Core.Logger);
        _config = _configService.LoadConfig();
        _precachingService = new PrecachingService(Core, _config);
        
        // Hook precache event for proper manifest registration on map load
        Core.Event.OnPrecacheResource += OnPrecache;
        
        Core.Logger.LogInformation("BlockPasses: Loaded. Config has {Count} maps with {Total} entities",
            _config.Maps.Count,
            _config.Maps.Values.Sum(list => list.Count));
        
        if (hotReload)
        {
            try
            {
                var mapName = Core.Engine.GlobalVars.MapName.Value;
                if (!string.IsNullOrEmpty(mapName))
                {
                    Core.Logger.LogWarning("BlockPasses loaded via hot-reload. New models require a map change.");
                }
            }
            catch
            {
                // GlobalVars not available yet, ignore
            }
        }
    }

    public override void Unload()
    {
        Core.Event.OnPrecacheResource -= OnPrecache;
    }


    [GameEventHandler(HookMode.Pre)]
    public HookResult OnRoundStart(EventRoundStart @event)
    {
        var playersCount = Core.PlayerManager.PlayerCount;
        if (playersCount >= _config.Players)
        {
            return HookResult.Continue;
        }

        var mapName = Core.Engine.GlobalVars.MapName.Value;
        if (!_config.Maps.TryGetValue(mapName, out var entitiesMap))
        {
            return HookResult.Continue;
        }

        foreach (var entity in entitiesMap)
        {
            var origin = GetVectorFromString(entity.Origin);
            var angles = GetQAngleFromString(entity.Angles);

            SpawnProp(entity.ModelPath, entity.Color, origin, angles, entity.Scale);
        }

        var message = _config.Message.Replace("{MINPLAYERS}", _config.Players.ToString(CultureInfo.InvariantCulture));
        Core.PlayerManager.SendChat(ReplaceColorTags(message).Colored());

        return HookResult.Continue;
    }

    private void OnPrecache(IOnPrecacheResourceEvent @event)
    {
        // Ensure config/service exist even if precache fires before Load
        if (_configService == null)
        {
            _configService = new ConfigService(Core.Logger);
        }
        _config ??= _configService.LoadConfig();
        _precachingService ??= new PrecachingService(Core, _config);

        _precachingService?.AddModels(@event);
        Core.Logger.LogInformation("BlockPasses: Precache complete. {Count} maps with {Total} entities",
            _config?.Maps.Count ?? 0,
            _config?.Maps.Values.Sum(list => list.Count) ?? 0);
    }

    private static Vector GetVectorFromString(string vector) => GetFromString(vector, (x, y, z) => new Vector(x, y, z));

    private static QAngle GetQAngleFromString(string angles) => GetFromString(angles, (x, y, z) => new QAngle(x, y, z));

    private static T GetFromString<T>(string values, Func<float, float, float, T> createInstance)
    {
        var split = values.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (split.Length >= 3 &&
            float.TryParse(split[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            float.TryParse(split[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
            float.TryParse(split[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
        {
            return createInstance(x, y, z);
        }

        return default!;
    }

    private void SpawnProp(string modelPath, int[] color, Vector origin, QAngle angles, float? entityScale)
    {
        var prop = Core.EntitySystem.CreateEntityByDesignerName<CBaseModelEntity>("prop_dynamic_override");

        if (prop == null)
        {
            return;
        }

        modelPath = modelPath.TrimStart('/', '\\');
        
        // Enable solid collision so players can't pass through
        prop.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
        
        // Teleport first, then spawn, then set model on next tick (like CounterStrikeSharp)
        prop.Teleport(origin, angles, Vector.Zero);
        prop.DispatchSpawn();
        
        // Set model on next tick to allow entity to fully initialize
        Core.Scheduler.NextTick(() =>
        {
            if (prop.IsValid)
            {
                prop.SetModel(modelPath);
            }
        });

        if (entityScale.HasValue && Math.Abs(entityScale.Value) > float.Epsilon)
        {
            // Scale handling can be added here when appropriate schema fields are exposed/confirmed.
        }
    }

    private static string ReplaceColorTags(string input)
    {
        string[] colorPatterns =
        {
            "{DEFAULT}", "{WHITE}", "{DARKRED}", "{GREEN}", "{LIGHTYELLOW}", "{LIGHTBLUE}", "{OLIVE}", "{LIME}",
            "{RED}", "{LIGHTPURPLE}", "{PURPLE}", "{GREY}", "{YELLOW}", "{GOLD}", "{SILVER}", "{BLUE}", "{DARKBLUE}",
            "{BLUEGREY}", "{MAGENTA}", "{LIGHTRED}", "{ORANGE}"
        };

        string[] colorReplacements =
        {
            ChatColors.Default, ChatColors.White, ChatColors.DarkRed, ChatColors.Green,
            ChatColors.LightYellow, ChatColors.LightBlue, ChatColors.Olive, ChatColors.Lime,
            ChatColors.Red, ChatColors.LightPurple, ChatColors.Purple, ChatColors.Grey,
            ChatColors.Yellow, ChatColors.Gold, ChatColors.Silver, ChatColors.Blue,
            ChatColors.DarkBlue, ChatColors.BlueGrey, ChatColors.Magenta, ChatColors.LightRed,
            ChatColors.Orange
        };

        for (var i = 0; i < colorPatterns.Length; i++)
        {
            input = input.Replace(colorPatterns[i], colorReplacements[i], StringComparison.OrdinalIgnoreCase);
        }
        return input;
    }
}

public class BlockPassesConfig
{
    public int Players { get; init; }
    public string Message { get; init; } = string.Empty;
    public Dictionary<string, List<BlockPassesEntity>> Maps { get; init; } = new();
}

public class BlockPassesEntity
{
    public string ModelPath { get; init; } = string.Empty;
    public int[] Color { get; init; } = { 255, 255, 255 };
    public string Origin { get; init; } = string.Empty;
    public string Angles { get; init; } = string.Empty;
    public float? Scale { get; init; }
}