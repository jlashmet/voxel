using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCaveDecorationPlanValidatorTests
    {
        [Test]
        public void PlannerOutputIsValidAcrossSeeds()
        {
            CavePlanningConstraints constraints = StandardConstraints();
            for (uint seed = 1; seed <= 64; seed++)
            {
                CavePlan cave = CavePlanner.Create(seed, in constraints);
                CastleCaveDecorationPlan decoration = CastleCaveDecorationPlanner.Create(cave);

                Assert.IsTrue(
                    CastleCaveDecorationPlanValidator.TryValidate(
                        cave, decoration, out CastleCaveDecorationPlanIssue issue),
                    $"seed {seed}: {issue}");
            }
        }

        [Test]
        public void ValidatorRejectsElementDetachedFromItsChamber()
        {
            CavePlanningConstraints constraints = StandardConstraints();
            CavePlan cave = CavePlanner.Create(19u, in constraints);
            CastleCaveDecorationPlan decoration = CastleCaveDecorationPlanner.Create(cave);
            CastleCaveDecorationSpec changed = decoration.Elements[2];
            changed.Position += new int3(10000, 0, 0);
            decoration.Elements[2] = changed;

            Assert.IsFalse(
                CastleCaveDecorationPlanValidator.TryValidate(
                    cave, decoration, out CastleCaveDecorationPlanIssue issue));
            Assert.AreEqual(CastleCaveDecorationPlanIssue.ElementOutsideChamber, issue);
        }

        [Test]
        public void ValidatorRejectsMismatchedEntryDecoration()
        {
            CavePlanningConstraints constraints = StandardConstraints();
            CavePlan cave = CavePlanner.Create(31u, in constraints);
            CastleCaveDecorationPlan decoration = CastleCaveDecorationPlanner.Create(cave);
            CastleCaveDecorationSpec changed = decoration.Elements[0];
            changed.Kind = CastleCaveDecorationKind.CrystalSpire;
            changed.Radius = 3;
            changed.Height = 8;
            decoration.Elements[0] = changed;

            Assert.IsFalse(
                CastleCaveDecorationPlanValidator.TryValidate(
                    cave, decoration, out CastleCaveDecorationPlanIssue issue));
            Assert.AreEqual(CastleCaveDecorationPlanIssue.EntryDecorationMismatch, issue);
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
