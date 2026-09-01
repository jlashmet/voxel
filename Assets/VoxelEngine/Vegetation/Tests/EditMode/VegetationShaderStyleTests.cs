using NUnit.Framework;
using VoxelEngine.AmbientLife.Api;
using VoxelEngine.Vegetation.Api;
using VoxelEngine.Rendering.Runtime.AmbientLife;
using VoxelEngine.Rendering.Runtime.Vegetation;

namespace VoxelEngine.Tests.EditMode
{
    public class VegetationShaderStyleTests
    {
        [Test]
        public void Vegetation_MapsGrowthFormsToSmallShaderSet()
        {
            Assert.AreEqual(VegetationShaderClass.Foliage,
                ProceduralVegetationMaterials.StyleFor(VegetationKind.Grass).ShaderClass);
            Assert.AreEqual(VegetationShaderClass.Surface,
                ProceduralVegetationMaterials.StyleFor(VegetationKind.Moss).ShaderClass);
            Assert.AreEqual(VegetationShaderClass.Vine,
                ProceduralVegetationMaterials.StyleFor(VegetationKind.ArcaneVine).ShaderClass);
            Assert.AreEqual(VegetationShaderClass.Woody,
                ProceduralVegetationMaterials.StyleFor(VegetationKind.FallenLog).ShaderClass);
        }

        [Test]
        public void MagicalVegetation_UsesEmission()
        {
            Assert.Greater(ProceduralVegetationMaterials.StyleFor(VegetationKind.Glowshroom).EmissionStrength, 0f);
            Assert.Greater(ProceduralVegetationMaterials.StyleFor(VegetationKind.StarMoss).EmissionStrength, 0f);
            Assert.Greater(ProceduralVegetationMaterials.StyleFor(VegetationKind.ArcaneVine).EmissionStrength, 0f);
        }

        [Test]
        public void AmbientLife_UsesSharedShaderWithSemanticShapes()
        {
            Assert.AreEqual(AmbientVisualShape.Mote,
                ProceduralAmbientLifeMaterials.StyleFor(AmbientLifeKind.Firefly).Shape);
            Assert.AreEqual(AmbientVisualShape.Butterfly,
                ProceduralAmbientLifeMaterials.StyleFor(AmbientLifeKind.Butterfly).Shape);
            Assert.AreEqual(AmbientVisualShape.Dragonfly,
                ProceduralAmbientLifeMaterials.StyleFor(AmbientLifeKind.Dragonfly).Shape);
            Assert.AreEqual(AmbientVisualShape.Wisp,
                ProceduralAmbientLifeMaterials.StyleFor(AmbientLifeKind.Wisp).Shape);
        }

        [Test]
        public void LuminousAmbientLife_UsesHdrEmission()
        {
            Assert.Greater(ProceduralAmbientLifeMaterials.StyleFor(AmbientLifeKind.Firefly).EmissionStrength, 1f);
            Assert.Greater(ProceduralAmbientLifeMaterials.StyleFor(AmbientLifeKind.Wisp).EmissionStrength, 1f);
            Assert.AreEqual(0f, ProceduralAmbientLifeMaterials.StyleFor(AmbientLifeKind.Butterfly).EmissionStrength);
        }
    }
}
