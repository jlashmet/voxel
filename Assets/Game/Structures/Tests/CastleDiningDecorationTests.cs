using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class CastleDiningDecorationTests
    {
        [Test]
        public void CastleGreatHallExposesDiningSpaceAndRequiredFurniture()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(80, 12, -140), 0xBEEFu);

            Assert.IsTrue(CastleDiningDecorationAdapter.TryResolve(
                in plan,
                out DecorationSpace space,
                out DecorationContext context,
                out DecorationExclusion[] exclusions,
                out DecorationPlacement[] placements));

            Assert.Multiple(() =>
            {
                Assert.IsTrue(space.IsWellFormed);
                Assert.AreEqual(DecorationSpaceKind.DiningRoom, space.Kind);
                Assert.IsTrue(context.IsWellFormed);
                Assert.AreEqual(DecorationSpaceKind.DiningRoom, context.SpaceKind);
                Assert.AreEqual(DecorationWealthTier.Noble, context.Wealth);
                Assert.AreEqual(5, exclusions.Length);
                Assert.GreaterOrEqual(placements.Length, 3);
                Assert.LessOrEqual(placements.Length, 5);
                Assert.AreEqual(DecorationPropFamily.Table, placements[0].Family);
                Assert.AreEqual(DecorationPropFamily.Bench, placements[1].Family);
                Assert.AreEqual(DecorationPropFamily.Bench, placements[2].Family);
                Assert.AreEqual(new int3(1, 0, 0), placements[0].Facing);
            });

            for (int i = 0; i < exclusions.Length; i++)
                Assert.IsTrue(exclusions[i].IsWellFormed, $"Dining exclusion {i} malformed.");

            for (int i = 0; i < placements.Length; i++)
            {
                Assert.IsTrue(space.Bounds.Contains(in placements[i].Bounds),
                    $"Dining placement {i} escaped great hall.");
                for (int e = 0; e < exclusions.Length; e++)
                    Assert.IsFalse(placements[i].Bounds.Overlaps(in exclusions[e].Bounds),
                        $"Dining placement {i} overlapped exclusion {e}.");
            }
        }

        [Test]
        public void CastleDiningResolutionIsStableForSamePlan()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(-220, 8, 310), 99173u);

            Assert.IsTrue(CastleDiningDecorationAdapter.TryResolve(
                in plan, out _, out _, out _, out DecorationPlacement[] first));
            Assert.IsTrue(CastleDiningDecorationAdapter.TryResolve(
                in plan, out _, out _, out _, out DecorationPlacement[] second));

            Assert.AreEqual(first.Length, second.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.Multiple(() =>
                {
                    Assert.AreEqual(first[i].Id, second[i].Id, $"Placement {i} identity changed.");
                    Assert.AreEqual(first[i].Bounds.Min, second[i].Bounds.Min, $"Placement {i} minimum changed.");
                    Assert.AreEqual(first[i].Bounds.MaxExclusive, second[i].Bounds.MaxExclusive,
                        $"Placement {i} maximum changed.");
                    Assert.AreEqual(first[i].Variant, second[i].Variant, $"Placement {i} variant changed.");
                });
            }
        }

        [Test]
        public void RequiredGreatHallDiningCompositionSurvivesRepresentativeSeeds()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan plan = CastlePlanner.Plan(new int3(0, 16, 0), seed);
                Assert.IsTrue(CastleDiningDecorationAdapter.TryResolve(
                    in plan,
                    out DecorationSpace space,
                    out _,
                    out DecorationExclusion[] exclusions,
                    out DecorationPlacement[] placements),
                    $"Dining adapter failed for seed {seed}.");

                Assert.GreaterOrEqual(placements.Length, 3, $"Required dining furniture missing for seed {seed}.");
                for (int i = 0; i < 3; i++)
                {
                    Assert.IsTrue(space.Bounds.Contains(in placements[i].Bounds),
                        $"Required placement {i} escaped for seed {seed}.");
                    for (int e = 0; e < exclusions.Length; e++)
                        Assert.IsFalse(placements[i].Bounds.Overlaps(in exclusions[e].Bounds),
                            $"Required placement {i} hit exclusion {e} for seed {seed}.");
                }
            }
        }
    }
}
