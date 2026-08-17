using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CaveDecorationTests
    {
        [Test]
        public void CaveAdapterExposesFloorWallsCeilingAlcoveLedgesAndExclusions()
        {
            CaveConfig config = SpaciousConfig();
            CaveWalkablePatch patch = CaveWalkablePatch.AtPathEnd(
                0x123456789ABCDEF0ul,
                new int3(120, -36, 80),
                Facing.North,
                in config);

            bool created = CaveDecorationSpaceAdapter.TryCreate(
                in patch,
                out DecorationSpace space,
                out DecorationContext context,
                out CaveDecorationCandidate[] candidates,
                out DecorationExclusion[] exclusions);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(created);
                Assert.IsTrue(space.IsWellFormed);
                Assert.AreEqual(DecorationSpaceKind.CaveChamber, space.Kind);
                Assert.IsTrue(context.IsWellFormed);
                Assert.AreEqual(DecorationStructureKind.Cave, context.StructureKind);
                Assert.AreEqual(DecorationSpaceKind.CaveChamber, context.SpaceKind);
                Assert.AreEqual(DecorationStyleFamily.Frontier, DecorationStyleIds.FamilyOf(context.StyleId));
                Assert.IsTrue((context.Environment & DecorationEnvironmentTags.Underground) != 0);
                Assert.AreEqual(9, candidates.Length);
                Assert.AreEqual(1, Count(candidates, CaveDecorationSurfaceKind.WalkableFloor));
                Assert.AreEqual(4, Count(candidates, CaveDecorationSurfaceKind.Wall));
                Assert.AreEqual(1, Count(candidates, CaveDecorationSurfaceKind.Ceiling));
                Assert.AreEqual(1, Count(candidates, CaveDecorationSurfaceKind.Alcove));
                Assert.AreEqual(2, Count(candidates, CaveDecorationSurfaceKind.Ledge));
                Assert.AreEqual(2, exclusions.Length);
                Assert.AreEqual(DecorationExclusionKind.Navigation, exclusions[0].Kind);
                Assert.AreEqual(DecorationExclusionKind.Hazard, exclusions[1].Kind);
            });

            for (int i = 0; i < candidates.Length; i++)
                Assert.IsTrue(candidates[i].IsWellFormed, $"Cave candidate {i} was malformed.");
            for (int i = 0; i < exclusions.Length; i++)
                Assert.IsTrue(exclusions[i].IsWellFormed, $"Cave exclusion {i} was malformed.");
        }

        [Test]
        public void CaveCampResolvesDeterministicallyOutsideNavigationAndHazards()
        {
            CaveConfig config = SpaciousConfig();
            CaveWalkablePatch patch = CaveWalkablePatch.AtPathEnd(
                0x0F1E2D3C4B5A6978ul,
                new int3(-200, -48, 310),
                Facing.East,
                in config);
            Assert.IsTrue(CaveDecorationSpaceAdapter.TryCreate(
                in patch,
                out DecorationSpace space,
                out DecorationContext context,
                out CaveDecorationCandidate[] candidates,
                out DecorationExclusion[] exclusions));

            Assert.IsTrue(CaveCampSceneResolver.TryResolve(
                in space, in context, candidates, exclusions, out DecorationPlacement[] first));
            Assert.IsTrue(CaveCampSceneResolver.TryResolve(
                in space, in context, candidates, exclusions, out DecorationPlacement[] second));

            Assert.Multiple(() =>
            {
                Assert.AreEqual(CaveCampSceneResolver.PlacementCount, first.Length);
                Assert.AreEqual(DecorationPropFamily.Campfire, first[0].Family);
                Assert.AreEqual(DecorationPropFamily.Bedroll, first[1].Family);
                Assert.AreEqual(DecorationPropFamily.Lantern, first[2].Family);
                Assert.AreEqual(first.Length, second.Length);
            });

            for (int i = 0; i < first.Length; i++)
            {
                Assert.IsTrue(first[i].IsWellFormed, $"Cave camp placement {i} was malformed.");
                Assert.IsTrue(space.Bounds.Contains(in first[i].Bounds),
                    $"Cave camp placement {i} escaped its patch.");
                Assert.AreEqual(first[i].Id, second[i].Id, $"Cave camp placement {i} ID changed.");
                Assert.AreEqual(first[i].Bounds.Min, second[i].Bounds.Min,
                    $"Cave camp placement {i} position changed.");

                for (int e = 0; e < exclusions.Length; e++)
                {
                    Assert.IsFalse(first[i].Bounds.Overlaps(in exclusions[e].Bounds),
                        $"Cave camp placement {i} overlapped exclusion {e}.");
                }
            }
        }

        [Test]
        public void CastleAndCaveSocketsFeedTheSameCorePlacementResolver()
        {
            var descriptor = new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Crate,
                AcceptedSockets = DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.Floor,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation,
                Size = new int3(4, 4, 4),
                Clearance = new int3(1, 0, 1),
                Variant = 1,
            };

            DecorationSpace castleSpace = new DecorationSpace
            {
                SpaceId = 100u,
                Kind = DecorationSpaceKind.Storage,
                Bounds = new DecorationBounds
                {
                    Min = new int3(-30, 4, -24),
                    MaxExclusive = new int3(30, 36, 24),
                },
            };
            DecorationContext castleContext = new DecorationContext
            {
                WorldSeed = 9u,
                StructureId = 10u,
                SpaceId = castleSpace.SpaceId,
                StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Courtly, 1u),
                StructureKind = DecorationStructureKind.Castle,
                SpaceKind = DecorationSpaceKind.Storage,
                Wealth = DecorationWealthTier.Comfortable,
                Condition = DecorationConditionTier.Maintained,
                Environment = DecorationEnvironmentTags.Interior,
            };
            DecorationSocket[] castleSockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in castleSpace);

            CaveConfig config = SpaciousConfig();
            CaveWalkablePatch patch = CaveWalkablePatch.AtPathEnd(
                777ul, new int3(100, -20, 100), Facing.South, in config);
            Assert.IsTrue(CaveDecorationSpaceAdapter.TryCreate(
                in patch, out DecorationSpace caveSpace, out DecorationContext caveContext,
                out CaveDecorationCandidate[] caveCandidates, out _));
            DecorationSocket[] caveSockets = CaveDecorationSurfaceAnalyzer.PlacementSockets(caveCandidates);

            bool castlePlaced = DecorationPlacementResolver.TryPlace(
                in castleSpace, in castleContext, 0x54455354u, 1u,
                in descriptor, castleSockets, null, null, 0, out DecorationPlacement castlePlacement);
            bool cavePlaced = DecorationPlacementResolver.TryPlace(
                in caveSpace, in caveContext, 0x54455354u, 1u,
                in descriptor, caveSockets, null, null, 0, out DecorationPlacement cavePlacement);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(castlePlaced);
                Assert.IsTrue(cavePlaced);
                Assert.IsTrue(castleSpace.Bounds.Contains(in castlePlacement.Bounds));
                Assert.IsTrue(caveSpace.Bounds.Contains(in cavePlacement.Bounds));
                Assert.AreEqual(DecorationPropFamily.Crate, castlePlacement.Family);
                Assert.AreEqual(DecorationPropFamily.Crate, cavePlacement.Family);
            });
        }

        private static CaveConfig SpaciousConfig()
        {
            CaveConfig config = CaveConfig.Default;
            config.TunnelWidth = 48;
            config.TunnelHeight = 34;
            config.SegmentLength = 40;
            config.WallRoughness = 1;
            config.FloorRoughness = 1;
            config.CeilingRoughness = 2;
            config.MinChamberRadius = 12;
            config.MaxChamberRadius = 30;
            config.MinChamberHeight = 14;
            config.MaxChamberHeight = 30;
            config.BoundsHalfExtents = new int3(320, 120, 320);
            return config;
        }

        private static int Count(CaveDecorationCandidate[] candidates, CaveDecorationSurfaceKind kind)
        {
            int count = 0;
            for (int i = 0; i < candidates.Length; i++)
                if (candidates[i].Kind == kind)
                    count++;
            return count;
        }
    }
}
