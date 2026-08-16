using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepTurretPlanningTests
    {
        [Test]
        public void LayoutPlannerFreezesCurrentKeepTurretRoofRecipe()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleKeepTurretPlan turretPlan = topology.KeepTurrets;

                Assert.IsTrue(
                    CastleKeepTurretPlanValidator.TryValidate(
                        turretPlan, out CastleKeepTurretPlanIssue issue),
                    $"seed {seed}: invalid keep turret plan: {issue}");

                CastleKeepTurretSpec[] turrets = turretPlan.Snapshot();
                Assert.AreEqual(4, turrets.Length);
                for (int i = 0; i < turrets.Length; i++)
                {
                    Assert.AreEqual((CastleKeepTurretCorner)i, turrets[i].Corner,
                        $"seed {seed}: corner {i} identity drifted");
                    Assert.IsTrue(turrets[i].HasRoof,
                        $"seed {seed}: current compatibility recipe roofs every keep turret");
                }
            }
        }

        [Test]
        public void ValidatorRejectsDuplicateCornerIdentity()
        {
            var plan = new CastleKeepTurretPlan(new[]
            {
                new CastleKeepTurretSpec { Corner = CastleKeepTurretCorner.MinXMinZ },
                new CastleKeepTurretSpec { Corner = CastleKeepTurretCorner.MaxXMinZ },
                new CastleKeepTurretSpec { Corner = CastleKeepTurretCorner.MinXMaxZ },
                new CastleKeepTurretSpec { Corner = CastleKeepTurretCorner.MinXMaxZ },
            });

            Assert.IsFalse(
                CastleKeepTurretPlanValidator.TryValidate(
                    plan, out CastleKeepTurretPlanIssue issue));
            Assert.AreEqual(CastleKeepTurretPlanIssue.DuplicateCorner, issue);
        }
    }
}
