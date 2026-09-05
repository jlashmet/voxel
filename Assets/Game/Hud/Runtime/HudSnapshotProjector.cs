using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Encounters.Api;
using Game.Hud.Api;
using Game.Input.Api;
using Game.Sessions.Api;
using Game.Vitality.Api;

namespace Game.Hud.Runtime
{
    public sealed class HudSnapshotProjector : IHudSnapshotProvider
    {
        private readonly IHudLocalPlayerResolver _localPlayers;
        private readonly IPartySessionQuery _party;
        private readonly IVitalityQuery _vitality;
        private readonly IEncounterQuery _encounters;
        private readonly IHudInteractionSource _interaction;
        private readonly IHudTrackedProgressionSource _progression;
        private readonly IHudTransientEventSource _transients;
        private readonly IInputBindingPresentation _bindings;
        private readonly IInputContextService _contexts;
        private readonly Dictionary<int, HashSet<string>> _consumedTransientIds = new Dictionary<int, HashSet<string>>();

        public HudSnapshotProjector(IHudLocalPlayerResolver localPlayers, IPartySessionQuery party, IVitalityQuery vitality,
            IEncounterQuery encounters, IHudInteractionSource interaction, IHudTrackedProgressionSource progression,
            IHudTransientEventSource transients, IInputBindingPresentation bindings, IInputContextService contexts)
        {
            _localPlayers = localPlayers ?? throw new ArgumentNullException(nameof(localPlayers));
            _party = party ?? throw new ArgumentNullException(nameof(party));
            _vitality = vitality ?? throw new ArgumentNullException(nameof(vitality));
            _encounters = encounters ?? throw new ArgumentNullException(nameof(encounters));
            _interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
            _progression = progression;
            _transients = transients;
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        }

        public HudSnapshot Project(LocalPlayerId localPlayerId)
        {
            HudLocalPlayerIdentity identity = default;
            PartyMemberSnapshot member = default;
            bool hasMember = _localPlayers.TryResolveMember(localPlayerId, out PartyMemberId memberId)
                && _party.TryGetMember(memberId, out member);
            if (hasMember) identity = new HudLocalPlayerIdentity(localPlayerId, member.MemberId, member.CharacterId);

            HudVitalityView vitality = default;
            if (hasMember && member.HasCharacter && _vitality.TryGet(member.CharacterId, out VitalitySnapshot vitalityState))
                vitality = new HudVitalityView(true, vitalityState.Current, vitalityState.Maximum, vitalityState.Defeated);

            InputContextId context = _contexts.ActiveContext;
            HudInteractionPromptView interaction = default;
            bool promptContext = context == InputContextId.Exploration || context == InputContextId.Combat;
            if (promptContext && hasMember && member.HasCharacter
                && _interaction.TryGetCurrent(localPlayerId, member.CharacterId, out HudInteractionCandidate candidate)
                && candidate.IsValid
                && _bindings.TryGetDisplayLabel(localPlayerId, candidate.InputAction, out string binding))
                interaction = new HudInteractionPromptView(true, candidate.TargetId, binding, candidate.ActionText, candidate.CapabilityText);

            HudEncounterView encounter = hasMember && member.HasCharacter ? ProjectEncounter(member.CharacterId) : default;
            HudTrackedProgressionView progression = default;
            if (_progression != null) _progression.TryGetTracked(localPlayerId, out progression);

            return new HudSnapshot(identity, vitality, interaction, encounter, progression,
                ProjectReadiness(hasMember, member), context, DrainNewTransientEvents(localPlayerId));
        }

        public void RebuildAfterReconnect(LocalPlayerId localPlayerId)
        {
            HashSet<string> consumed = GetConsumed(localPlayerId);
            consumed.Clear();
            if (_transients == null) return;
            IReadOnlyList<HudTransientEvent> current = _transients.Snapshot(localPlayerId);
            for (int i = 0; i < current.Count; i++) consumed.Add(current[i].EventId);
        }

        private HudEncounterView ProjectEncounter(CharacterId characterId)
        {
            IReadOnlyList<EncounterSnapshot> all = _encounters.GetAll();
            EncounterSnapshot selected = null;
            for (int i = 0; i < all.Count; i++)
            {
                EncounterSnapshot candidate = all[i];
                if (candidate == null || (candidate.Lifecycle != EncounterLifecycleState.Active && candidate.Lifecycle != EncounterLifecycleState.Resolving)) continue;
                bool member = false;
                for (int p = 0; p < candidate.Membership.Participants.Count; p++)
                    if (candidate.Membership.Participants[p].CharacterId == characterId) { member = true; break; }
                if (!member) continue;
                if (selected == null || candidate.Id.CompareTo(selected.Id) < 0) selected = candidate;
            }
            if (selected == null) return default;
            return new HudEncounterView(true, selected.Id.ToString(), selected.Definition.SemanticKind,
                selected.Lifecycle.ToString(), selected.Definition.CombatPolicy == EncounterCombatPolicy.Required);
        }

        private static HudReadinessState ProjectReadiness(bool hasMember, PartyMemberSnapshot member)
        {
            if (!hasMember) return HudReadinessState.Waiting;
            if (member.Presence == PartyPresenceState.Disconnected) return HudReadinessState.Reconnecting;
            if (member.Readiness == SessionReadinessState.GameplayReady) return HudReadinessState.GameplayReady;
            if (member.Presence == PartyPresenceState.Connected) return HudReadinessState.Resynchronizing;
            return HudReadinessState.Waiting;
        }

        private IReadOnlyList<HudTransientEvent> DrainNewTransientEvents(LocalPlayerId localPlayerId)
        {
            if (_transients == null) return Array.Empty<HudTransientEvent>();
            IReadOnlyList<HudTransientEvent> current = _transients.Snapshot(localPlayerId);
            HashSet<string> consumed = GetConsumed(localPlayerId);
            var fresh = new List<HudTransientEvent>();
            for (int i = 0; i < current.Count; i++)
                if (consumed.Add(current[i].EventId)) fresh.Add(current[i]);
            return fresh;
        }

        private HashSet<string> GetConsumed(LocalPlayerId player)
        {
            if (_consumedTransientIds.TryGetValue(player.Value, out HashSet<string> value)) return value;
            value = new HashSet<string>(StringComparer.Ordinal);
            _consumedTransientIds.Add(player.Value, value);
            return value;
        }
    }
}
