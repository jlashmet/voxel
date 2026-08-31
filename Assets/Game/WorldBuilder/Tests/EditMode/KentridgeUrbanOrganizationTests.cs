using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Runtime;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeUrbanOrganizationTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void UrbanPlanUsesBlocksToTurnCornersWithoutClosingTheMainAscent()
        {
            KentridgeUrbanMassingPlan plan = KentridgeUrbanOrganizer.Build(Seed);

            Assert.AreEqual(8, plan.Blocks.Count);
            Assert.AreEqual(16, plan.FrontageRuns.Count,
                "Each authored block should expose one contour frontage and one returning side edge.");
            Assert.AreEqual(1, plan.Thresholds.Count);

            int horizontalRuns = 0;
            int verticalRuns = 0;
            int accessGaps = 0;
            int civicSouthRuns = 0;
            int westCivicEnd = int.MinValue;
            int eastCivicStart = int.MaxValue;

            for (int i = 0; i < plan.FrontageRuns.Count; i++)
            {
                KentridgeFrontageRun run = plan.FrontageRuns[i];
                Assert.IsTrue(run.IsHorizontal || run.IsVertical);
                Assert.Greater(run.LengthDm, 80);
                Assert.That(run.CoveragePercent, Is.InRange(60, 90));

                if (run.District == DistrictKind.Working)
                {
                    Assert.That(run.MinStoreys, Is.InRange(1, 2));
                    Assert.That(run.MaxStoreys, Is.InRange(run.MinStoreys, 2));
                }
                else
                {
                    Assert.That(run.MinStoreys, Is.InRange(2, 3));
                    Assert.That(run.MaxStoreys, Is.InRange(run.MinStoreys, 3));
                }

                if (run.IsHorizontal) horizontalRuns++;
                if (run.IsVertical) verticalRuns++;
                if (run.HasGap) accessGaps++;

                if (run.Band == KentridgeUrbanBand.CivicCrown
                    && run.Frontage == FrontageDirection.South)
                {
                    civicSouthRuns++;
                    if (run.EndDm.X <= KentridgeTownPlanner.MainSpineXDm)
                        westCivicEnd = System.Math.Max(westCivicEnd, run.EndDm.X);
                    if (run.StartDm.X >= KentridgeTownPlanner.MainSpineXDm)
                        eastCivicStart = System.Math.Min(eastCivicStart, run.StartDm.X);
                }
            }

            Assert.AreEqual(8, horizontalRuns);
            Assert.AreEqual(8, verticalRuns,
                "Every block should turn at least one corner instead of reading as a detached roof row.");
            Assert.AreEqual(8, accessGaps,
                "Every block should reserve a visible lane into its interior court.");

            Assert.AreEqual(2, civicSouthRuns);
            Assert.LessOrEqual(westCivicEnd, 1110);
            Assert.GreaterOrEqual(eastCivicStart, 1240);
            Assert.Greater(eastCivicStart - westCivicEnd,
                KentridgeTownPlanner.MainRoadWidthDm,
                "Civic mass must frame rather than close the main uphill sight/circulation axis.");

            KentridgeUrbanThreshold threshold = plan.Thresholds[0];
            Assert.AreEqual(KentridgeTownPlanner.MainSpineXDm, threshold.CentreDm.X);
            Assert.AreEqual(KentridgeUrbanBand.UpperWard, threshold.LowerBand);
            Assert.AreEqual(KentridgeUrbanBand.CivicCrown, threshold.UpperBand);
        }

        [Test]
        public void EveryUrbanBlockProtectsAnInteriorVoidAndAFrontageAccess()
        {
            KentridgeUrbanMassingPlan plan = KentridgeUrbanOrganizer.Build(Seed);

            for (int i = 0; i < plan.Blocks.Count; i++)
            {
                KentridgeUrbanBlock block = plan.Blocks[i];
                Assert.Greater(block.InteriorMaxDm.X, block.InteriorMinDm.X, block.Id);
                Assert.Greater(block.InteriorMaxDm.Y, block.InteriorMinDm.Y, block.Id);
                Assert.AreNotEqual(KentridgeBlockEdge.None, block.CourtAccessEdge, block.Id);
                Assert.AreNotEqual(
                    KentridgeBlockEdge.None,
                    block.FrontageEdges & block.CourtAccessEdge,
                    block.Id);
                Assert.Greater(block.AccessWidthDm, 0, block.Id);
            }

            KentridgeUrbanBlock market = plan.Blocks[1];
            Assert.GreaterOrEqual(market.MinDm.Y, 660,
                "Anonymous market blocks must stay beyond the market-square public-space reservation.");

            KentridgeUrbanBlock civicWest = plan.Blocks[4];
            KentridgeUrbanBlock civicEast = plan.Blocks[5];
            Assert.GreaterOrEqual(civicWest.MinDm.Y, 194);
            Assert.GreaterOrEqual(civicEast.MinDm.Y, 194,
                "Civic blocks must frame rather than consume the crown forecourt reservation.");

            KentridgeUrbanBlock working = plan.Blocks[7];
            Assert.AreEqual("working-lane-block", working.Id);
            Assert.AreEqual(DistrictKind.Working, working.District);
            Assert.AreEqual(KentridgeUrbanBand.LowerWard, working.Band,
                "Working fabric should stay subordinate to the upper-town skyline.");
            Assert.AreEqual(KentridgeBlockEdge.South, working.CourtAccessEdge);
            Assert.AreNotEqual(KentridgeBlockEdge.None,
                working.FrontageEdges & KentridgeBlockEdge.East,
                "Working fabric must address the east service lane opposite the Warehouse.");
            Assert.Less(working.MaxDm.X, KentridgeTownPlanner.EastLaneXDm,
                "The working block must remain west of the service-lane centreline.");
        }

        [Test]
        public void CoarseMassingAdapterRealizesTurnedBlocksWithoutCreatingGameplayStructures()
        {
            FeatureCatalogue massing = KentridgeUrbanMassingCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                Assert.AreEqual(2, massing.Definitions.Length,
                    "CI massing adapter still only needs two silhouette heights.");
                Assert.AreEqual(37, massing.ExplicitPlacements.Length,
                    "Eight corner-turning blocks with court gaps should resolve to 37 anonymous masses.");

                for (int i = 0; i < massing.Definitions.Length; i++)
                {
                    Assert.AreEqual(FeatureKind.Infrastructure, massing.Definitions[i].Kind);
                    Assert.AreEqual(86, massing.Definitions[i].Precedence);
                    Assert.AreEqual(
                        massing.Definitions[i].Footprint.x,
                        massing.Definitions[i].Footprint.z,
                        "Quarter-turn block frontage requires an orientation-independent square envelope.");
                }
            }
            finally
            {
                massing.Dispose();
            }
        }

        [Test]
        public void SecondaryUrbanPlacementsRespectNamedPlotSpacing()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            FeatureCatalogue catalogue = KentridgeCombinedVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            int spacing = KentridgeTownPlanner.CompositionPolicy.Density.MinSpacingDm;

            try
            {
                for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
                {
                    PlacementRule rule = catalogue.Rules[ruleIndex];
                    FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                    string name = definition.Name.ToString();
                    if (!IsSecondaryUrbanBuilding(name)) continue;

                    for (int i = 0; i < rule.ExplicitCount; i++)
                    {
                        ExplicitPlacement placement = catalogue.ExplicitPlacements[
                            rule.ExplicitOffset + i];
                        bool quarterTurn = (placement.Orientation & 1) != 0;
                        int width = quarterTurn ? definition.Footprint.z : definition.Footprint.x;
                        int depth = quarterTurn ? definition.Footprint.x : definition.Footprint.z;
                        int minX = placement.Position.x;
                        int minZ = placement.Position.z;
                        int maxX = minX + width;
                        int maxZ = minZ + depth;

                        for (int plotIndex = 0; plotIndex < plan.Plots.Count; plotIndex++)
                        {
                            BuildingPlot plot = plan.Plots[plotIndex];
                            Int3 footprint = KentridgeDefinition.FootprintDm(plot.Archetype);
                            int reservedMinX = plot.PositionDm.X - spacing;
                            int reservedMinZ = plot.PositionDm.Y - spacing;
                            int reservedMaxX = plot.PositionDm.X + footprint.X + spacing;
                            int reservedMaxZ = plot.PositionDm.Y + footprint.Z + spacing;
                            bool intersects = maxX > reservedMinX && minX < reservedMaxX
                                           && maxZ > reservedMinZ && minZ < reservedMaxZ;

                            Assert.IsFalse(intersects,
                                name + " at " + placement.Position
                                + " violates the " + spacing + " dm reservation around "
                                + ((KentridgeRole)plot.RoleId) + " "
                                + reservedMinX + "," + reservedMinZ + ".."
                                + reservedMaxX + "," + reservedMaxZ + ".");
                        }
                    }
                }
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static bool IsSecondaryUrbanBuilding(string name) =>
            name.StartsWith("kentridge-fabric-", System.StringComparison.Ordinal)
            || name.StartsWith("kentridge-access-", System.StringComparison.Ordinal)
            || name.StartsWith("kentridge-gallery-", System.StringComparison.Ordinal)
            || name.EndsWith("-court", System.StringComparison.Ordinal)
            || name == "kentridge-upper-court-skybridge"
            || name.StartsWith("kentridge-vertical-", System.StringComparison.Ordinal)
                && name.Length > "kentridge-vertical-".Length
                && char.IsDigit(name["kentridge-vertical-".Length])
            || name == "kentridge-infrastructure-terrace-dwelling"
            || name == "kentridge-infrastructure-retaining-gallery";

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
