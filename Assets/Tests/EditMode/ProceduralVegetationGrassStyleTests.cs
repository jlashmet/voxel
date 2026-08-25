using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ProceduralVegetationGrassStyleTests
    {
        private const string FoliageShaderPath =
            "Assets/VoxelEngine/Rendering/Runtime/Shaders/ProceduralVegetationFoliage.shader";
        private const string MaterialBridgePath =
            "Assets/VoxelEngine/Rendering/Runtime/Vegetation/ProceduralVegetationMaterials.cs";

        [Test]
        public void FoliageShaderImplementsAuthoredGrassMotionAndToonVariationContract()
        {
            string shader = File.ReadAllText(ProjectPath(FoliageShaderPath));
            string bridge = File.ReadAllText(ProjectPath(MaterialBridgePath));

            StringAssert.Contains("QuantizedAnimationTime", shader,
                "Grass motion must use deliberately stepped animation time rather than continuous synchronized sine sway.");
            StringAssert.Contains("WorldNoise", shader,
                "Grass must use world-space noise for wind and colour variation.");
            StringAssert.Contains("SecondaryWindNoise", shader,
                "Grass wind must combine offset noise samples so large patches do not repeat in lockstep.");
            StringAssert.Contains("InstanceVariation", shader,
                "Each instanced plant needs a stable per-instance phase/colour variation derived in the shader.");
            StringAssert.Contains("ViewSway", shader,
                "Grass needs a small view-space sway term in addition to world-space wind.");
            StringAssert.Contains("HybridToonLight", shader,
                "Grass lighting must use softened toon bands instead of one smooth Lambert ramp.");
            StringAssert.Contains("ApplyCharacterDisplacement", shader,
                "Nearby characters must bend grass away rather than pass through an entirely static field.");
            StringAssert.Contains("_GrassInteractorPositions", shader,
                "The shader must consume the fixed grass-interactor array used for character displacement.");
            StringAssert.Contains("MaxGrassInteractors = 64", bridge,
                "The vegetation material bridge must cap the fixed shader interactor array at 64 entries.");
        }

        private static string ProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
