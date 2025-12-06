using SwiftlyS2.Shared.Commands;

namespace BlockPasses;

public partial class BlockPasses
{
    [Command("bp_reload", registerRaw: true, permission: "blockpasses.reload")]
    public void OnCmdReload(ICommandContext context)
    {
        _config = _configService?.ReloadConfig() ?? _config;
        _precachingService?.UpdateConfig(_config);

        const string msg = "Configuration reloaded. Note: New models require a map change to take effect.";
        context.Sender?.SendChat(msg);
    }
}
