using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleTerrainPlanningTests
    {
        [Test]
        public void HighestGroundResolutionIsDeterministicAndValid()
        {
            for (uint seed = 1; seed <= 32; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.KeepPlacement = CastleKeepPlacement.HighestGround;
                CastleSpatialPlan unresolved = CastleSpatialPlanner.Create(in dimensions, in topology);

                Assert.IsTrue(unresolved.KeepRequiresTerrainResolution,
                    $"seed {seed}: expected unresolved highest-ground keep");

                uint terrainSeed = seed ^ 0x71A5u;
                CastleSpatialPlan first = CastleTerrainPlanning.Resolve(
                    in dimensions, unresolved, terrainSeed);
                CastleSpatialPlan second = CastleTerrainPlanning.Resolve(
                    in dimensions, unresolved, terrainSeed);

                Assert.IsFalse(first.KeepRequiresTerrainResolution,
                    $"seed {seed}: keep remained unresolved");
                Assert.AreEqual(first.KeepCentre, second.KeepCentre,
                    $"seed {seed}: terrain resolution was not deterministic");
                Assert.IsTrue(
                    CastleSpatialPlanValidator.TryValidate(
                        in dimensions, first, out CastleSpatialPlanIssue issue),
                    $"seed {seed}: resolved plan invalid: {issue}");
            }
        }

        [Test]
        public void HighestGroundResolverRejectsKeepOutsideAssignedWard()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 91u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(91u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.HighestGround;
            CastleSpatialPlan unresolved = CastleSpatialPlanner.Create(in dimensions, in topology);
            var outside = new int2(dimensions.BaileyHalfX, 0);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CastleSpatialPlanner.ResolveHighestGroundKeep(dimensions, unresolved, outside));
        }
    }
}
