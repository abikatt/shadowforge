using System.CommandLine;
using ShadowForge.GameData;

namespace ShadowForge.CLI.Commands;

internal sealed class GameRootOption : Option<string?>
{
    public GameRootOption() : base("--game-root")
    {
        Description = "Game-data root (default: SHADOWFORGE_GAME_ROOT or the registry install).";
    }

    public GameInstall Locate(ParseResult parseResult) =>
        GameInstall.Locate(parseResult.GetValue(this));

    public GameInstall Locate(ParseResult parseResult, OverlayOption overlays) =>
        Locate(parseResult).WithOverlays(parseResult.GetValue(overlays) ?? []);
}
