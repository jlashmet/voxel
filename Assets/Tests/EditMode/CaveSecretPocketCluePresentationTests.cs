using System.Collections.Generic;
using System.Linq;
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
    public sealed class CaveSecretPocketCluePresentationTests
    {
        [Test]
        public void BoundaryEvidenceIsDeterministicAndPreservesVerifiedSeal()
        {
            var world = new SolidWorldSession();
            var terminal = new CaveTraversalCandidate
            {
                Position = new int3(20, -8, 30),
                TraversalDistance = 80,
                BranchDepth = 1,
                Flags = CaveTraversalFlags.ReachableFromEntrance |
                        CaveTraversalFlags.Terminal |
                        CaveTraversalFlags.Branch,
                ExitFacing = Facing.East,
            };
            var candidates = new CaveTraversalCandidateSet();
            candidates.Items.Add(terminal);

            CampaignBuilder campaign = Campaign.Create("cave-secret-clue-presentation");
            SiteRef hidden = campaign.World.Region("region").Site(
                "hidden-cave", SiteArchetype.Ruin,
                x => x.RequireCapability(SiteCapability.SecretCandidateHost));
            CavePlacementRequirements requirements = CavePlacementRequirements.AnyReachableTerminal();
            CavePlacementPreferences preferences = CavePlacementPreferences.PreferBranchTerminal;
            CaveSecretPocketConfig pocketConfig = CaveSecretPocketConfig.Default;

            Assert.That(CaveSecretPocketComposition.TryAuthorBest(
                world,
                in candidates,
                in requirements,
                in preferences,
                hidden,
                9000,
                in pocketConfig,
                out CaveSecretPocketProjection projection,
                out CaveSecretPocketCompositionFailure failure), Is.True, failure.ToString());

            long writesBeforeClue = world.TotalVoxelsWritten;
            var clueConfig = new CaveSecretPocketCluePresentationConfig(
                Coatings.Moss, 42, 0x434C5545u);
            Assert.That(CaveSecretPocketCluePresentation.TryApplyBoundaryEvidence(
                world, in projection, in clueConfig, out int firstCount), Is.True);
            int3[] firstPositions = world.Coated.OrderBy(p => p.x).ThenBy(p => p.y).ThenBy(p => p.z).ToArray();

            world.Coated.Clear();
            Assert.That(CaveSecretPocketCluePresentation.TryApplyBoundaryEvidence(
                world, in projection, in clueConfig, out int secondCount), Is.True);
            int3[] secondPositions = world.Coated.OrderBy(p => p.x).ThenBy(p => p.y).ThenBy(p => p.z).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(firstCount, Is.GreaterThan(0));
                Assert.That(secondCount, Is.EqualTo(firstCount));
                Assert.That(secondPositions, Is.EqualTo(firstPositions),
                    "The same physical secret and seed must produce the same boundary evidence.");
                Assert.That(world.TotalVoxelsWritten, Is.EqualTo(writesBeforeClue),
                    "Clue presentation must not carve or fill the verified secret topology.");
                Assert.That(firstPositions.All(p => Contains(in projection.Pocket.Barrier, p)), Is.True,
                    "Presentation may coat only the retained verified barrier.");
                Assert.That(firstPositions.All(p => world.IsSolid(p.x, p.y, p.z)), Is.True,
                    "Every clue voxel must remain part of the solid false wall.");
            });
        }

        private static bool Contains(in DecorationBounds bounds, int3 p) =>
            p.x >= bounds.Min.x && p.x < bounds.MaxExclusive.x &&
            p.y >= bounds.Min.y && p.y < bounds.MaxExclusive.y &&
            p.z >= bounds.Min.z && p.z < bounds.MaxExclusive.z;

        private sealed class SolidWorldSession : IStructureAuthoringSession
        {
            private readonly HashSet<int3> _empty = new HashSet<int3>();
            public readonly HashSet<int3> Coated = new HashSet<int3>();

            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => _empty.Count;
            public byte Get(int x, int y, int z) => IsSolid(x, y, z) ? (byte)2 : (byte)0;
            public byte GetCoating(int x, int y, int z) => Coated.Contains(new int3(x, y, z)) ? Coatings.Moss : (byte)0;
            public bool IsSolid(int x, int y, int z) => !_empty.Contains(new int3(x, y, z));
            public void Set(int x, int y, int z, byte material)
            {
                int3 p = new int3(x, y, z);
                if (material == 0) _empty.Add(p); else _empty.Remove(p);
            }
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = 0, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) => Set(x, y, z, material);
            public void Coat(int x, int y, int z, byte coating)
            {
                if (coating != 0) Coated.Add(new int3(x, y, z));
            }
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
