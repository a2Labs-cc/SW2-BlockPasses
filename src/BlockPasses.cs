using System;
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

namespace BlockPasses;

[PluginMetadata(Id = "BlockPasses", Version = "2.0.0", Name = "BlockPasses", Author = "aga", Description = "Blocks selected map passages with solid props until player-count threshold is met. Includes in-game editor for easy block placement and management.")]
public partial class BlockPasses : BasePlugin
{
    private BlockPassesConfig _config = null!;
    private PrecachingService? _precachingService;
    private ConfigService? _configService;
    private BlockPassMapDataService? _mapDataService;
    private BlockPassEntityManager? _entityManager;
    private BlockPassRaycastService? _raycastService;
    private BlockPassGrabService? _grabService;
    private BlockPassEditorService? _editorService;

    private readonly List<Guid> _editorCommandGuids = new();
    private bool _editorCommandsRegistered;
    private Guid _roundStartHook = Guid.Empty;
    private Guid _roundEndHook = Guid.Empty;
    private Guid _warmupEndHook = Guid.Empty;

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
        _configService = new ConfigService(Core, Core.Logger);
        _config = _configService.LoadConfig();
        _precachingService = new PrecachingService(Core, _config);
        _mapDataService = new BlockPassMapDataService(Core, Core.Logger);
        _entityManager = new BlockPassEntityManager(Core, Core.Logger, _precachingService);
        _raycastService = new BlockPassRaycastService(Core);
        _grabService = new BlockPassGrabService(Core, _entityManager, _raycastService);
        _editorService = new BlockPassEditorService(Core, Core.Logger, _configService, _mapDataService, _entityManager, _raycastService, _grabService, () => _config, cfg => _config = cfg);

        // Hook precache event for proper manifest registration on map load
        Core.Event.OnPrecacheResource += OnPrecache;
        Core.Event.OnClientProcessUsercmds += OnClientProcessUsercmds;
        Core.Event.OnClientDisconnected += OnClientDisconnected;

        // Hook game events for round/warmup transitions
        _roundStartHook = Core.GameEvent.HookPost<EventRoundStart>(OnRoundStart);
        _roundEndHook = Core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
        _warmupEndHook = Core.GameEvent.HookPost<EventWarmupEnd>(OnWarmupEnd);

        UpdateEditorCommandRegistration();

        if (_config.Debug)
        {
            Core.Logger.LogInformation("BlockPasses: Loaded. Config has {Count} maps with {Total} entities",
                0,
                0);
        }
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
        UnregisterEditorCommands();
        Core.Event.OnPrecacheResource -= OnPrecache;
        Core.Event.OnClientProcessUsercmds -= OnClientProcessUsercmds;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;

        // Unhook game events
        if (_roundStartHook != Guid.Empty) Core.GameEvent.Unhook(_roundStartHook);
        if (_roundEndHook != Guid.Empty) Core.GameEvent.Unhook(_roundEndHook);
        if (_warmupEndHook != Guid.Empty) Core.GameEvent.Unhook(_warmupEndHook);

        if (_grabService is not null)
        {
            _grabService.StopAll();
        }
        if (_entityManager is not null)
        {
            _entityManager.RemoveAll(immediate: false);
        }
        if (_editorService is not null)
        {
            _editorService.Dispose();
        }
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player is null || !player.IsValid) return;
        _editorService?.OnClientDisconnected(player);
        UpdateEditorCommandRegistration();
    }

    private void OnClientProcessUsercmds(IOnClientProcessUsercmdsEvent @event)
    {
        if (_editorService is null) return;

        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player is null || !player.IsValid) return;

        _editorService.OnClientProcessUsercmds(player, @event.Usercmds);
    }

    private void UpdateEditorCommandRegistration()
    {
        var shouldRegister = _editorService is not null && _editorService.EditModePlayerCount > 0;
        if (shouldRegister)
        {
            RegisterEditorCommands();
        }
        else
        {
            UnregisterEditorCommands();
        }
    }

    private void RegisterEditorCommands()
    {
        if (_editorCommandsRegistered) return;

        _editorCommandGuids.Add(Core.Command.RegisterCommand("bp_menu", OnCmdMenu, registerRaw: true, permission: "blockpasses.admin"));
        _editorCommandGuids.Add(Core.Command.RegisterCommand("bp_add", OnCmdAdd, registerRaw: true, permission: "blockpasses.admin"));
        _editorCommandGuids.Add(Core.Command.RegisterCommand("bp_remove", OnCmdRemove, registerRaw: true, permission: "blockpasses.admin"));
        _editorCommandGuids.Add(Core.Command.RegisterCommand("bp_rot", OnCmdRotate, registerRaw: true, permission: "blockpasses.admin"));
        _editorCommandGuids.Add(Core.Command.RegisterCommand("bp_scale", OnCmdScale, registerRaw: true, permission: "blockpasses.admin"));
        _editorCommandGuids.Add(Core.Command.RegisterCommand("bp_up", OnCmdUp, registerRaw: true, permission: "blockpasses.admin"));
        _editorCommandGuids.Add(Core.Command.RegisterCommand("bp_down", OnCmdDown, registerRaw: true, permission: "blockpasses.admin"));
        _editorCommandGuids.Add(Core.Command.RegisterCommand("bp_save", OnCmdSave, registerRaw: true, permission: "blockpasses.admin"));

        _editorCommandsRegistered = true;
    }

    private void UnregisterEditorCommands()
    {
        if (!_editorCommandsRegistered) return;

        foreach (var id in _editorCommandGuids)
        {
            Core.Command.UnregisterCommand(id);
        }

        _editorCommandGuids.Clear();
        _editorCommandsRegistered = false;
    }

    private void ReloadAndApplyMapBlocks(string? mapName, bool respawn)
    {
        if (_mapDataService is null || _precachingService is null) return;

        mapName = (mapName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(mapName)) return;

        var path = _mapDataService.GetMapFilePathFor(mapName);
        var blocks = _mapDataService.Reload(mapName);
        _precachingService.UpdateMapBlocks(blocks);

        if (_config.Debug)
        {
            Core.Logger.LogInformation("BlockPasses: Loaded {Count} map blocks for {Map} from {Path}", blocks.Count, mapName, path);
        }

        if (!respawn || _entityManager is null) return;

        if (!_config.SpawnBlocksOnWarmup)
        {
            try
            {
                var rules = Core.EntitySystem.GetGameRules();
                if (rules is not null && rules.WarmupPeriod)
                {
                    if (_config.Debug)
                    {
                        Core.Logger.LogInformation("BlockPasses: Skipping block spawn because warmup is active and SpawnBlocksOnWarmup is disabled.");
                    }
                    return;
                }
            }
            catch
            {
            }
        }

        _entityManager.RemoveAll(immediate: true);
        Core.Scheduler.NextTick(() =>
        {
            foreach (var cfg in blocks)
            {
                if (cfg is null) continue;
                _entityManager.Spawn(cfg);
            }
        });
    }

    private void OnPrecache(IOnPrecacheResourceEvent @event)
    {
        // Ensure config/service exist even if precache fires before Load
        if (_configService == null)
        {
            _configService = new ConfigService(Core, Core.Logger);
        }
        _config ??= _configService.LoadConfig();
        _precachingService ??= new PrecachingService(Core, _config);
        _mapDataService ??= new BlockPassMapDataService(Core, Core.Logger);

        try
        {
            var mapName = Core.Engine.GlobalVars.MapName.Value;
            var path = _mapDataService.GetMapFilePathFor(mapName);
            var blocks = _mapDataService.Reload(mapName);
            _precachingService.UpdateMapBlocks(blocks);
            if (_config.Debug)
            {
                Core.Logger.LogInformation("BlockPasses: Loaded {Count} map blocks for {Map} from {Path}", blocks.Count, mapName, path);
            }
        }
        catch
        {
        }

        _precachingService?.AddModels(@event);
        if (_config.Debug)
        {
            Core.Logger.LogInformation("BlockPasses: Precache complete. Blocks will spawn based on warmup/round events.");
        }
    }

    private HookResult OnWarmupEnd(EventWarmupEnd @event)
    {
        if (_config.Debug)
        {
            Core.Logger.LogInformation("BlockPasses: Warmup ended, match starting. Force respawning blocks.");
        }

        try
        {
            var mapName = Core.Engine.GlobalVars.MapName.Value;
            Core.Scheduler.NextTick(() => SpawnBlocksForMap(mapName));
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning(ex, "BlockPasses: Failed to spawn blocks on warmup end");
        }

        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event)
    {
        try
        {
            _grabService?.StopAll();
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning(ex, "BlockPasses: Error in OnRoundEnd");
        }

        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart @event)
    {
        try
        {
            var rules = Core.EntitySystem.GetGameRules();
            var isInWarmup = rules is not null && rules.WarmupPeriod;

            if (isInWarmup)
            {
                if (_config.Debug)
                {
                    Core.Logger.LogInformation("BlockPasses: Warmup round start.");
                }
            }
            else
            {
                if (_config.Debug)
                {
                    Core.Logger.LogInformation("BlockPasses: Live round, ensuring blocks spawned.");
                }
            }

            var shouldSpawn = !isInWarmup || _config.SpawnBlocksOnWarmup;
            if (shouldSpawn)
            {
                var mapName = Core.Engine.GlobalVars.MapName.Value;
                Core.Scheduler.NextTick(() => SpawnBlocksForMap(mapName));

                var blocks = _mapDataService?.GetBlocks(mapName);
                if (blocks is not null && blocks.Count > 0)
                {
                    BroadcastChatLocalized("blockpasses.message", _config.Players);
                }
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning(ex, "BlockPasses: Error in OnRoundStart");
        }

        return HookResult.Continue;
    }

    private void SpawnBlocksForMap(string? mapName)
    {
        if (_mapDataService is null || _entityManager is null) return;

        mapName = (mapName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(mapName)) return;

        var blocks = _mapDataService.GetBlocks(mapName);
        if (blocks is null || blocks.Count == 0)
        {
            if (_config.Debug)
            {
                Core.Logger.LogInformation("BlockPasses: No blocks configured for map {Map}", mapName);
            }
            return;
        }

        _entityManager.RemoveAll(immediate: true);
        Core.Scheduler.NextTick(() =>
        {
            foreach (var cfg in blocks)
            {
                if (cfg is null) continue;
                _entityManager.Spawn(cfg);
            }
        });

        if (_config.Debug)
        {
            Core.Logger.LogInformation("BlockPasses: Spawned {Count} blocks for map {Map}", blocks.Count, mapName);
        }
    }

    private static string? NormalizeColorTag(string color)
    {
        var c = (color ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(c)) return null;

        if (c.StartsWith("{", StringComparison.Ordinal) && c.EndsWith("}", StringComparison.Ordinal) && c.Length > 2)
        {
            c = c[1..^1];
        }
        if (c.StartsWith("[", StringComparison.Ordinal) && c.EndsWith("]", StringComparison.Ordinal) && c.Length > 2)
        {
            c = c[1..^1];
        }

        c = c.Trim().ToLowerInvariant();
        if (c is "default" or "none") return null;
        return c;
    }

    private string FormatChat(string message)
    {
        var prefix = (_config?.ChatPrefix ?? string.Empty).Trim();
        var prefixColor = (_config?.ChatPrefixColor ?? string.Empty).Trim();

        var trimmed = (message ?? string.Empty).TrimStart();
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return trimmed.Colored();
        }

        var colorTag = NormalizeColorTag(prefixColor);
        var formattedPrefix = string.IsNullOrWhiteSpace(colorTag)
            ? prefix
            : $"[{colorTag}]{prefix}[white]";

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return formattedPrefix.Colored();
        }

        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith(formattedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Colored();
        }

        return $"{formattedPrefix} {trimmed}".Colored();
    }

    private void SendChat(IPlayer player, string message)
    {
        if (player is null || !player.IsValid) return;

        var text = message ?? string.Empty;
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                player.SendMessage(MessageType.Chat, " ");
                continue;
            }

            player.SendMessage(MessageType.Chat, FormatChat(line));
        }
    }

    private void SendChatLocalized(IPlayer player, string key, params object[] args)
    {
        if (player is null || !player.IsValid) return;

        var loc = Core.Translation.GetPlayerLocalizer(player);
        var msg = args.Length == 0 ? loc[key] : loc[key, args];
        SendChat(player, msg);
    }

    private void BroadcastChatLocalized(string key, params object[] args)
    {
        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (player is null || !player.IsValid) continue;
            SendChatLocalized(player, key, args);
        }
    }
}