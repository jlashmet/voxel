using System;
using System.Collections.Generic;
using System.Linq;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SecretTopologyPlannerTests
    {
        private sealed class Provider : ISecretCandidateProvider
        {
            private readonly IReadOnlyList<SecretCandidate> _candidates;
            public Provider(params SecretCandidate[] candidates) => _candidates = candidates;
            public IReadOnlyList<SecretCandidate> GetCandidates(SiteRef site) => _candidates;
        }

        [Test]
        public void DestroyableFalseWallSelectsOnlyARealHiddenTraversableNonCriticalOpening()
        {
            var game = Campaign.Create("secret-topology");
            SiteRef ruins = game.World.RequireSite("ruins", site => site
                .Archetype(SiteArchetype.Ruin)
                .RequireCapability(SiteCapability.SecretCandidateHost));

            LootTableRef treasure = game.Loot.Table("treasure", loot => loot
                .RollCount(1, 1)
                .Guaranteed(LootCategory.Currency));

            game.World.Secrets.Policy("false-wall", secret => secret
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .Distribution(new SecretDistribution(1, 1, 10000))
                .RequireHiddenSpace()
                .Container(ContainerArchetype.TreasureChest)
                .RewardWith(treasure));

            SecretPolicySpec policy = game.Build().SecretPolicies.Single();

            var fakeCandidate = new SecretCandidate(
                new SecretCandidateId("critical-wall"),
                ruins,
                SecretSpaceKind.CavityBehindWall,
                hiddenFromNormalTraversal: true,
                qualityBasisPoints: 10000,
                entrances: new[]
                {
                    FalseWall(
                        "critical-entrance",
                        separates: true,
                        traversableAfterOpen: true,
                        structurallyCritical: true,
                        destructible: true,
                        matchesSurface: true)
                });

            var validCandidate = new SecretCandidate(
                new SecretCandidateId("real-hidden-room"),
                ruins,
                SecretSpaceKind.HiddenRoom,
                hiddenFromNormalTraversal: true,
                qualityBasisPoints: 8000,
                entrances: new[]
                {
                    FalseWall(
                        "west-false-wall",
                        separates: true,
                        traversableAfterOpen: true,
                        structurallyCritical: false,
                        destructible: true,
                        matchesSurface: true)
                });

            IReadOnlyList<ResolvedSecretPlan> resolved = SecretPlanner.ResolveForSite(
                policy,
                ruins,
                new Provider(fakeCandidate, validCandidate),
                worldSeed: 17);

            Assert.That(resolved.Count, Is.EqualTo(1));
            Assert.That(resolved[0].Candidate, Is.EqualTo(validCandidate.Id));
            Assert.That(resolved[0].EntranceId, Is.EqualTo("west-false-wall"));
            Assert.That(resolved[0].Container, Is.EqualTo(ContainerArchetype.TreasureChest));
            Assert.That(resolved[0].Reward, Is.EqualTo(treasure));
        }

        [Test]
        public void RequiredSecretFailsWhenGeometryOffersNoValidHiddenOpening()
        {
            var game = Campaign.Create("required-secret");
            SiteRef cave = game.World.RequireSite("cave", site => site
                .Archetype(SiteArchetype.Cave)
                .RequireCapability(SiteCapability.SecretCandidateHost));

            LootTableRef treasure = game.Loot.Table("treasure", loot => loot
                .RollCount(1, 1)
                .Guaranteed(LootCategory.Currency));

            game.World.Secrets.Policy("required-cache", secret => secret
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .Distribution(new SecretDistribution(1, 1, 10000))
                .RequireHiddenSpace()
                .RewardWith(treasure));

            SecretPolicySpec policy = game.Build().SecretPolicies.Single();
            var visibleRoom = new SecretCandidate(
                new SecretCandidateId("visible-room"),
                cave,
                SecretSpaceKind.HiddenRoom,
                hiddenFromNormalTraversal: false,
                qualityBasisPoints: 9000,
                entrances: new[]
                {
                    FalseWall("wall", true, true, false, true, true)
                });

            Assert.Throws<InvalidOperationException>(() =>
                SecretPlanner.ResolveForSite(policy, cave, new Provider(visibleRoom), 1));
        }

        [Test]
        public void SecretSelectionIsStableForTheSameWorldSeed()
        {
            var game = Campaign.Create("deterministic-secret");
            SiteRef dungeon = game.World.RequireSite("dungeon", site => site
                .Archetype(SiteArchetype.Dungeon)
                .RequireCapability(SiteCapability.SecretCandidateHost));

            LootTableRef treasure = game.Loot.Table("treasure", loot => loot
                .RollCount(1, 1)
                .Guaranteed(LootCategory.Currency));

            game.World.Secrets.Policy("scattered", secret => secret
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .Distribution(new SecretDistribution(0, 2, 5000))
                .RequireHiddenSpace()
                .RewardWith(treasure));

            SecretPolicySpec policy = game.Build().SecretPolicies.Single();
            var a = ValidCandidate("a", dungeon, 5000);
            var b = ValidCandidate("b", dungeon, 5000);

            string first = string.Join(",", SecretPlanner.ResolveForSite(policy, dungeon, new Provider(a, b), 1234)
                .Select(x => x.Candidate.Id));
            string second = string.Join(",", SecretPlanner.ResolveForSite(policy, dungeon, new Provider(a, b), 1234)
                .Select(x => x.Candidate.Id));

            Assert.That(second, Is.EqualTo(first));
        }

        private static SecretCandidate ValidCandidate(string id, SiteRef site, int quality) =>
            new SecretCandidate(
                new SecretCandidateId(id),
                site,
                SecretSpaceKind.CavityBehindWall,
                hiddenFromNormalTraversal: true,
                qualityBasisPoints: quality,
                entrances: new[] { FalseWall(id + "-wall", true, true, false, true, true) });

        private static SecretEntranceCandidate FalseWall(
            string id,
            bool separates,
            bool traversableAfterOpen,
            bool structurallyCritical,
            bool destructible,
            bool matchesSurface) =>
            new SecretEntranceCandidate(
                id,
                SecretEntranceType.DestroyableFalseWall,
                separates,
                traversableAfterOpen,
                structurallyCritical,
                destructible,
                matchesSurface);
    }
}
