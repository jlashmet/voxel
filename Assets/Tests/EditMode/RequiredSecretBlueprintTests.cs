using System.Collections.Generic;
using System.Linq;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class RequiredSecretBlueprintTests
    {
        private sealed class Provider : ISecretCandidateProvider
        {
            private readonly IReadOnlyList<SecretCandidate> _candidates;
            public Provider(params SecretCandidate[] candidates) => _candidates = candidates;
            public IReadOnlyList<SecretCandidate> GetCandidates(SiteRef site) => _candidates;
        }

        [Test]
        public void RequiredSecretCompilesAsHardWorkForExactlyItsHostSite()
        {
            var game = Campaign.Create("required-secret-blueprint");
            SiteRef ruins = game.World.RequireSite("ruins", site => site
                .Archetype(SiteArchetype.Ruin)
                .RequireCapability(SiteCapability.SecretCandidateHost));
            LootTableRef storyLoot = game.Loot.Table("story-loot", loot => loot
                .RollCount(0, 0)
                .Guaranteed(new LootItemId("story.ancient-key"), 1));

            SecretRef secretRef = game.World.RequireSecret("ancient-key-cache", secret => secret
                .Inside(ruins)
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .RequireHiddenSpace()
                .Container(ContainerArchetype.TreasureChest)
                .RewardWith(storyLoot));

            CampaignBlueprint blueprint = game.Build();
            Assert.That(BlueprintValidator.Validate(blueprint).IsValid, Is.True);

            PlanningGraph graph = BlueprintCompiler.Compile(blueprint);
            Assert.That(graph.RequiredSecrets.Count, Is.EqualTo(1));
            RequiredSecretCandidatePlan plan = graph.RequiredSecrets.Single();
            Assert.That(plan.Secret, Is.EqualTo(secretRef));
            Assert.That(plan.Site, Is.EqualTo(ruins));
            Assert.That(plan.RequiresHiddenSpace, Is.True);
            Assert.That(plan.Entrance, Is.EqualTo(SecretEntranceType.DestroyableFalseWall));

            PlanningNode node = graph.Nodes.Single(n => n.Kind == PlanningNodeKind.RequiredSecret);
            Assert.That(node.Dependencies, Does.Contain("site:ruins"));
            Assert.That(node.Dependencies, Does.Contain("loot:story-loot"));
        }

        [Test]
        public void RequiredSecretResolvesOneValidPhysicalCandidateAndPreservesIdentity()
        {
            var game = Campaign.Create("required-secret-resolution");
            SiteRef ruins = game.World.RequireSite("ruins", site => site
                .Archetype(SiteArchetype.Ruin)
                .RequireCapability(SiteCapability.SecretCandidateHost));
            LootTableRef storyLoot = game.Loot.Table("story-loot", loot => loot
                .RollCount(0, 0)
                .Guaranteed(new LootItemId("story.ancient-key"), 1));
            SecretRef secretRef = game.World.RequireSecret("ancient-key-cache", secret => secret
                .Inside(ruins)
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .RequireHiddenSpace()
                .RewardWith(storyLoot));

            RequiredSecretSpec spec = game.Build().RequiredSecrets.Single();
            var valid = new SecretCandidate(
                new SecretCandidateId("west-cavity"),
                ruins,
                SecretSpaceKind.CavityBehindWall,
                true,
                8000,
                new[]
                {
                    new SecretEntranceCandidate(
                        "west-wall", SecretEntranceType.DestroyableFalseWall,
                        true, true, false, true, true)
                });

            ResolvedSecretPlan resolved = SecretPlanner.ResolveRequired(spec, new Provider(valid), 99);
            Assert.That(resolved.SourceKind, Is.EqualTo(SecretResolutionSourceKind.RequiredSecret));
            Assert.That(resolved.RequiredSecret, Is.EqualTo(secretRef));
            Assert.That(resolved.Candidate, Is.EqualTo(valid.Id));
            Assert.That(resolved.Reward, Is.EqualTo(storyLoot));
        }

        [Test]
        public void ValidatorRejectsRequiredSecretOnSiteThatCannotExposeCandidates()
        {
            var game = Campaign.Create("invalid-required-secret");
            SiteRef camp = game.World.RequireSite("camp", site => site.Archetype(SiteArchetype.Camp));
            LootTableRef loot = game.Loot.Table("loot", table => table.RollCount(0, 0));
            game.World.RequireSecret("bad-cache", secret => secret
                .Inside(camp)
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .RequireHiddenSpace()
                .RewardWith(loot));

            BlueprintValidationResult result = BlueprintValidator.Validate(game.Build());
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Any(d => d.Code == "WB2312"), Is.True);
        }
    }
}
