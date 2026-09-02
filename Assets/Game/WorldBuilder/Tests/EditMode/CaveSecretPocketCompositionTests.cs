using System.Collections.Generic;
using Game.Composition.CaveWorldBuilder;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CaveSecretPocketCompositionTests
    {
        [Test]
        public void PhysicalConflictOnPreferredTerminalFallsBackDeterministically()
        {
            var world = new SolidWorldSession();
            var fallback = Terminal(new int3(10, -8, 30), 80, 1);
            var preferred = Terminal(new int3(40, -8, 30), 100, 2);
            var candidates = new CaveTraversalCandidateSet();
            candidates.Items.Add(fallback);
            candidates.Items.Add(preferred);

            // East-facing default barrier starts one voxel beyond the terminal. This pre-existing
            // void makes only the deeper/preferred terminal physically invalid.
            world.Carve(preferred.Position + new int3(1, 0, 0), new int3(1, 1, 1));

            var campaign = Campaign.Create("cave-secret-composition-test");
            SiteRef hidden = campaign.World.Region("region").Site(
                "hidden-cave", SiteArchetype.Ruin,
                x => x.RequireCapability(SiteCapability.SecretCandidateHost));
            CavePlacementRequirements requirements = CavePlacementRequirements.AnyReachableTerminal();
            CavePlacementPreferences preferences = CavePlacementPreferences.PreferBranchTerminal;
            CaveSecretPocketConfig config = CaveSecretPocketConfig.Default;

            bool authored = CaveSecretPocketComposition.TryAuthorBest(
                world,
                in candidates,
                in requirements,
                in preferences,
                hidden,
                9000,
                in config,
                out CaveSecretPocketProjection projection,
                out CaveSecretPocketCompositionFailure failure);

            Assert.Multiple(() =>
            {
                Assert.That(authored, Is.True);
                Assert.That(failure, Is.EqualTo(CaveSecretPocketCompositionFailure.None));
                Assert.That(projection.IsWellFormed, Is.True);
                Assert.That(projection.Pocket.Terminal.Position, Is.EqualTo(fallback.Position),
                    "Composition must retry the next resolver-ranked terminal after a physical conflict.");
                Assert.That(projection.Pocket.Terminal.TraversalDistance, Is.EqualTo(fallback.TraversalDistance));
            });
        }

        [Test]
        public void NoMatchingTraversalFailsWithoutMutatingWorld()
        {
            var world = new SolidWorldSession();
            var main = new CaveTraversalCandidate
            {
                Position = new int3(20, -8, 30),
                TraversalDistance = 40,
                BranchDepth = 0,
                Flags = CaveTraversalFlags.ReachableFromEntrance |
                        CaveTraversalFlags.Terminal |
                        CaveTraversalFlags.MainPath,
                ExitFacing = Facing.North,
            };
            var candidates = new CaveTraversalCandidateSet();
            candidates.Items.Add(main);

            var campaign = Campaign.Create("cave-secret-composition-no-match");
            SiteRef hidden = campaign.World.Region("region").Site(
                "hidden-cave", SiteArchetype.Ruin,
                x => x.RequireCapability(SiteCapability.SecretCandidateHost));
            CavePlacementRequirements requirements = CavePlacementRequirements.AnyReachableTerminal();
            requirements.RequiredFlags |= CaveTraversalFlags.Branch;
            CavePlacementPreferences preferences = CavePlacementPreferences.PreferBranchTerminal;
            CaveSecretPocketConfig config = CaveSecretPocketConfig.Default;

            bool authored = CaveSecretPocketComposition.TryAuthorBest(
                world,
                in candidates,
                in requirements,
                in preferences,
                hidden,
                9000,
                in config,
                out _,
                out CaveSecretPocketCompositionFailure failure);

            Assert.That(authored, Is.False);
            Assert.That(failure, Is.EqualTo(CaveSecretPocketCompositionFailure.NoMatchingTraversal));
            Assert.That(world.TotalVoxelsWritten, Is.Zero,
                "A semantic placement miss must not mutate generated cave geometry.");
        }

        private static CaveTraversalCandidate Terminal(int3 position, int distance, byte branchDepth) =>
            new CaveTraversalCandidate
            {
                Position = position,
                TraversalDistance = distance,
                BranchDepth = branchDepth,
                Flags = CaveTraversalFlags.ReachableFromEntrance |
                        CaveTraversalFlags.Terminal |
                        CaveTraversalFlags.Branch,
                ExitFacing = Facing.East,
            };

        private sealed class SolidWorldSession : IStructureAuthoringSession
        {
            private readonly HashSet<int3> _empty = new HashSet<int3>();

            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => _empty.Count;
            public byte Get(int x, int y, int z) => IsSolid(x, y, z) ? (byte)2 : (byte)0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => !_empty.Contains(new int3(x, y, z));
            public void Set(int x, int y, int z, byte material)
            {
                int3 p = new int3(x, y, z);
                if (material == 0) _empty.Add(p); else _empty.Remove(p);
            }
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) =>
                Set(x, y, z, material);
            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) { }
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) { }
            public void Box(int3 min, int3 size, byte material) { }
            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling) { }
            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material, int innerRadius = 0) { }
            public void Disc(int cx, int y, int cz, int radius, byte material) { }
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) { }
            public void Gable(int3 min, int3 size, bool alongX, byte material) { }
            public void Crenellate(int3 start, int3 step, int count, int width, int height, int merlon, int gap, byte material) { }
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) { }
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) { }
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) { }
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void Carve(int3 min, int3 size)
            {
                for (int y = min.y; y < min.y + size.y; y++)
                for (int z = min.z; z < min.z + size.z; z++)
                for (int x = min.x; x < min.x + size.x; x++)
                    _empty.Add(new int3(x, y, z));
            }
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
