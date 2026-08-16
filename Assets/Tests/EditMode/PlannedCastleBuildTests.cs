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

                Assert.AreEqual(seed, build.Dimensions.Seed);
                Assert.AreEqual(terrainSeed, build.TerrainSeed);
                Assert.NotNull(build.Spatial);
                Assert.IsFalse(build.Spatial.KeepRequiresTerrainResolution,
                    $"seed {seed}: runtime bundle retained unresolved keep placement");
                Assert.IsTrue(
                    CastleSpatialPlanValidator.TryValidate(
                        in build.Dimensions,
                        build.Spatial,
                        out CastleSpatialPlanIssue issue),
                    $"seed {seed}: runtime bundle invalid: {issue}");

                CastleSpatialProjection projection = build.Projection;
                Assert.AreEqual(
                    new int2(
                        build.Dimensions.Centre.x + build.Spatial.KeepCentre.x,
                        build.Dimensions.Centre.z + build.Spatial.KeepCentre.y),
                    projection.KeepCentreWorld,
                    $"seed {seed}: projection drifted from semantic keep centre");
                Assert.AreEqual(
                    build.Spatial.PrimaryGate.Centre,
                    CastleApproachFrame.FromGate(in build.Spatial.PrimaryGate).GateCentre);
            }
        }
    }
}
