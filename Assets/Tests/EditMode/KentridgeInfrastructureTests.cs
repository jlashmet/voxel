using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Runtime;

using VoxelEngine.Structures.Api;

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
            FeatureCatalogue courts = KentridgeUrbanCourtCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue piazza = KentridgeMarketPiazzaCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue civicForecourt = KentridgeCivicForecourtCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue massing = KentridgeUrbanMassingCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue verticalFrontage = KentridgeVerticalFrontageCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue galleries = KentridgeVerticalGalleryCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue upperSkybridge = KentridgeUpperSkybridgeCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue anchorUndercroft = KentridgeAnchorUndercroftCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue access = KentridgeUrbanAccessCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue architecture = KentridgeHillsideArchitectureCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue combined = KentridgeCombinedVoxelCatalogue.Build(Seed, BuildSettings(), Allocator.Temp);

            try
            {
                Assert.AreEqual(5, CountDefinitions(terraces, FeatureKind.Infrastructure));
                Assert.AreEqual(9, circulation.Definitions.Length);
                Assert.AreEqual(4, secondaryCirculation.Definitions.Length);
                Assert.AreEqual(2, CountDefinitions(secondaryCirculation, FeatureKind.Infrastructure));
                Assert.AreEqual(8, courts.Definitions.Length);
                AssertAllKind(courts, FeatureKind.Infrastructure);
                Assert.AreEqual(1, piazza.ExplicitPlacements.Length,
                    "The existing semantic Market Square should receive one hard shared-space surface.");
                AssertAllKind(piazza, FeatureKind.Infrastructure);
                Assert.AreEqual(1, civicForecourt.ExplicitPlacements.Length,
                    "Stable Church/Mayor anchors should frame one formal Civic Crown forecourt.");
                AssertAllKind(civicForecourt, FeatureKind.Infrastructure);
                Assert.AreEqual(37, massing.ExplicitPlacements.Length);
                Assert.AreEqual(6, verticalFrontage.ExplicitPlacements.Length);
                Assert.AreEqual(5, galleries.ExplicitPlacements.Length,
                    "Five public dense frontages should own reachable second-level galleries; NobleRidge stays private.");
                AssertAllKind(galleries, FeatureKind.Infrastructure);
                Assert.AreEqual(1, upperSkybridge.ExplicitPlacements.Length,
                    "Upper Ward courts should share one open over/under crossing above the main ascent.");
                AssertAllKind(upperSkybridge, FeatureKind.Infrastructure);
                Assert.AreEqual(4, anchorUndercroft.ExplicitPlacements.Length);
                Assert.AreEqual(8, access.ExplicitPlacements.Length);
                Assert.AreEqual(13, architecture.ExplicitPlacements.Length);

                int structures = 0;
                int infrastructureInstances = 0;
                for (int i = 0; i < combined.Rules.Length; i++)
                {
                    PlacementRule rule = combined.Rules[i];
                    FeatureDefinition definition = combined.Definitions[rule.DefinitionId];
                    if (definition.Kind == FeatureKind.Structure) structures += rule.ExplicitCount;
                    if (definition.Kind == FeatureKind.Infrastructure) infrastructureInstances += rule.ExplicitCount;
                }

                Assert.AreEqual(17, structures,
                    "Stable gameplay building identity must remain exactly the original Kentridge roster.");
                Assert.AreEqual(105, infrastructureInstances,
                    "The formal Civic Crown court adds one summit public-space instance without changing gameplay structures.");
            }
            finally
            {
                combined.Dispose();
                architecture.Dispose();
                access.Dispose();
                anchorUndercroft.Dispose();
                upperSkybridge.Dispose();
                galleries.Dispose();
                verticalFrontage.Dispose();
                massing.Dispose();
                civicForecourt.Dispose();
                piazza.Dispose();
                courts.Dispose();
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
