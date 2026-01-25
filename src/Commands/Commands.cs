using System;
using System.Threading.Tasks;

using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Core.Menus.OptionsBase;

namespace BlockPasses;

public partial class BlockPasses
{
    private IMenuAPI BuildAddPresetMenu(SwiftlyS2.Shared.ISwiftlyCore core, IPlayer player)
    {
        var builder = core.MenusAPI.CreateBuilder()
            .Design.SetMenuTitle("Add block")
            .EnableSound();

        if (_config.ModelPresets.Count == 0)
        {
            builder.AddOption(new ButtonMenuOption("(no presets configured)"));
            return builder.Build();
        }

        foreach (var preset in _config.ModelPresets)
        {
            if (preset is null) continue;
            if (string.IsNullOrWhiteSpace(preset.ModelPath)) continue;
            var modelPath = preset.ModelPath;
            var label = string.IsNullOrWhiteSpace(preset.Name) ? modelPath : preset.Name;
            var opt = new ButtonMenuOption(label);
            opt.Click += async (_, args) =>
            {
                core.Scheduler.NextTick(() =>
                {
                    _editorService?.AddBlockAtAim(args.Player, modelPath);
                });
                await ValueTask.CompletedTask;
            };
            builder.AddOption(opt);
        }

        return builder.Build();
    }

    public void OnCmdMenu(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is null)
        {
            context.Reply("This command can only be used by a player.");
            return;
        }

        var core = Core;
        core.MenusAPI.OpenMenuForPlayer(context.Sender, BuildBlockPassMenu(core, context.Sender));
    }

    private IMenuAPI BuildBlockPassMenu(SwiftlyS2.Shared.ISwiftlyCore core, IPlayer player)
    {
        var builder = core.MenusAPI.CreateBuilder()
            .Design.SetMenuTitle("BlockPasses Editor")
            .EnableSound();

        var editModeEnabled = _editorService is not null && _editorService.IsInEditMode(player);
        var editLabel = editModeEnabled ? "Edit mode: ON" : "Edit mode: OFF";

        var toggleEdit = new ButtonMenuOption(editLabel);
        toggleEdit.Click += async (_, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                if (_editorService is null) return;
                if (_editorService.IsInEditMode(args.Player))
                {
                    _editorService.DisableEditMode(args.Player);
                    core.Engine.ExecuteCommand("mp_warmup_pausetimer 0");
                    core.Engine.ExecuteCommand("mp_warmup_end");
                }
                else
                {
                    _editorService.EnableEditMode(args.Player);
                    core.Engine.ExecuteCommand("mp_warmup_pausetimer 1");
                    core.Engine.ExecuteCommand("mp_warmuptime 999999");
                    core.Engine.ExecuteCommand("mp_warmup_start");
                }

                core.MenusAPI.OpenMenuForPlayer(args.Player, BuildBlockPassMenu(core, args.Player));
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(toggleEdit);

        var add = new ButtonMenuOption("Add block at aim");
        add.Click += async (_, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                _editorService?.AddBlockAtAim(args.Player);
                core.MenusAPI.OpenMenuForPlayer(args.Player, BuildBlockPassMenu(core, args.Player));
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(add);

        var remove = new ButtonMenuOption("Remove block at aim");
        remove.Click += async (_, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                _editorService?.RemoveBlockAtAim(args.Player);
                core.MenusAPI.OpenMenuForPlayer(args.Player, BuildBlockPassMenu(core, args.Player));
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(remove);

        const float moveStep = 5f;
        var up = new ButtonMenuOption($"Move up (+{moveStep})");
        up.Click += async (_, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                _editorService?.MoveBlockAtAim(args.Player, +moveStep);
                core.MenusAPI.OpenMenuForPlayer(args.Player, BuildBlockPassMenu(core, args.Player));
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(up);

        var down = new ButtonMenuOption($"Move down (-{moveStep})");
        down.Click += async (_, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                _editorService?.MoveBlockAtAim(args.Player, -moveStep);
                core.MenusAPI.OpenMenuForPlayer(args.Player, BuildBlockPassMenu(core, args.Player));
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(down);

        const float rotStep = 15f;
        var rotLeft = new ButtonMenuOption($"Rotate -{rotStep}");
        rotLeft.Click += async (_, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                _editorService?.RotateBlockAtAim(args.Player, -rotStep);
                core.MenusAPI.OpenMenuForPlayer(args.Player, BuildBlockPassMenu(core, args.Player));
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(rotLeft);

        var rotRight = new ButtonMenuOption($"Rotate +{rotStep}");
        rotRight.Click += async (_, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                _editorService?.RotateBlockAtAim(args.Player, +rotStep);
                core.MenusAPI.OpenMenuForPlayer(args.Player, BuildBlockPassMenu(core, args.Player));
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(rotRight);

        const float scaleStep = 0.1f;
        var scalePlus = new ButtonMenuOption($"Scale +{scaleStep}");
        scalePlus.Click += async (_, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                _editorService?.ScaleBlockAtAim(args.Player, +scaleStep);
                core.MenusAPI.OpenMenuForPlayer(args.Player, BuildBlockPassMenu(core, args.Player));
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(scalePlus);

        var scaleMinus = new ButtonMenuOption($"Scale -{scaleStep}");
        scaleMinus.Click += async (_, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                _editorService?.ScaleBlockAtAim(args.Player, -scaleStep);
                core.MenusAPI.OpenMenuForPlayer(args.Player, BuildBlockPassMenu(core, args.Player));
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(scaleMinus);

        var save = new ButtonMenuOption("Save blocks");
        save.Click += async (_, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                _editorService?.Save(args.Player);
                core.MenusAPI.OpenMenuForPlayer(args.Player, BuildBlockPassMenu(core, args.Player));
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(save);

        var reload = new ButtonMenuOption("Reload config");
        reload.Click += async (_, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                _config = _configService?.LoadConfig() ?? _config;
                _precachingService?.UpdateConfig(_config);
                try
                {
                    var mapName = core.Engine.GlobalVars.MapName.Value;
                    var blocks = _mapDataService?.Reload(mapName) ?? new BlockPassMapDataService(core, core.Logger).Load(mapName);
                    _precachingService?.UpdateMapBlocks(blocks);
                }
                catch
                {
                }
                core.MenusAPI.OpenMenuForPlayer(args.Player, BuildBlockPassMenu(core, args.Player));
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(reload);

        return builder.Build();
    }

    [Command("bp_edit", registerRaw: true, permission: "blockpasses.edit")]
    public void OnCmdEdit(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is null)
        {
            context.Reply("This command can only be used by a player.");
            return;
        }

        if (_editorService is null)
        {
            SendChat(context.Sender, "BlockPasses editor not ready.");
            return;
        }

        var arg = context.Args.Length > 0 ? (context.Args[0] ?? string.Empty).Trim() : string.Empty;
        var enable = !(arg == "0" || arg.Equals("off", StringComparison.OrdinalIgnoreCase) || arg.Equals("false", StringComparison.OrdinalIgnoreCase));

        if (enable)
        {
            _editorService.EnableEditMode(context.Sender);
            Core.Engine.ExecuteCommand("mp_warmup_pausetimer 1");
            Core.Engine.ExecuteCommand("mp_warmuptime 999999");
            Core.Engine.ExecuteCommand("mp_warmup_start");
        }
        else
        {
            _editorService.DisableEditMode(context.Sender);
            Core.Engine.ExecuteCommand("mp_warmup_pausetimer 0");
            Core.Engine.ExecuteCommand("mp_warmup_end");
        }

        UpdateEditorCommandRegistration();
    }

    public void OnCmdAdd(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is null)
        {
            context.Reply("This command can only be used by a player.");
            return;
        }

        if (_editorService is null)
        {
            SendChat(context.Sender, "BlockPasses editor not ready.");
            return;
        }

        var modelPath = context.Args.Length > 0 ? context.Args[0] : null;
        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            _editorService.AddBlockAtAim(context.Sender, modelPath);
            return;
        }

        var core = Core;
        core.MenusAPI.OpenMenuForPlayer(context.Sender, BuildAddPresetMenu(core, context.Sender));
    }

    public void OnCmdRemove(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is null)
        {
            context.Reply("This command can only be used by a player.");
            return;
        }

        if (_editorService is null)
        {
            SendChat(context.Sender, "BlockPasses editor not ready.");
            return;
        }

        _editorService.RemoveBlockAtAim(context.Sender);
    }

    public void OnCmdRotate(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is null)
        {
            context.Reply("This command can only be used by a player.");
            return;
        }

        if (_editorService is null)
        {
            SendChat(context.Sender, "BlockPasses editor not ready.");
            return;
        }

        var degrees = 15f;
        if (context.Args.Length > 0 && float.TryParse(context.Args[0], out var parsed)) degrees = parsed;
        _editorService.RotateBlockAtAim(context.Sender, degrees);
    }

    public void OnCmdScale(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is null)
        {
            context.Reply("This command can only be used by a player.");
            return;
        }

        if (_editorService is null)
        {
            SendChat(context.Sender, "BlockPasses editor not ready.");
            return;
        }

        var delta = 0.1f;
        if (context.Args.Length > 0 && float.TryParse(context.Args[0], out var parsed)) delta = parsed;
        _editorService.ScaleBlockAtAim(context.Sender, delta);
    }

    public void OnCmdUp(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is null)
        {
            context.Reply("This command can only be used by a player.");
            return;
        }

        if (_editorService is null)
        {
            SendChat(context.Sender, "BlockPasses editor not ready.");
            return;
        }

        var step = 5f;
        if (context.Args.Length > 0 && float.TryParse(context.Args[0], out var parsed)) step = parsed;
        _editorService.MoveBlockAtAim(context.Sender, Math.Abs(step));
    }

    public void OnCmdDown(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is null)
        {
            context.Reply("This command can only be used by a player.");
            return;
        }

        if (_editorService is null)
        {
            SendChat(context.Sender, "BlockPasses editor not ready.");
            return;
        }

        var step = 5f;
        if (context.Args.Length > 0 && float.TryParse(context.Args[0], out var parsed)) step = parsed;
        _editorService.MoveBlockAtAim(context.Sender, -Math.Abs(step));
    }

    public void OnCmdSave(ICommandContext context)
    {
        if (_editorService is null)
        {
            context.Reply("BlockPasses editor not ready.");
            return;
        }

        _editorService.Save(context.Sender);
    }

    [Command("bp_reload", registerRaw: true, permission: "blockpasses.reload")]
    public void OnCmdReload(ICommandContext context)
    {
        _config = _configService?.LoadConfig() ?? _config;
        _precachingService?.UpdateConfig(_config);

        try
        {
            var mapName = Core.Engine.GlobalVars.MapName.Value;
            ReloadAndApplyMapBlocks(mapName, respawn: true);
        }
        catch
        {
        }

        if (context.Sender is not null)
        {
            SendChatLocalized(context.Sender, "blockpasses.message", _config.Players);
            SendChat(context.Sender, "Configuration reloaded. Note: New models require a map change to take effect.");
        }
    }
}
