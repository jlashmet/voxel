using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepTurretPlanTests
    {
        [Test]
        public void HistoricalPlannerKeepsAllFourTurretsRoofedAcrossSeeds()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastleKeepTurretPlan plan = CastleKeepTurretPlanner.Create(seed);
                Assert.IsTrue(
                    CastleKeepTurretPlanValidator.TryValidate(
                        plan, out CastleKeepTurretPlanIssue issue),
                    $"seed {seed}: {issue}");

                CastleKeepTurretSpec[] turrets = plan.Snapshot();
                Assert.AreEqual(4, turrets.Length);
                for (int i = 0; i < turrets.Length; i++)
                {
                    Assert.AreEqual((CastleKeepTurretCorner)i, turrets[i].Corner,
                        $"seed {seed}: corner identity changed at {i}");
                    Assert.IsTrue(turrets[i].HasRoof,
                        $"seed {seed}: historical keep turret {i} lost its roof");
                }
            }
        }
    }
}
