using System;
using System.Collections.Generic;
using Game.Application.Api;
using Game.Application.Runtime;
using Game.Persistence.Api;
using Game.SessionOrchestration.Api;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using VoxelEngine.Net.Runtime.Client;

namespace Game.Composition.Kentridge.Playable
{
    public sealed class KentridgeMultiplayerApplicationDependencies
    {
        public KentridgeMultiplayerApplicationDependencies(
            ISessionSaveCatalog saves,
            Game.Outcomes.Api.IGameOutcomeQuery outcomes,
            Game.Input.Api.IInputContextService inputContexts,
            Game.Input.Api.IInputBindingOverrideService inputBindings,
            IUserPreferencesStore preferences,
            IAudioPreferencesSink audio,
            IApplicationExitPort exit)
        {
            Saves = saves ?? throw new ArgumentNullException(nameof(saves));
            Outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
            InputContexts = inputContexts ?? throw new ArgumentNullException(nameof(inputContexts));
            InputBindings = inputBindings ?? throw new ArgumentNullException(nameof(inputBindings));
            Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            Audio = audio ?? throw new ArgumentNullException(nameof(audio));
            Exit = exit ?? throw new ArgumentNullException(nameof(exit));
        }

        public ISessionSaveCatalog Saves { get; }
        public Game.Outcomes.Api.IGameOutcomeQuery Outcomes { get; }
        public Game.Input.Api.IInputContextService InputContexts { get; }
        public Game.Input.Api.IInputBindingOverrideService InputBindings { get; }
        public IUserPreferencesStore Preferences { get; }
        public IAudioPreferencesSink Audio { get; }
        public IApplicationExitPort Exit { get; }
    }

    public sealed class KentridgeMultiplayerSessionPlanProvider : IApplicationSessionPlanProvider
    {
        private readonly string _campaignId;
        private readonly string _worldId;
        private readonly string _configurationId;

        public KentridgeMultiplayerSessionPlanProvider(string campaignId, string worldId, string configurationId)
        {
            _campaignId = Require(campaignId, nameof(campaignId));
            _worldId = Require(worldId, nameof(worldId));
            _configurationId = Require(configurationId, nameof(configurationId));
        }

        public GameSessionStartRequest PlanNewGame(ApplicationSessionDescriptor descriptor) =>
            GameSessionStartRequest.NewGame(new GameSessionIdentity(
                descriptor.CampaignId, descriptor.WorldId, descriptor.SessionId, descriptor.ConfigurationId));

        public GameSessionStartRequest PlanContinue(SessionSaveMetadata save) =>
            GameSessionStartRequest.Resume(
                new GameSessionIdentity(save.ContentId.Value, save.WorldId.Value, save.SessionId, _configurationId),
                save.SaveId.Value);

        public GameSessionStartRequest PlanMultiplayer(SessionFormationResult formation)
        {
            if (!formation.Succeeded || !formation.SessionId.IsValid || !formation.LocalMemberId.IsValid)
                throw new ArgumentException("Successful multiplayer formation is required.", nameof(formation));
            return GameSessionStartRequest.NewGame(new GameSessionIdentity(
                _campaignId, _worldId, formation.SessionId.Value, _configurationId));
        }

        private static string Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Semantic id is required.", name);
            return value.Trim();
        }
    }

    public sealed class KentridgeTrackedSessionFormationService : IAsyncSessionFormationService
    {
        private readonly IAsyncSessionFormationService _inner;
        private Operation _pending;

        public KentridgeTrackedSessionFormationService(IAsyncSessionFormationService inner) =>
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public GameSessionId ActiveSessionId { get; private set; }
        public PartyMemberId ActiveMemberId { get; private set; }

        public ISessionFormationOperation BeginHost(HostSessionRequest request) => Begin(_inner.BeginHost(request));
        public ISessionFormationOperation BeginJoin(JoinSessionRequest request) => Begin(_inner.BeginJoin(request));
        public SessionFormationResult Host(HostSessionRequest request) => _inner.Host(request);
        public SessionFormationResult Join(JoinSessionRequest request) => _inner.Join(request);

        public void ClearActiveIdentity()
        {
            ActiveSessionId = default;
            ActiveMemberId = default;
        }

        private ISessionFormationOperation Begin(ISessionFormationOperation inner)
        {
            if (inner == null) throw new InvalidOperationException("Formation provider returned no operation.");
            if (_pending != null) throw new InvalidOperationException("A formation operation is already active.");
            return _pending = new Operation(this, inner);
        }

        private void Complete(Operation operation, SessionFormationResult result)
        {
            if (!ReferenceEquals(_pending, operation)) return;
            _pending = null;
            if (!result.Succeeded) return;
            ActiveSessionId = result.SessionId;
            ActiveMemberId = result.LocalMemberId;
        }

        private void Abandon(Operation operation)
        {
            if (ReferenceEquals(_pending, operation)) _pending = null;
        }

        private sealed class Operation : ISessionFormationOperation
        {
            private readonly KentridgeTrackedSessionFormationService _owner;
            private readonly ISessionFormationOperation _inner;
            private bool _cancelled;

            public Operation(KentridgeTrackedSessionFormationService owner, ISessionFormationOperation inner)
            {
                _owner = owner;
                _inner = inner;
            }

            public bool TryGetResult(out SessionFormationResult result)
            {
                result = default;
                if (_cancelled || !_inner.TryGetResult(out result)) return false;
                _owner.Complete(this, result);
                return true;
            }

            public void Cancel()
            {
                if (_cancelled) return;
                _cancelled = true;
                _inner.Cancel();
                _owner.Abandon(this);
            }
        }
    }

    public sealed class KentridgeDynamicRemoteSessionIntentRouter : ISessionPresentationIntentRouter
    {
        private readonly Func<PartyMemberId> _localMember;
        private readonly Func<ClientNetworkRuntime> _activeClient;

        public KentridgeDynamicRemoteSessionIntentRouter(
            Func<PartyMemberId> localMember,
            Func<ClientNetworkRuntime> activeClient)
        {
            _localMember = localMember ?? throw new ArgumentNullException(nameof(localMember));
            _activeClient = activeClient ?? throw new ArgumentNullException(nameof(activeClient));
        }

        public PartySessionCommandResult Request(SessionPresentationIntent intent)
        {
            PartyMemberId member = _localMember();
            if (!member.IsValid || intent.MemberId != member)
                return PartySessionCommandResult.Reject(PartySessionCommandFailure.UnknownMember);
            if (intent.Kind != SessionPresentationIntentKind.Leave)
                return PartySessionCommandResult.Reject(PartySessionCommandFailure.InvalidRequest);
            ClientNetworkRuntime client = _activeClient();
            if (client == null || !client.IsConnected)
                return PartySessionCommandResult.Reject(PartySessionCommandFailure.InvalidRequest);
            Span<byte> payload = stackalloc byte[KentridgeSessionControlCodec.LeavePayloadBytes];
            if (!KentridgeSessionControlCodec.TryEncodeLeave(payload, out int written) ||
                !client.TrySendSessionAdmission(payload.Slice(0, written)))
                return PartySessionCommandResult.Reject(PartySessionCommandFailure.InvalidRequest);
            client.FlushSends();
            return PartySessionCommandResult.Accept();
        }
    }

    public sealed class KentridgeAuthoritativePartyScreenQuery : IPartyScreenPresentationQuery
    {
        private readonly Func<PartySession> _session;
        private readonly Func<PartySessionApplication> _application;

        public KentridgeAuthoritativePartyScreenQuery(
            Func<PartySession> session,
            Func<PartySessionApplication> application)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _application = application ?? throw new ArgumentNullException(nameof(application));
        }

        public PartyScreenPresentationSnapshot CapturePartyScreen(PartyMemberId localMemberId)
        {
            PartySession session = _session();
            PartySessionApplication application = _application();
            if (session == null || application == null || !localMemberId.IsValid) return null;
            PartyRosterSnapshot roster = session.Snapshot();
            PartySessionApplicationSnapshot app = application.Snapshot();
            var members = new List<PartyMemberPresentationSnapshot>(roster.Members.Count);
            for (int i = 0; i < roster.Members.Count; i++)
            {
                PartyMemberSnapshot member = roster.Members[i];
                app.TryIsReady(member.MemberId, out bool ready);
                bool gameplayReady = member.Readiness == SessionReadinessState.GameplayReady;
                members.Add(new PartyMemberPresentationSnapshot(
                    member.MemberId, member.Slot, member.CharacterId, member.LeadershipRole,
                    member.MemberId == localMemberId, Connection(member.Presence, member.Readiness),
                    Readiness(member.Readiness), ready, gameplayReady,
                    new MemberDisplayMetadata(
                        member.LeadershipRole == PartyLeadershipRole.Leader ? "Leader" : "Player " + (member.Slot.Value + 1),
                        gameplayReady ? "Ready" : "Synchronizing",
                        member.CharacterId.IsValid ? member.CharacterId.Value : string.Empty)));
            }

            SessionPresentationLifecycle lifecycle;
            if (members.Count == 0) lifecycle = SessionPresentationLifecycle.Empty;
            else if (app.GameplayStarted) lifecycle = SessionPresentationLifecycle.Active;
            else
            {
                bool allConnected = true;
                bool allGameplayReady = true;
                for (int i = 0; i < members.Count; i++)
                {
                    allConnected &= members[i].Connection == MemberConnectionPresentationState.Connected;
                    allGameplayReady &= members[i].GameplayReady;
                }
                lifecycle = !allConnected
                    ? SessionPresentationLifecycle.WaitingForPlayers
                    : allGameplayReady ? SessionPresentationLifecycle.ReadyToStart : SessionPresentationLifecycle.Synchronizing;
            }

            bool canStart = !app.GameplayStarted;
            bool localLeader = false;
            for (int i = 0; i < members.Count; i++)
            {
                canStart &= members[i].GameplayReady && members[i].ReadyToStart;
                if (members[i].MemberId == localMemberId && members[i].LeadershipRole == PartyLeadershipRole.Leader)
                    localLeader = true;
            }
            return new PartyScreenPresentationSnapshot(roster.SessionId, app.Capacity, lifecycle, canStart && localLeader, members);
        }

        private static MemberConnectionPresentationState Connection(PartyPresenceState presence, SessionReadinessState readiness)
        {
            if (presence == PartyPresenceState.Disconnected) return MemberConnectionPresentationState.Interrupted;
            if (presence != PartyPresenceState.Connected) return MemberConnectionPresentationState.Joined;
            return readiness == SessionReadinessState.Synchronized
                ? MemberConnectionPresentationState.Resynchronizing
                : MemberConnectionPresentationState.Connected;
        }

        private static MemberReadinessPresentationState Readiness(SessionReadinessState readiness)
        {
            if (readiness == SessionReadinessState.GameplayReady) return MemberReadinessPresentationState.GameplayReady;
            if (readiness >= SessionReadinessState.Connected) return MemberReadinessPresentationState.Synchronizing;
            return MemberReadinessPresentationState.WaitingForConnection;
        }
    }
}
