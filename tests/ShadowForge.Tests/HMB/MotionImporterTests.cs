using ShadowForge.Formats.GLTF;
using ShadowForge.Formats.HDB;
using ShadowForge.Formats.HMB;
using ShadowForge.Formats.HMB.Import;

namespace ShadowForge.Tests.HMB;

public sealed class MotionImporterTests
{
    private const string HDB = "testdata/models/humanoid/bs01_obj.hdb";
    private const string HMB = "testdata/anim/bs01_bt_wt01.hmb";

    [Fact]
    public void Import_FromExportedGlb_RoundTripsClipIntoMPK()
    {
        byte[] hdbBytes = File.ReadAllBytes(HDB);
        var clip = MotionReader.Read(File.ReadAllBytes(HMB), "bs01_bt_wt01");

        string glb = Path.Combine(Path.GetTempPath(), "sf_moti_" + Guid.NewGuid().ToString("N") + ".glb");
        SceneExporter.Export(ModelCooker.Bake(ModelReader.Read(hdbBytes)), glb, textureDir: null, embed: true, clips: new[] { clip });
        try
        {
            var result = MotionImporter.Import(glb, hdbBytes, templateMPK: null);

            Assert.NotEmpty(result.MPKBytes);

            string mpk = Path.ChangeExtension(glb, ".mpk");
            File.WriteAllBytes(mpk, result.MPKBytes);
            try
            {
                var clips = MotPack.ReadAll(mpk, out _);
                Assert.Contains(clips, c => c.Name == "bs01_bt_wt01");
            }
            finally { File.Delete(mpk); }
        }
        finally { if (File.Exists(glb)) File.Delete(glb); }
    }
}
