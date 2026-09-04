using System;
using System.Collections.Generic;
using Game.CharacterAI.Api;
using Game.Characters.Api;
using Game.Encounters.Api;
using Game.Residency.Api;
using Game.WorldObjects.Api;

namespace Game.Residency.Runtime
{
    public sealed class CharacterResidencyAdapter : IResidencyTargetAdapter
    {
        private readonly ICharacterQuery _characters;
        private readonly Func<CharacterId, ICharacterAiSimulationFidelity> _aiResolver;
        private readonly Func<CharacterId, ResidencyRegion> _regionResolver;
        private readonly ICharacterKinematicsWriter _kinematics;
        private readonly Func<CharacterId, AiCoarseStateSnapshot, CharacterKinematicState?> _detailedPlacement;

        public CharacterResidencyAdapter(
            ICharacterQuery characters,
            Func<CharacterId, ICharacterAiSimulationFidelity> aiResolver,
            Func<CharacterId, ResidencyRegion> regionResolver,
            ICharacterKinematicsWriter kinematics = null,
            Func<CharacterId, AiCoarseStateSnapshot, CharacterKinematicState?> detailedPlacement = null)
        {
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _aiResolver = aiResolver;
            _regionResolver = regionResolver ?? throw new ArgumentNullException(nameof(regionResolver));
            _kinematics = kinematics;
            _detailedPlacement = detailedPlacement;
            if ((_kinematics == null) != (_detailedPlacement == null))
                throw new ArgumentException("Detailed placement requires both the Character kinematics writer and a semantic placement resolver.");
        }

        public ResidencyTargetKind Kind => ResidencyTargetKind.Character;

        public bool TryGetDetailedRegion(ResidencyTarget target, out ResidencyRegion region)
        {
            CharacterId id = Id(target);
            region = _regionResolver(id);
            return true;
        }

        public ResidencyAdapterResult Promote(ResidencyTarget target, ResidencyFidelity from, ResidencyFidelity to) =>
            Set(target, from, to);

        public ResidencyAdapterResult Demote(ResidencyTarget target, ResidencyFidelity from, ResidencyFidelity to) =>
            Set(target, from, to);

        private ResidencyAdapterResult Set(ResidencyTarget target, ResidencyFidelity from, ResidencyFidelity fidelity)
        {
            CharacterId id = Id(target);
            if (!_characters.TryGet(id, out CharacterSnapshot _))
                return ResidencyAdapterResult.Failed("unknown CharacterId " + id);

            ICharacterAiSimulationFidelity ai = _aiResolver?.Invoke(id);
            if (ai != null && ai.Actor != id)
                return ResidencyAdapterResult.Failed("AI fidelity capability belongs to a different CharacterId.");

            if (from == ResidencyFidelity.Coarse && fidelity == ResidencyFidelity.Detailed &&
                ai != null && _kinematics != null && _detailedPlacement != null &&
                ai.TryGetCoarseState(out AiCoarseStateSnapshot coarse))
            {
                CharacterKinematicState? placement = _detailedPlacement(id, coarse);
                if (placement.HasValue)
                {
                    CharacterRegistryFailure update = _kinematics.UpdateKinematics(id, placement.Value, out CharacterSnapshot _);
                    if (update != CharacterRegistryFailure.None)
                        return ResidencyAdapterResult.Failed("authoritative detailed placement rejected: " + update);
                }
            }

            ai?.SetSimulationFidelity(ToAiFidelity(fidelity));
            return ResidencyAdapterResult.Completed("CharacterId preserved at " + fidelity);
        }

        private static AiSimulationFidelity ToAiFidelity(ResidencyFidelity fidelity)
        {
            switch (fidelity)
            {
                case ResidencyFidelity.Dormant: return AiSimulationFidelity.Dormant;
                case ResidencyFidelity.Coarse: return AiSimulationFidelity.Coarse;
                case ResidencyFidelity.Detailed: return AiSimulationFidelity.Detailed;
                default: throw new ArgumentOutOfRangeException(nameof(fidelity), fidelity, null);
            }
        }

        private static CharacterId Id(ResidencyTarget target)
        {
            if (target.Kind != ResidencyTargetKind.Character)
                throw new ArgumentException("Character adapter received " + target.Kind + ".", nameof(target));
            return new CharacterId(target.Id);
        }
    }

    public sealed class WorldObjectResidencyAdapter : IResidencyTargetAdapter
    {
        private readonly IWorldObjectRegistry _registry;
        private readonly IWorldObjectRealizationLifecycle _realization;
        private readonly Func<WorldObjectId, ResidencyRegion> _regionResolver;

        public WorldObjectResidencyAdapter(
            IWorldObjectRegistry registry,
            IWorldObjectRealizationLifecycle realization,
            Func<WorldObjectId, ResidencyRegion> regionResolver)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _realization = realization ?? throw new ArgumentNullException(nameof(realization));
            _regionResolver = regionResolver ?? throw new ArgumentNullException(nameof(regionResolver));
        }

        public ResidencyTargetKind Kind => ResidencyTargetKind.WorldObject;

        public bool TryGetDetailedRegion(ResidencyTarget target, out ResidencyRegion region)
        {
            WorldObjectId id = Id(target);
            region = _regionResolver(id);
            return true;
        }

        public ResidencyAdapterResult Promote(ResidencyTarget target, ResidencyFidelity from, ResidencyFidelity to)
        {
            WorldObjectId id = Id(target);
            if (!_registry.TryGet(id, out IWorldObjectBehavior _))
                return ResidencyAdapterResult.Failed("unknown WorldObjectId " + id);
            if (to == ResidencyFidelity.Detailed && !_realization.IsRealized(id) && !_realization.TryRealize(id))
                return ResidencyAdapterResult.Failed("WorldObject realization rejected for " + id);
            return ResidencyAdapterResult.Completed();
        }

        public ResidencyAdapterResult Demote(ResidencyTarget target, ResidencyFidelity from, ResidencyFidelity to)
        {
            WorldObjectId id = Id(target);
            if (!_registry.TryGet(id, out IWorldObjectBehavior _))
                return ResidencyAdapterResult.Failed("unknown WorldObjectId " + id);
            if (from == ResidencyFidelity.Detailed && _realization.IsRealized(id) && !_realization.TryUnrealize(id))
                return ResidencyAdapterResult.Failed("WorldObject unrealization rejected for " + id);
            return ResidencyAdapterResult.Completed();
        }

        private static WorldObjectId Id(ResidencyTarget target)
        {
            if (target.Kind != ResidencyTargetKind.WorldObject)
                throw new ArgumentException("WorldObject adapter received " + target.Kind + ".", nameof(target));
            return new WorldObjectId(target.Id);
        }
    }

    /// <summary>Encounter policy bridge. Encounter owns lifecycle; this bridge owns only the demands it creates.</summary>
    public sealed class EncounterResidencyDemandBridge : IDisposable
    {
        private readonly IEncounterRegistry _encounters;
        private readonly IGameplayResidencyCoordinator _residency;
        private readonly Func<EncounterSnapshot, IReadOnlyList<ResidencyTarget>> _targets;
        private readonly Dictionary<EncounterId, List<IResidencyDemandLease>> _leases =
            new Dictionary<EncounterId, List<IResidencyDemandLease>>();
        private bool _disposed;

        public EncounterResidencyDemandBridge(
            IEncounterRegistry encounters,
            IGameplayResidencyCoordinator residency,
            Func<EncounterSnapshot, IReadOnlyList<ResidencyTarget>> targets = null)
        {
            _encounters = encounters ?? throw new ArgumentNullException(nameof(encounters));
            _residency = residency ?? throw new ArgumentNullException(nameof(residency));
            _targets = targets ?? ParticipantTargets;
            _encounters.Changed += OnChanged;
            IReadOnlyList<EncounterSnapshot> existing = _encounters.GetAll();
            for (int i = 0; i < existing.Count; i++) Refresh(existing[i].Id);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _encounters.Changed -= OnChanged;
            var ids = new List<EncounterId>(_leases.Keys);
            ids.Sort();
            for (int i = 0; i < ids.Count; i++) Release(ids[i]);
        }

        private void OnChanged(EncounterEvent evt)
        {
            if (!_disposed) Refresh(evt.EncounterId);
        }

        private void Refresh(EncounterId id)
        {
            Release(id);
            if (!_encounters.TryGet(id, out EncounterSnapshot snapshot)) return;
            if (snapshot.Lifecycle != EncounterLifecycleState.Active &&
                snapshot.Lifecycle != EncounterLifecycleState.Resolving) return;

            IReadOnlyList<ResidencyTarget> targets = _targets(snapshot) ?? Array.Empty<ResidencyTarget>();
            var ordered = new List<ResidencyTarget>(targets);
            ordered.Sort();
            var leases = new List<IResidencyDemandLease>(ordered.Count);
            string requester = "encounter:" + id.Value;
            for (int i = 0; i < ordered.Count; i++)
            {
                leases.Add(_residency.Acquire(new ResidencyDemandRequest(
                    ordered[i],
                    ResidencyFidelity.Detailed,
                    requester,
                    "Encounter",
                    "active encounter requires detailed simulation")));
            }
            _leases[id] = leases;
        }

        private void Release(EncounterId id)
        {
            if (!_leases.TryGetValue(id, out List<IResidencyDemandLease> leases)) return;
            for (int i = 0; i < leases.Count; i++) leases[i].Dispose();
            _leases.Remove(id);
        }

        private static IReadOnlyList<ResidencyTarget> ParticipantTargets(EncounterSnapshot snapshot)
        {
            var result = new ResidencyTarget[snapshot.Membership.Participants.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new ResidencyTarget(
                    ResidencyTargetKind.Character,
                    snapshot.Membership.Participants[i].CharacterId.Value);
            }
            Array.Sort(result);
            return Array.AsReadOnly(result);
        }
    }
}
