using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepTurretPlannerTests
    {
        [Test]
        public void PlannerPreservesHistoricalFourRoofedCornerTurrets()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                CastleKeepTurretPlan plan = CastleKeepTurretPlanner.Create(seed);

                Assert.IsTrue(
                    CastleKeepTurretPlanValidator.TryValidate(
                        plan, out CastleKeepTurretPlanIssue issue),
                    $"seed {seed}: {issue}");

                CastleKeepTurretSpec[] turrets = plan.Snapshot();
                Assert.AreEqual(4, turrets.Length, $"seed {seed}");
                for (int i = 0; i < turrets.Length; i++)
                {
                    Assert.AreEqual((CastleKeepTurretCorner)i, turrets[i].Corner,
                        $"seed {seed}, turret {i}: corner identity drifted");
                    Assert.IsTrue(turrets[i].HasRoof,
                        $"seed {seed}, turret {i}: legacy keep turrets are all roofed");
                }
            }
        }

        [Test]
        public void PlanSnapshotsConstructorInputAndReturnedArrays()
        {
            var input = new[]
            {
                new CastleKeepTurretSpec { Corner = CastleKeepTurretCorner.MinXMinZ, HasRoof = true },
                new CastleKeepTurretSpec { Corner = CastleKeepTurretCorner.MaxXMinZ, HasRoof = true },
                new CastleKeepTurretSpec { Corner = CastleKeepTurretCorner.MinXMaxZ, HasRoof = true },
                new CastleKeepTurretSpec { Corner = CastleKeepTurretCorner.MaxXMaxZ, HasRoof = true },
            };
            var plan = new CastleKeepTurretPlan(input);

            input[0].HasRoof = false;
            CastleKeepTurretSpec[] first = plan.Snapshot();
            Assert.IsTrue(first[0].HasRoof,
                "Mutating constructor input must not mutate the frozen turret plan.");

            first[1].HasRoof = false;
            CastleKeepTurretSpec[] second = plan.Snapshot();
            Assert.IsTrue(second[1].HasRoof,
                "Mutating one snapshot must not mutate future snapshots.");
        }

        [Test]
        public void ValidatorRejectsMissingAndDuplicateCorners()
        {
            var missing = new CastleKeepTurretPlan(new[]
            {
                new CastleKeepTurretSpec { Corner = CastleKeepTurretCorner.MinXMinZ, HasRoof = true },
                new CastleKeepTurretSpec { Corner = CastleKeepTurretCorner.MaxXMinZ, HasRoof = true },
                new CastleKeepTurretSpec { Corner = CastleKeepTurretCorner.MinXMaxZ, HasRoof = true },
            });
            Assert.IsFalse(
                CastleKeepTurretPlanValidator.TryValidate(
                    missing, out CastleKeepTurretPlanIssue missingIssue));
            Assert.AreEqual(CastleKeepTurretPlanIssue.WrongTurretCount, missingIssue);

            var duplicate = new CastleKeepTurretPlan(new[]
            {
                new CastleKeepTurretSpec { Corner = CastleKeepTurretCorner.MinXMinZ, HasRoof = true },
                new CastleKeepTurretSpec { Corner = CastleKeepTurretCorner.MinXMinZ, HasRoof = false },
                new CastleKeepTurretSpec { Corner = CastleKeepTurretCorner.MinXMaxZ, HasRoof = true },
                new CastleKeepTurretSpec { Corner = CastleKeepTurretCorner.MaxXMaxZ, HasRoof = true },
            });
            Assert.IsFalse(
                CastleKeepTurretPlanValidator.TryValidate(
                    duplicate, out CastleKeepTurretPlanIssue duplicateIssue));
            Assert.AreEqual(CastleKeepTurretPlanIssue.DuplicateCorner, duplicateIssue);
        }
    }
}
