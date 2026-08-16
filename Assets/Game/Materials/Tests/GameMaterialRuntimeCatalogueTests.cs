using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Storage.Api;

namespace Game.Materials.Tests
{
    public sealed class GameMaterialRuntimeCatalogueTests
    {
        [Test]
        public void EveryRuntimeRow_UsesOneCanonicalIdentityAcrossProjections()
        {
            Assert.That(GameMaterialRuntimeCatalogue.Count, Is.EqualTo(GameMaterialCatalogue.Count));

            for (byte materialId = 0; materialId < GameMaterialRuntimeCatalogue.Count; materialId++)
            {
                ref readonly GameMaterialRuntimeDefinition row =
                    ref GameMaterialRuntimeCatalogue.Get(materialId);

                Assert.That(row.MaterialId, Is.EqualTo(materialId));
                Assert.That(row.Rendering.MaterialIndex, Is.EqualTo(materialId));
                Assert.That(row.Simulation.MaterialId, Is.EqualTo(materialId));
                Assert.That(GameMaterialCatalogue.IsCanonicalId(materialId), Is.True);
            }
        }

        [Test]
        public void CompiledSubsystemViews_AreExactProjectionsOfUnifiedRows()
        {
            MaterialDefinition[] simulation = GameMaterialSimulationDefinitions.Create();
            MaterialPresentationDefinition[] rendering = GameMaterialRenderingDefinitions.Create();

            Assert.That(rendering.Length, Is.EqualTo(GameMaterialRuntimeCatalogue.Count));
            Assert.That(simulation.Length, Is.EqualTo(GameMaterialRuntimeCatalogue.SimulationCount));

            int simulationIndex = 0;
            for (byte materialId = 0; materialId < GameMaterialRuntimeCatalogue.Count; materialId++)
            {
                ref readonly GameMaterialRuntimeDefinition row =
                    ref GameMaterialRuntimeCatalogue.Get(materialId);

                Assert.That(rendering[materialId].MaterialIndex, Is.EqualTo(row.Rendering.MaterialIndex));
                Assert.That(rendering[materialId].Albedo, Is.EqualTo(row.Rendering.Albedo));
                Assert.That(rendering[materialId].Sampling, Is.EqualTo(row.Rendering.Sampling));
                Assert.That(rendering[materialId].Surface, Is.EqualTo(row.Rendering.Surface));
                Assert.That(rendering[materialId].Variation, Is.EqualTo(row.Rendering.Variation));

                if (!row.HasSimulation) continue;
                Assert.That(simulation[simulationIndex].MaterialId, Is.EqualTo(row.Simulation.MaterialId));
                Assert.That(simulation[simulationIndex].Hardness, Is.EqualTo(row.Simulation.Hardness));
                Assert.That(simulation[simulationIndex].DestructionClass,
                    Is.EqualTo(row.Simulation.DestructionClass));
                Assert.That(simulation[simulationIndex].Flammable, Is.EqualTo(row.Simulation.Flammable));
                simulationIndex++;
            }

            Assert.That(simulationIndex, Is.EqualTo(simulation.Length));
        }

        [Test]
        public void Empty_IsTheOnlyPresentationOnlyRuntimeRow()
        {
            int presentationOnly = 0;
            byte presentationOnlyId = byte.MaxValue;
            for (byte materialId = 0; materialId < GameMaterialRuntimeCatalogue.Count; materialId++)
            {
                ref readonly GameMaterialRuntimeDefinition row =
                    ref GameMaterialRuntimeCatalogue.Get(materialId);
                if (row.HasSimulation) continue;
                presentationOnly++;
                presentationOnlyId = materialId;
            }

            Assert.That(presentationOnly, Is.EqualTo(1));
            Assert.That(presentationOnlyId, Is.EqualTo(GameMaterialIds.Empty));
        }
    }
}
