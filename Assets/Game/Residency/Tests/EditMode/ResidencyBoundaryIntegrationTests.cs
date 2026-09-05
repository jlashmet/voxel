using System;
using System.Collections.Generic;
using Game.GameplayReplication.Api;
using Game.GameplayReplication.Runtime;
using Game.Residency.Api;
using Game.Residency.Runtime;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace Game.Residency.Tests
{
    public sealed class ResidencyBoundaryIntegrationTests
    {
        [Test]
        public void WorldBuilderScaleUsesStableSemanticIdsAndBoundsDetailedWork()
        {
            string[] firstIds = BuildWorldBuilderNpcIds();
            string[] secondIds = BuildWorldBuilderNpcIds();
            CollectionAssert.AreEqual(firstIds, secondIds, "The same authored fixture must compile to the same semantic NPC ids.");
            Assert.AreEqual(64, firstIds.Length);
            Assert.AreEqual("npc-anchor-important", firstIds[0]);

            using var coordinator = new GameplayResidencyCoordinator(null);
            var leases = new List<IResidencyDemandLease>(firstIds.Length);
            try
            {
                for (int i = 0; i < firstIds.Length; i++)
                {
                    ResidencyFidelity fidelity = i < 4
                        ? ResidencyFidelity.Detailed
                        : i < 16 ? ResidencyFidelity.Coarse : ResidencyFidelity.Dormant;
                    leases.Add(coordinator.Acquire(new ResidencyDemandRequest(
                        new ResidencyTarget(ResidencyTargetKind.Character, firstIds[i]),
                        fidelity,
                        "worldbuilder-scale",
                        "WorldBuilder",
                        "generated semantic fixture")));
                }

                coordinator.Reconcile();
                ResidencyDiagnosticsSnapshot diagnostics = coordinator.GetDiagnostics();
                Assert.AreEqual(48, diagnostics.DormantCount);
                Assert.AreEqual(12, diagnostics.CoarseCount);
                Assert.AreEqual(4, diagnostics.DetailedCount);
                Assert.AreEqual(20, diagnostics.TransitionHistory.Count,
                    "Only explicitly demanded targets should pay promotion transition cost: 12 coarse + 4 two-step detailed promotions.");
                Assert.AreEqual(firstIds.Length, diagnostics.Demands.Count);
            }
            finally
            {
                for (int i = 0; i < leases.Count; i++) leases[i].Dispose();
            }
        }

        [Test]
        public void ReplicationSnapshotLifecycleDoesNotOwnServerResidencyAndLateReaderGetsCurrentTruth()
        {
            ResidencyTarget target = new ResidencyTarget(ResidencyTargetKind.Character, "npc-replication-boundary");
            using var coordinator = new GameplayResidencyCoordinator(null);
            IResidencyDemandLease simulation = coordinator.Acquire(new ResidencyDemandRequest(
                target, ResidencyFidelity.Detailed, "simulation", "Simulation", "authoritative server demand"));
            coordinator.Reconcile();
            AssertCurrent(coordinator, target, ResidencyFidelity.Detailed);

            var source = new MutableProjectionSource(target.Id, "travelling");
            var publisher = new GameplayPublicationBuilder(new IGameplayProjectionSource[] { source });
            var firstClient = new GameplayReplicationReadState(new[] { source.Descriptor });
            Assert.AreEqual(GameplayApplyResult.Applied, firstClient.Apply(publisher.PublishSnapshot()));
            Assert.IsTrue(firstClient.GameplayReady);
            AssertProjection(firstClient, target.Id, "travelling");
            AssertCurrent(coordinator, target, ResidencyFidelity.Detailed);

            simulation.Dispose();
            coordinator.Reconcile();
            AssertCurrent(coordinator, target, ResidencyFidelity.Dormant);

            source.SemanticState = "at-home";
            var lateClient = new GameplayReplicationReadState(new[] { source.Descriptor });
            Assert.AreEqual(GameplayApplyResult.Applied, lateClient.Apply(publisher.PublishSnapshot()));
            Assert.IsTrue(lateClient.GameplayReady);
            AssertProjection(lateClient, target.Id, "at-home");
            AssertCurrent(coordinator, target, ResidencyFidelity.Dormant);
        }

        private static string[] BuildWorldBuilderNpcIds()
        {
            CampaignBuilder campaign = Campaign.Create("residency-scale-fixture");
            RegionHandle region = campaign.World.Region("region-scale", builder => builder.Biome(BiomeFamily.TemperateForest));
            SettlementHandle settlement = region.Town("settlement-scale", builder => builder.Population(64, 64));
            SiteHandle site = settlement.Site("site-common", SiteArchetype.Unspecified);
            var ids = new string[64];
            ids[0] = site.Npc("npc-anchor-important", builder => builder.RequireConversation()).Id;
            for (int i = 1; i < ids.Length; i++)
                ids[i] = site.Npc("npc-generated-" + i.ToString("D3")).Id;

            CampaignBlueprint blueprint = campaign.Build();
            Assert.AreEqual(64, blueprint.Npcs.Count);
            Assert.AreEqual(1, blueprint.Hierarchy.Regions.Count);
            Assert.AreEqual(1, blueprint.Hierarchy.Settlements.Count);
            return ids;
        }

        private static void AssertProjection(GameplayReplicationReadState state, string expectedId, string expectedSemanticState)
        {
            Assert.IsTrue(state.TryGetProjection(MutableProjectionSource.ProjectionId, out GameplayProjectionState projection));
            Assert.AreEqual(2, projection.Entries.Count);
            Assert.AreEqual("id", projection.Entries[0].Key);
            Assert.AreEqual(expectedId, projection.Entries[0].Value);
            Assert.AreEqual("state", projection.Entries[1].Key);
            Assert.AreEqual(expectedSemanticState, projection.Entries[1].Value);
        }

        private static void AssertCurrent(IGameplayResidencyCoordinator coordinator, ResidencyTarget target, ResidencyFidelity expected)
        {
            Assert.IsTrue(coordinator.TryGetState(target, out ResidencyTargetSnapshot snapshot));
            Assert.AreEqual(expected, snapshot.Current);
        }

        private sealed class MutableProjectionSource : IGameplayProjectionSource
        {
            public static readonly GameplayProjectionId ProjectionId = new GameplayProjectionId("residency-boundary-character");
            private readonly string _id;

            public MutableProjectionSource(string id, string semanticState)
            {
                _id = id;
                SemanticState = semanticState;
                Descriptor = new GameplayProjectionDescriptor(ProjectionId, 1, true);
            }

            public GameplayProjectionDescriptor Descriptor { get; }
            public string SemanticState { get; set; }

            public GameplayProjectionState Capture() => new GameplayProjectionState(
                Descriptor,
                new[]
                {
                    new GameplayProjectionEntry("id", _id),
                    new GameplayProjectionEntry("state", SemanticState)
                });
        }
    }
}
