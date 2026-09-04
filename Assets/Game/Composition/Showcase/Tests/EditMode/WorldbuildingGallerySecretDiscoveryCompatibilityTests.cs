using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldbuildingGallerySecretDiscoveryCompatibilityTests
    {
        [Test]
        public void ReplayCompatibilityAcceptsVerticalAuthoringDriftForSameRoute()
        {
            int3 baked = new int3(280, 168, 2016);
            CaveAuthoringResult replay = Replay(new int3(280, 174, 2016));

            Assert.That(
                ShowcaseWorld.IsWorldbuildingGalleryCaveReplayCompatible(baked, in replay),
                Is.True,
                "A restored bake may carry an older vertical endpoint while the replay preserves the same planar route and main terminal semantics.");
        }

        [TestCase(281, 2016)]
        [TestCase(280, 2017)]
        public void ReplayCompatibilityRejectsPlanarRouteDrift(int x, int z)
        {
            int3 baked = new int3(280, 168, 2016);
            CaveAuthoringResult replay = Replay(new int3(x, 174, z));

            Assert.That(
                ShowcaseWorld.IsWorldbuildingGalleryCaveReplayCompatible(baked, in replay),
                Is.False,
                "Horizontal endpoint drift changes route identity and must not be hidden as bake compatibility.");
        }

        [Test]
        public void ReplayCompatibilityRejectsMissingMainTraversalTerminal()
        {
            int3 baked = new int3(280, 168, 2016);
            CaveAuthoringResult replay = Replay(new int3(280, 174, 2016));
            CaveTraversalCandidate branch = replay.TraversalCandidates.Items[0];
            branch.BranchDepth = 1;
            branch.Flags = CaveTraversalFlags.ReachableFromEntrance |
                           CaveTraversalFlags.Branch |
                           CaveTraversalFlags.Terminal;
            replay.TraversalCandidates.Items[0] = branch;

            Assert.That(
                ShowcaseWorld.IsWorldbuildingGalleryCaveReplayCompatible(baked, in replay),
                Is.False,
                "Matching coordinates alone are insufficient; the replay must still prove a reachable main-path terminal.");
        }

        [Test]
        public void ReplaySessionPreservesReadsAndDiscardsCaveAuthoringWrites()
        {
            var source = new RecordingAuthoringSession();
            Type replayType = typeof(ShowcaseWorld).GetNestedType(
                "WorldbuildingGalleryCaveReplaySession",
                BindingFlags.NonPublic);
            Assert.That(replayType, Is.Not.Null);

            var replay = (IStructureAuthoringSession)Activator.CreateInstance(replayType, source);
            Assert.That(replay.Get(1, 2, 3), Is.EqualTo(17));
            Assert.That(replay.GetCoating(1, 2, 3), Is.EqualTo(9));
            Assert.That(replay.IsSolid(1, 2, 3), Is.True);

            // These are the mutation primitives CaveNetworkAuthoringCore currently uses for
            // entrances, tunnel cross-sections and chambers. A compatibility replay may execute
            // them, but none may reach authoritative baked storage.
            replay.FillColumnBulk(1, 2, 8, 3, 0);
            replay.Box(new int3(1, 2, 3), new int3(4, 5, 6), 0);
            replay.Cylinder(1, 2, 3, 4, 5, 0);
            replay.Disc(1, 2, 3, 4, 0);

            Assert.That(source.MutationCalls, Is.Zero,
                "Traversal-metadata replay must not carve a vertically shifted second cave into the baked Gallery.");
            Assert.That(replay.TotalVoxelsWritten, Is.Zero);
            Assert.That(replay.BudgetExceeded, Is.False);
        }

        private static CaveAuthoringResult Replay(int3 endpoint)
        {
            var candidates = new CaveTraversalCandidateSet();
            candidates.Items.Add(new CaveTraversalCandidate
            {
                Position = endpoint,
                TraversalDistance = 112,
                BranchDepth = 0,
                Flags = CaveTraversalFlags.ReachableFromEntrance |
                        CaveTraversalFlags.MainPath |
                        CaveTraversalFlags.Terminal,
                ExitFacing = Facing.North,
            });

            return new CaveAuthoringResult
            {
                MainPathEnd = endpoint,
                MainPathTraversalDistance = 112,
                TraversalCandidates = candidates,
            };
        }

        private sealed class RecordingAuthoringSession : IStructureAuthoringSession
        {
            public int MutationCalls { get; private set; }
            public bool BudgetExceeded => false;
            public int WriteBudget => 1000000;
            public long TotalVoxelsWritten => MutationCalls;

            public byte Get(int x, int y, int z) => 17;
            public byte GetCoating(int x, int y, int z) => 9;
            public bool IsSolid(int x, int y, int z) => true;
            public void Set(int x, int y, int z, byte material) => MutationCalls++;
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) => MutationCalls++;
            public void Coat(int x, int y, int z, byte coating) => MutationCalls++;
            public void FillBulk(int3 min, int3 size, byte material) => MutationCalls++;
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) => MutationCalls++;
            public void Box(int3 min, int3 size, byte material) => MutationCalls++;
            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling) => MutationCalls++;
            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material,
                int innerRadius = 0) => MutationCalls++;
            public void Disc(int cx, int y, int cz, int radius, byte material) => MutationCalls++;
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) => MutationCalls++;
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) => MutationCalls++;
            public void Gable(int3 min, int3 size, bool alongX, byte material) => MutationCalls++;
            public void Crenellate(int3 start, int3 step, int count, int width, int height,
                int merlon, int gap, byte material) => MutationCalls++;
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) => MutationCalls++;
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) => MutationCalls++;
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) => MutationCalls++;
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) => MutationCalls++;
            public void Carve(int3 min, int3 size) => MutationCalls++;
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) => MutationCalls++;
        }
    }
}
