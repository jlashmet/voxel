using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepTurretPlanningTests
    {
        [Test]
        public void LayoutPlannerFreezesDeterministicKeepTurretRoofVariation()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                CastleTopologyPlan firstTopology = CastleLayoutPlanner.Create(seed);
                CastleTopologyPlan secondTopology = CastleLayoutPlanner.Create(seed);
                CastleKeepTurretPlan firstPlan = firstTopology.KeepTurrets;
                CastleKeepTurretPlan secondPlan = secondTopology.KeepTurrets;

                Assert.IsTrue(
                    CastleKeepTurretPlanValidator.TryValidate(
                        firstPlan, out CastleKeepTurretPlanIssue issue),
                    $"seed {seed}: invalid keep turret plan: {issue}");

                CastleKeepTurretSpec[] first = firstPlan.Snapshot();
                CastleKeepTurretSpec[] second = secondPlan.Snapshot();
                Assert.AreEqual(4, first.Length);
                Assert.AreEqual(4, second.Length);
                for (int i = 0; i < first.Length; i++)
                {
                    Assert.AreEqual((CastleKeepTurretCorner)i, first[i].Corner,
                        $"seed {seed}: corner {i} identity drifted");
                    Assert.AreEqual(first[i].Corner, second[i].Corner,
                        $"seed {seed}: corner {i} was not deterministic");
                    Assert.AreEqual(first[i].HasRoof, second[i].HasRoof,
                        $"seed {seed}: roof {i} was not deterministic");
                    Assert.IsNull(first[i].Slits,
                        $"seed {seed}: topology planning must leave spatial slit phases unresolved");
                }
            }
        }

        [Test]
        public void SeedSpaceReachesEveryGeneratedRoofComposition()
        {
            bool allRoofed = false;
            bool minZPair = false;
            bool maxZPair = false;
            bool diagonal = false;
            bool bare = false;

            for (uint seed = 0; seed < 2048; seed++)
            {
                int mask = RoofMask(CastleKeepTurretPlanner.Create(seed));
                allRoofed |= mask == 0b1111;
                minZPair |= mask == 0b0011;
                maxZPair |= mask == 0b1100;
                diagonal |= mask == 0b1001;
                bare |= mask == 0;
            }

            Assert.IsTrue(allRoofed, "No all-roofed keep was reachable.");
            Assert.IsTrue(minZPair, "No min-Z roof pair was reachable.");
            Assert.IsTrue(maxZPair, "No max-Z roof pair was reachable.");
            Assert.IsTrue(diagonal, "No diagonal roof composition was reachable.");
            Assert.IsTrue(bare, "No bare keep-turret composition was reachable.");
        }

        [Test]
        public void HistoricalRecipeRoofsEveryKeepTurret()
        {
            CastleKeepTurretPlan plan = CastleKeepTurretRecipe.Historical();

            Assert.AreEqual(0b1111, RoofMask(plan));
            Assert.IsTrue(
                CastleKeepTurretPlanValidator.TryValidate(
                    plan, out CastleKeepTurretPlanIssue issue),
                issue.ToString());
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

        private static int RoofMask(CastleKeepTurretPlan plan)
        {
            int mask = 0;
            CastleKeepTurretSpec[] turrets = plan.Snapshot();
            for (int i = 0; i < turrets.Length; i++)
                if (turrets[i].HasRoof)
                    mask |= 1 << (int)turrets[i].Corner;
            return mask;
        }
    }
}
