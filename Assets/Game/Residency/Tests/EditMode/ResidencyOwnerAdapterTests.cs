using System;
using System.Collections.Generic;
using Game.CharacterAI.Api;
using Game.CharacterAI.Runtime;
using Game.Characters.Api;
using Game.Characters.Runtime;
using Game.Encounters.Api;
using Game.Encounters.Runtime;
using Game.Residency.Api;
using Game.Residency.Runtime;
using Game.WorldObjects.Api;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Streaming.Api;

namespace Game.Residency.Tests
{
    public sealed class ResidencyOwnerAdapterTests
    {
        [Test]
        public void CharacterCyclesAllFidelitiesWithoutReplacingCharacterIdOrRunningDetailedPerceptionAtCoarse()
        {
            CharacterId id = CharacterId.FromStableKey("npc", "town-worker");
            var registry = new CharacterRegistry();
            Assert.AreEqual(
                CharacterRegistryFailure.None,
                registry.Create(
                    new CharacterDefinition(id, CharacterTraits.ConversationCapable),
                    Kinematics(12, 0, 8),
                    out CharacterSnapshot before));

            var perception = new CountingPerception(id);
            var executor = new CountingExecutor();
            var coarse = new SemanticCoarseCycleSimulation(id, new[] { "Work", "TravelHome", "AtHome" });
            var ai = new CharacterAiController(id, perception, new IdlePolicy(), executor, coarse);
            var pins = new ReadyPins();
            CharacterKinematicState home = Kinematics(40, 0, 40);
            var adapter = new CharacterResidencyAdapter(
                registry,
                actor => actor == id ? ai : null,
                actor => new ResidencyRegion(2, 0, 1, 9u),
                registry,
                (actor, state) => state.SemanticState == "AtHome" ? home : (CharacterKinematicState?)null);
            using var coordinator = new GameplayResidencyCoordinator(
                pins,
                new IResidencyTargetAdapter[] { adapter });
            ResidencyTarget target = new ResidencyTarget(ResidencyTargetKind.Character, id.Value);

            using (IResidencyDemandLease coarseDemand = coordinator.Acquire(
                       Demand(target, ResidencyFidelity.Coarse, "proximity")))
            {
                coordinator.Reconcile();
                ai.Tick();
                ai.Tick();
                Assert.That(ai.TryGetCoarseState(out AiCoarseStateSnapshot state), Is.True);
                Assert.AreEqual("AtHome", state.SemanticState);
                Assert.Zero(perception.Count);
                Assert.Zero(executor.Count);

                using (IResidencyDemandLease control = coordinator.Acquire(
                           Demand(target, ResidencyFidelity.Detailed, "control")))
                {
                    coordinator.Reconcile();
                    Assert.AreEqual(AiSimulationFidelity.Detailed, ai.SimulationFidelity);
                    Assert.That(registry.TryGet(id, out CharacterSnapshot realized), Is.True);
                    Assert.AreEqual(home, realized.Kinematics,
                        "Detailed promotion must restore an authoritative believable placement derived from coarse semantic state through Characters.Api.");
                    ai.Tick();
                    Assert.AreEqual(1, perception.Count);
                    Assert.AreEqual(1, executor.Count);
                }

                coordinator.Reconcile();
                Assert.AreEqual(AiSimulationFidelity.Coarse, ai.SimulationFidelity);
            }

            coordinator.Reconcile();
            Assert.AreEqual(AiSimulationFidelity.Dormant, ai.SimulationFidelity);
            Assert.That(registry.TryGet(id, out CharacterSnapshot after), Is.True);
            Assert.AreEqual(before.Id, after.Id);
            Assert.AreEqual(home, after.Kinematics,
                "Residency does not replace or rewind the authoritative Character state after demotion.");
        }

        [Test]
        public void ChangedWorldObjectStateSurvivesRealizationUnloadReloadWithSameId()
        {
            WorldObjectId id = new WorldObjectId("door:market-east");
            var behavior = new MutableWorldObject(id);
            var registry = new SimpleWorldRegistry(behavior);
            var realization = new RecordingRealization();
            var pins = new ReadyPins();
            var adapter = new WorldObjectResidencyAdapter(
                registry,
                realization,
                ignored => new ResidencyRegion(5, 0, 5, 11u));
            using var coordinator = new GameplayResidencyCoordinator(
                pins,
                new IResidencyTargetAdapter[] { adapter });
            ResidencyTarget target = new ResidencyTarget(ResidencyTargetKind.WorldObject, id.Value);
            behavior.Toggle();
            WorldObjectStateSnapshot changed = behavior.CaptureState();

            IResidencyDemandLease lease = coordinator.Acquire(
                Demand(target, ResidencyFidelity.Detailed, "proximity"));
            coordinator.Reconcile();
            Assert.IsTrue(realization.IsRealized(id));
            lease.Dispose();
            coordinator.Reconcile();
            Assert.IsFalse(realization.IsRealized(id));

            lease = coordinator.Acquire(Demand(target, ResidencyFidelity.Detailed, "proximity"));
            coordinator.Reconcile();
            Assert.IsTrue(registry.TryGet(id, out IWorldObjectBehavior same));
            Assert.AreEqual(id, same.Id);
            Assert.AreEqual(changed.StateCode, same.CaptureState().StateCode);
            Assert.AreEqual(changed.Revision, same.CaptureState().Revision);
            lease.Dispose();
        }

        [Test]
        public void EncounterDemandReleasesOnlyEncounterPinWhileIndependentControlDemandRemains()
        {
            CharacterId id = CharacterId.FromStableKey("npc", "bandit");
            var registry = new CharacterRegistry();
            registry.Create(
                new CharacterDefinition(id, CharacterTraits.Combatant),
                Kinematics(0, 0, 0),
                out _);
            var encounters = new EncounterRegistry(registry);
            EncounterId encounterId = new EncounterId("ambush");
            encounters.Register(
                new EncounterDefinition(encounterId, EncounterCombatPolicy.None, "ambush"),
                out _);
            encounters.Join(
                encounterId,
                new EncounterParticipant(id, EncounterParticipantOwnership.Persistent, "bandit"),
                out _);

            using var coordinator = new GameplayResidencyCoordinator(null);
            ResidencyTarget target = new ResidencyTarget(ResidencyTargetKind.Character, id.Value);
            IResidencyDemandLease control = coordinator.Acquire(
                Demand(target, ResidencyFidelity.Detailed, "control"));
            using var bridge = new EncounterResidencyDemandBridge(encounters, coordinator);

            encounters.Activate(new EncounterActivationRequest(encounterId, "player entered"), out _);
            coordinator.Reconcile();
            Assert.AreEqual(2, coordinator.GetDiagnostics().Demands.Count);
            AssertState(coordinator, target, ResidencyFidelity.Detailed);

            encounters.ResolveWithoutCombat(
                encounterId,
                new EncounterResolution(EncounterResolutionResult.Completed, "escaped"),
                out _);
            coordinator.Reconcile();
            Assert.AreEqual(1, coordinator.GetDiagnostics().Demands.Count);
            AssertState(coordinator, target, ResidencyFidelity.Detailed);

            control.Dispose();
            coordinator.Reconcile();
            AssertState(coordinator, target, ResidencyFidelity.Dormant);
        }

        private static ResidencyDemandRequest Demand(
            ResidencyTarget target,
            ResidencyFidelity fidelity,
            string requester) =>
            new ResidencyDemandRequest(target, fidelity, requester, "test", "fixture");

        private static CharacterKinematicState Kinematics(float x, float y, float z) =>
            new CharacterKinematicState(
                new CharacterVector3(x, y, z),
                new CharacterVector3(0, 0, 0),
                new CharacterVector3(0, 0, 1));

        private static void AssertState(
            IGameplayResidencyCoordinator coordinator,
            ResidencyTarget target,
            ResidencyFidelity expected)
        {
            Assert.IsTrue(coordinator.TryGetState(target, out ResidencyTargetSnapshot state));
            Assert.AreEqual(expected, state.Current);
        }

        private sealed class ReadyPins : IRegionResidencyPins
        {
            public IRegionResidencyLease AcquireResidency(in RegionLoadRequest request) =>
                new ReadyLease(request.RegionCoord);

            private sealed class ReadyLease : IRegionResidencyLease
            {
                public ReadyLease(int3 coord) { RegionCoord = coord; }
                public int3 RegionCoord { get; }
                public bool IsReady => true;
                public void Dispose() { }
            }
        }

        private sealed class CountingPerception : IAiPerceptionSource
        {
            private readonly CharacterId _id;
            public CountingPerception(CharacterId id) { _id = id; }
            public int Count;
            public AiPerceptionSnapshot Observe(CharacterId actor)
            {
                Count++;
                return new AiPerceptionSnapshot(_id, new AiObservation[0]);
            }
        }

        private sealed class IdlePolicy : IAiIntentPolicy
        {
            public AiIntent SelectIntent(AiPerceptionSnapshot p) =>
                new AiIntent(p.Actor, AiIntentKind.Idle, default, "", 0, "idle");
        }

        private sealed class CountingExecutor : IAiIntentExecutor
        {
            public int Count;
            public AiIntentExecutionResult TryExecute(AiIntent intent)
            {
                Count++;
                return AiIntentExecutionResult.Accept();
            }
        }

        private sealed class MutableWorldObject : IWorldObjectBehavior
        {
            private int _state;
            private ulong _revision = 1;
            public MutableWorldObject(WorldObjectId id) { Id = id; }
            public WorldObjectId Id { get; }
            public WorldObjectKind Kind => WorldObjectKind.DoorToggle;
            public CharacterVector3 Position => new CharacterVector3(0, 0, 0);
            public void Toggle() { _state = _state == 0 ? 1 : 0; _revision++; }
            public WorldInteractionResult Interact(WorldInteractionContext context)
            {
                Toggle();
                return WorldInteractionResult.Success();
            }
            public WorldObjectStateSnapshot CaptureState() =>
                new WorldObjectStateSnapshot(Id, Kind, true, _state, _revision);
            public WorldInteractionResult RestoreState(WorldObjectStateSnapshot snapshot)
            {
                if (snapshot.ObjectId != Id)
                    return WorldInteractionResult.Reject(WorldInteractionFailure.InvalidState);
                _state = snapshot.StateCode;
                _revision = snapshot.Revision;
                return WorldInteractionResult.Success();
            }
        }

        private sealed class SimpleWorldRegistry : IWorldObjectRegistry
        {
            private readonly Dictionary<WorldObjectId, IWorldObjectBehavior> _items =
                new Dictionary<WorldObjectId, IWorldObjectBehavior>();

            public SimpleWorldRegistry(params IWorldObjectBehavior[] items)
            {
                for (int i = 0; i < items.Length; i++) _items.Add(items[i].Id, items[i]);
            }

            public bool TryRegister(IWorldObjectBehavior behavior)
            {
                if (_items.ContainsKey(behavior.Id)) return false;
                _items.Add(behavior.Id, behavior);
                return true;
            }

            public bool TryGet(WorldObjectId id, out IWorldObjectBehavior behavior) =>
                _items.TryGetValue(id, out behavior);

            public IReadOnlyList<IWorldObjectBehavior> GetAt(CharacterVector3 p) =>
                new List<IWorldObjectBehavior>(_items.Values).AsReadOnly();

            public IReadOnlyList<WorldObjectStateSnapshot> CaptureState()
            {
                var result = new List<WorldObjectStateSnapshot>();
                foreach (IWorldObjectBehavior value in _items.Values)
                    result.Add(value.CaptureState());
                result.Sort((a, b) => a.ObjectId.CompareTo(b.ObjectId));
                return result.AsReadOnly();
            }

            public WorldInteractionResult RestoreState(
                IReadOnlyList<WorldObjectStateSnapshot> snapshots)
            {
                for (int i = 0; i < snapshots.Count; i++)
                {
                    if (_items.TryGetValue(
                            snapshots[i].ObjectId,
                            out IWorldObjectBehavior value))
                        value.RestoreState(snapshots[i]);
                }
                return WorldInteractionResult.Success();
            }
        }

        private sealed class RecordingRealization : IWorldObjectRealizationLifecycle
        {
            private readonly HashSet<WorldObjectId> _realized = new HashSet<WorldObjectId>();
            public bool IsRealized(WorldObjectId id) => _realized.Contains(id);
            public bool TryRealize(WorldObjectId id) { _realized.Add(id); return true; }
            public bool TryUnrealize(WorldObjectId id) { _realized.Remove(id); return true; }
        }
    }
}
