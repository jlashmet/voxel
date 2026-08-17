using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class CastleBedroomDecorationTests
    {
        [Test]
        public void CastleBedchamberExposesSemanticSpaceAndResolvesCoreProps()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(80, 12, -140), 0xBEEFu);

            bool resolved = CastleBedroomDecorationAdapter.TryResolve(
                in plan,
                out DecorationSpace space,
                out DecorationContext context,
                out DecorationExclusion[] exclusions,
                out DecorationPlacement[] placements);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(resolved);
                Assert.IsTrue(space.IsWellFormed);
                Assert.AreEqual(DecorationSpaceKind.Bedroom, space.Kind);
                Assert.IsTrue(context.IsWellFormed);
                Assert.AreEqual(DecorationStructureKind.Castle, context.StructureKind);
                Assert.AreEqual(DecorationSpaceKind.Bedroom, context.SpaceKind);
                Assert.AreEqual(DecorationWealthTier.Noble, context.Wealth);
                Assert.AreEqual(8, exclusions.Length);
                Assert.AreEqual(BedroomSceneResolver.PlacementCount, placements.Length);
                Assert.AreEqual(DecorationPropFamily.Bed, placements[0].Family);
                Assert.AreEqual(DecorationPropFamily.Rug, placements[1].Family);
                Assert.AreEqual(DecorationPropFamily.Dresser, placements[2].Family);
                Assert.AreEqual(DecorationPropFamily.Painting, placements[3].Family);
                Assert.AreEqual(DecorationPropFamily.WallTorch, placements[4].Family);
            });

            for (int i = 0; i < exclusions.Length; i++)
                Assert.IsTrue(exclusions[i].IsWellFormed, $"Castle bedroom exclusion {i} was malformed.");

            for (int i = 0; i < placements.Length; i++)
            {
                Assert.IsTrue(space.Bounds.Contains(in placements[i].Bounds),
                    $"Castle bedroom placement {i} escaped its semantic space.");
                for (int e = 0; e < exclusions.Length; e++)
                {
                    Assert.IsFalse(placements[i].Bounds.Overlaps(in exclusions[e].Bounds),
                        $"Castle bedroom placement {i} overlapped exclusion {e}.");
                }
            }
        }

        [Test]
        public void CastleBedchamberResolutionIsStableForTheSamePlan()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(-220, 8, 310), 99173u);

            Assert.IsTrue(CastleBedroomDecorationAdapter.TryResolve(
                in plan, out _, out _, out _, out DecorationPlacement[] first));
            Assert.IsTrue(CastleBedroomDecorationAdapter.TryResolve(
                in plan, out _, out _, out _, out DecorationPlacement[] second));

            Assert.AreEqual(first.Length, second.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i].Id, second[i].Id, $"Placement {i} identity changed.");
                Assert.AreEqual(first[i].Bounds.Min, second[i].Bounds.Min, $"Placement {i} minimum changed.");
                Assert.AreEqual(first[i].Bounds.MaxExclusive, second[i].Bounds.MaxExclusive,
                    $"Placement {i} maximum changed.");
                Assert.AreEqual(first[i].Variant, second[i].Variant, $"Placement {i} variant changed.");
            }
        }
    }
}
