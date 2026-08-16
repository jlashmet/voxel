using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepTurretSlitResolutionTests
    {
        [Test]
        public void RuntimeReadyCastleFreezesAllKeepTurretSlitPhases()
        {
            for (uint seed = 1; seed <= 32; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(new int3(256, 220, 376), seed);
                CastleSpatialPlan spatial = StructuresComposition.PlanCastleSpatial(
                    in dimensions, seed ^ 0x71A5u);
                CastleSpatialProjection projection = CastleSpatialProjection.Create(
                    in dimensions, spatial);
                CastleKeepTurretPlan turrets = spatial.Topology.KeepTurrets;

                Assert.IsTrue(
                    CastleKeepTurretPlanValidator.TryValidateSlits(
                        in projection.KeepPlan,
                        turrets,
                        out CastleKeepTurretPlanIssue issue),
                    $"seed {seed}: {issue}");

                CastleKeepTurretSpec[] specs = turrets.Snapshot();
                Assert.AreEqual(4, specs.Length);
                for (int i = 0; i < specs.Length; i++)
                    Assert.NotNull(specs[i].Slits, $"seed {seed}, turret {i}");
            }
        }
    }
}
