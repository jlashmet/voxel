using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Composition.Kentridge.Playable;
using Game.Hud.Api;
using Game.Hud.Runtime;
using Game.Input.Api;
using Game.Input.Runtime;
using Game.Sessions.Api;
using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Kentridge-only composition adapter for the reusable production HUD. All projected state comes
    /// from the canonical character/session-extension services created by the System14 production graph.
    /// This component owns no gameplay state and exposes only semantic input actions to the scene driver.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class KentridgeGameplayHudInstaller : MonoBehaviour
    {
        private static readonly LocalPlayerId LocalPlayer = new LocalPlayerId(0);
        private static readonly PartyMemberId LocalMember = new PartyMemberId("kentridge.local-player");
        private static readonly GameSessionId SessionId = new GameSessionId("local-player-session");
        private static readonly CharacterBinding PlayerSlotBinding = new CharacterBinding("player-slot", "0");

        private UnityInputBindingService _bindings;
        private GameplayHudPresenter _presenter;
        private bool _configured;

        public IInputBindingPresentation BindingPresentation => _bindings;

        private void Awake()
        {
            _bindings = new UnityInputBindingService();
        }

        private void OnEnable()
        {
            _configured = false;
        }

        private void Update()
        {
            if (!_configured) TryConfigure();
        }

        private void OnDisable()
        {
            _presenter?.Clear();
            _configured = false;
        }

        public bool WasPressed(InputActionId action) =>
            _bindings != null && _bindings.WasPressed(LocalPlayer, action);

        public bool IsHeld(InputActionId action) =>
            _bindings != null && _bindings.IsHeld(LocalPlayer, action);

        private void TryConfigure()
        {
            KentridgePlayableSlice slice = GetComponent<KentridgePlayableSlice>();
            KentridgeForestBanditEncounter gameplay = GetComponent<KentridgeForestBanditEncounter>();
            KentridgeCharacterRegistryAnchor anchor = GetComponent<KentridgeCharacterRegistryAnchor>();
            if (slice == null || gameplay == null || anchor == null || anchor.Characters == null) return;
            if (!gameplay.GameplayBindingsReady || !slice.OpeningCutsceneStarted) return;
            if (gameplay.VitalityQuery == null || gameplay.EncounterQuery == null || gameplay.InputContexts == null) return;

            var party = new KentridgeHudPartyQuery(slice, anchor.Characters);
            var projector = new HudSnapshotProjector(
                new KentridgeLocalPlayerResolver(),
                party,
                gameplay.VitalityQuery,
                gameplay.EncounterQuery,
                new KentridgeInteractionSource(slice, anchor.Characters),
                progression: null,
                transients: null,
                _bindings,
                gameplay.InputContexts);

            _presenter = GetComponent<GameplayHudPresenter>() ?? gameObject.AddComponent<GameplayHudPresenter>();
            _presenter.Configure(projector, LocalPlayer);
            _configured = true;
        }

        private sealed class KentridgeLocalPlayerResolver : IHudLocalPlayerResolver
        {
            public bool TryResolveMember(LocalPlayerId localPlayerId, out PartyMemberId memberId)
            {
                if (localPlayerId.Equals(LocalPlayer))
                {
                    memberId = LocalMember;
                    return true;
                }
                memberId = default;
                return false;
            }
        }

        private sealed class KentridgeHudPartyQuery : IPartySessionQuery
        {
            private readonly KentridgePlayableSlice _slice;
            private readonly ICharacterQuery _characters;

            public KentridgeHudPartyQuery(KentridgePlayableSlice slice, ICharacterQuery characters)
            {
                _slice = slice ?? throw new ArgumentNullException(nameof(slice));
                _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            }

            public PartyRosterSnapshot Snapshot()
            {
                PartyMemberSnapshot member = CurrentMember();
                return new PartyRosterSnapshot(SessionId, new[] { member });
            }

            public bool TryGetMember(PartyMemberId memberId, out PartyMemberSnapshot member)
            {
                if (memberId != LocalMember)
                {
                    member = default;
                    return false;
                }
                member = CurrentMember();
                return true;
            }

            private PartyMemberSnapshot CurrentMember()
            {
                _characters.TryResolve(PlayerSlotBinding, out CharacterId characterId);
                SessionReadinessState readiness = _slice.OpeningCutsceneStarted
                    ? SessionReadinessState.GameplayReady
                    : _slice.OpeningPresentationReady
                        ? SessionReadinessState.Synchronized
                        : SessionReadinessState.Connected;
                return new PartyMemberSnapshot(
                    LocalMember,
                    new PlayerSlot(0),
                    PartyLeadershipRole.Leader,
                    PartyPresenceState.Connected,
                    readiness,
                    characterId);
            }
        }

        private sealed class KentridgeInteractionSource : IHudInteractionSource
        {
            private readonly KentridgePlayableSlice _slice;
            private readonly ICharacterQuery _characters;

            public KentridgeInteractionSource(KentridgePlayableSlice slice, ICharacterQuery characters)
            {
                _slice = slice ?? throw new ArgumentNullException(nameof(slice));
                _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            }

            public bool TryGetCurrent(
                LocalPlayerId localPlayerId,
                CharacterId characterId,
                out HudInteractionCandidate candidate)
            {
                candidate = default;
                if (!localPlayerId.Equals(LocalPlayer) || !characterId.IsValid || !_slice.GameplayControlEnabled)
                    return false;
                if (!_characters.TryGet(characterId, out CharacterSnapshot player)) return false;

                IReadOnlyList<CharacterSnapshot> all = _characters.GetAll();
                CharacterSnapshot selected = null;
                float bestDistanceSquared = Mathf.Max(0f, _slice.InteractionRangeMetres);
                bestDistanceSquared *= bestDistanceSquared;
                for (int i = 0; i < all.Count; i++)
                {
                    CharacterSnapshot other = all[i];
                    if (other == null || other.Id == characterId || other.Lifecycle != CharacterLifecycleState.Active) continue;
                    if (!other.Definition.HasTrait(CharacterTraits.ConversationCapable)) continue;
                    float distanceSquared = DistanceSquared(player.Kinematics.Position, other.Kinematics.Position);
                    if (distanceSquared > bestDistanceSquared) continue;
                    bestDistanceSquared = distanceSquared;
                    selected = other;
                }

                if (selected == null) return false;
                candidate = new HudInteractionCandidate(
                    selected.Id.ToString(),
                    "Talk",
                    "Conversation",
                    StandardInputActions.Interact);
                return true;
            }

            private static float DistanceSquared(CharacterVector3 a, CharacterVector3 b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                float dz = a.Z - b.Z;
                return dx * dx + dy * dy + dz * dz;
            }
        }
    }
}
