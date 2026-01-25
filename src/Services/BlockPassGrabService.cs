using System;
using System.Collections.Generic;
using System.Linq;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;
using InputBitMask_t = SwiftlyS2.Shared.SchemaDefinitions.InputBitMask_t;
using SolidType_t = SwiftlyS2.Shared.SchemaDefinitions.SolidType_t;
using SwiftlyS2.Shared.SchemaDefinitions;

using CBaseModelEntity = SwiftlyS2.Shared.SchemaDefinitions.CBaseModelEntity;

namespace BlockPasses;

public sealed class BlockPassGrabService
{
    private sealed class GrabState
    {
        public required CHandle<CBaseModelEntity> Entity;
        public required BlockPassEntityConfig Config;
        public required SolidType_t OriginalSolidType;
        public float HoldDistance;
        public float PendingYawDelta;
    }

    private readonly ISwiftlyCore _core;
    private readonly BlockPassEntityManager _entityManager;
    private readonly BlockPassRaycastService _raycast;

    private readonly Dictionary<ulong, GrabState> _grabbedBySteamId = new();
    private readonly Dictionary<ulong, bool> _lastReloadDownBySteamId = new();
    private readonly Dictionary<ulong, bool> _lastInspectDownBySteamId = new();
    private readonly Dictionary<ulong, int> _reloadHoldTicksBySteamId = new();
    private readonly Dictionary<ulong, int> _reloadRepeatCountdownBySteamId = new();
    private readonly Dictionary<ulong, int> _inspectHoldTicksBySteamId = new();
    private readonly Dictionary<ulong, int> _inspectRepeatCountdownBySteamId = new();

    public event Action? OnGrabsChanged;

    public BlockPassGrabService(ISwiftlyCore core, BlockPassEntityManager entityManager, BlockPassRaycastService raycast)
    {
        _core = core;
        _entityManager = entityManager;
        _raycast = raycast;
    }

    public int GrabbedCount => _grabbedBySteamId.Count;

    public void StopAll()
    {
        foreach (var steamId in _grabbedBySteamId.Keys.ToList())
        {
            StopGrab(steamId);
        }
    }

    public void StopGrab(IPlayer player)
    {
        StopGrab(player.SteamID);
    }

    public void StopGrab(ulong steamId)
    {
        if (!_grabbedBySteamId.TryGetValue(steamId, out var state)) return;

        _grabbedBySteamId.Remove(steamId);
        _lastReloadDownBySteamId.Remove(steamId);
        _lastInspectDownBySteamId.Remove(steamId);
        _reloadHoldTicksBySteamId.Remove(steamId);
        _reloadRepeatCountdownBySteamId.Remove(steamId);
        _inspectHoldTicksBySteamId.Remove(steamId);
        _inspectRepeatCountdownBySteamId.Remove(steamId);

        var ent = ResolveHandle(state.Entity);
        if (ent != null)
            ent.Collision.SolidType = state.OriginalSolidType;

        OnGrabsChanged?.Invoke();
    }

    public bool IsGrabbing(ulong steamId) => _grabbedBySteamId.ContainsKey(steamId);

    public void ToggleGrab(IPlayer player)
    {
        if (IsGrabbing(player.SteamID))
        {
            StopGrab(player);
            return;
        }

        var entity = GetBlockAtAim(player);
        if (entity is null || !entity.IsValid) return;

        var cfg = _entityManager.GetConfig(entity);
        if (cfg is null) return;

        var originalSolid = entity.Collision.SolidType;
        entity.Collision.SolidType = SolidType_t.SOLID_NONE;

        var holdDistance = 200.0f;
        var pawn = player.PlayerPawn;
        if (pawn != null && pawn.IsValid)
        {
            var eyePos = GetEyePosition(pawn);
            var entPos = entity.AbsOrigin ?? Vector.Zero;
            holdDistance = (entPos - eyePos).Length();
            holdDistance = Math.Clamp(holdDistance, 32.0f, 512.0f);
        }

        _grabbedBySteamId[player.SteamID] = new GrabState
        {
            Entity = _core.EntitySystem.GetRefEHandle(entity),
            Config = cfg,
            OriginalSolidType = originalSolid,
            HoldDistance = holdDistance,
            PendingYawDelta = 0f
        };

        OnGrabsChanged?.Invoke();
    }

    public void ProcessUserCmds(IPlayer player, IEnumerable<CSGOUserCmdPB> usercmds)
    {
        if (!IsGrabbing(player.SteamID)) return;

        HandleRotateInput(player, usercmds);
        ClearButtonMask(usercmds, (ulong)InputBitMask_t.IN_USE);
    }

    public void OnTick()
    {
        foreach (var (steamId, state) in _grabbedBySteamId.ToList())
        {
            var player = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.SteamID == steamId);

            var ent = ResolveHandle(state.Entity);
            if (player is null || !player.IsValid || ent == null)
            {
                StopGrab(steamId);
                continue;
            }

            if (!_raycast.TryGetTargetPosition(player, state.HoldDistance, out var targetPosition)) continue;

            var angles = ent.AbsRotation ?? ParseUtil.ParseQAngle(state.Config.Angles);
            if (Math.Abs(state.PendingYawDelta) > 0.001f)
            {
                angles = new QAngle(angles.Pitch, NormalizeYaw(angles.Yaw + state.PendingYawDelta), angles.Roll);
                state.Config.Angles = QAngleToString(angles);
                state.PendingYawDelta = 0f;
            }

            state.Config.Origin = VectorToString(targetPosition);
            ent.Teleport(targetPosition, angles, Vector.Zero);
        }
    }

    private static CBaseModelEntity? ResolveHandle(CHandle<CBaseModelEntity> handle)
    {
        if (!handle.IsValid) return null;

        try
        {
            var ent = handle.Value;
            if (ent == null || !ent.IsValid) return null;
            return ent;
        }
        catch
        {
            return null;
        }
    }

    private void HandleRotateInput(IPlayer player, IEnumerable<CSGOUserCmdPB> usercmds)
    {
        var steamId = player.SteamID;

        var reloadDown = IsButtonDown(usercmds, (ulong)InputBitMask_t.IN_RELOAD);
        var inspectDown = IsButtonDown(usercmds, (ulong)InputBitMask_t.IN_LOOK_AT_WEAPON);

        var lastReloadDown = _lastReloadDownBySteamId.GetValueOrDefault(steamId);
        var lastInspectDown = _lastInspectDownBySteamId.GetValueOrDefault(steamId);

        _lastReloadDownBySteamId[steamId] = reloadDown;
        _lastInspectDownBySteamId[steamId] = inspectDown;

        if (!_grabbedBySteamId.TryGetValue(steamId, out var state)) return;

        const float step = 1.0f;
        var delta = 0f;

        delta += GetAcceleratedRotateDelta(
            steamId,
            reloadDown,
            lastReloadDown,
            _reloadHoldTicksBySteamId,
            _reloadRepeatCountdownBySteamId,
            +1);

        delta += GetAcceleratedRotateDelta(
            steamId,
            inspectDown,
            lastInspectDown,
            _inspectHoldTicksBySteamId,
            _inspectRepeatCountdownBySteamId,
            -1);

        if (Math.Abs(delta) > 0.001f)
        {
            state.PendingYawDelta += delta * step;
        }

        ClearButtonMask(usercmds, (ulong)InputBitMask_t.IN_RELOAD | (ulong)InputBitMask_t.IN_LOOK_AT_WEAPON);
    }

    private static float GetAcceleratedRotateDelta(
        ulong steamId,
        bool down,
        bool lastDown,
        Dictionary<ulong, int> holdTicks,
        Dictionary<ulong, int> repeatCountdown,
        int direction)
    {
        if (!down)
        {
            holdTicks.Remove(steamId);
            repeatCountdown.Remove(steamId);
            return 0f;
        }

        if (!lastDown)
        {
            holdTicks[steamId] = 0;
            repeatCountdown[steamId] = 10;
            return direction;
        }

        var hold = holdTicks.GetValueOrDefault(steamId, 0) + 1;
        holdTicks[steamId] = hold;

        var countdown = repeatCountdown.GetValueOrDefault(steamId, 10) - 1;
        if (countdown > 0)
        {
            repeatCountdown[steamId] = countdown;
            return 0f;
        }

        var accelSteps = Math.Max(0, (hold - 10) / 15);

        // Accelerate in two ways:
        // 1) Repeat faster (countdown gets smaller)
        // 2) Apply more degrees per repeat (multiplier grows)
        repeatCountdown[steamId] = Math.Max(1, 6 - accelSteps);
        var multiplier = Math.Min(10, 1 + accelSteps);
        return direction * multiplier;
    }

    private CBaseModelEntity? GetBlockAtAim(IPlayer player)
    {
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return null;

        var eyePosition = GetEyePosition(pawn);
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

    private static Vector GetEyePosition(CCSPlayerPawn pawn)
    {
        var absOrigin = pawn.AbsOrigin ?? Vector.Zero;
        return pawn.CameraServices?.OldPlayerViewOffsetZ != null
            ? new Vector(absOrigin.X, absOrigin.Y, absOrigin.Z + 64f)
            : absOrigin;
    }

    private static bool IsButtonDown(IEnumerable<CSGOUserCmdPB> usercmds, ulong mask)
    {
        foreach (var cmd in usercmds)
        {
            if (cmd?.Base?.ButtonsPb == null) continue;
            var buttons = cmd.Base.ButtonsPb;
            if ((buttons.Buttonstate1 & mask) != 0 || (buttons.Buttonstate2 & mask) != 0 || (buttons.Buttonstate3 & mask) != 0)
                return true;
        }

        return false;
    }

    private static void ClearButtonMask(IEnumerable<CSGOUserCmdPB> usercmds, ulong mask)
    {
        foreach (var cmd in usercmds)
        {
            if (cmd?.Base?.ButtonsPb == null) continue;
            cmd.Base.ButtonsPb.Buttonstate1 &= ~mask;
            cmd.Base.ButtonsPb.Buttonstate2 &= ~mask;
            cmd.Base.ButtonsPb.Buttonstate3 &= ~mask;
        }
    }

    private static float NormalizeYaw(float yaw)
    {
        yaw %= 360f;
        if (yaw > 180f) yaw -= 360f;
        if (yaw < -180f) yaw += 360f;
        return yaw;
    }

    private static string VectorToString(Vector v) => $"{v.X.ToString(System.Globalization.CultureInfo.InvariantCulture)} {v.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)} {v.Z.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    private static string QAngleToString(QAngle a) => $"{a.Pitch.ToString(System.Globalization.CultureInfo.InvariantCulture)} {a.Yaw.ToString(System.Globalization.CultureInfo.InvariantCulture)} {a.Roll.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
}
