using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Input.Api;
using Game.Sessions.Api;

namespace Game.Hud.Api
{
    public enum HudReadinessState : byte
    {
        Waiting = 0,
        Reconnecting = 1,
        Resynchronizing = 2,
        GameplayReady = 3
    }

    public readonly struct HudLocalPlayerIdentity
    {
        public LocalPlayerId LocalPlayerId { get; }
        public PartyMemberId MemberId { get; }
        public CharacterId CharacterId { get; }
        public bool HasCharacter => CharacterId.IsValid;

        public HudLocalPlayerIdentity(LocalPlayerId localPlayerId, PartyMemberId memberId, CharacterId characterId)
        {
            if (!memberId.IsValid) throw new ArgumentException("Party member id is required.", nameof(memberId));
            LocalPlayerId = localPlayerId;
            MemberId = memberId;
            CharacterId = characterId;
        }
    }

    public readonly struct HudVitalityView
    {
        public bool Visible { get; }
        public int Current { get; }
        public int Maximum { get; }
        public bool Defeated { get; }
        public HudVitalityView(bool visible, int current, int maximum, bool defeated)
        { Visible = visible; Current = current; Maximum = maximum; Defeated = defeated; }
    }

    public readonly struct HudInteractionCandidate
    {
        public string TargetId { get; }
        public string ActionText { get; }
        public string CapabilityText { get; }
        public InputActionId InputAction { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(TargetId) && !string.IsNullOrWhiteSpace(ActionText) && InputAction.IsValid;

        public HudInteractionCandidate(string targetId, string actionText, string capabilityText, InputActionId inputAction)
        {
            TargetId = targetId ?? string.Empty;
            ActionText = actionText ?? string.Empty;
            CapabilityText = capabilityText ?? string.Empty;
            InputAction = inputAction;
        }
    }

    public readonly struct HudInteractionPromptView
    {
        public bool Visible { get; }
        public string TargetId { get; }
        public string BindingLabel { get; }
        public string ActionText { get; }
        public string CapabilityText { get; }
        public HudInteractionPromptView(bool visible, string targetId, string bindingLabel, string actionText, string capabilityText)
        { Visible = visible; TargetId = targetId ?? string.Empty; BindingLabel = bindingLabel ?? string.Empty; ActionText = actionText ?? string.Empty; CapabilityText = capabilityText ?? string.Empty; }
    }

    public readonly struct HudEncounterView
    {
        public bool Visible { get; }
        public string EncounterId { get; }
        public string SemanticKind { get; }
        public string Lifecycle { get; }
        public bool CombatRequired { get; }
        public HudEncounterView(bool visible, string encounterId, string semanticKind, string lifecycle, bool combatRequired)
        { Visible = visible; EncounterId = encounterId ?? string.Empty; SemanticKind = semanticKind ?? string.Empty; Lifecycle = lifecycle ?? string.Empty; CombatRequired = combatRequired; }
    }

    public readonly struct HudTrackedProgressionView
    {
        public bool Visible { get; }
        public string StableId { get; }
        public string Label { get; }
        public string ProgressText { get; }
        public HudTrackedProgressionView(bool visible, string stableId, string label, string progressText)
        { Visible = visible; StableId = stableId ?? string.Empty; Label = label ?? string.Empty; ProgressText = progressText ?? string.Empty; }
    }

    public readonly struct HudTransientEvent
    {
        public string EventId { get; }
        public string Text { get; }
        public HudTransientEvent(string eventId, string text)
        {
            if (string.IsNullOrWhiteSpace(eventId)) throw new ArgumentException("Transient event id is required.", nameof(eventId));
            EventId = eventId;
            Text = text ?? string.Empty;
        }
    }

    public sealed class HudSnapshot
    {
        private readonly HudTransientEvent[] _transientEvents;
        public HudLocalPlayerIdentity Identity { get; }
        public HudVitalityView Vitality { get; }
        public HudInteractionPromptView Interaction { get; }
        public HudEncounterView Encounter { get; }
        public HudTrackedProgressionView TrackedProgression { get; }
        public HudReadinessState Readiness { get; }
        public InputContextId InputContext { get; }
        public IReadOnlyList<HudTransientEvent> TransientEvents => _transientEvents;

        public HudSnapshot(HudLocalPlayerIdentity identity, HudVitalityView vitality, HudInteractionPromptView interaction,
            HudEncounterView encounter, HudTrackedProgressionView trackedProgression, HudReadinessState readiness,
            InputContextId inputContext, IReadOnlyList<HudTransientEvent> transientEvents)
        {
            Identity = identity; Vitality = vitality; Interaction = interaction; Encounter = encounter;
            TrackedProgression = trackedProgression; Readiness = readiness; InputContext = inputContext;
            if (transientEvents == null) throw new ArgumentNullException(nameof(transientEvents));
            _transientEvents = new HudTransientEvent[transientEvents.Count];
            for (int i = 0; i < transientEvents.Count; i++) _transientEvents[i] = transientEvents[i];
        }
    }

    public interface IHudLocalPlayerResolver
    {
        bool TryResolveMember(LocalPlayerId localPlayerId, out PartyMemberId memberId);
    }

    public interface IHudInteractionSource
    {
        bool TryGetCurrent(LocalPlayerId localPlayerId, CharacterId characterId, out HudInteractionCandidate candidate);
    }

    public interface IHudTrackedProgressionSource
    {
        bool TryGetTracked(LocalPlayerId localPlayerId, out HudTrackedProgressionView tracked);
    }

    public interface IHudTransientEventSource
    {
        IReadOnlyList<HudTransientEvent> Snapshot(LocalPlayerId localPlayerId);
    }

    public interface IHudSnapshotProvider
    {
        HudSnapshot Project(LocalPlayerId localPlayerId);
        void RebuildAfterReconnect(LocalPlayerId localPlayerId);
    }
}
