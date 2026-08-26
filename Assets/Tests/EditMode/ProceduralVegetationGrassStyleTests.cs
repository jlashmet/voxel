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
        private const string InteractorRegistryPath =
            "Assets/VoxelEngine/Rendering/Runtime/Vegetation/GrassInteractorRegistry.cs";
        private const string CharacterPublisherPath =
            "Assets/Game/Composition/CharacterEquipment/Runtime/CharacterEquipmentController.cs";
        private const string CharacterAssemblyPath =
            "Assets/Game/Composition/CharacterEquipment/Runtime/Game.Composition.CharacterEquipment.Runtime.asmdef";

        [Test]
        public void FoliageShaderImplementsAuthoredGrassMotionAndToonVariationContract()
        {
            string shader = File.ReadAllText(ProjectPath(FoliageShaderPath));
            string bridge = File.ReadAllText(ProjectPath(MaterialBridgePath));
            string registry = File.ReadAllText(ProjectPath(InteractorRegistryPath));
            string characterPublisher = File.ReadAllText(ProjectPath(CharacterPublisherPath));
            string characterAssembly = File.ReadAllText(ProjectPath(CharacterAssemblyPath));

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
            StringAssert.Contains("SetGrassInteractors(s_Published)", registry,
                "The live interactor registry must publish sampled character positions into the vegetation bridge.");
            StringAssert.Contains("Time.frameCount", registry,
                "The interactor registry should sample the shared character set at most once per rendered frame.");
            StringAssert.Contains("GrassInteractorRegistry.Register(transform", characterPublisher,
                "Standard character roots must register themselves as live grass interactors.");
            StringAssert.Contains("GrassInteractorRegistry.Publish()", characterPublisher,
                "Standard character roots must drive the bounded interactor publication every frame.");
            StringAssert.Contains("GrassInteractorRegistry.Unregister(transform", characterPublisher,
                "Disabled character roots must stop displacing grass.");
            StringAssert.Contains("VoxelEngine.Rendering.Runtime", characterAssembly,
                "Character composition must depend one-way on the rendering API instead of making VoxelEngine depend on game code.");
        }

        private static string ProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
