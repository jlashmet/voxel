using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;

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
        public void SimulationDefinitions_RegisterWithoutPaletteHoles()
        {
            MaterialDefinition[] definitions = GameMaterialSimulationDefinitions.Create();
            MaterialPalette palette = default;
            for (int i = 0; i < definitions.Length; i++)
                palette.Register(in definitions[i]);

            Assert.That(palette.Count, Is.EqualTo(GameMaterialCatalogue.Count));
            Assert.That(palette.IsRegistered(GameMaterialIds.Empty), Is.False);
            for (byte materialId = 1; materialId < GameMaterialCatalogue.Count; materialId++)
                Assert.That(palette.IsRegistered(materialId), Is.True,
                    $"Palette hole at {GameMaterialCatalogue.NameOf(materialId)} ({materialId}).");

            Assert.That(palette.IsFlammable(GameMaterialIds.Wood), Is.True);
            Assert.That(palette.IsFlammable(GameMaterialIds.Cloth), Is.True);
            Assert.That(palette.GetDestructionClass(GameMaterialIds.Water),
                Is.EqualTo(DestructionClass.Spreading));
        }

        [Test]
        public void PreviouslyImplicitRows_AreExplicitButBehaviorPreserving()
        {
            MaterialDefinition[] definitions = GameMaterialSimulationDefinitions.Create();
            MaterialPalette palette = default;
            for (int i = 0; i < definitions.Length; i++)
                palette.Register(in definitions[i]);

            byte[] compatibilityRows =
            {
                GameMaterialIds.Cascade,
                GameMaterialIds.Crystal,
                GameMaterialIds.FlowerWhite,
            };

            for (int i = 0; i < compatibilityRows.Length; i++)
            {
                byte materialId = compatibilityRows[i];
                Assert.That(palette.IsRegistered(materialId), Is.True);
                Assert.That(palette.GetHardness(materialId), Is.EqualTo(0));
                Assert.That(palette.GetDestructionClass(materialId), Is.EqualTo(DestructionClass.None));
                Assert.That(palette.IsFlammable(materialId), Is.False);
            }
        }
    }
}
