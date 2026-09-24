using System.Text.Json;
using ShadowForge.Formats.GLTF;
using ShadowForge.Formats.HDB;
using static ShadowForge.Tests.CLI.EntityCommandRunner;

namespace ShadowForge.Tests.CLI;

public sealed class EntityOverlayCookTests
{
    private const string HDB = "testdata/models/humanoid/bs01_obj.hdb";
    private const string MPK = "testdata/models/humanoid/bs01_mot.mpk";

    private static string NewTempDir() => Directory.CreateDirectory(MissingPath("sf_ovlcook_")).FullName;

    [Fact]
    public void Import_EntityResolvedOnlyThroughOverlay_CooksWithoutFileNotFound()
    {
        string gameRoot = NewTempDir();
        Directory.CreateDirectory(Path.Combine(gameRoot, "chara"));

        string overlay = NewTempDir();
        string mdlDir = Path.Combine(overlay, "database", "model", "chara", "ply");
        Directory.CreateDirectory(mdlDir);
        File.WriteAllText(Path.Combine(mdlDir, "model_pc11.mdl"),
            "PATH\t\"chara\\ply\\pc11\\\"\nOBJECT\t\"pc11_obj.hdb\"\nMOTPACK\t\"pc11_mot.mpk\"\n");

        string rigDir = Path.Combine(overlay, "chara", "ply", "pc11");
        Directory.CreateDirectory(rigDir);
        File.Copy(HDB, Path.Combine(rigDir, "pc11_obj.hdb"));
        File.Copy(MPK, Path.Combine(rigDir, "pc11_mot.mpk"));

        string fromDir = NewTempDir();
        string glbPath = Path.Combine(fromDir, "pc11.glb");
        SceneExporter.Export(ModelCooker.Bake(ModelReader.Read(HDB)), glbPath, textureDir: null, embed: true, clips: null);

        var (code, output) = Run(
            "import", "pc11", "--from", fromDir, "--game-root", gameRoot, "--overlay", overlay, "--json");

        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean(), output);
        Assert.Equal(0, code);
    }
}
