using System;
using System.Collections.Generic;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Trace;

namespace BlockPasses;

public sealed class BlockPassRaycastService
{
    private readonly ISwiftlyCore _core;

    public BlockPassRaycastService(ISwiftlyCore core)
    {
        _core = core;
    }

    public unsafe bool TryGetTargetPosition(IPlayer player, float? fixedDistance, out Vector targetPosition)
    {
        targetPosition = Vector.Zero;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return false;

        var absOrigin = pawn.AbsOrigin ?? Vector.Zero;
        var camera = pawn.CameraServices;
        var eyePosition = camera is not null && camera.IsValid
            ? new Vector(absOrigin.X, absOrigin.Y, absOrigin.Z + 64f)
            : absOrigin;

        var forward = GetForwardVector(pawn.EyeAngles);
        var distance = fixedDistance ?? 200.0f;
        var desiredEnd = eyePosition + (forward * distance);

        if (fixedDistance.HasValue)
        {
            targetPosition = desiredEnd;
            return true;
        }

        var traceStart = eyePosition + (forward * 16.0f);
        var traceEnd = traceStart + (forward * 8192.0f);
        TraceParams? traceParams = new TraceParams
        {
            InteractWith = MaskTrace.Solid | MaskTrace.WorldGeometry | MaskTrace.StaticLevel | MaskTrace.PhysicsProp,
            InteractExclude = MaskTrace.Empty,
            InteractAs = MaskTrace.Empty,
            ObjectQuery = RnQueryObjectSet.All,
            Collision = CollisionGroup.Default,
            EntitiesToIgnore = new List<CEntityInstance> { pawn }
        };
        var trace = _core.Trace.TraceShapeLine(in traceStart, in traceEnd, in traceParams);

        if (!trace.StartInSolid && trace.Fraction < 1.0f)
        {
            targetPosition = trace.HitPoint;
            return true;
        }

        targetPosition = desiredEnd;
        return true;
    }

    public static Vector GetForwardVector(QAngle viewAngles)
    {
        return new Vector(
            (float)(Math.Cos(viewAngles.Yaw * Math.PI / 180) * Math.Cos(viewAngles.Pitch * Math.PI / 180)),
            (float)(Math.Sin(viewAngles.Yaw * Math.PI / 180) * Math.Cos(viewAngles.Pitch * Math.PI / 180)),
            (float)(-Math.Sin(viewAngles.Pitch * Math.PI / 180))
        );
    }
}
