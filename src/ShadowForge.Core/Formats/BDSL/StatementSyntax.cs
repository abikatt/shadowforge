namespace ShadowForge.Formats.BDSL;

/// <summary>
/// Spellings shared by <see cref="StatementParser"/> and <see cref="StatementWriter"/>.
/// </summary>
internal static class StatementSyntax
{
    /// <summary>
    /// Indexed by the set_variable op word.
    /// </summary>
    public static readonly string[] AssignOps = ["=", "+=", "-=", "*=", "/="];

    /// <summary>
    /// set_variable value_type words that read a game value instead of an operand.
    /// </summary>
    public static readonly KeywordTable ValueSources = new(
    [
        (4, "playtime"), (5, "realtime"), (6, "leader_id"), (7, "party_count"),
        (8, "gold"), (9, "medals"), (100, "encounters"),
    ]);
}
