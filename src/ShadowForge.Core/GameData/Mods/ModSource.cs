using ShadowForge.GameData.Entities;
using Tomlyn;
using Tomlyn.Model;

namespace ShadowForge.GameData.Mods;

/// <summary>
/// One declared entity. Db maps a mod-relative db file path to the row appended to it.
/// </summary>
public sealed record SourceEntity(
    string Id, string Class, string Name, string? DeriveFrom, string? MotScrTemplate,
    IReadOnlyDictionary<string, string> Db, string Dir);

/// <summary>
/// A mod source tree: exactly one {name}.toml at the root plus entities\{id}\entity.toml.
/// The builder copies every other file in an entity folder, so a toml declares only what a
/// file name cannot say.
/// </summary>
public sealed record ModSource(string Name, string? Description, IReadOnlyList<SourceEntity> Entities)
{
    internal const string EntityToml = "entity.toml";

    public static ModSource Load(string rootDir)
    {
        var tomls = Directory.EnumerateFiles(rootDir, "*.toml").ToList();
        if (tomls.Count == 0)
            throw new FileNotFoundException($"No mod toml at the source root: {rootDir}");
        if (tomls.Count > 1)
            throw new InvalidOperationException(
                $"Multiple mod toml files at the source root, expected one: {string.Join(", ", tomls)}");
        string modToml = tomls[0];

        var mod = Table(Toml.ToModel(File.ReadAllText(modToml)), "mod")
            ?? throw new InvalidOperationException($"{modToml} has no [mod] table.");
        string name = Str(mod, "name")
            ?? throw new InvalidOperationException($"{modToml} [mod] has no name.");

        var entities = new List<SourceEntity>();
        string entitiesDir = Path.Combine(rootDir, "entities");
        if (Directory.Exists(entitiesDir))
        {
            foreach (string dir in Directory
                         .EnumerateDirectories(entitiesDir)
                         .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                entities.Add(LoadEntity(dir));
        }
        return new ModSource(name, Str(mod, "description"), entities);
    }

    private static SourceEntity LoadEntity(string dir)
    {
        string folder = Path.GetFileName(dir);
        string path = Path.Combine(dir, EntityToml);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Entity folder '{folder}' has no {EntityToml}.");

        var doc = Toml.ToModel(File.ReadAllText(path));
        var entity = Table(doc, "entity")
            ?? throw new InvalidOperationException($"{path} has no [entity] table.");

        string id = Str(entity, "id")
            ?? throw new InvalidOperationException($"{path} [entity] has no id.");
        if (!id.Equals(folder, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Entity folder '{folder}' declares id '{id}'. Rename one so they agree.");

        string cls = Str(entity, "class")
            ?? EntityId.ClassFor(id)
            ?? throw new InvalidOperationException($"{path} has no class and '{id}' implies none.");
        ModPath.Segment(cls, $"the class declared by entity '{id}'");

        var db = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Table(doc, "db") is { } dbTable)
            foreach (var (key, value) in dbTable)
            {
                if (value is string row) db[key] = row;
                else
                    throw new InvalidOperationException(
                        $"{path} [db] value for '{key}' is not a string.");
            }

        string? motscr = null;
        if (Table(doc, "generate") is { } gen && gen.TryGetValue("motscr", out var m)
            && m is TomlTable motTable)
            motscr = Str(motTable, "template");

        return new SourceEntity(
            id, cls, Str(entity, "name") ?? id,
            Table(doc, "derive") is { } d ? Str(d, "from") : null,
            motscr, db, dir);
    }

    private static TomlTable? Table(TomlTable doc, string key) =>
        doc.TryGetValue(key, out var v) ? v as TomlTable : null;

    private static string? Str(TomlTable table, string key) =>
        table.TryGetValue(key, out var v) ? v as string : null;
}
