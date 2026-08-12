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
            FeatureCatalogue terraces = KentridgeDistrictTerraceCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue circulation = KentridgeVerticalConnectorCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue massing = KentridgeUrbanMassingCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue verticalFrontage = KentridgeVerticalFrontageCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue anchorUndercroft = KentridgeAnchorUndercroftCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue access = KentridgeUrbanAccessCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue architecture = KentridgeHillsideArchitectureCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            FeatureCatalogue combined = KentridgeCombinedVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);

            try
            {
                int terraceInfrastructure = CountDefinitions(terraces, FeatureKind.Infrastructure);
                Assert.AreEqual(5, terraceInfrastructure,
                    "Only the five dense urban terraces should receive crisp retaining skins.");

                Assert.AreEqual(9, circulation.Definitions.Length,
                    "Four stair flights, four retaining sections, and one campanile should compose the primary hardscape pass.");
                Assert.AreEqual(9, circulation.ExplicitPlacements.Length);

                Assert.AreEqual(2, massing.Definitions.Length,
                    "Macro urban organisation is rendered as two coarse silhouette heights.");
                Assert.AreEqual(37, massing.ExplicitPlacements.Length,
                    "Eight semantic blocks should currently resolve to 37 anonymous massing sites.");

                Assert.AreEqual(6, verticalFrontage.ExplicitPlacements.Length,
                    "Six dense upper blocks should expose occupied downhill arcades/undercrofts.");
                Assert.AreEqual(4, anchorUndercroft.ExplicitPlacements.Length,
                    "Pub and Warehouse should each receive two role-derived undercroft bays.");
                Assert.AreEqual(8, access.ExplicitPlacements.Length,
                    "Every authored urban block should have one hard pedestrian access interface.");

                Assert.AreEqual(3, architecture.Definitions.Length,
                    "Secondary hillside architecture should reuse terrace-dwelling, civic-bridge, and retaining-gallery grammars.");
                Assert.AreEqual(13, architecture.ExplicitPlacements.Length,
                    "Seven embedded dwellings, one overhead civic bridge, and five roofed galleries should densify the hill without adding gameplay roles.");

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
                    if (definition.Kind == FeatureKind.Structure)
                        structures += rule.ExplicitCount;
                    if (definition.Kind == FeatureKind.Infrastructure)
                        infrastructureInstances += rule.ExplicitCount;
                }

                Assert.AreEqual(17, structures,
                    "Stable gameplay building identity must remain exactly the original Kentridge roster.");
                Assert.AreEqual(82, infrastructureInstances,
                    "Retaining skins, vertical fabric, working-quarter fabric/access, and hillside architecture must stay independently classified from gameplay structures.");
            }
            finally
            {
                combined.Dispose();
                architecture.Dispose();
                access.Dispose();
                anchorUndercroft.Dispose();
                verticalFrontage.Dispose();
                massing.Dispose();
                circulation.Dispose();
                terraces.Dispose();
            }
        }

        private static int CountDefinitions(FeatureCatalogue catalogue, FeatureKind kind)
        {
            int count = 0;
            for (int i = 0; i < catalogue.Definitions.Length; i++)
                if (catalogue.Definitions[i].Kind == kind) count++;
            return count;
        }

        private static void AssertAllKind(FeatureCatalogue catalogue, FeatureKind kind)
        {
            for (int i = 0; i < catalogue.Definitions.Length; i++)
                Assert.AreEqual(kind, catalogue.Definitions[i].Kind);
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
