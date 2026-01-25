using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.EntitySystem;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;

using CBaseModelEntity = SwiftlyS2.Shared.SchemaDefinitions.CBaseModelEntity;

namespace BlockPasses;

public sealed class BlockPassEntityManager
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;
    private readonly PrecachingService _precaching;

    private readonly List<CHandle<CBaseModelEntity>> _active = new();
    private readonly Dictionary<uint, BlockPassEntityConfig> _handleToConfig = new();

    private readonly object _removalLock = new();
    private List<CHandle<CBaseModelEntity>>? _pendingRemoval;

    public BlockPassEntityManager(ISwiftlyCore core, ILogger logger, PrecachingService precaching)
    {
        _core = core;
        _logger = logger;
        _precaching = precaching;
    }

    public IReadOnlyList<CBaseModelEntity> ActiveEntities
    {
        get
        {
            // Return a safe snapshot of valid entities.
            var resolved = new List<CBaseModelEntity>(_active.Count);
            foreach (var handle in _active)
            {
                var ent = ResolveHandle(handle);
                if (ent != null) resolved.Add(ent);
            }

            return resolved;
        }
    }

    public void RemoveAll(bool immediate)
    {
        lock (_removalLock)
        {
            _pendingRemoval = null;

            var entitiesToRemove = _active.ToList();
            
            if (entitiesToRemove.Count == 0) return;

            if (immediate)
            {
                foreach (var handle in entitiesToRemove) SafeKillHandle(handle);
                _active.Clear();
                _handleToConfig.Clear();
                return;
            }

            _pendingRemoval = entitiesToRemove;
            _core.Scheduler.NextTick(() =>
            {
                lock (_removalLock)
                {
                    if (_pendingRemoval != entitiesToRemove) return;
                    foreach (var handle in entitiesToRemove) SafeKillHandle(handle);
                    
                    foreach (var handle in entitiesToRemove)
                    {
                        _active.Remove(handle);
                        _handleToConfig.Remove(handle.Raw);
                    }
                    
                    _pendingRemoval = null;
                }
            });
        }
    }

    public void RemoveEntity(CBaseModelEntity entity)
    {
        if (entity == null || !entity.IsValid) return;

        var handle = _core.EntitySystem.GetRefEHandle(entity);
        _active.Remove(handle);
        _handleToConfig.Remove(handle.Raw);

        _core.Scheduler.NextTick(() => SafeKillHandle(handle));
    }

    public BlockPassEntityConfig? GetConfig(CBaseModelEntity entity)
    {
        if (entity == null || !entity.IsValid) return null;

        var handle = _core.EntitySystem.GetRefEHandle(entity);
        return _handleToConfig.TryGetValue(handle.Raw, out var cfg) ? cfg : null;
    }

    public CBaseModelEntity? GetEntityByConfig(BlockPassEntityConfig cfg)
    {
        foreach (var (raw, existingCfg) in _handleToConfig)
        {
            if (!ReferenceEquals(existingCfg, cfg)) continue;

            var handle = new CHandle<CBaseModelEntity>(raw);
            return ResolveHandle(handle);
        }

        return null;
    }

    public int GetActiveCount()
    {
        // Clean up invalid entities first
        CleanupInvalidEntities();
        return _active.Count;
    }

    public bool AreBlocksSpawned(List<BlockPassEntityConfig> expectedBlocks)
    {
        // Clean up invalid entities
        CleanupInvalidEntities();
        
        // If we don't have the same count, blocks are missing
        if (_active.Count != expectedBlocks.Count) return false;
        
        // Check if all expected blocks exist
        foreach (var cfg in expectedBlocks)
        {
            var exists = _handleToConfig.Values.Any(existingCfg => 
                existingCfg.ModelPath == cfg.ModelPath &&
                existingCfg.Origin == cfg.Origin &&
                existingCfg.Angles == cfg.Angles);
            
            if (!exists) return false;
        }
        
        return true;
    }

    public void EnsureBlocksSpawned(List<BlockPassEntityConfig> expectedBlocks)
    {
        // Clean up invalid entities
        CleanupInvalidEntities();
        
        // Find which blocks are missing
        var missingBlocks = new List<BlockPassEntityConfig>();
        
        foreach (var cfg in expectedBlocks)
        {
            var exists = _handleToConfig.Values.Any(existingCfg => 
                existingCfg.ModelPath == cfg.ModelPath &&
                existingCfg.Origin == cfg.Origin &&
                existingCfg.Angles == cfg.Angles);
            
            if (!exists)
            {
                missingBlocks.Add(cfg);
            }
        }
        
        // Spawn only the missing blocks
        foreach (var cfg in missingBlocks)
        {
            Spawn(cfg);
        }
    }

    private void CleanupInvalidEntities()
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var handle = _active[i];
            if (ResolveHandle(handle) != null) continue;

            _active.RemoveAt(i);
            _handleToConfig.Remove(handle.Raw);
        }
    }

    public CBaseModelEntity? Spawn(BlockPassEntityConfig cfg)
    {
        var prop = _core.EntitySystem.CreateEntityByDesignerName<CBaseModelEntity>("prop_dynamic_override");
        if (prop == null)
        {
            _logger.LogWarning("BlockPasses: Failed to create prop entity");
            return null;
        }

        var modelPath = (cfg.ModelPath ?? string.Empty).TrimStart('/', '\\');
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            SafeKillEntity(prop);
            return null;
        }

        _precaching.AddModel(modelPath);

        var origin = ParseUtil.ParseVector(cfg.Origin);
        var angles = ParseUtil.ParseQAngle(cfg.Angles);

        prop.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
        prop.Teleport(origin, angles, Vector.Zero);

        using (var kv = new CEntityKeyValues())
        {
            kv.SetString("model", modelPath);
            prop.DispatchSpawn(kv);
        }

        _core.Scheduler.NextTick(() =>
        {
            if (!prop.IsValid) return;
            prop.SetModel(modelPath);
            var s = cfg.Scale ?? 1.0f;
            if (s <= 0.01f) s = 1.0f;
            prop.SetScale(s);
        });

        var handle = _core.EntitySystem.GetRefEHandle(prop);
        _active.Add(handle);
        _handleToConfig[handle.Raw] = cfg;

        return prop;
    }

    private CBaseModelEntity? ResolveHandle(CHandle<CBaseModelEntity> handle)
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

    private void SafeKillHandle(CHandle<CBaseModelEntity> handle)
    {
        var ent = ResolveHandle(handle);
        if (ent == null) return;
        SafeKillEntity(ent);
    }

    private void SafeKillEntity(CBaseModelEntity? entity)
    {
        if (entity == null) return;

        try
        {
            if (!entity.IsValid) return;

            try
            {
                entity.Despawn();
            }
            catch
            {
                _core.Scheduler.NextTick(() =>
                {
                    try
                    {
                        if (entity.IsValid) entity.Despawn();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "BlockPasses: Failed to despawn entity");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BlockPasses: Failed to schedule entity removal");
        }
    }
}
