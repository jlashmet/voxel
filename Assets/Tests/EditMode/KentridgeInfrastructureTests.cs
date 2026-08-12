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
            FeatureCatalogue circulation = KentridgeVerticalConnectorCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue architecture = KentridgeHillsideArchitectureCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue combined = KentridgeCombinedVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                Assert.AreEqual(9, circulation.Definitions.Length,
                    "Four stair flights, four retaining sections, and one campanile should compose the primary hardscape pass.");
                Assert.AreEqual(9, circulation.ExplicitPlacements.Length);

                Assert.AreEqual(2, architecture.Definitions.Length,
                    "Secondary hillside architecture should reuse a terrace-dwelling grammar and one civic bridge grammar.");
                Assert.AreEqual(8, architecture.ExplicitPlacements.Length,
                    "Seven embedded dwellings plus the overhead civic bridge should densify the hill without adding gameplay roles.");

                for (int i = 0; i < circulation.Definitions.Length; i++)
                    Assert.AreEqual(FeatureKind.Infrastructure, circulation.Definitions[i].Kind);
                for (int i = 0; i < architecture.Definitions.Length; i++)
                    Assert.AreEqual(FeatureKind.Infrastructure, architecture.Definitions[i].Kind);

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
                Assert.AreEqual(17, infrastructureInstances,
                    "Hard civic/ambient fabric should be independently classified from gameplay structures.");
            }
            finally
            {
                combined.Dispose();
                architecture.Dispose();
                circulation.Dispose();
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
