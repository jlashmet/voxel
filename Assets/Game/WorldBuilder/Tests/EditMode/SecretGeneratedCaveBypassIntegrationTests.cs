using System.Collections.Generic;
using System.Linq;
using Game.Composition.CaveWorldBuilder;
using Game.Composition.WorldBuilderWorldGen;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SecretGeneratedCaveBypassIntegrationTests
    {
        [Test]
        public void VerifiedGeneratedCaveBarrierFeedsAuthoredBreakableBypassPolicy()
        {
            var world = new SolidWorldSession();
            var terminal = new CaveTraversalCandidate
            {
                Position = new int3(20, -8, 30),
                TraversalDistance = 72,
                BranchDepth = 1,
                Flags = CaveTraversalFlags.ReachableFromEntrance |
                        CaveTraversalFlags.Terminal |
                        CaveTraversalFlags.Branch,
                ExitFacing = Facing.East,
            };
            CaveSecretPocketConfig config = CaveSecretPocketConfig.Default;
            Assert.That(CaveSecretPocketAuthoring.TryAuthor(
                world, in terminal, in config, out CaveSecretPocket pocket), Is.True);

            var campaign = Campaign.Create("generated-cave-secret-bypass");
            RegionHandle region = campaign.World.Region("generated-cave-region");
            SiteRef approach = region.Site("approach", SiteArchetype.Ruin);
            SiteRef hidden = region.Site("hidden-cave", SiteArchetype.Ruin,
                x => x.RequireCapability(SiteCapability.SecretCandidateHost));
            LootTableRef reward = campaign.Loot.Table("reward", loot => loot
                .RollCount(1, 1).Guaranteed(LootCategory.Currency));
            SecretRef secret = campaign.World.RequireSecret("cave-pocket", required => required
                .Inside(hidden)
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .RequireHiddenSpace()
                .RewardWith(reward));

            var provider = new CaveSecretPocketSecretCandidateProvider(1, new[]
            {
                new CaveSecretPocketProjection(hidden, in pocket, 9000),
            });
            CampaignBlueprint blueprint = campaign.Build();
            RequiredSecretSpec requiredSecret = blueprint.RequiredSecrets.Single(x => x.Ref.Equals(secret));
            ResolvedSecretPlan canonical = SecretPlanner.ResolveRequired(requiredSecret, provider, 164351u);
            ResolvedSecretWorldGeometry geometry = SecretWorldGeometryResolver.Resolve(canonical, provider);
            SecretBypassEvidence evidence = CaveSecretPocketBypassEvidence.AuthoredBreakable(in pocket);

            long barrierVoxels = (long)pocket.Barrier.Size.x * pocket.Barrier.Size.y * pocket.Barrier.Size.z;
            Assert.Multiple(() =>
            {
                Assert.That(canonical.Candidate.Id, Does.StartWith("cave-secret/terminal/"));
                Assert.That(canonical.EntranceId, Does.EndWith("/barrier"));
                Assert.That(geometry.EntranceBounds.MinInclusive.X, Is.EqualTo(pocket.Barrier.Min.x));
                Assert.That(geometry.EntranceBounds.MinInclusive.Y, Is.EqualTo(pocket.Barrier.Min.y));
                Assert.That(geometry.EntranceBounds.MinInclusive.Z, Is.EqualTo(pocket.Barrier.Min.z));
                Assert.That(geometry.HiddenSpaceBounds.MinInclusive.X, Is.EqualTo(pocket.Pocket.Min.x));
                Assert.That(geometry.HiddenSpaceBounds.MinInclusive.Y, Is.EqualTo(pocket.Pocket.Min.y));
                Assert.That(geometry.HiddenSpaceBounds.MinInclusive.Z, Is.EqualTo(pocket.Pocket.Min.z));
                Assert.That(evidence.HasTrivialUnintendedBypass, Is.False,
                    "The cave authoring preflight requires a solid one-voxel envelope around the future hidden volume.");
                Assert.That(evidence.DesignatedBreakableVoxelCount, Is.EqualTo(barrierVoxels));
                Assert.That(evidence.UndesignatedBreakableVoxelCount, Is.Zero);
                Assert.That(AllSolid(world, in pocket.Barrier), Is.True);
                Assert.That(AllEmpty(world, in pocket.Connector), Is.True);
                Assert.That(AllEmpty(world, in pocket.Pocket), Is.True);
            });

            var route = new SecretRouteSpec(
                new SecretRouteId("generated-cave-barrier"), secret,
                SecretRouteKind.BreakableBarrier,
                SecretBypassPolicy.AuthoredBreakablesOnly,
                "generated-cave-barrier", true, evidence);
            var clue = new SecretClueAnchorSpec(
                new SecretClueAnchorId("approach-weathering"), approach,
                SecretClueAnchorRole.ApproachEvidence,
                new[] { SecretClueChannel.Environmental }, true,
                SecretHiddenVolumeRelation.Outside, 1f, 80f);
            SiteRoleBinding[] sites =
            {
                new SiteRoleBinding(approach, new ResolvedSiteId("generated/cave-approach")),
                new SiteRoleBinding(hidden, new ResolvedSiteId("generated/cave-pocket")),
            };

            SecretDiscoveryPlanningResult result = SecretDiscoveryPlanner.Resolve(
                164351,
                new SecretDiscoverySpec(secret, SecretImportance.Standard,
                    new[] { route }, new[] { clue }),
                new[] { canonical }, sites);

            Assert.That(result.IsResolved, Is.True,
                string.Join(" | ", result.Diagnostics.Select(x => x.ToString())));
            Assert.That(result.Plan.Candidate, Is.EqualTo(canonical.Candidate));
            Assert.That(result.Plan.EntranceId, Is.EqualTo(canonical.EntranceId));
            Assert.That(result.Plan.Routes.Single().BypassPolicy,
                Is.EqualTo(SecretBypassPolicy.AuthoredBreakablesOnly));
        }

        private static bool AllSolid(IStructureAuthoringSession world, in DecorationBounds bounds)
        {
            for (int y = bounds.Min.y; y < bounds.MaxExclusive.y; y++)
            for (int z = bounds.Min.z; z < bounds.MaxExclusive.z; z++)
            for (int x = bounds.Min.x; x < bounds.MaxExclusive.x; x++)
                if (!world.IsSolid(x, y, z)) return false;
            return true;
        }

        private static bool AllEmpty(IStructureAuthoringSession world, in DecorationBounds bounds)
        {
            for (int y = bounds.Min.y; y < bounds.MaxExclusive.y; y++)
            for (int z = bounds.Min.z; z < bounds.MaxExclusive.z; z++)
            for (int x = bounds.Min.x; x < bounds.MaxExclusive.x; x++)
                if (world.IsSolid(x, y, z)) return false;
            return true;
        }

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
