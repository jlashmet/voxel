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
            StringAssert.Contains("_MaterialVariation[32]", source);
            StringAssert.Contains("ResolveMaterialRow", source);
            StringAssert.Contains("nointerpolation uint material", source);
            StringAssert.Contains("float fineNoise", source);
            StringAssert.Contains("float macroNoise", source);
            StringAssert.Contains("input.positionWS / max(_VoxelSize", source);
        }
    }
}
