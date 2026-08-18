using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationClutterTests
    {
        private const uint SceneId = 0x434C5431u; // CLT1
        private const uint ClusterId = 0xA11CEu;

        [Test]
        public void NobleMaintainedTableProducesAllClutterKindsDeterministically()
        {
            DecorationSpace space = Space();
            DecorationContext context = Context(17u, DecorationWealthTier.Noble, DecorationConditionTier.Maintained);
            Assert.IsTrue(TryTable(in space, in context, out DecorationPlacement table));
            int count = DecorationClutterPlanner.RecommendedCount(in context);
            Assert.AreEqual(6, count);

            Assert.IsTrue(DecorationClutterPlanner.TryPopulate(
                in space, in context, SceneId, ClusterId, in table, count, out DecorationClutterInstance[] first));
            Assert.IsTrue(DecorationClutterPlanner.TryPopulate(
                in space, in context, SceneId, ClusterId, in table, count, out DecorationClutterInstance[] second));

            var seen = new bool[DecorationClutterCatalog.KindCount];
            Assert.AreEqual(6, first.Length);
            for (int i = 0; i < first.Length; i++)
            {
                seen[(int)first[i].Kind] = true;
                Assert.Multiple(() =>
                {
                    Assert.IsTrue(first[i].IsWellFormed, $"Clutter item {i} malformed.");
                    Assert.AreEqual(table.Id, first[i].ParentId, $"Clutter item {i} lost its parent.");
                    Assert.IsTrue(space.Bounds.Contains(in first[i].Bounds), $"Clutter item {i} escaped room.");
                    Assert.AreEqual(first[i].Id, second[i].Id, $"Clutter item {i} identity changed.");
                    Assert.AreEqual(first[i].Bounds.Min, second[i].Bounds.Min, $"Clutter item {i} moved.");
                    Assert.AreEqual(first[i].Bounds.MaxExclusive, second[i].Bounds.MaxExclusive,
                        $"Clutter item {i} resized.");
                });

                for (int j = i + 1; j < first.Length; j++)
                    Assert.IsFalse(first[i].Bounds.Overlaps(in first[j].Bounds),
                        $"Clutter items {i} and {j} overlapped.");
            }

            for (int kind = 0; kind < seen.Length; kind++)
                Assert.IsTrue(seen[kind], $"Clutter kind {(DecorationClutterKind)kind} was missing.");
        }

        [Test]
        public void ConditionControlsRecommendedClutterDensity()
        {
            DecorationContext maintained = Context(29u, DecorationWealthTier.Comfortable, DecorationConditionTier.Maintained);
            DecorationContext worn = Context(29u, DecorationWealthTier.Comfortable, DecorationConditionTier.Worn);
            DecorationContext abandoned = Context(29u, DecorationWealthTier.Comfortable, DecorationConditionTier.Abandoned);
            DecorationContext ruined = Context(29u, DecorationWealthTier.Comfortable, DecorationConditionTier.Ruined);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(4, DecorationClutterPlanner.RecommendedCount(in maintained));
                Assert.AreEqual(3, DecorationClutterPlanner.RecommendedCount(in worn));
                Assert.AreEqual(1, DecorationClutterPlanner.RecommendedCount(in abandoned));
                Assert.AreEqual(0, DecorationClutterPlanner.RecommendedCount(in ruined));
            });
        }

        [Test]
        public void SixItemClusterSplitsIntoTwoBoxItemsAndFourMeshRequests()
        {
            DecorationSpace space = Space();
            DecorationContext context = Context(41u, DecorationWealthTier.Noble, DecorationConditionTier.Maintained);
            Assert.IsTrue(TryTable(in space, in context, out DecorationPlacement table));
            Assert.IsTrue(DecorationClutterPlanner.TryPopulate(
                in space, in context, SceneId, ClusterId, in table, 6, out DecorationClutterInstance[] items));

            DecorationClutterMeshRequest[] mesh = DecorationClutterPresentation.CollectMeshRequests(items);
            int boxCount = 0;
            for (int i = 0; i < items.Length; i++)
                if (items[i].Backend == DecorationRenderBackend.BoxAssembly) boxCount++;

            Assert.Multiple(() =>
            {
                Assert.AreEqual(2, boxCount);
                Assert.AreEqual(4, mesh.Length);
                for (int i = 0; i < mesh.Length; i++)
                    Assert.AreNotEqual(0UL, mesh[i].Id.Value);
            });
        }

        [Test]
        public void ContainerAndToolClutterCarryLootSemantics()
        {
            DecorationContext context = Context(73u, DecorationWealthTier.Modest, DecorationConditionTier.Maintained);
            DecorationClutterDescriptor container = DecorationClutterCatalog.Describe(
                in context, SceneId, ClusterId, 0, DecorationClutterKind.Container);
            DecorationClutterDescriptor tool = DecorationClutterCatalog.Describe(
                in context, SceneId, ClusterId, 1, DecorationClutterKind.Tool);

            Assert.Multiple(() =>
            {
                Assert.AreNotEqual(DecorationInteractionFlags.None,
                    container.Interaction & DecorationInteractionFlags.Container);
                Assert.AreNotEqual(DecorationInteractionFlags.None,
                    container.Interaction & DecorationInteractionFlags.Lootable);
                Assert.AreNotEqual(DecorationInteractionFlags.None,
                    tool.Interaction & DecorationInteractionFlags.Lootable);
                Assert.AreNotEqual(DecorationInteractionFlags.None,
                    tool.Interaction & DecorationInteractionFlags.Movable);
            });
        }

        private static bool TryTable(
            in DecorationSpace space,
            in DecorationContext context,
            out DecorationPlacement table)
        {
            DecorationPropDescriptor descriptor = DiningPropPresets.Table(in context);
            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            return DecorationPlacementResolver.TryPlace(
                in space, in context, SceneId, 99u, in descriptor,
                sockets, null, null, 0, out table);
        }

        private static DecorationContext Context(
            uint seed,
            DecorationWealthTier wealth,
            DecorationConditionTier condition) => new DecorationContext
            {
                WorldSeed = seed,
                StructureId = 0xC1A77E2u,
                SpaceId = 0xC1A7700u,
                StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Courtly, seed),
                StructureKind = DecorationStructureKind.Castle,
                SpaceKind = DecorationSpaceKind.DiningRoom,
                Wealth = wealth,
                Condition = condition,
                Environment = DecorationEnvironmentTags.Interior,
            };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0xC1A7700u,
            Kind = DecorationSpaceKind.DiningRoom,
            Bounds = new DecorationBounds
            {
                Min = new int3(-80, 10, -55),
                MaxExclusive = new int3(80, 70, 55),
            },
        };
    }
}
