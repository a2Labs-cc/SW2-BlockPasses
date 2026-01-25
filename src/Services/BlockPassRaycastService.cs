using System;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace BlockPasses;

public sealed class BlockPassRaycastService
{
    private readonly ISwiftlyCore _core;

    public BlockPassRaycastService(ISwiftlyCore core)
    {
        _core = core;
    }

    public unsafe CTraceFilter CreateTraceFilter(uint ignoreIndex)
    {
        var filter = CreateTraceFilterInternal();
        filter.QueryShapeAttributes.InteractsWith = MaskTrace.Solid | MaskTrace.WorldGeometry | MaskTrace.StaticLevel | MaskTrace.PhysicsProp;
        filter.QueryShapeAttributes.InteractsExclude = MaskTrace.Empty;
        filter.QueryShapeAttributes.InteractsAs = MaskTrace.Empty;
        filter.QueryShapeAttributes.ObjectSetMask = RnQueryObjectSet.All;
        filter.QueryShapeAttributes.CollisionGroup = CollisionGroup.Default;
        filter.QueryShapeAttributes.EntityIdsToIgnore[0] = ignoreIndex;
        filter.QueryShapeAttributes.EntityIdsToIgnore[1] = 0;
        return filter;
    }

    private static unsafe CTraceFilter CreateTraceFilterInternal()
    {
        try
        {
            var withBool = Activator.CreateInstance(typeof(CTraceFilter), new object?[] { true });
            if (withBool is not null) return (CTraceFilter)withBool;
        }
        catch { }

        return (CTraceFilter)(Activator.CreateInstance(typeof(CTraceFilter)) ?? default(CTraceFilter));
    }

    public unsafe bool TryGetTargetPosition(IPlayer player, float? fixedDistance, out Vector targetPosition)
    {
        targetPosition = Vector.Zero;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return false;

        var absOrigin = pawn.AbsOrigin ?? Vector.Zero;
        var eyePosition = pawn.CameraServices?.OldPlayerViewOffsetZ != null
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
        var trace = new CGameTrace();
        var ray = new Ray_t();
        ray.Init(Vector.Zero);
        var filter = CreateTraceFilter((uint)pawn.Index);

        var traceEnd = traceStart + (forward * 8192.0f);
        _core.Trace.TraceShape(traceStart, traceEnd, ray, filter, ref trace);

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
