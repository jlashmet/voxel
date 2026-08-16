using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CavePlannerTests
    {
        [Test]
        public void PlannerIsDeterministicAndStructurallyValidAcrossSeeds()
        {
            CavePlanningConstraints constraints = StandardConstraints();
            bool observedVariation = false;
            CavePlan baseline = CavePlanner.Create(1u, in constraints);

            for (uint seed = 1; seed <= 128; seed++)
            {
                CavePlan first = CavePlanner.Create(seed, in constraints);
                CavePlan second = CavePlanner.Create(seed, in constraints);

                Assert.IsTrue(
                    CavePlanValidator.TryValidate(first, out CavePlanIssue issue),
                    $"seed {seed}: {issue}");
                Assert.AreEqual(first.Chambers.Length, second.Chambers.Length);
                Assert.AreEqual(first.Passages.Length, second.Passages.Length);
                for (int i = 0; i < first.Chambers.Length; i++)
                {
                    Assert.AreEqual(first.Chambers[i].Centre, second.Chambers[i].Centre,
                        $"seed {seed}, chamber {i}: centre changed");
                    Assert.AreEqual(first.Chambers[i].Radii, second.Chambers[i].Radii,
                        $"seed {seed}, chamber {i}: radii changed");
                    Assert.AreEqual(first.Chambers[i].RotationRadians,
                                    second.Chambers[i].RotationRadians,
                        $"seed {seed}, chamber {i}: rotation changed");
                }
                for (int i = 0; i < first.Passages.Length; i++)
                    Assert.AreEqual(first.Passages[i].ToChamberId,
                                    second.Passages[i].ToChamberId,
                        $"seed {seed}, passage {i}: connectivity changed");

                if (seed > 1 && !first.Chambers[1].Centre.Equals(baseline.Chambers[1].Centre))
                    observedVariation = true;
            }

            Assert.IsTrue(observedVariation,
                "Independent cave seeds should vary secondary chamber placement.");
        }

        [Test]
        public void ValidatorRejectsSelfPassage()
        {
            CavePlanningConstraints constraints = StandardConstraints();
            CavePlan plan = CavePlanner.Create(17u, in constraints);
            CavePassagePlan corrupted = plan.Passages[0];
            corrupted.ToChamberId = corrupted.FromChamberId;
            plan.Passages[0] = corrupted;

            Assert.IsFalse(CavePlanValidator.TryValidate(plan, out CavePlanIssue issue));
            Assert.AreEqual(CavePlanIssue.SelfPassage, issue);
        }

        [Test]
        public void ValidatorRejectsNonFiniteChamberRotation()
        {
            CavePlanningConstraints constraints = StandardConstraints();
            CavePlan plan = CavePlanner.Create(19u, in constraints);
            CaveChamberPlan corrupted = plan.Chambers[1];
            corrupted.RotationRadians = float.NaN;
            plan.Chambers[1] = corrupted;

            Assert.IsFalse(CavePlanValidator.TryValidate(plan, out CavePlanIssue issue));
            Assert.AreEqual(CavePlanIssue.InvalidChamberRotation, issue);
        }

        [Test]
        public void SnapshotDetachesMutablePlanningArrays()
        {
            CavePlanningConstraints constraints = StandardConstraints();
            CavePlan original = CavePlanner.Create(23u, in constraints);
            CavePlan snapshot = original.Snapshot();
            int3 savedCentre = snapshot.Chambers[1].Centre;

            CaveChamberPlan mutated = original.Chambers[1];
            mutated.Centre += new int3(999, 0, 0);
            original.Chambers[1] = mutated;
            CavePassagePlan passage = original.Passages[0];
            passage.Width += 99;
            original.Passages[0] = passage;

            Assert.AreEqual(savedCentre, snapshot.Chambers[1].Centre);
            Assert.AreNotEqual(original.Passages[0].Width, snapshot.Passages[0].Width);
            Assert.IsTrue(CavePlanValidator.TryValidate(snapshot, out CavePlanIssue issue),
                issue.ToString());
        }

        private static CavePlanningConstraints StandardConstraints() =>
            new CavePlanningConstraints
            {
                Entrance = new int3(120, 80, -40),
                EntranceToMainOffset = new int3(0, 18, 0),
                MainRadii = new int3(82, 36, 104),
                SecondaryChamberCount = 4,
                SecondaryMinRadii = new int3(30, 22, 34),
                SecondaryMaxRadii = new int3(62, 38, 78),
                MinimumHorizontalSpread = 54,
                MaximumHorizontalSpread = 126,
                VerticalSpread = 18,
                PassageWidth = 20,
                PassageHeight = 30,
            };
    }
}
