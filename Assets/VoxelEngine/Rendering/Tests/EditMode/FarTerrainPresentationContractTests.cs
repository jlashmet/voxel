using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarTerrainPresentationContractTests
    {
        [Test]
        public void FarTerrainShaderConsumesSharedMaterialRowsAndDeterministicVariation()
        {
            Shader shader = Shader.Find("VoxelEngine/FarTerrain");
            Assert.That(shader, Is.Not.Null);

            string assetPath = AssetDatabase.GetAssetPath(shader);
            Assert.That(assetPath, Is.Not.Empty);
            string source = File.ReadAllText(assetPath);

            StringAssert.Contains("_MaterialAlbedo[32]", source);
            StringAssert.Contains("_MaterialSampling[32]", source);
            StringAssert.Contains("_MaterialSurface[32]", source);
            StringAssert.Contains("_MaterialVariation[32]", source);
            StringAssert.Contains("ResolveMaterialRow", source);
            StringAssert.Contains("nointerpolation uint material", source);
            StringAssert.Contains("float fineNoise", source);
            StringAssert.Contains("float macroNoise", source);
            StringAssert.Contains("input.positionWS / max(_VoxelSize", source);
        }

        [Test]
        public void FarTerrainShaderFiltersWorldSpaceDetailWithoutGeometryDensityCoupling()
        {
            Shader shader = Shader.Find("VoxelEngine/FarTerrain");
            Assert.That(shader, Is.Not.Null);

            string assetPath = AssetDatabase.GetAssetPath(shader);
            string source = File.ReadAllText(assetPath);

            StringAssert.Contains("SpatialNoise", source);
            StringAssert.Contains("SpatialDetailFilter", source);
            StringAssert.Contains("ddx(scaled)", source);
            StringAssert.Contains("ddy(scaled)", source);
            StringAssert.Contains("detailFrequency", source);
            StringAssert.Contains("macroFrequency", source);
            StringAssert.Contains("detailNormalStrength", source);
            StringAssert.Contains("roughness = saturate(surface.z", source);
            StringAssert.Contains("detailFade", source);
            StringAssert.Contains("macroFade", source);

            // T003B must improve presentation in the fragment domain; it must not introduce a new
            // geometry-density/residency knob into the renderer contract.
            StringAssert.DoesNotContain("_FarVertexDensity", source);
            StringAssert.DoesNotContain("_FarResidencyRadius", source);
        }
    }
}
