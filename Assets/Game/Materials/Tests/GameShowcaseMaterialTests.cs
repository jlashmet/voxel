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
            Assert.That(roles.TerrainSubsurface, Is.EqualTo(GameMaterialIds.Stone));
            Assert.That(roles.TerrainLowSurface, Is.EqualTo(GameMaterialIds.Sand));
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

            Assert.That(roles.SurfaceAt(split - 1, split), Is.EqualTo(GameMaterialIds.Sand));
            Assert.That(roles.SurfaceAt(split, split), Is.EqualTo(GameMaterialIds.Grass));
            Assert.That(roles.SurfaceAt(split + 1, split), Is.EqualTo(GameMaterialIds.Grass));
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
