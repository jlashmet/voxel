using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardWellPlanningTests
    {
        [Test]
        public void NarrowWardFallsBackToValidCourtyardWideWellSite()
        {
            // The preferred well ray starts at max(keepHalf) + 58 = 158 voxels from the keep.
            // With the 20-voxel well clearance this cannot fit inside a +/-170 rectangular ward,
            // although valid sites exist just beyond the keep's required 120-voxel clearance.
            var dimensions = new CastlePlan
            {
                Centre = int3.zero,
                PlateauRadius = 260,
                PlateauHeight = 30,
                CliffDrop = 40,
                BaileyHalfX = 170,
                BaileyHalfZ = 170,
                WallHeight = 80,
                WallThickness = 12,
                TowerRadius = 28,
                TowerHeight = 120,
                GateTowerRadius = 32,
                GateTowerHeight = 130,
                KeepHalfX = 100,
                KeepHalfZ = 100,
                KeepHeight = 230,
                FloorHeight = 46,
                Floors = 5,
                Seed = 101u,
            };
            var topology = new CastleTopologyPlan
            {
                Perimeter = CastlePerimeterKind.Rectangular,
                KeepPlacement = CastleKeepPlacement.Central,
                Wards = CastleWardPattern.SingleWard,
                DesiredTowerCount = 4,
                HasPosternGate = false,
            };

            CastleSpatialPlan first = CastleSpatialPlanner.Create(in dimensions, in topology);
            CastleSpatialPlan second = CastleSpatialPlanner.Create(in dimensions, in topology);

            Assert.IsFalse(first.KeepRequiresTerrainResolution);
            Assert.IsTrue(first.HasWell,
                "A resolved castle must search the full courtyard before giving up its well.");
            Assert.AreEqual(first.WellCentre, second.WellCentre,
                "Fallback well placement must remain deterministic.");

            int2 well = first.WellCentre;
            Assert.LessOrEqual(math.abs(well.x) + 20, dimensions.BaileyHalfX);
            Assert.LessOrEqual(math.abs(well.y) + 20, dimensions.BaileyHalfZ);
            Assert.IsTrue(
                math.abs(well.x) > dimensions.KeepHalfX + 20
                || math.abs(well.y) > dimensions.KeepHalfZ + 20,
                "Fallback well must retain clearance from the keep footprint.");

            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, first, out CastleSpatialPlanIssue issue),
                $"Fallback well produced an invalid spatial plan: {issue}");
        }
    }
}
