using Game.Materials.Api;
using NUnit.Framework;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MountainDragonPalettePolicyTests
    {
        [Test]
        public void UnmaterialedSource_MapsToCanonicalDarkStone()
        {
            Assert.That(MountainDragonPalettePolicy.MapSourceMaterial(GameMaterialIds.Empty),
                Is.EqualTo(GameMaterialIds.DarkStone));
        }

        [Test]
        public void ExistingSourceMaterial_IsPreservedByCompositionPolicy()
        {
            Assert.That(MountainDragonPalettePolicy.MapSourceMaterial(GameMaterialIds.Gold),
                Is.EqualTo(GameMaterialIds.Gold));
        }

        [Test]
        public void AuthoringSettings_UseCompositionOwnedDragonMaterial()
        {
            MeshVoxelizationSettings settings = MountainDragonAuthoringPolicy.CreateVoxelizationSettings();
            Assert.That(settings.FallbackMaterial, Is.EqualTo(GameMaterialIds.DarkStone));
            Assert.That(settings.VoxelSize, Is.EqualTo(MountainDragonVoxelBakePolicy.SourceVoxelSize));
        }
    }
}
