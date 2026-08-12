using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeInfrastructureTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void HillsideInfrastructureIsHardButDoesNotInflateGameplayStructures()
        {
            FeatureCatalogue infrastructure = KentridgeVerticalConnectorCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue combined = KentridgeCombinedVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                Assert.AreEqual(9, infrastructure.Definitions.Length,
                    "Four stair flights, four retaining sections, and one campanile should compose the first hardscape pass.");
                Assert.AreEqual(9, infrastructure.ExplicitPlacements.Length);

                for (int i = 0; i < infrastructure.Definitions.Length; i++)
                    Assert.AreEqual(FeatureKind.Infrastructure, infrastructure.Definitions[i].Kind,
                        "Built hillside fabric must use the crisp hard-surface path without becoming a gameplay building.");

                int structures = 0;
                int infrastructureInstances = 0;
                for (int i = 0; i < combined.Rules.Length; i++)
                {
                    PlacementRule rule = combined.Rules[i];
                    FeatureDefinition definition = combined.Definitions[rule.DefinitionId];
                    if (definition.Kind == FeatureKind.Structure)
                        structures += rule.ExplicitCount;
                    if (definition.Kind == FeatureKind.Infrastructure)
                        infrastructureInstances += rule.ExplicitCount;
                }

                Assert.AreEqual(17, structures,
                    "Stable gameplay building identity must remain exactly the original Kentridge roster.");
                Assert.AreEqual(9, infrastructureInstances,
                    "Hardscape should be independently classified from gameplay structures.");
            }
            finally
            {
                combined.Dispose();
                infrastructure.Dispose();
            }
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
