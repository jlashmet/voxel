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
            Assert.AreEqual(CharacterRegistryFailure.None,
                registry.Create(new CharacterDefinition(id, CharacterTraits.ConversationCapable), Kinematics(12, 0, 8), out CharacterSnapshot before));

            var perception = new CountingPerception(id);
            var executor = new CountingExecutor();
            var coarse = new SemanticCoarseCycleSimulation(id, new[] { "Work", "TravelHome", "AtHome" });
            var ai = new CharacterAiController(id, perception, new IdlePolicy(), executor, coarse);
            CharacterKinematicState home = Kinematics(40, 0, 40);
            var adapter = new CharacterResidencyAdapter(
                registry,
                actor => actor == id ? ai : null,
                actor => new ResidencyRegion(2, 0, 1, 9u),
                registry,
                (actor, state) => state.SemanticState == "AtHome" ? home : (CharacterKinematicState?)null);
            using var coordinator = new GameplayResidencyCoordinator(new ReadyPins(), new IResidencyTargetAdapter[] { adapter });
            ResidencyTarget target = Character(id);

            using (IResidencyDemandLease coarseDemand = coordinator.Acquire(Demand(target, ResidencyFidelity.Coarse, "proximity")))
            {
                coordinator.Reconcile();
                ai.Tick();
                ai.Tick();
                Assert.That(ai.TryGetCoarseState(out AiCoarseStateSnapshot state), Is.True);
                Assert.AreEqual("AtHome", state.SemanticState);
                Assert.Zero(perception.Count, "Coarse AI must not run detailed perception.");
                Assert.Zero(executor.Count, "Coarse AI must not run detailed intent execution.");

                using (coordinator.Acquire(Demand(target, ResidencyFidelity.Detailed, "control")))
                {
                    coordinator.Reconcile();
                    Assert.AreEqual(AiSimulationFidelity.Detailed, ai.SimulationFidelity);
                    Assert.That(registry.TryGet(id, out CharacterSnapshot realized), Is.True);
                    Assert.AreEqual(home, realized.Kinematics);
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
            Assert.AreEqual(home, after.Kinematics, "Residency must not replace or rewind authoritative Character state.");
        }

        [Test]
        public void TwoCharactersCanHoldDifferentFidelityWithoutGlobalTownLoadedSwitch()
        {
            CharacterId nearId = CharacterId.FromStableKey("npc", "near");
            CharacterId farId = CharacterId.FromStableKey("npc", "far");
            var registry = new CharacterRegistry();
            registry.Create(new CharacterDefinition(nearId, CharacterTraits.ConversationCapable), Kinematics(0, 0, 0), out _);
            registry.Create(new CharacterDefinition(farId, CharacterTraits.ConversationCapable), Kinematics(100, 0, 100), out _);
            var nearAi = new CharacterAiController(nearId, new CountingPerception(nearId), new IdlePolicy(), new CountingExecutor(), new SemanticCoarseCycleSimulation(nearId, new[] { "Work" }));
            var farAi = new CharacterAiController(farId, new CountingPerception(farId), new IdlePolicy(), new CountingExecutor(), new SemanticCoarseCycleSimulation(farId, new[] { "Work" }));
            var adapter = new CharacterResidencyAdapter(registry, id => id == nearId ? nearAi : farAi, id => new ResidencyRegion(id == nearId ? 0 : 8, 0, 0, 7u));
            using var coordinator = new GameplayResidencyCoordinator(new ReadyPins(), new IResidencyTargetAdapter[] { adapter });
            using IResidencyDemandLease near = coordinator.Acquire(Demand(Character(nearId), ResidencyFidelity.Detailed, "proximity-near"));
            using IResidencyDemandLease far = coordinator.Acquire(Demand(Character(farId), ResidencyFidelity.Coarse, "background-life"));
            coordinator.Reconcile();

            AssertState(coordinator, Character(nearId), ResidencyFidelity.Detailed);
            AssertState(coordinator, Character(farId), ResidencyFidelity.Coarse);
            Assert.AreEqual(AiSimulationFidelity.Detailed, nearAi.SimulationFidelity);
            Assert.AreEqual(AiSimulationFidelity.Coarse, farAi.SimulationFidelity);
        }

        [Test]
        public void ChangedWorldObjectStateSurvivesRealizationUnloadReloadWithSameId()
        {
            WorldObjectId id = new WorldObjectId("door:market-east");
            var behavior = new MutableWorldObject(id);
            var registry = new SimpleWorldRegistry(behavior);
            var realization = new RecordingRealization();
            var adapter = new WorldObjectResidencyAdapter(registry, realization, ignored => new ResidencyRegion(5, 0, 5, 11u));
            using var coordinator = new GameplayResidencyCoordinator(new ReadyPins(), new IResidencyTargetAdapter[] { adapter });
            ResidencyTarget target = WorldObject(id);
            behavior.Toggle();
            WorldObjectStateSnapshot changed = behavior.CaptureState();

            IResidencyDemandLease lease = coordinator.Acquire(Demand(target, ResidencyFidelity.Detailed, "proximity"));
            coordinator.Reconcile();
            Assert.IsTrue(realization.IsRealized(target));
            lease.Dispose();
            coordinator.Reconcile();
            Assert.IsFalse(realization.IsRealized(target));

            lease = coordinator.Acquire(Demand(target, ResidencyFidelity.Detailed, "proximity"));
            coordinator.Reconcile();
            Assert.IsTrue(registry.TryGet(id, out IWorldObjectBehavior same));
            Assert.AreEqual(id, same.Id);
            Assert.AreEqual(changed.StateCode, same.CaptureState().StateCode);
            Assert.AreEqual(changed.Revision, same.CaptureState().Revision);
            lease.Dispose();
        }

        [Test]
        public void UnchangedWorldObjectsRequireNoResidencyRetainedState()
        {
            var first = new MutableWorldObject(new WorldObjectId("door:a"));
            var second = new MutableWorldObject(new WorldObjectId("door:b"));
            var registry = new SimpleWorldRegistry(first, second);
            using var coordinator = new GameplayResidencyCoordinator(new ReadyPins(), new IResidencyTargetAdapter[]
            {
                new WorldObjectResidencyAdapter(registry, new RecordingRealization(), ignored => new ResidencyRegion(0, 0, 0, 1u))
            });

            Assert.AreEqual(0, coordinator.GetDiagnostics().Demands.Count);
            Assert.AreEqual(2, registry.CaptureState().Count, "WorldObject authority owns semantic definitions/state independently of Residency.");
        }

        [Test]
        public void EncounterDemandReleasesOnlyEncounterPinWhileIndependentControlDemandRemains()
        {
            CharacterId id = CharacterId.FromStableKey("npc", "bandit");
            var registry = new CharacterRegistry();
            registry.Create(new CharacterDefinition(id, CharacterTraits.Combatant), Kinematics(0, 0, 0), out _);
            var encounters = new EncounterRegistry(registry);
            EncounterId encounterId = new EncounterId("ambush");
            encounters.Register(new EncounterDefinition(encounterId, EncounterCombatPolicy.None, "ambush"), out _);
            encounters.Join(encounterId, new EncounterParticipant(id, EncounterParticipantOwnership.Persistent, "bandit"), out _);

            using var coordinator = new GameplayResidencyCoordinator(null);
            ResidencyTarget target = Character(id);
            IResidencyDemandLease control = coordinator.Acquire(Demand(target, ResidencyFidelity.Detailed, "control"));
            using var bridge = new EncounterResidencyDemandBridge(encounters, coordinator);

            encounters.Activate(new EncounterActivationRequest(encounterId, "player entered"), out _);
            coordinator.Reconcile();
            Assert.AreEqual(2, coordinator.GetDiagnostics().Demands.Count);
            AssertState(coordinator, target, ResidencyFidelity.Detailed);

            encounters.ResolveWithoutCombat(encounterId, new EncounterResolution(EncounterResolutionResult.Completed, "escaped"), out _);
            coordinator.Reconcile();
            Assert.AreEqual(1, coordinator.GetDiagnostics().Demands.Count);
            AssertState(coordinator, target, ResidencyFidelity.Detailed);

            control.Dispose();
            coordinator.Reconcile();
            AssertState(coordinator, target, ResidencyFidelity.Dormant);
        }

        private static ResidencyTarget Character(CharacterId id) => new ResidencyTarget(ResidencyTargetKind.Character, id.Value);
        private static ResidencyTarget WorldObject(WorldObjectId id) => new ResidencyTarget(ResidencyTargetKind.WorldObject, id.Value);
        private static ResidencyDemandRequest Demand(ResidencyTarget target, ResidencyFidelity fidelity, string requester) => new ResidencyDemandRequest(target, fidelity, requester, "test", "fixture");
        private static CharacterKinematicState Kinematics(float x, float y, float z) => new CharacterKinematicState(new CharacterVector3(x, y, z), new CharacterVector3(0, 0, 0), new CharacterVector3(0, 0, 1));

        private static void AssertState(IGameplayResidencyCoordinator coordinator, ResidencyTarget target, ResidencyFidelity expected)
        {
            Assert.IsTrue(coordinator.TryGetState(target, out ResidencyTargetSnapshot state));
            Assert.AreEqual(expected, state.Current);
        }

        private sealed class ReadyPins : IRegionResidencyPins
        {
            public IRegionResidencyLease AcquireResidency(in RegionLoadRequest request) => new ReadyLease(request.RegionCoord);
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
            public AiPerceptionSnapshot Observe(CharacterId actor) { Count++; return new AiPerceptionSnapshot(_id, new AiObservation[0]); }
        }

        private sealed class IdlePolicy : IAiIntentPolicy
        {
            public AiIntent SelectIntent(AiPerceptionSnapshot p) => new AiIntent(p.Actor, AiIntentKind.Idle, default, "", 0, "idle");
        }

        private sealed class CountingExecutor : IAiIntentExecutor
        {
            public int Count;
            public AiIntentExecutionResult TryExecute(AiIntent intent) { Count++; return AiIntentExecutionResult.Accept(); }
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
            public WorldInteractionResult Interact(WorldInteractionContext context) { Toggle(); return WorldInteractionResult.Success(); }
            public WorldObjectStateSnapshot CaptureState() => new WorldObjectStateSnapshot(Id, Kind, true, _state, _revision);
            public WorldInteractionResult RestoreState(WorldObjectStateSnapshot snapshot)
            {
                if (snapshot.ObjectId != Id) return WorldInteractionResult.Reject(WorldInteractionFailure.InvalidState);
                _state = snapshot.StateCode; _revision = snapshot.Revision; return WorldInteractionResult.Success();
            }
        }

        private sealed class SimpleWorldRegistry : IWorldObjectRegistry
        {
            private readonly Dictionary<WorldObjectId, IWorldObjectBehavior> _items = new Dictionary<WorldObjectId, IWorldObjectBehavior>();
            public SimpleWorldRegistry(params IWorldObjectBehavior[] items) { for (int i = 0; i < items.Length; i++) _items.Add(items[i].Id, items[i]); }
            public bool TryRegister(IWorldObjectBehavior behavior) { if (_items.ContainsKey(behavior.Id)) return false; _items.Add(behavior.Id, behavior); return true; }
            public bool TryGet(WorldObjectId id, out IWorldObjectBehavior behavior) => _items.TryGetValue(id, out behavior);
            public IReadOnlyList<IWorldObjectBehavior> GetAt(CharacterVector3 p) => new List<IWorldObjectBehavior>(_items.Values).AsReadOnly();
            public IReadOnlyList<WorldObjectStateSnapshot> CaptureState()
            {
                var result = new List<WorldObjectStateSnapshot>();
                foreach (IWorldObjectBehavior value in _items.Values) result.Add(value.CaptureState());
                result.Sort((a, b) => a.ObjectId.CompareTo(b.ObjectId));
                return result.AsReadOnly();
            }
            public WorldInteractionResult RestoreState(IReadOnlyList<WorldObjectStateSnapshot> snapshots)
            {
                for (int i = 0; i < snapshots.Count; i++) if (_items.TryGetValue(snapshots[i].ObjectId, out IWorldObjectBehavior value)) value.RestoreState(snapshots[i]);
                return WorldInteractionResult.Success();
            }
        }

        private sealed class RecordingRealization : IResidencyTargetRealizationLifecycle
        {
            private readonly HashSet<ResidencyTarget> _realized = new HashSet<ResidencyTarget>();
            public bool IsRealized(ResidencyTarget target) => _realized.Contains(target);
            public bool TryRealize(ResidencyTarget target) { _realized.Add(target); return true; }
            public bool TryUnrealize(ResidencyTarget target) { _realized.Remove(target); return true; }
        }
    }
}
