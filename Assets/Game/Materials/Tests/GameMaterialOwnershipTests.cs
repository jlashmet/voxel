using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;

namespace Game.Materials.Tests
{
    public sealed class GameMaterialOwnershipTests
    {
        [Test]
        public void StableMaterialIds_AreFrozenDuringOwnershipMigration()
        {
            Assert.That(GameMaterialIds.Empty, Is.EqualTo(0));
            Assert.That(GameMaterialIds.Stone, Is.EqualTo(1));
            Assert.That(GameMaterialIds.Wood, Is.EqualTo(2));
            Assert.That(GameMaterialIds.Sand, Is.EqualTo(3));
            Assert.That(GameMaterialIds.Glass, Is.EqualTo(4));
            Assert.That(GameMaterialIds.Bedrock, Is.EqualTo(5));
            Assert.That(GameMaterialIds.DarkStone, Is.EqualTo(6));
            Assert.That(GameMaterialIds.Slate, Is.EqualTo(7));
            Assert.That(GameMaterialIds.Tile, Is.EqualTo(8));
            Assert.That(GameMaterialIds.Cloth, Is.EqualTo(9));
            Assert.That(GameMaterialIds.Grass, Is.EqualTo(10));
            Assert.That(GameMaterialIds.Water, Is.EqualTo(11));
            Assert.That(GameMaterialIds.Gold, Is.EqualTo(12));
            Assert.That(GameMaterialIds.Dirt, Is.EqualTo(13));
            Assert.That(GameMaterialIds.Moss, Is.EqualTo(14));
            Assert.That(GameMaterialIds.LitWindow, Is.EqualTo(15));
            Assert.That(GameMaterialIds.Cascade, Is.EqualTo(16));
            Assert.That(GameMaterialIds.Crystal, Is.EqualTo(17));
            Assert.That(GameMaterialIds.MasonrySmall, Is.EqualTo(18));
            Assert.That(GameMaterialIds.MasonryMedium, Is.EqualTo(19));
            Assert.That(GameMaterialIds.MasonryLarge, Is.EqualTo(20));
            Assert.That(GameMaterialIds.FlowerWhite, Is.EqualTo(21));
        }

        [Test]
        public void CanonicalCatalogue_CoversEveryStableMaterialId()
        {
            Assert.That(GameMaterialCatalogue.Count, Is.EqualTo(22));
            for (byte materialId = 0; materialId < GameMaterialCatalogue.Count; materialId++)
            {
                Assert.That(GameMaterialCatalogue.IsCanonicalId(materialId), Is.True);
                Assert.That(GameMaterialCatalogue.NameOf(materialId), Is.Not.Empty);
                Assert.That(GameMaterialCatalogue.NameOf(materialId), Is.Not.EqualTo("unknown"));
            }

            byte firstUnknownId = (byte)GameMaterialCatalogue.Count;
            Assert.That(GameMaterialCatalogue.IsCanonicalId(firstUnknownId), Is.False);
            Assert.That(GameMaterialCatalogue.NameOf(firstUnknownId), Is.EqualTo("unknown"));
        }

        [Test]
        public void SimulationProjection_RegistersEveryPhysicalMaterialWithoutChangingLegacyBehavior()
        {
            MaterialDefinition[] definitions = GameMaterialSimulationDefinitions.Create();
            Assert.That(definitions.Length, Is.EqualTo(GameMaterialSimulationDefinitions.Count));
            Assert.That(definitions.Length, Is.EqualTo(GameMaterialCatalogue.Count - 1));

            MaterialPalette palette = default;
            for (int i = 0; i < definitions.Length; i++)
            {
                MaterialDefinition definition = definitions[i];
                Assert.That(definition.MaterialId, Is.EqualTo(i + 1),
                    "Physical definitions must remain ordered by their stable material id.");
                palette.Register(in definition);
            }

            Assert.That(palette.Count, Is.EqualTo(GameMaterialCatalogue.Count));
            Assert.That(palette.IsRegistered(GameMaterialIds.Empty), Is.False,
                "Empty is semantic absence, not a physical material registration.");

            for (byte materialId = 1; materialId < GameMaterialCatalogue.Count; materialId++)
            {
                MaterialDefinition definition = definitions[materialId - 1];
                Assert.That(palette.IsRegistered(materialId), Is.True,
                    $"Material {materialId} ({GameMaterialCatalogue.NameOf(materialId)}) was not registered.");
                Assert.That(palette.GetHardness(materialId), Is.EqualTo(definition.Hardness));
                Assert.That(palette.GetDestructionClass(materialId), Is.EqualTo(definition.DestructionClass));
                Assert.That(palette.IsFlammable(materialId), Is.EqualTo(definition.Flammable));
            }
        }

        [Test]
        public void PreviouslyUnregisteredRows_AreExplicitButRemainInertDuringOwnershipMigration()
        {
            MaterialDefinition[] definitions = GameMaterialSimulationDefinitions.Create();
            MaterialDefinition cascade = definitions[GameMaterialIds.Cascade - 1];
            MaterialDefinition crystal = definitions[GameMaterialIds.Crystal - 1];
            MaterialDefinition flower = definitions[GameMaterialIds.FlowerWhite - 1];

            Assert.That(cascade.MaterialId, Is.EqualTo(GameMaterialIds.Cascade));
            Assert.That(cascade.Hardness, Is.Zero);
            Assert.That(cascade.DestructionClass, Is.EqualTo(DestructionClass.None));
            Assert.That(crystal.MaterialId, Is.EqualTo(GameMaterialIds.Crystal));
            Assert.That(crystal.Hardness, Is.Zero);
            Assert.That(crystal.DestructionClass, Is.EqualTo(DestructionClass.None));
            Assert.That(flower.MaterialId, Is.EqualTo(GameMaterialIds.FlowerWhite));
            Assert.That(flower.Hardness, Is.Zero);
            Assert.That(flower.DestructionClass, Is.EqualTo(DestructionClass.None));
        }

        [Test]
        public void BuildableMaterials_AreOwnedByGameCatalogueInStableHotkeyOrder()
        {
            Assert.That(GameMaterialCatalogue.BuildableCount, Is.EqualTo(4));
            Assert.That(GameMaterialCatalogue.BuildableAt(0), Is.EqualTo(GameMaterialIds.Stone));
            Assert.That(GameMaterialCatalogue.BuildableAt(1), Is.EqualTo(GameMaterialIds.Wood));
            Assert.That(GameMaterialCatalogue.BuildableAt(2), Is.EqualTo(GameMaterialIds.Sand));
            Assert.That(GameMaterialCatalogue.BuildableAt(3), Is.EqualTo(GameMaterialIds.Glass));
        }

        [Test]
        public void TransitionalAliases_RemainExplicitAndNumericallyStable()
        {
            Assert.That(GameMaterialIds.TerrainTurf, Is.EqualTo(GameMaterialIds.Grass));
            Assert.That(GameMaterialIds.TerrainLimestone, Is.EqualTo(GameMaterialIds.MasonryMedium));
            Assert.That(GameMaterialIds.TerrainEarth, Is.EqualTo(GameMaterialIds.Dirt));
            Assert.That(GameMaterialIds.TerrainPathStone, Is.EqualTo(GameMaterialIds.MasonrySmall));
            Assert.That(GameMaterialIds.FlowerYellow, Is.EqualTo(GameMaterialIds.Gold));
            Assert.That(GameMaterialIds.FlowerPink, Is.EqualTo(GameMaterialIds.Cloth));
            Assert.That(GameMaterialIds.FlowerBlue, Is.EqualTo(GameMaterialIds.Cascade));
        }

        [Test]
        public void DefaultTerrainMaterials_MapGameSemanticsToOpaqueEngineSlots()
        {
            Assert.That(GameTerrainMaterials.Default.Deep, Is.EqualTo(GameMaterialIds.Bedrock));
            Assert.That(GameTerrainMaterials.Default.Subsurface, Is.EqualTo(GameMaterialIds.Stone));
            Assert.That(GameTerrainMaterials.Default.Surface, Is.EqualTo(GameMaterialIds.Sand));
        }

        [Test]
        public void LegacyStructurePalette_RemainsNumericallyAlignedDuringMigration()
        {
            Assert.That(Mat.Empty, Is.EqualTo(GameMaterialIds.Empty));
            Assert.That(Mat.Stone, Is.EqualTo(GameMaterialIds.Stone));
            Assert.That(Mat.Wood, Is.EqualTo(GameMaterialIds.Wood));
            Assert.That(Mat.Sand, Is.EqualTo(GameMaterialIds.Sand));
            Assert.That(Mat.Glass, Is.EqualTo(GameMaterialIds.Glass));
            Assert.That(Mat.Bedrock, Is.EqualTo(GameMaterialIds.Bedrock));
            Assert.That(Mat.DarkStone, Is.EqualTo(GameMaterialIds.DarkStone));
            Assert.That(Mat.Slate, Is.EqualTo(GameMaterialIds.Slate));
            Assert.That(Mat.Tile, Is.EqualTo(GameMaterialIds.Tile));
            Assert.That(Mat.Cloth, Is.EqualTo(GameMaterialIds.Cloth));
            Assert.That(Mat.Grass, Is.EqualTo(GameMaterialIds.Grass));
            Assert.That(Mat.Water, Is.EqualTo(GameMaterialIds.Water));
            Assert.That(Mat.Gold, Is.EqualTo(GameMaterialIds.Gold));
            Assert.That(Mat.Dirt, Is.EqualTo(GameMaterialIds.Dirt));
            Assert.That(Mat.Moss, Is.EqualTo(GameMaterialIds.Moss));
            Assert.That(Mat.LitWindow, Is.EqualTo(GameMaterialIds.LitWindow));
            Assert.That(Mat.Cascade, Is.EqualTo(GameMaterialIds.Cascade));
            Assert.That(Mat.Crystal, Is.EqualTo(GameMaterialIds.Crystal));
            Assert.That(Mat.MasonrySmall, Is.EqualTo(GameMaterialIds.MasonrySmall));
            Assert.That(Mat.MasonryMedium, Is.EqualTo(GameMaterialIds.MasonryMedium));
            Assert.That(Mat.MasonryLarge, Is.EqualTo(GameMaterialIds.MasonryLarge));
            Assert.That(Mat.FlowerWhite, Is.EqualTo(GameMaterialIds.FlowerWhite));
        }
    }
}
