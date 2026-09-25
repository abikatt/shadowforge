using Avalonia.Controls;
using ShadowForge.GameData.Entities;

namespace ShadowForge.Workbench;

/// <summary>
/// Asks for the id of a new entity derived from an existing one. Closes with the new id, or
/// null when cancelled. The id must be well formed, map to a known class and not already exist.
/// </summary>
public partial class DeriveDialog : Window
{
    private readonly ISet<string> _existing;
    private readonly string _derivedRoot;

    public DeriveDialog() : this("", new HashSet<string>(), "") { }

    public DeriveDialog(string sourceId, ISet<string> existingIds, string derivedRoot)
    {
        InitializeComponent();
        _existing = existingIds;
        _derivedRoot = derivedRoot;
        IntroText.Text = $"Copy {sourceId} under a new id the game does not ship. The model, motions, "
            + "textures and rig tables are re-ided; edit the result in Blender and deploy it as a mod.";
        IdBox.TextChanged += (_, _) => Validate();
        CancelButton.Click += (_, _) => Close(null);
        DeriveButton.Click += (_, _) => Close(IdBox.Text!.Trim().ToLowerInvariant());
        Opened += (_, _) => IdBox.Focus();
        Validate();
    }

    private void Validate()
    {
        string id = IdBox.Text?.Trim().ToLowerInvariant() ?? "";
        string? problem =
            id.Length == 0 ? "" :
            !EntityId.IsEntityId(id) ? "Use a 2-letter class prefix, 2 or 3 digits and an optional _variant." :
            EntityId.ClassFor(id) is null ? $"'{id[..2]}' is not a known entity class." :
            _existing.Contains(id) ? $"{id} already exists in the game." :
            null;

        DeriveButton.IsEnabled = problem is null;
        ValidationText.Text = problem ?? $"Class folder: {EntityId.ClassFor(id)}";
        OutText.Text = Path.Combine(_derivedRoot, id.Length > 0 ? id : "<id>");
    }
}
