using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ProceduralVegetationMaterialTests
    {
        [Test]
        public void IvyUsesClimberVineShaderClass()
        {
            Assert.That(VegetationCatalogue.Get(VegetationKind.Ivy).GrowthForm,
                Is.EqualTo(VegetationGrowthForm.Climber));
            Assert.That(ProceduralVegetationMaterials.StyleFor(VegetationKind.Ivy).ShaderClass,
                Is.EqualTo(VegetationShaderClass.Vine));
        }
    }
}
