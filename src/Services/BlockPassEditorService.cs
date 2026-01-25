using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Microsoft.Extensions.Logging;
using BlockPasses.Configuration;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.SchemaDefinitions;

using CBaseModelEntity = SwiftlyS2.Shared.SchemaDefinitions.CBaseModelEntity;

namespace BlockPasses;

public sealed class BlockPassEditorService : IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;
    private readonly ConfigService _configService;
    private readonly BlockPassMapDataService _mapData;
    private readonly BlockPassEntityManager _entityManager;
    private readonly BlockPassRaycastService _raycast;
    private readonly BlockPassGrabService _grab;
    private readonly Func<BlockPassesConfig> _getConfig;
    private readonly Action<BlockPassesConfig> _setConfig;

    private readonly HashSet<ulong> _editModePlayers = new();
    private readonly Dictionary<ulong, bool> _lastUseButtonState = new();
    private bool _tickHandlerRegistered;

    public BlockPassEditorService(
        ISwiftlyCore core,
        ILogger logger,
        ConfigService configService,
        BlockPassMapDataService mapData,
        BlockPassEntityManager entityManager,
        BlockPassRaycastService raycast,
        BlockPassGrabService grab,
        Func<BlockPassesConfig> getConfig,
        Action<BlockPassesConfig> setConfig)
    {
        _core = core;
        _logger = logger;
        _configService = configService;
        _mapData = mapData;
        _entityManager = entityManager;
        _raycast = raycast;
        _grab = grab;
        _getConfig = getConfig;
        _setConfig = setConfig;

        _grab.OnGrabsChanged += UpdateTickHandler;
    }

    public void Dispose()
    {
        _grab.OnGrabsChanged -= UpdateTickHandler;
        if (_tickHandlerRegistered)
        {
            _core.Event.OnTick -= _grab.OnTick;
            _tickHandlerRegistered = false;
        }
    }

    public void EnableEditMode(IPlayer player)
    {
        _editModePlayers.Add(player.SteamID);
        player.SendChat("BlockPasses edit mode enabled. Press E to grab/drop blocks.");
    }

    public void DisableEditMode(IPlayer player)
    {
        _grab.StopGrab(player);
        _editModePlayers.Remove(player.SteamID);
        _lastUseButtonState.Remove(player.SteamID);
        player.SendChat("BlockPasses edit mode disabled.");
    }

    public bool IsInEditMode(IPlayer player) => _editModePlayers.Contains(player.SteamID);

    public int EditModePlayerCount => _editModePlayers.Count;

    public void OnClientDisconnected(IPlayer player)
    {
        DisableEditMode(player);
    }

    public void OnClientProcessUsercmds(IPlayer player, IEnumerable<CSGOUserCmdPB> usercmds)
    {
        if (!IsInEditMode(player))
        {
            _grab.StopGrab(player);
            _lastUseButtonState.Remove(player.SteamID);
            return;
        }

        var usePressed = IsUseButtonPressed(usercmds);
        var lastUsePressed = _lastUseButtonState.GetValueOrDefault(player.SteamID, false);
        _lastUseButtonState[player.SteamID] = usePressed;

        if (usePressed && !lastUsePressed)
        {
            _grab.ToggleGrab(player);
        }

        _grab.ProcessUserCmds(player, usercmds);
    }

    public void AddBlockAtAim(IPlayer player, string? modelPath = null)
    {
        if (!IsInEditMode(player)) { player.SendChat("You must be in edit mode to add blocks."); return; }

        if (!_raycast.TryGetTargetPosition(player, null, out var targetPos)) return;

        var mapName = _core.Engine.GlobalVars.MapName.Value;
        if (string.IsNullOrWhiteSpace(mapName)) return;

        var list = _mapData.GetBlocks(mapName);

        var nextId = 1;
        foreach (var cfg in list)
        {
            if (cfg is null) continue;
            var raw = (cfg.Id ?? string.Empty).Trim();
            if (int.TryParse(raw, out var n) && n >= nextId) nextId = n + 1;
        }

        var resolvedModel = (modelPath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(resolvedModel))
        {
            var cfg = _getConfig();
            resolvedModel = cfg.ModelPresets.Count > 0
                ? cfg.ModelPresets[0].ModelPath
                : "models/props/de_dust/hr_dust/dust_windows/dust_rollupdoor_96x128_surface_lod.vmdl";
        }

        var pawn = player.PlayerPawn;
        var yaw = pawn != null && pawn.IsValid ? (pawn.EyeAngles.Yaw + 180f) % 360f : 0f;
        if (yaw > 180f) yaw -= 360f;

        var entry = new BlockPassEntityConfig
        {
            Id = nextId.ToString(),
            ModelPath = resolvedModel.TrimStart('/', '\\'),
            Color = new[] { 255, 255, 255 },
            Origin = VectorToString(targetPos),
            Angles = QAngleToString(new QAngle(0, yaw, 0)),
            Scale = 1.0f
        };

        list.Add(entry);
        _entityManager.Spawn(entry);

        player.SendChat("Block added.");
    }

    public void RemoveBlockAtAim(IPlayer player)
    {
        if (!IsInEditMode(player)) { player.SendChat("You must be in edit mode to remove blocks."); return; }

        var entity = GetBlockAtAim(player);
        if (entity is null) { player.SendChat("No block found at aim."); return; }

        var cfgEntry = _entityManager.GetConfig(entity);
        if (cfgEntry is null) return;

        var mapName = _core.Engine.GlobalVars.MapName.Value;
        var list = _mapData.GetBlocks(mapName);
        list.Remove(cfgEntry);

        _entityManager.RemoveEntity(entity);

        player.SendChat("Block removed.");
    }

    public void RotateBlockAtAim(IPlayer player, float degrees)
    {
        if (!IsInEditMode(player)) { player.SendChat("You must be in edit mode to rotate blocks."); return; }

        var entity = GetBlockAtAim(player);
        if (entity is null) { player.SendChat("No block found at aim."); return; }

        var cfg = _entityManager.GetConfig(entity);
        if (cfg is null) return;

        var angles = ParseUtil.ParseQAngle(cfg.Angles);
        var newAngles = new QAngle(angles.Pitch, angles.Yaw + degrees, angles.Roll);
        cfg.Angles = QAngleToString(newAngles);
        entity.Teleport(entity.AbsOrigin, newAngles, Vector.Zero);
    }

    public void MoveBlockAtAim(IPlayer player, float zDelta)
    {
        if (!IsInEditMode(player)) { player.SendChat("You must be in edit mode to move blocks."); return; }

        var entity = GetBlockAtAim(player);
        if (entity is null) { player.SendChat("No block found at aim."); return; }

        var cfg = _entityManager.GetConfig(entity);
        if (cfg is null) return;

        var pos = entity.AbsOrigin ?? ParseUtil.ParseVector(cfg.Origin);
        var newPos = new Vector(pos.X, pos.Y, pos.Z + zDelta);
        cfg.Origin = VectorToString(newPos);

        var angles = ParseUtil.ParseQAngle(cfg.Angles);
        entity.Teleport(newPos, angles, Vector.Zero);
    }

    public void ScaleBlockAtAim(IPlayer player, float delta)
    {
        if (!IsInEditMode(player)) { player.SendChat("You must be in edit mode to scale blocks."); return; }

        var entity = GetBlockAtAim(player);
        if (entity is null) { player.SendChat("No block found at aim."); return; }

        var cfg = _entityManager.GetConfig(entity);
        if (cfg is null) return;

        var current = cfg.Scale ?? 1.0f;
        var next = Math.Max(0.1f, current + delta);
        cfg.Scale = next;
        entity.SetScale(next);
    }

    public void Save(IPlayer? player)
    {
        var mapName = _core.Engine.GlobalVars.MapName.Value;
        if (string.IsNullOrWhiteSpace(mapName)) return;

        var list = _mapData.GetBlocks(mapName);
        _mapData.Save(mapName, list);
        player?.SendChat("BlockPasses blocks saved.");
    }

    private static bool IsUseButtonPressed(IEnumerable<CSGOUserCmdPB> usercmds)
    {
        const ulong useMask = (ulong)InputBitMask_t.IN_USE;
        foreach (var cmd in usercmds)
        {
            if (cmd?.Base?.ButtonsPb == null) continue;
            var buttons = cmd.Base.ButtonsPb;
            if ((buttons.Buttonstate1 & useMask) != 0 || (buttons.Buttonstate2 & useMask) != 0 || (buttons.Buttonstate3 & useMask) != 0)
                return true;
        }

        return false;
    }

    private void UpdateTickHandler()
    {
        if (_grab.GrabbedCount > 0 && !_tickHandlerRegistered)
        {
            _core.Event.OnTick += _grab.OnTick;
            _tickHandlerRegistered = true;
        }
        else if (_grab.GrabbedCount == 0 && _tickHandlerRegistered)
        {
            _core.Event.OnTick -= _grab.OnTick;
            _tickHandlerRegistered = false;
        }
    }

    private CBaseModelEntity? GetBlockAtAim(IPlayer player)
    {
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return null;

        var eyePosition = pawn.AbsOrigin ?? Vector.Zero;
        if (pawn.CameraServices?.OldPlayerViewOffsetZ != null) eyePosition = new Vector(eyePosition.X, eyePosition.Y, eyePosition.Z + 64f);

        var forward = BlockPassRaycastService.GetForwardVector(pawn.EyeAngles);

        CBaseModelEntity? best = null;
        float bestPerp = float.MaxValue;
        float bestForward = float.MaxValue;

        foreach (var ent in _entityManager.ActiveEntities.ToList())
        {
            if (!ent.IsValid) continue;
            var entPos = ent.AbsOrigin ?? Vector.Zero;
            var toEnt = entPos - eyePosition;
            var fDist = toEnt.Dot(forward);
            if (fDist < 0.0f || fDist > 512.0f) continue;

            var closestPoint = eyePosition + (forward * fDist);
            var perpDist = (entPos - closestPoint).Length();
            if (perpDist > 72.0f) continue;

            if (perpDist < bestPerp || (Math.Abs(perpDist - bestPerp) < 0.001f && fDist < bestForward))
            {
                best = ent;
                bestPerp = perpDist;
                bestForward = fDist;
            }
        }

        return best;
    }

    private static string VectorToString(Vector v) => $"{v.X.ToString(CultureInfo.InvariantCulture)} {v.Y.ToString(CultureInfo.InvariantCulture)} {v.Z.ToString(CultureInfo.InvariantCulture)}";
    private static string QAngleToString(QAngle a) => $"{a.Pitch.ToString(CultureInfo.InvariantCulture)} {a.Yaw.ToString(CultureInfo.InvariantCulture)} {a.Roll.ToString(CultureInfo.InvariantCulture)}";
}
