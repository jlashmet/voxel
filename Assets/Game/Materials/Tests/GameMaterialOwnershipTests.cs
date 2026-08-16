using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using VoxelEngine.Storage.Api;

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
        public void SimulationProjection_CoversEveryPhysicalMaterialWithoutChangingStableOrdering()
        {
            MaterialDefinition[] definitions = GameMaterialSimulationDefinitions.Create();
            Assert.That(definitions.Length, Is.EqualTo(GameMaterialSimulationDefinitions.Count));
            Assert.That(definitions.Length, Is.EqualTo(GameMaterialCatalogue.Count - 1));

            for (int i = 0; i < definitions.Length; i++)
            {
                MaterialDefinition definition = definitions[i];
                byte expectedId = (byte)(i + 1);
                Assert.That(definition.MaterialId, Is.EqualTo(expectedId),
                    "Physical definitions must remain ordered by their stable material id.");

                ref readonly GameMaterialRuntimeDefinition authored =
                    ref GameMaterialRuntimeCatalogue.Get(expectedId);
                Assert.That(authored.HasSimulation, Is.True,
                    $"Material {GameMaterialCatalogue.NameOf(expectedId)} has no simulation projection.");
                Assert.That(definition.Hardness, Is.EqualTo(authored.Simulation.Hardness));
                Assert.That(definition.DestructionClass,
                    Is.EqualTo(authored.Simulation.DestructionClass));
                Assert.That(definition.Flammable, Is.EqualTo(authored.Simulation.Flammable));
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
    }
}
