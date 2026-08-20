using System;
using System.Collections.Generic;
using Game.Composition.CaveWorldBuilder;
using Game.Composition.WorldBuilderWorldGen;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CaveSecretPocketWorldBuilderProjectionTests
    {
        [Test]
        public void AuthoredPocketProjectsAsDestroyableSideCaveWithSharedPhysicalIdentity()
        {
            var world = new SolidWorldSession();
            CaveTraversalCandidate terminal = Branch(new int3(10, -8, 20), 72, Facing.East);
            CaveSecretPocketConfig config = PocketConfig();
            Assert.IsTrue(CaveSecretPocketAuthoring.TryAuthor(
                world, in terminal, in config, out CaveSecretPocket pocket));

            CampaignBuilder campaign = Campaign.Create("cave-secret-projection-test");
            RegionHandle region = campaign.World.Region("test-region");
            SiteRef primarySite = region.Site("cave.primary").Ref;
            SiteRef aliasSite = region.Site("cave.alias").Ref;
            var provider = new CaveSecretPocketSecretCandidateProvider(1, new[]
            {
                new CaveSecretPocketProjection(primarySite, in pocket, 8750),
                new CaveSecretPocketProjection(aliasSite, in pocket, 8750),
            });

            IReadOnlyList<SecretCandidate> primary = provider.GetCandidates(primarySite);
            IReadOnlyList<SecretCandidate> alias = provider.GetCandidates(aliasSite);
            Assert.AreEqual(1, primary.Count);
            Assert.AreEqual(1, alias.Count);

            SecretCandidate candidate = primary[0];
            SecretEntranceCandidate entrance = candidate.Entrances[0];
            Assert.Multiple(() =>
            {
                Assert.AreEqual(primarySite, candidate.Site);
                Assert.AreEqual(SecretSpaceKind.SideCave, candidate.SpaceKind);
                Assert.IsTrue(candidate.HiddenFromNormalTraversal);
                Assert.AreEqual(8750, candidate.QualityBasisPoints);
                Assert.AreEqual(1, candidate.Entrances.Count);
                Assert.AreEqual(SecretEntranceType.DestroyableFalseWall, entrance.Type);
                Assert.IsTrue(entrance.SeparatesHiddenSpaceBeforeOpen);
                Assert.IsTrue(entrance.GrantsNormalTraversalAfterOpen);
                Assert.IsFalse(entrance.IsStructurallyCritical);
                Assert.IsTrue(entrance.SupportsDestruction);
                Assert.IsTrue(entrance.CanMatchHostSurface);
                Assert.AreEqual(candidate.Id, alias[0].Id,
                    "Site-role aliases of one physical cave pocket must share reservation identity.");
                Assert.AreEqual(aliasSite, alias[0].Site);
            });

            LootTableRef reward = campaign.Loot.Table(
                "cave-secret-loot",
                loot => loot.RollCount(1, 1).Guaranteed(LootCategory.Currency));
            campaign.World.Secrets.Policy(
                "cave-secret-policy",
                policy => policy
                    .Entrance(SecretEntranceType.DestroyableFalseWall)
                    .Distribution(new SecretDistribution(1, 1, 10000))
                    .RequireHiddenSpace()
                    .Container(ContainerArchetype.TreasureChest)
                    .RewardWith(reward));
            CampaignBlueprint blueprint = campaign.Build();

            IReadOnlyList<ResolvedSecretPlan> resolved = SecretPlanner.ResolveForSite(
                blueprint.SecretPolicies[0], primarySite, provider, 0x12345678u);
            Assert.AreEqual(1, resolved.Count,
                "The existing WorldBuilder planner must accept the verified cave topology.");

            ResolvedSecretWorldGeometry geometry = SecretWorldGeometryResolver.Resolve(
                resolved[0], provider);
            Assert.Multiple(() =>
            {
                Assert.AreEqual(candidate.Id, resolved[0].Candidate);
                Assert.AreEqual(entrance.Id, resolved[0].EntranceId);
                Assert.AreEqual(ContainerArchetype.TreasureChest, resolved[0].Container);
                Assert.AreEqual(reward, resolved[0].Reward);
                AssertBounds(in pocket.Pocket, geometry.HiddenSpaceBounds, 1);
                AssertBounds(in pocket.Barrier, geometry.EntranceBounds, 1);
                Assert.AreEqual(1, geometry.ContainerFloorPoint.UnitsPerDecimetre);
                Assert.AreEqual(pocket.Pocket.Min.y, geometry.ContainerFloorPoint.Position.Y);
                Assert.AreEqual(
                    pocket.Pocket.Min.x + (pocket.Pocket.Size.x - 1) / 2,
                    geometry.ContainerFloorPoint.Position.X);
                Assert.AreEqual(
                    pocket.Pocket.Min.z + (pocket.Pocket.Size.z - 1) / 2,
                    geometry.ContainerFloorPoint.Position.Z);
            });
        }

        [Test]
        public void RealizationFactsPreserveExplicitVoxelScale()
        {
            var world = new SolidWorldSession();
            CaveTraversalCandidate terminal = Branch(new int3(-12, 4, 8), 40, Facing.West);
            CaveSecretPocketConfig config = PocketConfig();
            Assert.IsTrue(CaveSecretPocketAuthoring.TryAuthor(
                world, in terminal, in config, out CaveSecretPocket pocket));

            CampaignBuilder campaign = Campaign.Create("cave-secret-scale-test");
            RegionHandle region = campaign.World.Region("test-region");
            SiteRef site = region.Site("cave.scaled").Ref;
            var provider = new CaveSecretPocketSecretCandidateProvider(2, new[]
            {
                new CaveSecretPocketProjection(site, in pocket, 5000),
            });
            SecretCandidate candidate = provider.GetCandidates(site)[0];

            Assert.IsTrue(provider.TryGetCandidateBounds(
                candidate.Id.Id, out RealizedWorldBounds candidateBounds));
            Assert.IsTrue(provider.TryGetEntranceBounds(
                candidate.Entrances[0].Id, out RealizedWorldBounds entranceBounds));
            Assert.Multiple(() =>
            {
                AssertBounds(in pocket.Pocket, candidateBounds, 2);
                AssertBounds(in pocket.Barrier, entranceBounds, 2);
            });
        }

        [Test]
        public void UnverifiedPocketCannotBeProjectedIntoWorldBuilder()
        {
            CampaignBuilder campaign = Campaign.Create("cave-secret-unverified-test");
            RegionHandle region = campaign.World.Region("test-region");
            SiteRef site = region.Site("cave.site").Ref;
            Assert.Throws<ArgumentException>(() =>
                new CaveSecretPocketProjection(site, default, 5000));
        }

        private static void AssertBounds(
            in DecorationBounds expected,
            RealizedWorldBounds actual,
            int unitsPerDecimetre)
        {
            Assert.AreEqual(unitsPerDecimetre, actual.UnitsPerDecimetre);
            Assert.AreEqual(expected.Min.x, actual.MinInclusive.X);
            Assert.AreEqual(expected.Min.y, actual.MinInclusive.Y);
            Assert.AreEqual(expected.Min.z, actual.MinInclusive.Z);
            Assert.AreEqual(expected.MaxExclusive.x - 1, actual.MaxInclusive.X);
            Assert.AreEqual(expected.MaxExclusive.y - 1, actual.MaxInclusive.Y);
            Assert.AreEqual(expected.MaxExclusive.z - 1, actual.MaxInclusive.Z);
        }

        private static CaveTraversalCandidate Branch(int3 position, int distance, Facing facing) =>
            new CaveTraversalCandidate
            {
                Position = position,
                TraversalDistance = distance,
                BranchDepth = 1,
                Flags = CaveTraversalFlags.ReachableFromEntrance |
                        CaveTraversalFlags.Terminal |
                        CaveTraversalFlags.Branch,
                ExitFacing = facing,
            };

        private static CaveSecretPocketConfig PocketConfig() => new CaveSecretPocketConfig
        {
            BarrierThickness = 2,
            EntranceWidth = 5,
            EntranceHeight = 7,
            ConnectorLength = 3,
            PocketWidth = 9,
            PocketHeight = 9,
            PocketDepth = 9,
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
