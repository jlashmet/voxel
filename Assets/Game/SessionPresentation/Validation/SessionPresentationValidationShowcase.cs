using System.Collections.Generic;
using Game.Characters.Api;
using Game.Continuity.Api;
using Game.GameplayReplication.Api;
using Game.SessionPresentation.Api;
using Game.SessionPresentation.Runtime;
using Game.Sessions.Api;
using UnityEngine;

namespace Game.SessionPresentation.Validation
{
    public sealed class SessionPresentationValidationShowcase : MonoBehaviour
    {
        private PartyMemberId _local;
        private PartyMemberId _reconnecting;
        private PartyMemberId _third;
        private ValidationSessionQuery _sessions;
        private ValidationContinuityQuery _continuity;
        private ValidationReplicationQuery _replication;
        private SessionPresentationProjector _projector;
        private bool _reconnectLogged;
        private bool _readyLogged;
        private float _startedAt;

        private void Start()
        {
            EnsureValidationCamera();
            _startedAt = Time.unscaledTime;
            _local = new PartyMemberId("party:alpha");
            _reconnecting = new PartyMemberId("party:bravo");
            _third = new PartyMemberId("party:charlie");
            _sessions = new ValidationSessionQuery(
                Member(_local, 0, PartyLeadershipRole.Leader, PartyPresenceState.Connected, "character:alpha"),
                Member(_reconnecting, 1, PartyLeadershipRole.Member, PartyPresenceState.Disconnected, "character:bravo"),
                Member(_third, 2, PartyLeadershipRole.Member, PartyPresenceState.Connected, "character:charlie"));
            _continuity = new ValidationContinuityQuery();
            _continuity.Set(_reconnecting, RecoveryState.Reconnecting);
            _replication = new ValidationReplicationQuery();
            _replication.Set(_local, GameplaySynchronizationPhase.GameplayReady, 10);
            _replication.Set(_reconnecting, GameplaySynchronizationPhase.Synchronizing, 10);
            _replication.Set(_third, GameplaySynchronizationPhase.Synchronizing, 10);
            var app = new ValidationApplicationQuery(4,
                new PartyMemberReadySnapshot(_local, true),
                new PartyMemberReadySnapshot(_reconnecting, true),
                new PartyMemberReadySnapshot(_third, true));
            _projector = new SessionPresentationProjector(_sessions, app, _continuity, _replication);
            PartyScreenPresentationSnapshot initial = _projector.CapturePartyScreen(_local);
            Debug.Log("SESSION_PRESENTATION_VALIDATION ready: members=" + initial.Members.Count + " local=" + _local.Value);
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - _startedAt;
            if (!_reconnectLogged && elapsed >= 3f)
            {
                _sessions.Replace(Member(_reconnecting, 1, PartyLeadershipRole.Member, PartyPresenceState.Connected, "character:bravo"));
                _continuity.Set(_reconnecting, RecoveryState.Recovered);
                _replication.Set(_reconnecting, GameplaySynchronizationPhase.GameplayReady, 11);
                PartyMemberPresentationSnapshot row = Find(_projector.CapturePartyScreen(_local), _reconnecting);
                Debug.Log("SESSION_PRESENTATION_VALIDATION reconnect-stable: member=" + row.MemberId.Value + " slot=" + row.Slot.Value + " character=" + row.CharacterId.Value);
                _reconnectLogged = true;
            }

            if (!_readyLogged && elapsed >= 6f)
            {
                _replication.Set(_third, GameplaySynchronizationPhase.GameplayReady, 12);
                PartyScreenPresentationSnapshot party = _projector.CapturePartyScreen(_local);
                Debug.Log("SESSION_PRESENTATION_VALIDATION ready-to-start: lifecycle=" + party.Lifecycle + " canStart=" + party.CanStart);
                _readyLogged = true;
            }
        }

        private void OnGUI()
        {
            if (_projector == null) return;
            PartyScreenPresentationSnapshot party = _projector.CapturePartyScreen(_local);
            TeammateHudPresentationSnapshot hud = _projector.CaptureTeammateHud(_local);

            GUI.Box(new Rect(36, 30, 760, 430), string.Empty);
            GUI.Label(new Rect(60, 48, 700, 40), "MULTIPLAYER PARTY  •  " + party.Lifecycle);
            GUI.Label(new Rect(60, 88, 700, 30), "Session " + party.SessionId.Value + "   " + party.Members.Count + "/" + party.Capacity + "   Start: " + (party.CanStart ? "AVAILABLE" : "WAITING"));
            for (int i = 0; i < party.Members.Count; i++)
            {
                PartyMemberPresentationSnapshot row = party.Members[i];
                float y = 135 + i * 82;
                string marker = row.IsLocal ? "YOU" : "TEAMMATE";
                GUI.Label(new Rect(60, y, 700, 28), marker + "  •  " + row.Display.PrimaryLabel + "  •  " + row.Display.SecondaryLabel);
                GUI.Label(new Rect(82, y + 28, 680, 24), row.Display.CharacterLabel + "   Presence: " + row.Connection + "   Sync: " + row.Readiness + "   Ready: " + row.ReadyToStart);
            }

            GUI.Box(new Rect(830, 30, 410, 430), string.Empty);
            GUI.Label(new Rect(855, 48, 360, 40), "COMPACT TEAMMATE HUD");
            for (int i = 0; i < hud.Members.Count; i++)
            {
                TeammateStatusSnapshot row = hud.Members[i];
                float y = 102 + i * 92;
                GUI.Label(new Rect(855, y, 350, 28), row.Label + (row.IsLocal ? "  [YOU]" : string.Empty));
                GUI.Label(new Rect(875, y + 30, 330, 24), row.Connection + "  •  " + row.Readiness);
                GUI.Label(new Rect(875, y + 54, 330, 24), "Health ref: " + (row.HasCharacter ? row.CharacterId.Value : "unbound"));
            }
        }

        private static void EnsureValidationCamera()
        {
            if (Camera.main != null) return;
            var cameraObject = new GameObject("Session Presentation Validation Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.07f, 1f);
        }

        private static PartyMemberSnapshot Member(PartyMemberId id, int slot, PartyLeadershipRole role, PartyPresenceState presence, string characterId) =>
            new PartyMemberSnapshot(id, new PlayerSlot(slot), role, presence, presence == PartyPresenceState.Connected ? SessionReadinessState.Synchronized : SessionReadinessState.Joined, new CharacterId(characterId));

        private static PartyMemberPresentationSnapshot Find(PartyScreenPresentationSnapshot party, PartyMemberId id)
        {
            for (int i = 0; i < party.Members.Count; i++) if (party.Members[i].MemberId == id) return party.Members[i];
            return default;
        }

        private sealed class ValidationSessionQuery : IPartySessionQuery
        {
            private readonly List<PartyMemberSnapshot> _members;
            public ValidationSessionQuery(params PartyMemberSnapshot[] members) => _members = new List<PartyMemberSnapshot>(members);
            public void Replace(PartyMemberSnapshot member)
            {
                for (int i = 0; i < _members.Count; i++) if (_members[i].MemberId == member.MemberId) { _members[i] = member; return; }
            }
            public PartyRosterSnapshot Snapshot() => new PartyRosterSnapshot(new GameSessionId("session:validation"), _members.ToArray());
            public bool TryGetMember(PartyMemberId memberId, out PartyMemberSnapshot member)
            {
                for (int i = 0; i < _members.Count; i++) if (_members[i].MemberId == memberId) { member = _members[i]; return true; }
                member = default;
                return false;
            }
        }

        private sealed class ValidationApplicationQuery : IPartySessionApplicationQuery
        {
            private readonly PartyMemberReadySnapshot[] _ready;
            private readonly int _capacity;
            public ValidationApplicationQuery(int capacity, params PartyMemberReadySnapshot[] ready) { _capacity = capacity; _ready = ready; }
            public PartySessionApplicationSnapshot Snapshot() => new PartySessionApplicationSnapshot(_capacity, false, _ready);
        }

        private sealed class ValidationContinuityQuery : IContinuityQuery
        {
            private readonly Dictionary<PartyMemberId, RecoverySnapshot> _states = new Dictionary<PartyMemberId, RecoverySnapshot>();
            public void Set(PartyMemberId memberId, RecoveryState state) => _states[memberId] = new RecoverySnapshot(memberId, state, 0);
            public bool TryGetRecovery(PartyMemberId memberId, out RecoverySnapshot recovery) => _states.TryGetValue(memberId, out recovery);
        }

        private sealed class ValidationReplicationQuery : IGameplayReplicationClientState
        {
            private readonly Dictionary<PartyMemberId, GameplaySynchronizationStatus> _states = new Dictionary<PartyMemberId, GameplaySynchronizationStatus>();
            public void Set(PartyMemberId memberId, GameplaySynchronizationPhase phase, ulong revision) => _states[memberId] = new GameplaySynchronizationStatus(phase, new GameplayRevision(revision));
            public void RequestRecovery(PartyMemberId memberId, GameplayRecoveryMode mode) { }
            public bool TryGetSynchronization(PartyMemberId memberId, out GameplaySynchronizationStatus status) => _states.TryGetValue(memberId, out status);
            public bool TryGetCurrent<TState>(PartyMemberId memberId, out GameplayProjectionSnapshot<TState> snapshot) where TState : struct { snapshot = default; return false; }
        }
    }
}
