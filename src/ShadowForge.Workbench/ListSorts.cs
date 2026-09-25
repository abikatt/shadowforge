namespace ShadowForge.Workbench;

/// <summary>
/// One entry of a list's Sort box. Every sort breaks ties by id, so the order is stable.
/// </summary>
public sealed record SortOption<T>(string Label, Func<IEnumerable<T>, IEnumerable<T>> Apply)
{
    public override string ToString() => Label;
}

public static class ListSorts
{
    private static readonly StringComparer Text = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Sorting by name puts the entries the game leaves unnamed last, in id order, rather
    /// than mixing bare ids in among the names.
    /// </summary>
    public static readonly SortOption<CharacterRow>[] Characters =
    [
        new("Id", rows => rows.OrderBy(r => r.Id, Text)),
        new("Name", rows => rows.OrderBy(r => !r.IsNamed).ThenBy(r => r.DisplayName, Text).ThenBy(r => r.Id, Text)),
        new("Class", rows => rows.OrderBy(r => r.Class, Text).ThenBy(r => r.Id, Text)),
        new("File path", rows => rows.OrderBy(r => r.ModelDef, Text)),
    ];

    public static readonly SortOption<MapRow>[] Maps =
    [
        new("Id", rows => rows.OrderBy(r => r.Id, Text)),
        new("Name", rows => rows.OrderBy(r => !r.IsNamed).ThenBy(r => r.Name, Text).ThenBy(r => r.Id, Text)),
        new("Category", rows => rows.OrderBy(r => r.Category, Text).ThenBy(r => r.Id, Text)),
        new("Region pack", rows => rows.OrderBy(r => r.Region, Text).ThenBy(r => r.Id, Text)),
        new("Model count", rows => rows.OrderByDescending(r => r.ModelCount).ThenBy(r => r.Id, Text)),
    ];
}
