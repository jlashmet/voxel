using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class UtilityRoomSceneTests
    {
        [TestCase(UtilityRoomSceneKind.GuardPost)]
        [TestCase(UtilityRoomSceneKind.Kitchen)]
        [TestCase(UtilityRoomSceneKind.LibraryStudy)]
        [TestCase(UtilityRoomSceneKind.ChapelShrine)]
        [TestCase(UtilityRoomSceneKind.Barracks)]
        [TestCase(UtilityRoomSceneKind.ThroneRoom)]
        [TestCase(UtilityRoomSceneKind.Cellar)]
        [TestCase(UtilityRoomSceneKind.Storage)]
        public void SceneRecipesValidateAndRequiredSlotsResolve(UtilityRoomSceneKind kind)
        {
            DecorationSpaceKind spaceKind = SpaceKind(kind);
            DecorationSpace space = Space(spaceKind);
            DecorationContext context = Context(17u + (uint)kind, spaceKind, kind,
                DecorationWealthTier.Wealthy, DecorationConditionTier.Maintained);
            DecorationSceneSlot[] slots = UtilityRoomSceneCatalog.CreateSlots(kind);

            Assert.IsTrue(DecorationValidation.ValidateScene(slots, out uint error),
                $"{kind} scene validation failed at slot {error}.");
            Assert.IsTrue(UtilityRoomSceneResolver.TryResolve(
                kind, in space, in context, null, out DecorationPlacement[] placements),
                $"{kind} failed to resolve.");
            Assert.GreaterOrEqual(placements.Length, RequiredCount(kind),
                $"{kind} did not resolve all required slots.");

            for (int i = 0; i < placements.Length; i++)
            {
                Assert.IsTrue(placements[i].IsWellFormed, $"{kind} placement {i} malformed.");
                Assert.IsTrue(space.Bounds.Contains(in placements[i].Bounds),
                    $"{kind} placement {i} escaped the room.");
                for (int j = i + 1; j < placements.Length; j++)
                    Assert.IsFalse(placements[i].Bounds.Overlaps(in placements[j].Bounds),
                        $"{kind} placements {i} and {j} overlapped.");
            }
        }

        [TestCase(UtilityRoomSceneKind.GuardPost)]
        [TestCase(UtilityRoomSceneKind.Kitchen)]
        [TestCase(UtilityRoomSceneKind.LibraryStudy)]
        [TestCase(UtilityRoomSceneKind.ChapelShrine)]
        [TestCase(UtilityRoomSceneKind.Barracks)]
        [TestCase(UtilityRoomSceneKind.ThroneRoom)]
        [TestCase(UtilityRoomSceneKind.Cellar)]
        [TestCase(UtilityRoomSceneKind.Storage)]
        public void UtilitySceneResolutionIsDeterministic(UtilityRoomSceneKind kind)
        {
            DecorationSpaceKind spaceKind = SpaceKind(kind);
            DecorationSpace space = Space(spaceKind);
            DecorationContext context = Context(0xBEEFu, spaceKind, kind,
                DecorationWealthTier.Comfortable, DecorationConditionTier.Maintained);

            Assert.IsTrue(UtilityRoomSceneResolver.TryResolve(
                kind, in space, in context, null, out DecorationPlacement[] first));
            Assert.IsTrue(UtilityRoomSceneResolver.TryResolve(
                kind, in space, in context, null, out DecorationPlacement[] second));
            Assert.AreEqual(first.Length, second.Length);

            for (int i = 0; i < first.Length; i++)
            {
                Assert.Multiple(() =>
                {
                    Assert.AreEqual(first[i].Id, second[i].Id, $"{kind} placement {i} ID changed.");
                    Assert.AreEqual(first[i].Bounds.Min, second[i].Bounds.Min,
                        $"{kind} placement {i} minimum changed.");
                    Assert.AreEqual(first[i].Bounds.MaxExclusive, second[i].Bounds.MaxExclusive,
                        $"{kind} placement {i} maximum changed.");
                    Assert.AreEqual(first[i].Variant, second[i].Variant,
                        $"{kind} placement {i} variant changed.");
                });
            }
        }

        [TestCase(UtilityRoomSceneKind.GuardPost)]
        [TestCase(UtilityRoomSceneKind.Kitchen)]
        [TestCase(UtilityRoomSceneKind.LibraryStudy)]
        [TestCase(UtilityRoomSceneKind.ChapelShrine)]
        [TestCase(UtilityRoomSceneKind.Barracks)]
        [TestCase(UtilityRoomSceneKind.ThroneRoom)]
        [TestCase(UtilityRoomSceneKind.Cellar)]
        [TestCase(UtilityRoomSceneKind.Storage)]
        public void RuinedUtilityScenesRetainOnlyRequiredBaseline(UtilityRoomSceneKind kind)
        {
            DecorationSpaceKind spaceKind = SpaceKind(kind);
            DecorationSpace space = Space(spaceKind);
            DecorationContext context = Context(71u, spaceKind, kind,
                DecorationWealthTier.Noble, DecorationConditionTier.Ruined);

            Assert.IsTrue(UtilityRoomSceneResolver.TryResolve(
                kind, in space, in context, null, out DecorationPlacement[] placements));
            Assert.AreEqual(RequiredCount(kind), placements.Length,
                $"{kind} retained optional decoration while ruined.");
        }

        [Test]
        public void DiningHallComposesRelationalDiningWithHallScaleLighting()
        {
            DecorationSpace space = Space(DecorationSpaceKind.DiningRoom);
            DecorationContext context = Context(91u, DecorationSpaceKind.DiningRoom,
                UtilityRoomSceneKind.ThroneRoom,
                DecorationWealthTier.Noble, DecorationConditionTier.Maintained);

            Assert.IsTrue(DiningHallSceneResolver.TryResolve(
                in space, in context, null, out DecorationPlacement[] placements));

            int tables = Count(placements, DecorationPropFamily.Table);
            int benches = Count(placements, DecorationPropFamily.Bench);
            int chandeliers = Count(placements, DecorationPropFamily.Chandelier);
            Assert.Multiple(() =>
            {
                Assert.AreEqual(1, tables);
                Assert.AreEqual(2, benches);
                Assert.AreEqual(1, chandeliers);
                Assert.GreaterOrEqual(placements.Length, 4);
            });
        }

        private static int RequiredCount(UtilityRoomSceneKind kind)
        {
            DecorationSceneSlot[] slots = UtilityRoomSceneCatalog.CreateSlots(kind);
            int count = 0;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].Required) count++;
            return count;
        }

        private static int Count(DecorationPlacement[] placements, DecorationPropFamily family)
        {
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (placements[i].Family == family) count++;
            return count;
        }

        private static DecorationSpaceKind SpaceKind(UtilityRoomSceneKind kind)
        {
            switch (kind)
            {
                case UtilityRoomSceneKind.GuardPost: return DecorationSpaceKind.GuardPost;
                case UtilityRoomSceneKind.Kitchen:
                case UtilityRoomSceneKind.Cellar:
                case UtilityRoomSceneKind.Storage: return DecorationSpaceKind.Storage;
                case UtilityRoomSceneKind.LibraryStudy: return DecorationSpaceKind.Study;
                case UtilityRoomSceneKind.ChapelShrine: return DecorationSpaceKind.Chapel;
                case UtilityRoomSceneKind.Barracks: return DecorationSpaceKind.Bedroom;
                default: return DecorationSpaceKind.DiningRoom;
            }
        }

        private static DecorationContext Context(
            uint seed,
            DecorationSpaceKind spaceKind,
            UtilityRoomSceneKind kind,
            DecorationWealthTier wealth,
            DecorationConditionTier condition) => new DecorationContext
            {
                WorldSeed = seed,
                StructureId = 0x5CE4E001u + (uint)kind,
                SpaceId = 0x5CE4E100u + (uint)kind,
                StyleId = DecorationStyleIds.Compose(Style(kind), seed),
                StructureKind = DecorationStructureKind.Castle,
                SpaceKind = spaceKind,
                Wealth = wealth,
                Condition = condition,
                Environment = DecorationEnvironmentTags.Interior,
            };

        private static DecorationStyleFamily Style(UtilityRoomSceneKind kind)
        {
            switch (kind)
            {
                case UtilityRoomSceneKind.GuardPost:
                case UtilityRoomSceneKind.Barracks: return DecorationStyleFamily.Martial;
                case UtilityRoomSceneKind.ChapelShrine: return DecorationStyleFamily.Sacred;
                case UtilityRoomSceneKind.Kitchen:
                case UtilityRoomSceneKind.Cellar:
                case UtilityRoomSceneKind.Storage: return DecorationStyleFamily.Rustic;
                default: return DecorationStyleFamily.Courtly;
            }
        }

        private static DecorationSpace Space(DecorationSpaceKind kind) => new DecorationSpace
        {
            SpaceId = 0x5CE4E100u + (uint)kind,
            Kind = kind,
            Bounds = new DecorationBounds
            {
                Min = new int3(-100, 10, -80),
                MaxExclusive = new int3(100, 78, 80),
            },
        };
    }
}
