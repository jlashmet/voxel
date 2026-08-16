using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleBuildBoundsTests
    {
        [Test]
        public void SpatialBoundsCoverUpperStructureApproachAndDungeonEnvelope()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(new int3(256, 220, 376), seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
                if (spatial.KeepRequiresTerrainResolution)
                {
                    spatial = CastleSpatialPlanner.ResolveHighestGroundKeep(
                        in plan, spatial, int2.zero);
                }
                spatial = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);

                CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, spatial);
                CastleBuildBounds bounds = CastleBuildBoundsResolver.Resolve(in plan, spatial);
                int baseY = plan.Centre.y + plan.PlateauHeight;

                Assert.Greater(bounds.MaxExclusive.y, 512,
                    $"seed {seed}: upper castle must reserve the Y=1 voxel region");
                Assert.IsTrue(bounds.Contains(projection.TrapdoorCentre),
                    $"seed {seed}: trapdoor escaped build bounds");

                int currentSiteReach = plan.PlateauRadius + plan.CliffDrop - 8;
                int currentGorgeOutward = plan.WallThickness + 92 + 11 + 90;
                int2 farApproachLocal = projection.Approach.LocalPoint(
                    currentSiteReach, currentGorgeOutward);
                var farApproach = new int3(
                    plan.Centre.x + farApproachLocal.x,
                    baseY - CastleLayout.LowerRiverDepth,
                    plan.Centre.z + farApproachLocal.y);
                Assert.IsTrue(bounds.Contains(farApproach),
                    $"seed {seed}: gate-oriented gorge escaped build bounds");

                // Keep compatibility details retain a broad historical envelope. Planned dungeon,
                // cave, and cave-decoration geometry are additionally included from their actual
                // semantic coordinates.
                var farDungeon = new int3(
                    projection.KeepCentreWorld.x + 276,
                    baseY - 178,
                    projection.KeepCentreWorld.y - 505);
                Assert.IsTrue(bounds.Contains(farDungeon),
                    $"seed {seed}: far dungeon/cave escaped build bounds");

                for (int vertex = 0; vertex < spatial.OuterWardVertices.Length; vertex++)
                {
                    int2 local = spatial.OuterWardVertices[vertex];
                    var world = new int3(
                        plan.Centre.x + local.x,
                        baseY,
                        plan.Centre.z + local.y);
                    Assert.IsTrue(bounds.Contains(world),
                        $"seed {seed}: perimeter vertex {vertex} escaped build bounds");
                }
            }
        }

        [Test]
        public void DeepPlannedRiverRecipeExpandsVerticalBounds()
        {
            const int plannedRiverDepth = 900;
            const int plannedBedDepth = 120;

            CastlePlan plan = CastlePlanner.Create(new int3(256, 220, 376), 211u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(211u);
            topology.KeepPlacement = CastleKeepPlacement.Central;

            CastleSitePlan originalSite = topology.Site;
            CastleSiteGeometryPlan originalGeometry = originalSite.Geometry;
            CastleRiverCrossSectionPlan originalCrossSection = originalGeometry.RiverCrossSection;
            var deepCrossSection = new CastleRiverCrossSectionPlan(
                originalCrossSection.BankBlendStart,
                originalCrossSection.BankBlendEnd,
                originalCrossSection.OutsideTerraceDrop,
                originalCrossSection.InsideTerraceDrop,
                originalCrossSection.LooseBankThreshold,
                originalCrossSection.DeepSoilThreshold,
                originalCrossSection.GrassThreshold,
                6,
                18,
                plannedBedDepth,
                originalCrossSection.BedRise,
                originalCrossSection.ExistingSurfaceRejectDepth,
                14);
            var deepGeometry = new CastleSiteGeometryPlan(
                originalGeometry.EdgeFrequencyA,
                originalGeometry.EdgeAmplitudeA,
                originalGeometry.EdgeFrequencyB,
                originalGeometry.EdgeAmplitudeB,
                originalGeometry.EdgeFrequencyC,
                originalGeometry.EdgeAmplitudeC,
                originalGeometry.CliffFalloffExponent,
                originalGeometry.CliffNoiseAngularFrequency,
                originalGeometry.CliffNoiseProgressFrequency,
                originalGeometry.CliffNoiseAmplitude,
                originalGeometry.CliffGroundInset,
                originalGeometry.GrassEdgeInset,
                originalGeometry.ApproachReachInset,
                originalGeometry.RiverOffset,
                originalGeometry.RiverHalfWidth,
                originalGeometry.WaterHalfWidth,
                plannedRiverDepth,
                originalGeometry.MeanderFrequencyA,
                originalGeometry.MeanderAmplitudeA,
                originalGeometry.MeanderFrequencyB,
                originalGeometry.MeanderAmplitudeB,
                in deepCrossSection);
            topology.Site = new CastleSitePlan(
                originalSite.GrassPatternSeed,
                originalSite.GrassCoveragePercent,
                originalSite.CourtyardPatternSeed,
                originalSite.CourtyardStonePercent,
                in deepGeometry);

            Assert.IsTrue(
                CastleTopologyPlanValidator.TryValidate(
                    in topology, out CastleTopologyPlanIssue topologyIssue),
                topologyIssue.ToString());

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            spatial = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);
            CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, spatial);
            CastleBuildBounds bounds = CastleBuildBoundsResolver.Resolve(in plan, spatial);

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int deepestBedY = baseY - plannedRiverDepth - plannedBedDepth;
            int2 channelLocal = projection.Approach.LocalPoint(
                0f,
                plan.WallThickness + deepGeometry.RiverOffset);
            var deepestWater = new int3(
                plan.Centre.x + channelLocal.x,
                deepestBedY,
                plan.Centre.z + channelLocal.y);

            Assert.Less(bounds.Min.y, baseY - 256,
                "A valid deep site recipe must expand beyond the historical fixed Y reserve.");
            Assert.IsTrue(bounds.Contains(deepestWater),
                "Castle dependency bounds must include the planned river bed write floor.");
        }

        [Test]
        public void PlannedForwardCaveExitIncludesCaveAndDecorationEnvelopes()
        {
            bool foundForwardExit = false;

            for (uint seed = 1; seed <= 512 && !foundForwardExit; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(new int3(256, 220, 376), seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
                if (spatial.KeepRequiresTerrainResolution)
                {
                    spatial = CastleSpatialPlanner.ResolveHighestGroundKeep(
                        in plan, spatial, int2.zero);
                }
                spatial = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);

                DungeonPlan dungeon = spatial.Dungeon;
                if (!dungeon.HasCaveExit) continue;

                DungeonRoomPlan threshold = dungeon.Rooms[dungeon.CaveThresholdRoomId];
                DungeonRoomPlan hall = default;
                bool foundHall = false;
                for (int room = 0; room < dungeon.Rooms.Length; room++)
                {
                    if (dungeon.Rooms[room].Purpose != DungeonRoomPurpose.GreatHall) continue;
                    hall = dungeon.Rooms[room];
                    foundHall = true;
                    break;
                }

                Assert.IsTrue(foundHall, $"seed {seed}: dungeon has no great hall");
                if (threshold.Centre.z <= hall.Centre.z) continue;

                foundForwardExit = true;
                Assert.NotNull(spatial.Cave, $"seed {seed}: completed cave exit has no CavePlan");
                Assert.NotNull(spatial.CaveDecoration,
                    $"seed {seed}: completed cave exit has no decoration plan");

                CastleBuildBounds bounds = CastleBuildBoundsResolver.Resolve(in plan, spatial);
                CaveBuildBounds caveBounds = CaveBuildBoundsResolver.Resolve(spatial.Cave);
                CastleCaveDecorationBuildBounds decorationBounds =
                    CastleCaveDecorationBuildBoundsResolver.Resolve(
                        spatial.Cave, spatial.CaveDecoration);

                for (int chamber = 0; chamber < spatial.Cave.Chambers.Length; chamber++)
                {
                    CaveChamberPlan planned = spatial.Cave.Chambers[chamber];
                    Assert.IsTrue(bounds.Contains(planned.Centre - planned.Radii),
                        $"seed {seed}: planned cave chamber {chamber} minimum escaped castle bounds");
                    Assert.IsTrue(bounds.Contains(planned.Centre + planned.Radii),
                        $"seed {seed}: planned cave chamber {chamber} maximum escaped castle bounds");
                }

                Assert.IsTrue(bounds.Contains(caveBounds.Min),
                    $"seed {seed}: planned cave bounds minimum escaped castle bounds");
                Assert.IsTrue(bounds.Contains(caveBounds.MaxExclusive - 1),
                    $"seed {seed}: planned cave bounds maximum escaped castle bounds");
                Assert.IsTrue(bounds.Contains(decorationBounds.Min),
                    $"seed {seed}: planned decoration minimum escaped castle bounds");
                Assert.IsTrue(bounds.Contains(decorationBounds.MaxExclusive - 1),
                    $"seed {seed}: planned decoration maximum escaped castle bounds");
            }

            Assert.IsTrue(foundForwardExit,
                "Expected the dungeon seed stream to produce at least one +Z cave exit.");
        }

        [Test]
        public void SpatialBoundsRemainConservativeBelowWorldZero()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(-180, -140, 95), 131u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(131u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            spatial = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);
            CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, spatial);
            CastleBuildBounds bounds = CastleBuildBoundsResolver.Resolve(in plan, spatial);
            int baseY = plan.Centre.y + plan.PlateauHeight;

            var caveFloor = new int3(
                projection.KeepCentreWorld.x,
                baseY - 178,
                projection.KeepCentreWorld.y - 371);

            Assert.Less(bounds.Min.y, 0,
                "Signed voxel worlds must not clamp castle dependency bounds to Y=0.");
            Assert.IsTrue(bounds.Contains(caveFloor),
                "The underground castle envelope must remain valid at negative world Y.");
        }
    }
}
