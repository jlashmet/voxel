using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using VoxelEngine.Storage.Api;

namespace Game.Materials.Tests
{
    public sealed class GameMaterialSimulationTests
    {
        [Test]
        public void SimulationDefinitions_CoverEveryCanonicalNonEmptyIdExactlyOnce()
        {
            MaterialDefinition[] definitions = GameMaterialSimulationDefinitions.Create();
            Assert.That(definitions.Length, Is.EqualTo(GameMaterialSimulationDefinitions.Count));

            var seen = new bool[GameMaterialCatalogue.Count];
            for (int i = 0; i < definitions.Length; i++)
            {
                byte materialId = definitions[i].MaterialId;
                Assert.That(materialId, Is.GreaterThan(GameMaterialIds.Empty));
                Assert.That(materialId, Is.LessThan(GameMaterialCatalogue.Count));
                Assert.That(seen[materialId], Is.False,
                    $"Material {GameMaterialCatalogue.NameOf(materialId)} ({materialId}) is defined twice.");
                seen[materialId] = true;
            }

            for (byte materialId = 1; materialId < GameMaterialCatalogue.Count; materialId++)
                Assert.That(seen[materialId], Is.True,
                    $"Missing simulation definition for {GameMaterialCatalogue.NameOf(materialId)} ({materialId}).");
        }

        [Test]
        public void SimulationDefinitions_PreserveExpectedGameBehaviorProperties()
        {
            MaterialDefinition[] definitions = GameMaterialSimulationDefinitions.Create();

            Assert.That(Find(definitions, GameMaterialIds.Wood).Flammable, Is.True);
            Assert.That(Find(definitions, GameMaterialIds.Cloth).Flammable, Is.True);
            Assert.That(Find(definitions, GameMaterialIds.Water).DestructionClass,
                Is.EqualTo(DestructionClass.Spreading));
            Assert.That(Find(definitions, GameMaterialIds.Bedrock).DestructionClass,
                Is.EqualTo(DestructionClass.None));
        }

        [Test]
        public void StructurePlacementBehavior_IsAuthoredByGameMaterialRows()
        {
            MaterialDefinition[] definitions = GameMaterialSimulationDefinitions.Create();

            Assert.That(Find(definitions, GameMaterialIds.Stone).PlacementSurfaceStyle,
                Is.EqualTo(SurfaceStyles.Planar));
            Assert.That(Find(definitions, GameMaterialIds.Glass).PlacementSurfaceStyle,
                Is.EqualTo(SurfaceStyles.Planar));
            Assert.That(Find(definitions, GameMaterialIds.Sand).PlacementSurfaceStyle,
                Is.EqualTo(SurfaceStyles.MaterialDefault));
            Assert.That(Find(definitions, GameMaterialIds.Grass).PlacementSurfaceStyle,
                Is.EqualTo(SurfaceStyles.MaterialDefault));
            Assert.That(Find(definitions, GameMaterialIds.Dirt).PlacementSurfaceStyle,
                Is.EqualTo(SurfaceStyles.MaterialDefault));
            Assert.That(Find(definitions, GameMaterialIds.Water).PlacementSurfaceStyle,
                Is.EqualTo(SurfaceStyles.MaterialDefault));

            Assert.That(Find(definitions, GameMaterialIds.Moss).PlacementSurfaceStyle,
                Is.EqualTo(SurfaceStyles.MaterialDefault));
            Assert.That(Find(definitions, GameMaterialIds.Moss).PlacementCoating,
                Is.EqualTo(Coatings.Moss));
            Assert.That(Find(definitions, GameMaterialIds.Stone).PlacementCoating,
                Is.EqualTo(Coatings.None));
        }

        [Test]
        public void PreviouslyImplicitRows_AreExplicitButBehaviorPreserving()
        {
            MaterialDefinition[] definitions = GameMaterialSimulationDefinitions.Create();
            byte[] compatibilityRows =
            {
                GameMaterialIds.Cascade,
                GameMaterialIds.Crystal,
                GameMaterialIds.FlowerWhite,
            };

            for (int i = 0; i < compatibilityRows.Length; i++)
            {
                MaterialDefinition definition = Find(definitions, compatibilityRows[i]);
                Assert.That(definition.Hardness, Is.EqualTo(0));
                Assert.That(definition.DestructionClass, Is.EqualTo(DestructionClass.None));
                Assert.That(definition.Flammable, Is.False);
            }
        }

        private static MaterialDefinition Find(MaterialDefinition[] definitions, byte materialId)
        {
            for (int i = 0; i < definitions.Length; i++)
                if (definitions[i].MaterialId == materialId)
                    return definitions[i];

            Assert.Fail($"Missing simulation definition for material {materialId}.");
            return default;
        }
    }
}
