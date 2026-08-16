using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCaveDecorationPlannerTests
    {
        [Test]
        public void DecorationPlanningIsDeterministicAcrossSeeds()
        {
            CavePlanningConstraints constraints = StandardConstraints();
            for (uint seed = 1; seed <= 64; seed++)
            {
                CavePlan cave = CavePlanner.Create(seed, in constraints);
                CastleCaveDecorationPlan first = CastleCaveDecorationPlanner.Create(cave);
                CastleCaveDecorationPlan second = CastleCaveDecorationPlanner.Create(cave);

                Assert.AreEqual(first.Elements.Length, second.Elements.Length, $"seed {seed}");
                for (int i = 0; i < first.Elements.Length; i++)
                {
                    CastleCaveDecorationSpec a = first.Elements[i];
                    CastleCaveDecorationSpec b = second.Elements[i];
                    Assert.AreEqual(i, a.Id, $"seed {seed}, element {i}: unstable id");
                    Assert.AreEqual(a.ChamberId, b.ChamberId, $"seed {seed}, element {i}: chamber");
                    Assert.AreEqual(a.Kind, b.Kind, $"seed {seed}, element {i}: kind");
                    Assert.AreEqual(a.Position, b.Position, $"seed {seed}, element {i}: position");
                    Assert.AreEqual(a.Size, b.Size, $"seed {seed}, element {i}: size");
                    Assert.AreEqual(a.Radius, b.Radius, $"seed {seed}, element {i}: radius");
                    Assert.AreEqual(a.Height, b.Height, $"seed {seed}, element {i}: height");
                }
            }
        }

        [Test]
        public void DecorationElementsStayAnchoredToTheirPlannedChambers()
        {
            CavePlanningConstraints constraints = StandardConstraints();
            CavePlan cave = CavePlanner.Create(91u, in constraints);
            CastleCaveDecorationPlan decoration = CastleCaveDecorationPlanner.Create(cave);

            int expected = 2;
            for (int chamber = 0; chamber < cave.Chambers.Length; chamber++)
                expected += 3 + (chamber == cave.EntryChamberId ? 5 : 3) + 1;
            Assert.AreEqual(expected, decoration.Elements.Length);

            for (int i = 0; i < decoration.Elements.Length; i++)
            {
                CastleCaveDecorationSpec spec = decoration.Elements[i];
                Assert.GreaterOrEqual(spec.ChamberId, 0, $"element {i}: chamber below zero");
                Assert.Less(spec.ChamberId, cave.Chambers.Length, $"element {i}: chamber overflow");
                CaveChamberPlan chamber = cave.Chambers[spec.ChamberId];

                Assert.LessOrEqual(
                    math.abs(spec.Position.x - chamber.Centre.x),
                    chamber.Radii.x,
                    $"element {i}: X escaped chamber envelope");
                Assert.LessOrEqual(
                    math.abs(spec.Position.z - chamber.Centre.z),
                    chamber.Radii.z,
                    $"element {i}: Z escaped chamber envelope");
                Assert.GreaterOrEqual(
                    spec.Position.y,
                    chamber.Centre.y - chamber.Radii.y,
                    $"element {i}: Y below chamber envelope");
                Assert.LessOrEqual(
                    spec.Position.y,
                    chamber.Centre.y + chamber.Radii.y,
                    $"element {i}: Y above chamber envelope");
            }
        }

        [Test]
        public void SnapshotDetachesDecorationArray()
        {
            CavePlanningConstraints constraints = StandardConstraints();
            CavePlan cave = CavePlanner.Create(17u, in constraints);
            CastleCaveDecorationPlan original = CastleCaveDecorationPlanner.Create(cave);
            CastleCaveDecorationPlan snapshot = original.Snapshot();
            int3 saved = snapshot.Elements[0].Position;

            CastleCaveDecorationSpec changed = original.Elements[0];
            changed.Position += new int3(999, 0, 0);
            original.Elements[0] = changed;

            Assert.AreEqual(saved, snapshot.Elements[0].Position);
            Assert.AreNotEqual(original.Elements[0].Position, snapshot.Elements[0].Position);
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
