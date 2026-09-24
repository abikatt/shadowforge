using ShadowForge.Formats.GLTF;
using ShadowForge.Formats.HDB;

namespace ShadowForge.Tests.GLTF;

public sealed class ModelExporterTests
{
    private const string HDB = "testdata/models/humanoid/bs01_obj.hdb";

    [Fact]
    public void Export_MatchesDirectExporter_ByteForByte()
    {
        byte[] hdb = File.ReadAllBytes(HDB);
        string viaHelper = Path.Combine(Path.GetTempPath(), "sf_h_" + Guid.NewGuid().ToString("N") + ".glb");
        string viaDirect = Path.Combine(Path.GetTempPath(), "sf_d_" + Guid.NewGuid().ToString("N") + ".glb");
        try
        {
            ModelExporter.Export(hdb, viaHelper);
            SceneExporter.Export(ModelCooker.Bake(ModelReader.Read(hdb)), viaDirect);
            Assert.Equal(File.ReadAllBytes(viaDirect), File.ReadAllBytes(viaHelper));
        }
        finally
        {
            if (File.Exists(viaHelper)) File.Delete(viaHelper);
            if (File.Exists(viaDirect)) File.Delete(viaDirect);
        }
    }
}
