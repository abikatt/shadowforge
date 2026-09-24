using System.Diagnostics.CodeAnalysis;

namespace ShadowForge.Formats.BDSL;

/// <summary>
/// Two-way map between a numeric field value and its BDSL keyword, so the reader
/// and the writer spell every value the same way.
/// </summary>
internal sealed class KeywordTable((uint Value, string Name)[] entries)
{
    public IReadOnlyList<(uint Value, string Name)> Entries => entries;

    public bool TryGetName(uint value, [NotNullWhen(true)] out string? name)
    {
        foreach (var entry in entries)
        {
            if (entry.Value == value)
            {
                name = entry.Name;
                return true;
            }
        }
        name = null;
        return false;
    }

    public bool TryGetValue(string name, out uint value)
    {
        foreach (var entry in entries)
        {
            if (entry.Name == name)
            {
                value = entry.Value;
                return true;
            }
        }
        value = 0;
        return false;
    }
}
