using ShadowForge.Formats.GLTF;
using ShadowForge.Formats.HDB;
using SharpGLTF.Schema2;
using Xunit.Abstractions;

namespace ShadowForge.Tests.GLTF;

public sealed class SkinnedExportFacts
{
    private readonly ITestOutputHelper _out;
    public SkinnedExportFacts(ITestOutputHelper o) => _out = o;

    [Fact]
    public void Bs01_ExportsWithSkin()
    {
        string src = Path.Combine("testdata", "models", "humanoid", "bs01_obj.hdb");
        Assert.True(File.Exists(src));

        var model = ModelCooker.Bake(ModelReader.Read(File.ReadAllBytes(src)));
        string outputPath = Path.GetTempFileName() + ".glb";
        try
        {
            SceneExporter.Export(model, outputPath);

            var gltf = ModelRoot.Load(outputPath);
            Assert.NotEmpty(gltf.LogicalMeshes);
            Assert.NotEmpty(gltf.LogicalSkins);

            var skin = gltf.LogicalSkins[0];
            Assert.Equal(model.Bones.Count, skin.JointsCount);

            bool sawJoints = false, sawWeights = false;
            foreach (var mesh in gltf.LogicalMeshes)
                foreach (var prim in mesh.Primitives)
                {
                    if (prim.GetVertexAccessor("JOINTS_0") != null) sawJoints = true;
                    if (prim.GetVertexAccessor("WEIGHTS_0") != null) sawWeights = true;
                }
            Assert.True(sawJoints, "expected JOINTS_0 on at least one primitive");
            Assert.True(sawWeights, "expected WEIGHTS_0 on at least one primitive");

            _out.WriteLine($"skins={gltf.LogicalSkins.Count}  joints={skin.JointsCount}  meshes={gltf.LogicalMeshes.Count}");
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void UnskinnedSphere_ExportsAsRigid()
    {
        string src = Path.Combine("testdata", "models", "simple", "sphere.hdb");
        Assert.True(File.Exists(src));

        var model = ModelCooker.Bake(ModelReader.Read(File.ReadAllBytes(src)));
        string outputPath = Path.GetTempFileName() + ".glb";
        try
        {
            SceneExporter.Export(model, outputPath);

            var gltf = ModelRoot.Load(outputPath);
            Assert.NotEmpty(gltf.LogicalMeshes);

            if (model.Bones.Count == 0)
            {
                Assert.Empty(gltf.LogicalSkins);
                foreach (var mesh in gltf.LogicalMeshes)
                    foreach (var prim in mesh.Primitives)
                    {
                        Assert.Null(prim.GetVertexAccessor("JOINTS_0"));
                        Assert.Null(prim.GetVertexAccessor("WEIGHTS_0"));
                    }
            }
            _out.WriteLine($"sphere bones={model.Bones.Count}  skins={gltf.LogicalSkins.Count}");
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }
}
