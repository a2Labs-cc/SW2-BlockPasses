using System;
using System.Collections.Generic;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;

using Microsoft.Extensions.Logging;

namespace BlockPasses;

public class PrecachingService
{
    private readonly ISwiftlyCore _core;
    private BlockPassesConfig _config;
    private readonly List<string> _modelsToPrecache = new();

    public PrecachingService(ISwiftlyCore core, BlockPassesConfig config)
    {
        _core = core;
        _config = config;
        
        // Immediately precache all models from config
        PrecacheAllModels();
    }

    public void UpdateConfig(BlockPassesConfig config)
    {
        _config = config;
        PrecacheAllModels();
    }

    /// <summary>
    /// Precaches all models from config using GameFileSystem.PrecacheFile.
    /// This is called immediately on load as a fallback for late loading.
    /// </summary>
    private void PrecacheAllModels()
    {
        _modelsToPrecache.Clear();
        
        foreach (var kvp in _config.Maps)
        {
            foreach (var entity in kvp.Value)
            {
                if (!string.IsNullOrWhiteSpace(entity.ModelPath))
                {
                    AddModel(entity.ModelPath);
                }
            }
        }
    }

    /// <summary>
    /// Adds a model to the precache list and immediately precaches it via filesystem.
    /// </summary>
    public void AddModel(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        
        path = path.TrimStart('/', '\\');
        
        if (!_modelsToPrecache.Contains(path))
        {
            _modelsToPrecache.Add(path);
            // Precache immediately via filesystem as fallback
            _core.GameFileSystem.PrecacheFile(path, "GAME");
            _core.Logger.LogInformation("BlockPasses: Precached model: {Path}", path);
        }
    }

    /// <summary>
    /// Adds models to the resource manifest during the OnPrecacheResource event.
    /// This is the proper way to register models during map load.
    /// </summary>
    public void AddModels(IOnPrecacheResourceEvent @event)
    {
        _core.Logger.LogInformation("BlockPasses: OnPrecacheResource - registering {Count} models to manifest...", _modelsToPrecache.Count);
        
        foreach (var model in _modelsToPrecache)
        {
            @event.AddItem(model);
            _core.Logger.LogInformation("BlockPasses: Manifest registered: {Path}", model);
        }
    }
}
