using System;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeOrganicPlotRoutePrecedenceSceneIssueRegressionTests
    {
        private const uint VoxelShowcaseSeed = 1592594996u;
        private const int MarkMinX = 910;
        private const int MarkMaxX = 938;
        private const int MarkMinZ = 286;
        private const int MarkMaxZ = 304;

        [Test]
        public void SceneIssue20260826132234356OrganicRouteWinsInsideCapturedWideHouseOverlap()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            FeatureCatalogue plots = KentridgePlotSurfaceCatalogue.Build(
                VoxelShowcaseSeed, settings, Allocator.Temp);
            FeatureCatalogue routes = KentridgeDirectedTownSurfaceCatalogue.Build(
                VoxelShowcaseSeed, settings, Allocator.Temp);
            FeatureCatalogue combined = KentridgeCombinedVoxelCatalogue.Build(
                VoxelShowcaseSeed, settings, Allocator.Temp);

            try
            {
                int wideHouseId = FindDefinition(plots, "kentridge-plot-widehouse");
                FeatureDefinition wideHouse = plots.Definitions[wideHouseId];
                PlacementRule wideRule = FindRule(plots, wideHouseId);
                ExplicitPlacement mayor = FindPlacement(plots, wideRule, 910, 250);

                int pc = wideHouse.ProgramOffset;
                Assert.AreEqual(ShapeOp.EmitBox, (ShapeOp)plots.Program[pc]);
                int padMinX = mayor.Position.x + plots.Program[pc + 2];
                int padMinZ = mayor.Position.z + plots.Program[pc + 4];
                int padMaxX = padMinX + plots.Program[pc + 5];
                int padMaxZ = padMinZ + plots.Program[pc + 7];

                int overlapMinX = Math.Max(MarkMinX, padMinX);
                int overlapMaxX = Math.Min(MarkMaxX, padMaxX);
                int overlapMinZ = Math.Max(MarkMinZ, padMinZ);
                int overlapMaxZ = Math.Min(MarkMaxZ, padMaxZ);
                Assert.Less(overlapMinX, overlapMaxX,
                    "The saved upper mark must continue to intersect the MayorHouse grading pad.");
                Assert.Less(overlapMinZ, overlapMaxZ);

                Assert.IsTrue(RouteCrossesRectangle(
                        routes, overlapMinX, overlapMaxX, overlapMinZ, overlapMaxZ),
                    "The exact saved upper mark must include a real organic Dirt-route/pad overlap; " +
                    "otherwise precedence cannot be claimed as the captured owner discriminator.");

                int combinedPlotId = FindDefinition(combined, "kentridge-plot-widehouse");
                int plotPrecedence = combined.Definitions[combinedPlotId].Precedence;
                Assert.AreEqual(10, plotPrecedence,
                    "Organic layout grading must remain above ground cover but below public circulation.");

                int routePrecedence = FindPrefixPrecedence(combined, "kentridge-organic-route-");
                int groundPrecedence = FindPrefixPrecedence(combined, "kentridge-ground-");
                Assert.Greater(routePrecedence, plotPrecedence,
                    "Authored Dirt circulation must own the visible surface where it crosses a plot pad.");
                Assert.Greater(plotPrecedence, groundPrecedence,
                    "Building grading must still own its pad over generic settlement ground cover.");
            }
            finally
            {
                plots.Dispose();
                routes.Dispose();
                combined.Dispose();
            }
        }

        private static bool RouteCrossesRectangle(
            FeatureCatalogue routes, int minX, int maxX, int minZ, int maxZ)
        {
            for (int definitionId = 0; definitionId < routes.Definitions.Length; definitionId++)
            {
                FeatureDefinition definition = routes.Definitions[definitionId];
                if (!definition.Name.ToString().StartsWith("kentridge-organic-route-"))
                    continue;

                PlacementRule rule = FindRule(routes, definitionId);
                int radius = definition.Footprint.x / 2;
                for (int i = 0; i < rule.ExplicitCount; i++)
                {
                    ExplicitPlacement placement = routes.ExplicitPlacements[rule.ExplicitOffset + i];
                    int centerX = placement.Position.x + radius;
                    int centerZ = placement.Position.z + radius;
                    int nearestX = Math.Max(minX, Math.Min(centerX, maxX));
                    int nearestZ = Math.Max(minZ, Math.Min(centerZ, maxZ));
                    int dx = centerX - nearestX;
                    int dz = centerZ - nearestZ;
                    if (dx * dx + dz * dz <= radius * radius)
                        return true;
                }
            }
            return false;
        }

        private static int FindDefinition(FeatureCatalogue catalogue, string name)
        {
            for (int i = 0; i < catalogue.Definitions.Length; i++)
                if (catalogue.Definitions[i].Name.ToString() == name)
                    return i;
            Assert.Fail("Missing production definition: " + name);
            return -1;
        }

        private static int FindPrefixPrecedence(FeatureCatalogue catalogue, string prefix)
        {
            int? result = null;
            for (int i = 0; i < catalogue.Definitions.Length; i++)
            {
                FeatureDefinition definition = catalogue.Definitions[i];
                if (!definition.Name.ToString().StartsWith(prefix)) continue;
                if (!result.HasValue) result = definition.Precedence;
                else Assert.AreEqual(result.Value, definition.Precedence,
                    prefix + " definitions must share one semantic precedence.");
            }
            Assert.IsTrue(result.HasValue, "Missing production definitions with prefix " + prefix);
            return result.Value;
        }

        private static PlacementRule FindRule(FeatureCatalogue catalogue, int definitionId)
        {
            for (int i = 0; i < catalogue.Rules.Length; i++)
                if (catalogue.Rules[i].DefinitionId == definitionId)
                    return catalogue.Rules[i];
            Assert.Fail("Missing placement rule for definition " + definitionId);
            return default;
        }

        private static ExplicitPlacement FindPlacement(
            FeatureCatalogue catalogue, PlacementRule rule, int x, int z)
        {
            for (int i = 0; i < rule.ExplicitCount; i++)
            {
                ExplicitPlacement placement = catalogue.ExplicitPlacements[rule.ExplicitOffset + i];
                if (placement.Position.x == x && placement.Position.z == z)
                    return placement;
            }
            Assert.Fail("Missing exact scene placement at " + x + "," + z);
            return default;
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
