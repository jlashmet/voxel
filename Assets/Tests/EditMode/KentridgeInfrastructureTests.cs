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
        public void UrbanInfrastructureStaysSeparateFromStableGameplayStructures()
        {
            FeatureCatalogue terraces = KentridgeDistrictTerraceCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue circulation = KentridgeVerticalConnectorCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue secondaryCirculation = KentridgeUrbanCirculationCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue massing = KentridgeUrbanMassingCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue verticalFrontage = KentridgeVerticalFrontageCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue anchorUndercroft = KentridgeAnchorUndercroftCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue access = KentridgeUrbanAccessCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue architecture = KentridgeHillsideArchitectureCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue combined = KentridgeCombinedVoxelCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);

            try
            {
                Assert.AreEqual(5, CountDefinitions(terraces, FeatureKind.Infrastructure));
                Assert.AreEqual(9, circulation.Definitions.Length);
                Assert.AreEqual(9, circulation.ExplicitPlacements.Length);

                Assert.AreEqual(4, secondaryCirculation.Definitions.Length);
                Assert.AreEqual(2, CountDefinitions(secondaryCirculation, FeatureKind.Infrastructure),
                    "Both west-side alternate climbs should be crisp stair streets while their contour links stay smooth.");

                Assert.AreEqual(2, massing.Definitions.Length);
                Assert.AreEqual(37, massing.ExplicitPlacements.Length);
                Assert.AreEqual(6, verticalFrontage.ExplicitPlacements.Length);
                Assert.AreEqual(4, anchorUndercroft.ExplicitPlacements.Length);
                Assert.AreEqual(8, access.ExplicitPlacements.Length);
                Assert.AreEqual(3, architecture.Definitions.Length);
                Assert.AreEqual(13, architecture.ExplicitPlacements.Length);

                AssertAllKind(circulation, FeatureKind.Infrastructure);
                AssertAllKind(massing, FeatureKind.Infrastructure);
                AssertAllKind(verticalFrontage, FeatureKind.Infrastructure);
                AssertAllKind(anchorUndercroft, FeatureKind.Infrastructure);
                AssertAllKind(access, FeatureKind.Infrastructure);
                AssertAllKind(architecture, FeatureKind.Infrastructure);

                int structures = 0;
                int infrastructureInstances = 0;
                for (int i = 0; i < combined.Rules.Length; i++)
                {
                    PlacementRule rule = combined.Rules[i];
                    FeatureDefinition definition = combined.Definitions[rule.DefinitionId];
                    if (definition.Kind == FeatureKind.Structure) structures += rule.ExplicitCount;
                    if (definition.Kind == FeatureKind.Infrastructure) infrastructureInstances += rule.ExplicitCount;
                }

                Assert.AreEqual(17, structures);
                Assert.AreEqual(84, infrastructureInstances,
                    "The second west stair adds one hard circulation instance while smooth contours remain Landform.");
            }
            finally
            {
                combined.Dispose();
                architecture.Dispose();
                access.Dispose();
                anchorUndercroft.Dispose();
                verticalFrontage.Dispose();
                massing.Dispose();
                secondaryCirculation.Dispose();
                circulation.Dispose();
                terraces.Dispose();
            }
        }

        private static int CountDefinitions(FeatureCatalogue catalogue, FeatureKind kind)
        {
            int count = 0;
            for (int i = 0; i < catalogue.Definitions.Length; i++) if (catalogue.Definitions[i].Kind == kind) count++;
            return count;
        }

        private static void AssertAllKind(FeatureCatalogue catalogue, FeatureKind kind)
        {
            for (int i = 0; i < catalogue.Definitions.Length; i++) Assert.AreEqual(kind, catalogue.Definitions[i].Kind);
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(foundationStone: 1, masonry: 1, darkMasonry: 6, timber: 2, glass: 4, warmWindow: 15, roofTile: 8, slate: 7, cloth: 9, moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
