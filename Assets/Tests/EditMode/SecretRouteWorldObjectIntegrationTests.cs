using System.Linq;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SecretRouteWorldObjectIntegrationTests
    {
        [Test]
        public void MechanismAndNaturalRoutesShareCanonicalDiscoveryAcrossInteractionAndReload()
        {
            Fixture(out SiteRef approach, out SecretRef secret,
                out ResolvedSecretPlan canonical, out SiteRoleBinding[] sites);
            var natural = new SecretRouteSpec(
                new SecretRouteId("garden-climb"), secret, SecretRouteKind.NaturalTraversal,
                SecretBypassPolicy.SystemicBypassAllowed, "garden-cliff", false,
                new SecretBypassEvidence(false, 0, 0));
            var breakable = new SecretRouteSpec(
                new SecretRouteId("recessed-panel"), secret, SecretRouteKind.BreakableBarrier,
                SecretBypassPolicy.AuthoredBreakablesOnly, "hidden-chamber-panel", true,
                new SecretBypassEvidence(false, 12, 0));
            var anchor = new SecretClueAnchorSpec(
                new SecretClueAnchorId("displaced-stones"), approach,
                SecretClueAnchorRole.ApproachEvidence,
                new[] { SecretClueChannel.Environmental }, true,
                SecretHiddenVolumeRelation.Outside, 1f, 80f);

            SecretDiscoveryPlanningResult planning = SecretDiscoveryPlanner.Resolve(
                164351,
                new SecretDiscoverySpec(secret, SecretImportance.Standard,
                    new[] { natural, breakable }, new[] { anchor }),
                new[] { canonical }, sites);
            Assert.That(planning.IsResolved, Is.True,
                string.Join(" | ", planning.Diagnostics.Select(x => x.ToString())));

            PlannedSecretRoute mechanism = planning.Plan.Routes.Single(x => x.Id.Equals(breakable.Id));
            PlannedSecretRoute traversal = planning.Plan.Routes.Single(x => x.Id.Equals(natural.Id));
            Assert.Multiple(() =>
            {
                Assert.That(mechanism.RequiresInteractable, Is.True);
                Assert.That(mechanism.Kind, Is.EqualTo(SecretRouteKind.BreakableBarrier));
                Assert.That(mechanism.SemanticAnchorRole, Is.EqualTo("hidden-chamber-panel"));
                Assert.That(traversal.RequiresInteractable, Is.False);
                Assert.That(mechanism.Secret, Is.EqualTo(traversal.Secret));
                Assert.That(mechanism.Secret, Is.EqualTo(planning.Plan.Secret));
            });

            const uint parentId = 0x53454352u; // SECR
            var authoring = new WorldObjectAuthoringSession(164351u, parentId);
            WorldObjectId panelId = authoring.Place(
                1u,
                WorldObjectKind.BreakableWall,
                new DecorationBounds
                {
                    Min = new int3(0, 0, 0),
                    MaxExclusive = new int3(4, 5, 1),
                },
                new int3(0, 0, 1));

            var registry = new WorldObjectSceneRegistry();
            WorldObjectGeneratedScene scene = registry.LoadAuthored(
                parentId, authoring.BuildObjects(), authoring.BuildConnections());
            Assert.That(scene.Runtime.TryInteract(
                panelId, WorldObjectInteraction.Primary, out WorldObjectInteractionResult interaction), Is.True);
            Assert.That(interaction.Changed, Is.True);
            Assert.That(scene.Runtime.TryResolve(panelId, out WorldObjectResolvedState openedPanel), Is.True);
            Assert.That(openedPanel.IsDestroyed, Is.True,
                "The planned breakable route must execute through the reusable world-object runtime.");

            var authority = new SecretDiscoveryState();
            var ledger = new SecretDiscoveryLedger(authority);
            int firstDiscoveryEvents = 0;
            authority.Discovered += _ => firstDiscoveryEvents++;

            Assert.That(ledger.Discover(planning.Plan), Is.True,
                "Reaching the secret through the mechanism route must credit the canonical candidate.");
            Assert.That(ledger.Discover(planning.Plan), Is.False,
                "Reaching the same secret through the natural route must not create a second discovery.");
            Assert.That(firstDiscoveryEvents, Is.EqualTo(1));

            WorldObjectStateDelta[] objectSnapshot = registry.Snapshot(parentId);
            SecretDiscoverySnapshot discoverySnapshot = ledger.Capture();
            Assert.That(registry.Unload(parentId), Is.True);
            registry.Restore(parentId, objectSnapshot);
            WorldObjectGeneratedScene restoredScene = registry.LoadAuthored(
                parentId, authoring.BuildObjects(), authoring.BuildConnections());
            Assert.That(restoredScene.Runtime.TryResolve(panelId, out WorldObjectResolvedState restoredPanel), Is.True);
            Assert.That(restoredPanel.IsDestroyed, Is.True,
                "Reusable mechanism state must survive unload/restore without a WorldBuilder-local state machine.");
            Assert.That(restoredScene.Runtime.TryInteract(
                panelId, WorldObjectInteraction.Primary, out _), Is.False,
                "Repeated mechanism activation after restore must not replay the destroyed route.");

            var restoredAuthority = new SecretDiscoveryState();
            var restoredLedger = new SecretDiscoveryLedger(restoredAuthority);
            int restoredDiscoveryEvents = 0;
            restoredAuthority.Discovered += _ => restoredDiscoveryEvents++;
            restoredLedger.Restore(discoverySnapshot);
            Assert.That(restoredLedger.IsDiscovered(planning.Plan), Is.True);
            Assert.That(restoredLedger.Discover(planning.Plan), Is.False,
                "Reload/revisit must preserve the one canonical discovery identity.");
            Assert.That(restoredDiscoveryEvents, Is.EqualTo(0),
                "Restore/revisit must not replay discovery reward events.");
        }

        private static void Fixture(
            out SiteRef approach,
            out SecretRef secret,
            out ResolvedSecretPlan canonical,
            out SiteRoleBinding[] sites)
        {
            var campaign = Campaign.Create("secret-route-world-object-integration");
            RegionHandle region = campaign.World.Region("integration-region");
            SiteRef localApproach = region.Site("approach", SiteArchetype.Ruin);
            SiteRef hidden = region.Site("hidden", SiteArchetype.Ruin,
                x => x.RequireCapability(SiteCapability.SecretCandidateHost));
            LootTableRef reward = campaign.Loot.Table("reward", loot => loot
                .RollCount(1, 1).Guaranteed(LootCategory.Currency));
            SecretRef localSecret = campaign.World.RequireSecret("secret", required => required
                .Inside(hidden)
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .RequireHiddenSpace()
                .RewardWith(reward));

            approach = localApproach;
            secret = localSecret;
            canonical = new ResolvedSecretPlan(
                localSecret, hidden, new SecretCandidateId("generated/hidden-volume"),
                "generated/recessed-panel", ContainerArchetype.TreasureChest, reward);
            sites = new[]
            {
                new SiteRoleBinding(localApproach, new ResolvedSiteId("generated/approach")),
                new SiteRoleBinding(hidden, new ResolvedSiteId("generated/hidden"))
            };
        }
    }
}
