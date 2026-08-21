using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using VoxelEngine.Composition.Api;

namespace Game.Materials.Tests
{
    public sealed class GameShowcaseMaterialTests
    {
        [Test]
        public void DefaultRoles_MapEveryShowcaseRoleToGameOwnedMaterialIdentity()
        {
            ShowcaseMaterialSet roles = GameShowcaseMaterials.Default;

            Assert.That(roles.TerrainDeep, Is.EqualTo(GameMaterialIds.Bedrock));
            Assert.That(roles.TerrainSubsurface, Is.EqualTo(GameMaterialIds.Dirt));
            Assert.That(roles.TerrainLowSurface, Is.EqualTo(GameMaterialIds.Dirt));
            Assert.That(roles.TerrainHighSurface, Is.EqualTo(GameMaterialIds.Grass));
            Assert.That(roles.Gate, Is.EqualTo(GameMaterialIds.Wood));
            Assert.That(roles.ReferenceArch, Is.EqualTo(GameMaterialIds.DarkStone));
            Assert.That(roles.FarStructure, Is.EqualTo(GameMaterialIds.Stone));

            Assert.That(roles.WorldgenFoundation, Is.EqualTo(GameMaterialIds.Stone));
            Assert.That(roles.WorldgenMasonry, Is.EqualTo(GameMaterialIds.Stone));
            Assert.That(roles.WorldgenDarkMasonry, Is.EqualTo(GameMaterialIds.DarkStone));
            Assert.That(roles.WorldgenTimber, Is.EqualTo(GameMaterialIds.Wood));
            Assert.That(roles.WorldgenGlass, Is.EqualTo(GameMaterialIds.Glass));
            Assert.That(roles.WorldgenWarmWindow, Is.EqualTo(GameMaterialIds.LitWindow));
            Assert.That(roles.WorldgenRoofTile, Is.EqualTo(GameMaterialIds.Tile));
            Assert.That(roles.WorldgenSlate, Is.EqualTo(GameMaterialIds.Slate));
            Assert.That(roles.WorldgenCloth, Is.EqualTo(GameMaterialIds.Cloth));
            Assert.That(roles.WorldgenMoss, Is.EqualTo(GameMaterialIds.Moss));
            Assert.That(roles.WorldgenWater, Is.EqualTo(GameMaterialIds.Water));
            Assert.That(roles.WorldgenRoadSurface, Is.EqualTo(GameMaterialIds.Dirt));
        }

        [Test]
        public void SurfaceRoleSelection_IsSemanticFreeAndHeightDriven()
        {
            ShowcaseMaterialSet roles = GameShowcaseMaterials.Default;
            const int split = 220;

            Assert.That(roles.SurfaceAt(split - 1, split), Is.EqualTo(GameMaterialIds.Dirt));
            Assert.That(roles.SurfaceAt(split, split), Is.EqualTo(GameMaterialIds.Grass));
            Assert.That(roles.SurfaceAt(split + 1, split), Is.EqualTo(GameMaterialIds.Grass));
        }

        /// <summary>
        /// The distant analytic mesh and the near voxel surface pick ground cover independently,
        /// from two separately authored material sets. Nothing forces them to agree, and when they
        /// disagreed the world drew a hard material seam in a ring at the streaming radius — near
        /// ground in sand and stone, horizon in grass. Neither set is wrong on its own, which is
        /// why this has to be asserted across both rather than inside either.
        /// </summary>
        [Test]
        public void NearAndFarTerrainAgreeOnGroundCover()
        {
            ShowcaseMaterialSet far = GameShowcaseMaterials.Default;
            VoxelEngine.Terrain.Api.TerrainMaterialSet near = GameTerrainMaterials.Default;
            const int split = 220;

            Assert.That(near.Deep, Is.EqualTo(far.TerrainDeep));
            Assert.That(near.Subsurface, Is.EqualTo(far.TerrainSubsurface));

            for (int height = split - 24; height <= split + 24; height++)
                Assert.That(
                    near.SurfaceAt(height, split),
                    Is.EqualTo(far.SurfaceAt(height, split)),
                    "Ground at height " + height + " is one material up close and another at " +
                    "distance, so the two representations meet at a visible seam.");
        }

        [Test]
        public void StructuralClassification_IsOwnedByGameBinding()
        {
            ShowcaseMaterialSet roles = GameShowcaseMaterials.Default;

            byte[] structural =
            {
                GameMaterialIds.Wood,
                GameMaterialIds.Glass,
                GameMaterialIds.DarkStone,
                GameMaterialIds.Slate,
                GameMaterialIds.Tile,
                GameMaterialIds.Cloth,
                GameMaterialIds.Gold,
                GameMaterialIds.LitWindow,
            };

            for (int i = 0; i < structural.Length; i++)
                Assert.That(roles.IsStructural(structural[i]), Is.True,
                    $"Expected {GameMaterialCatalogue.NameOf(structural[i])} to be structural.");

            byte[] nonStructural =
            {
                GameMaterialIds.Empty,
                GameMaterialIds.Sand,
                GameMaterialIds.Bedrock,
                GameMaterialIds.Grass,
                GameMaterialIds.Water,
                GameMaterialIds.Dirt,
            };

            for (int i = 0; i < nonStructural.Length; i++)
                Assert.That(roles.IsStructural(nonStructural[i]), Is.False,
                    $"Expected {GameMaterialCatalogue.NameOf(nonStructural[i])} to be non-structural.");
        }
    }
}
