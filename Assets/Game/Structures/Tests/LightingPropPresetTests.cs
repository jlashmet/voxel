using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class LightingPropPresetTests
    {
        private const uint SceneId = 0x4C495431u; // LIT1

        [Test]
        public void LightingFamiliesUseExpectedMountsAndBackends()
        {
            DecorationContext context = Context(17u, DecorationConditionTier.Maintained);
            DecorationPropDescriptor fireplace = LightingPropPresets.Fireplace(in context, SceneId, 1u);
            DecorationPropDescriptor candle = LightingPropPresets.Candle(in context, SceneId, 2u);
            DecorationPropDescriptor chandelier = LightingPropPresets.Chandelier(in context, SceneId, 3u);
            DecorationPropDescriptor lamp = LightingPropPresets.StandingLamp(in context, SceneId, 4u);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(fireplace.IsWellFormed);
                Assert.IsTrue(candle.IsWellFormed);
                Assert.IsTrue(chandelier.IsWellFormed);
                Assert.IsTrue(lamp.IsWellFormed);
                Assert.AreEqual(DecorationMountMode.FloorAgainstWall, fireplace.MountMode);
                Assert.AreEqual(DecorationRenderBackend.VoxelStamp, fireplace.Backend);
                Assert.AreEqual(DecorationMountMode.Floor, candle.MountMode);
                Assert.AreEqual(DecorationMountMode.Ceiling, chandelier.MountMode);
                Assert.AreEqual(DecorationPropFamily.Lantern, lamp.Family);
                Assert.AreEqual(DecorationMountMode.Floor, lamp.MountMode);
            });
        }

        [Test]
        public void MaintainedLightingPlacementsProduceExpectedEffectHooks()
        {
            DecorationContext context = Context(29u, DecorationConditionTier.Maintained);
            Assert.IsTrue(TryPlacements(in context, out DecorationPlacement[] placements));

            DecorationEffectHook[] hooks = DecorationEffectHookPlanner.Collect(placements, in context);
            int lights = 0;
            int particles = 0;
            for (int i = 0; i < hooks.Length; i++)
            {
                if (hooks[i].Kind == DecorationEffectKind.Light) lights++;
                if (hooks[i].Kind == DecorationEffectKind.Particles) particles++;
            }

            Assert.Multiple(() =>
            {
                Assert.AreEqual(4, lights);
                Assert.AreEqual(2, particles);
                Assert.AreEqual(6, hooks.Length);
            });
        }

        [Test]
        public void RuinedLightingKeepsStablePlacementsButSuppressesEffects()
        {
            DecorationContext maintained = Context(41u, DecorationConditionTier.Maintained);
            DecorationContext ruined = Context(41u, DecorationConditionTier.Ruined);
            Assert.IsTrue(TryPlacements(in maintained, out DecorationPlacement[] maintainedPlacements));
            Assert.IsTrue(TryPlacements(in ruined, out DecorationPlacement[] ruinedPlacements));

            DecorationEffectHook[] ruinedHooks = DecorationEffectHookPlanner.Collect(ruinedPlacements, in ruined);
            Assert.AreEqual(0, ruinedHooks.Length);
            Assert.AreEqual(maintainedPlacements.Length, ruinedPlacements.Length);

            for (int i = 0; i < maintainedPlacements.Length; i++)
            {
                Assert.Multiple(() =>
                {
                    Assert.AreEqual(maintainedPlacements[i].Id, ruinedPlacements[i].Id,
                        $"Fixture {i} identity changed with condition.");
                    Assert.AreEqual(maintainedPlacements[i].Bounds.Min, ruinedPlacements[i].Bounds.Min,
                        $"Fixture {i} moved with condition.");
                    Assert.AreEqual(maintainedPlacements[i].Bounds.MaxExclusive, ruinedPlacements[i].Bounds.MaxExclusive,
                        $"Fixture {i} resized at placement time with condition.");
                });
            }
        }

        [Test]
        public void LightingVariantsAreStablePerSceneAndSlot()
        {
            DecorationContext context = Context(0xCAFEu, DecorationConditionTier.Maintained);
            DecorationPropDescriptor first = LightingPropPresets.Chandelier(in context, SceneId, 7u);
            DecorationPropDescriptor again = LightingPropPresets.Chandelier(in context, SceneId, 7u);
            DecorationPropDescriptor other = LightingPropPresets.Chandelier(in context, SceneId, 8u);

            Assert.Multiple(() =>
            {
                Assert.AreEqual(first.Size, again.Size);
                Assert.AreEqual(first.Variant, again.Variant);
                Assert.AreNotEqual(first.Variant, other.Variant);
            });
        }

        private static bool TryPlacements(
            in DecorationContext context,
            out DecorationPlacement[] placements)
        {
            DecorationSpace space = Space();
            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var resolved = new DecorationPlacement[4];

            DecorationPropDescriptor fireplace = LightingPropPresets.Fireplace(in context, SceneId, 1u);
            if (!DecorationPlacementResolver.TryPlace(
                    in space, in context, SceneId, 1u, in fireplace,
                    sockets, null, resolved, 0, out resolved[0]))
            {
                placements = new DecorationPlacement[0];
                return false;
            }

            DecorationPropDescriptor candle = LightingPropPresets.Candle(in context, SceneId, 2u);
            if (!DecorationPlacementResolver.TryPlace(
                    in space, in context, SceneId, 2u, in candle,
                    sockets, null, resolved, 1, out resolved[1]))
            {
                placements = new DecorationPlacement[0];
                return false;
            }

            DecorationPropDescriptor chandelier = LightingPropPresets.Chandelier(in context, SceneId, 3u);
            if (!DecorationPlacementResolver.TryPlace(
                    in space, in context, SceneId, 3u, in chandelier,
                    sockets, null, resolved, 2, out resolved[2]))
            {
                placements = new DecorationPlacement[0];
                return false;
            }

            DecorationPropDescriptor lamp = LightingPropPresets.StandingLamp(in context, SceneId, 4u);
            if (!DecorationPlacementResolver.TryPlace(
                    in space, in context, SceneId, 4u, in lamp,
                    sockets, null, resolved, 3, out resolved[3]))
            {
                placements = new DecorationPlacement[0];
                return false;
            }

            placements = resolved;
            return true;
        }

        private static DecorationContext Context(uint seed, DecorationConditionTier condition) =>
            new DecorationContext
            {
                WorldSeed = seed,
                StructureId = 0x117711u,
                SpaceId = 0x117700u,
                StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Courtly, seed),
                StructureKind = DecorationStructureKind.Castle,
                SpaceKind = DecorationSpaceKind.DiningRoom,
                Wealth = DecorationWealthTier.Wealthy,
                Condition = condition,
                Environment = DecorationEnvironmentTags.Interior,
            };

        private static DecorationSpace Space() => new DecorationSpace
        {
            SpaceId = 0x117700u,
            Kind = DecorationSpaceKind.DiningRoom,
            Bounds = new DecorationBounds
            {
                Min = new int3(-80, 10, -60),
                MaxExclusive = new int3(80, 70, 60),
            },
        };
    }
}
