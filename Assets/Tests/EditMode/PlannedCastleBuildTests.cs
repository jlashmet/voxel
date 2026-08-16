using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class PlannedCastleBuildTests
    {
        [Test]
        public void RuntimeBundleKeepsResolvedSpatialPlanProjectionAndTerrainSeedTogether()
        {
            var centre = new int3(256, 220, 376);
            for (uint seed = 1; seed <= 64; seed++)
            {
                uint terrainSeed = seed ^ 0x71A5u;
                PlannedCastleBuild build = StructuresComposition.PlanCastleBuild(
                    centre, seed, terrainSeed);
                CastlePlan dimensions = build.Dimensions;
                CastleSpatialPlan spatial = build.Spatial;

                Assert.AreEqual(seed, dimensions.Seed);
                Assert.AreEqual(terrainSeed, build.TerrainSeed);
                Assert.NotNull(spatial);
                Assert.IsFalse(spatial.KeepRequiresTerrainResolution,
                    $"seed {seed}: runtime bundle retained unresolved keep placement");
                Assert.IsTrue(
                    CastleSpatialPlanValidator.TryValidate(
                        in dimensions,
                        spatial,
                        out CastleSpatialPlanIssue issue),
                    $"seed {seed}: runtime bundle invalid: {issue}");

                CastleSpatialProjection projection = build.Projection;
                Assert.AreEqual(
                    new int2(
                        dimensions.Centre.x + spatial.KeepCentre.x,
                        dimensions.Centre.z + spatial.KeepCentre.y),
                    projection.KeepCentreWorld,
                    $"seed {seed}: projection drifted from semantic keep centre");

                CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
                Assert.AreEqual(
                    primaryGate.Centre,
                    CastleApproachFrame.FromGate(in primaryGate).GateCentre);
            }
        }
    }
}
