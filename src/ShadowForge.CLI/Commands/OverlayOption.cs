using System.CommandLine;

namespace ShadowForge.CLI.Commands;

internal sealed class OverlayOption : Option<string[]>
{
    public OverlayOption() : base("--overlay")
    {
        Description = "Loose root searched ahead of the install, repeatable, first match wins.";
        AllowMultipleArgumentsPerToken = false;
    }
}
