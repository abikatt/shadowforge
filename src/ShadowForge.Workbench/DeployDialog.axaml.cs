using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ShadowForge.Workbench;

public sealed record DeployRequest(
    string ModelPath, string ModName, string? Author, string? Version, string? Description, bool Enable);

/// <summary>
/// Asks for an edited character model and the mod to deploy it as. Closes with the request,
/// or null when cancelled. The model defaults to the one the Blender extension keeps for the
/// character, when there is one. <see cref="ForTextures"/> asks only for the mod.
/// </summary>
public partial class DeployDialog : Window
{
    private readonly ISet<string> _installedMods;
    private bool _askForModel = true;

    public DeployDialog() : this("", null, "", new HashSet<string>()) { }

    /// <summary>
    /// The dialog for deploying a character's edited textures, which has no model to choose.
    /// </summary>
    public static DeployDialog ForTextures(string entityId, string textureDir, string suggestedName, ISet<string> installedMods)
    {
        var dialog = new DeployDialog(entityId, null, suggestedName, installedMods)
        {
            Title = "Deploy textures as mod",
            _askForModel = false,
        };
        dialog.ModelSection.IsVisible = false;
        dialog.IntroText.Text = $"Convert the PNGs you changed in {textureDir} back into {entityId}'s textures "
            + "and write them as a mod. Unchanged PNGs are skipped, and each must keep its original size.";
        dialog.Validate();
        return dialog;
    }

    public DeployDialog(string entityId, string? suggestedModel, string suggestedName, ISet<string> installedMods)
    {
        InitializeComponent();
        _installedMods = installedMods;
        IntroText.Text = $"Cook your edited {entityId} back into game files (model, textures and motions) "
            + "and write them as a mod over the shipped character.";
        ModelBox.Text = suggestedModel ?? "";
        ModelHint.Text = suggestedModel is null
            ? "Export your edit from Blender as .glb and choose it here."
            : "This is the file Open in Blender imported. It only has your edits if you exported over it "
              + "from Blender; otherwise choose the .glb you exported.";
        NameBox.Text = suggestedName;

        ModelBox.TextChanged += (_, _) => Validate();
        NameBox.TextChanged += (_, _) => Validate();
        BrowseButton.Click += async (_, _) =>
        {
            var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Choose the edited {entityId} model",
                FileTypeFilter = [new FilePickerFileType("glTF model") { Patterns = ["*.glb", "*.gltf"] }],
            });
            if (picked is [var file, ..] && file.TryGetLocalPath() is { } path) ModelBox.Text = path;
        };
        CancelButton.Click += (_, _) => Close(null);
        DeployButton.Click += (_, _) => Close(new DeployRequest(
            ModelBox.Text!.Trim(), NameBox.Text!.Trim(), Optional(AuthorBox.Text), Optional(VersionBox.Text),
            Optional(DescriptionBox.Text), EnableBox.IsChecked == true));
        Opened += (_, _) => NameBox.Focus();
        Validate();
    }

    private static string? Optional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private void Validate()
    {
        string model = ModelBox.Text?.Trim() ?? "";
        string name = NameBox.Text?.Trim() ?? "";
        string? problem =
            _askForModel && model.Length == 0 ? "Choose the edited model." :
            _askForModel && !File.Exists(model) ? "That model file does not exist." :
            name.Length == 0 ? "Enter a mod name." :
            !name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_') ? "Use letters, digits, spaces, '-' or '_'." :
            null;

        DeployButton.IsEnabled = problem is null;
        ValidationText.Text = problem
            ?? (_installedMods.Contains(name) ? $"Updates the installed mod {name}." : $"Creates mods\\{name}.");
    }
}
