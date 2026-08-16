using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardBuildingValidationTests
    {
        [Test]
        public void ValidatorRejectsCallerMutatedCourtyardBuildingPlacement()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 131u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(131u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            Assert.Greater(spatial.CourtyardBuildings.Length, 0,
                "Baseline plan must contain a courtyard building for this corruption test.");
            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in plan, spatial, out CastleSpatialPlanIssue baselineIssue),
                $"Baseline spatial plan was invalid: {baselineIssue}");

            CastleCourtyardBuildingSpec corrupted = spatial.CourtyardBuildings[0];
            corrupted.Centre += new int2(5000, 5000);
            spatial.CourtyardBuildings[0] = corrupted;

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(
                    in plan, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.InvalidCourtyardBuildingPlacement, issue);
        }

        [Test]
        public void HighestGroundPlanCarriesNoCourtyardBuildingsUntilTerrainResolution()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 401u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(401u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.HighestGround;

            CastleSpatialPlan unresolved = CastleSpatialPlanner.Create(in plan, in topology);

            Assert.IsTrue(unresolved.KeepRequiresTerrainResolution);
            Assert.AreEqual(0, unresolved.CourtyardBuildings.Length);
            Assert.IsTrue(
                CastleSpatialPlanValidator.TryValidate(
                    in plan, unresolved, out CastleSpatialPlanIssue issue),
                issue.ToString());
        }
    }
}
