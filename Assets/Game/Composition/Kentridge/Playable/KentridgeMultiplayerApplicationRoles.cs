using System;
using Game.Application.Runtime;
using Game.GameplayReplication.Adapters;
using Game.GameplayReplication.Api;
using Game.GameplayReplication.Runtime;
using Game.GameplayReplication.Transport;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using Unity.Networking.Transport;
using VoxelEngine.Net.Api;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Server;

namespace Game.Composition.Kentridge.Playable
{
    public sealed class KentridgeAuthoritativeMultiplayerApplication : IDisposable
    {
        private readonly Func<GameplayStateClientPacketHandler, IServerSessionAdmissionHandler, ClientNetworkRuntime> _clientFactory;
        private readonly Func<IAuthoritativeGameplayStateEmitter, IAuthoritativeSessionAdmissionConsumer, AuthoritativeServerSession> _serverFactory;
        private readonly Func<NetworkEndpoint> _listenEndpoint;
        private readonly Func<AuthoritativeServerSession, NetworkEndpoint> _connectEndpoint;
        private readonly Action<AuthoritativeServerSession> _advanceFixedTick;
        private PartySession _party;
        private PartySessionApplication _partyApplication;
        private KentridgeAuthoritativeSessionAdmission _admission;
        private KentridgeAuthoritativeSessionControl _sessionControl;
        private AuthoritativeServerSession _server;
        private readonly GameplayStateClientPacketHandler _clientPacketHandler;
        private bool _disposed;

        public KentridgeAuthoritativeMultiplayerApplication(
            ISessionRuntimeGraphFactory authorityGraphFactory,
            KentridgeMultiplayerApplicationDependencies dependencies,
            IApplicationSessionPlanProvider plans,
            Func<GameplayStateClientPacketHandler, IServerSessionAdmissionHandler, ClientNetworkRuntime> clientFactory,
            Func<IAuthoritativeGameplayStateEmitter, IAuthoritativeSessionAdmissionConsumer, AuthoritativeServerSession> serverFactory,
            Func<NetworkEndpoint> listenEndpoint,
            Func<AuthoritativeServerSession, NetworkEndpoint> connectEndpoint,
            Action<AuthoritativeServerSession> advanceFixedTick)
        {
            if (authorityGraphFactory == null) throw new ArgumentNullException(nameof(authorityGraphFactory));
            if (dependencies == null) throw new ArgumentNullException(nameof(dependencies));
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _serverFactory = serverFactory ?? throw new ArgumentNullException(nameof(serverFactory));
            _listenEndpoint = listenEndpoint ?? throw new ArgumentNullException(nameof(listenEndpoint));
            _connectEndpoint = connectEndpoint ?? throw new ArgumentNullException(nameof(connectEndpoint));
            _advanceFixedTick = advanceFixedTick ?? throw new ArgumentNullException(nameof(advanceFixedTick));

            ReadState = CreateLobbyReadState();
            _clientPacketHandler = new GameplayStateClientPacketHandler(ReadState);
            Session = new GameSessionOrchestrator(authorityGraphFactory);
            UtpFormation = new KentridgeUtpSessionFormationService(
                admissionHandler => CreateClient(admissionHandler),
                () => _server == null ? default : _connectEndpoint(_server),
                PrepareAuthority,
                PumpAuthority);
            Formation = new KentridgeTrackedSessionFormationService(UtpFormation);
            PartyQuery = new KentridgeAuthoritativePartyScreenQuery(() => _party, () => _partyApplication);
            PartyIntents = new HostIntentRouter(
                () => _partyApplication,
                () => Formation.ActiveMemberId,
                () => UtpFormation.ActiveClient);
            Application = new ApplicationFlowCoordinator(
                Session,
                dependencies.Saves,
                Formation,
                PartyQuery,
                PartyIntents,
                dependencies.Outcomes,
                dependencies.InputContexts,
                dependencies.InputBindings,
                dependencies.Preferences,
                dependencies.Audio,
                dependencies.Exit,
                plans ?? throw new ArgumentNullException(nameof(plans)));
        }

        public ApplicationFlowCoordinator Application { get; }
        public GameSessionOrchestrator Session { get; }
        public GameplayReplicationReadState ReadState { get; }
        public KentridgeUtpSessionFormationService UtpFormation { get; }
        public KentridgeTrackedSessionFormationService Formation { get; }
        public IPartyScreenPresentationQuery PartyQuery { get; }
        public ISessionPresentationIntentRouter PartyIntents { get; }
        public PartySession PartySession => _party;
        public PartySessionApplication PartyApplication => _partyApplication;
        public AuthoritativeServerSession Server => _server;

        public void TickNetworkAndAuthority()
        {
            ThrowIfDisposed();
            PumpAuthority();
            UtpFormation.ActiveClient?.PumpTransport();
        }

        private ClientNetworkRuntime CreateClient(IServerSessionAdmissionHandler admissionHandler)
        {
            ClientNetworkRuntime client = _clientFactory(_clientPacketHandler, admissionHandler)
                ?? throw new InvalidOperationException("Client factory returned no production network runtime.");
            _clientPacketHandler.BindRepairRequester(request => client.TryRequestGameplayStateRepair(in request));
            return client;
        }

        private void PrepareAuthority(HostSessionRequest request)
        {
            ThrowIfDisposed();
            if (_server != null) throw new InvalidOperationException("Kentridge multiplayer authority is already prepared.");
            _party = new PartySession(request.SessionId, request.Configuration);
            _partyApplication = new PartySessionApplication(_party);
            _admission = new KentridgeAuthoritativeSessionAdmission(_party);
            var ready = new KentridgeReadySessionAdmissionConsumer(_admission, _party, _partyApplication);
            _sessionControl = new KentridgeAuthoritativeSessionControl(ready, _party, _partyApplication);
            var emitter = new GameplayStateServerEmitter(new IGameplayProjectionSource[]
            {
                new SessionsGameplayProjectionSource(_party),
                new KentridgeSessionApplicationGameplayProjectionSource(_partyApplication)
            });
            _server = _serverFactory(emitter, _sessionControl)
                ?? throw new InvalidOperationException("Server factory returned no canonical authority.");
            _admission.BindAuthority(_server);
            _sessionControl.BindAuthority(_server);
            NetworkEndpoint endpoint = _listenEndpoint();
            if (endpoint.Port == 0 || _server.Listen(endpoint) != 0)
                throw new InvalidOperationException("Kentridge multiplayer authority failed to listen.");
        }

        private void PumpAuthority()
        {
            if (_server == null) return;
            _server.PumpTransport();
            _advanceFixedTick(_server);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { Application.Dispose(); }
            finally
            {
                Formation.ClearActiveIdentity();
                UtpFormation.Dispose();
                _admission?.Dispose();
                _server?.Dispose();
                _server = null;
                _sessionControl = null;
                _admission = null;
                _partyApplication = null;
                _party = null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KentridgeAuthoritativeMultiplayerApplication));
        }

        private static GameplayReplicationReadState CreateLobbyReadState() =>
            new GameplayReplicationReadState(new[]
            {
                new GameplayProjectionDescriptor(KentridgeReplicatedPartyState.SessionsProjectionId, 1, true),
                new GameplayProjectionDescriptor(KentridgeSessionApplicationGameplayProjectionSource.ProjectionId, 1, true)
            });

        private sealed class HostIntentRouter : ISessionPresentationIntentRouter
        {
            private readonly Func<PartySessionApplication> _application;
            private readonly Func<PartyMemberId> _localMember;
            private readonly Func<ClientNetworkRuntime> _activeClient;

            public HostIntentRouter(
                Func<PartySessionApplication> application,
                Func<PartyMemberId> localMember,
                Func<ClientNetworkRuntime> activeClient)
            {
                _application = application;
                _localMember = localMember;
                _activeClient = activeClient;
            }

            public PartySessionCommandResult Request(SessionPresentationIntent intent)
            {
                PartySessionApplication application = _application();
                if (application == null)
                    return PartySessionCommandResult.Reject(PartySessionCommandFailure.InvalidRequest);
                switch (intent.Kind)
                {
                    case SessionPresentationIntentKind.SetReady:
                        return application.SetReady(intent.MemberId, intent.Ready);
                    case SessionPresentationIntentKind.Start:
                        return application.RequestStart(intent.MemberId);
                    case SessionPresentationIntentKind.Leave:
                    {
                        if (intent.MemberId != _localMember())
                            return PartySessionCommandResult.Reject(PartySessionCommandFailure.UnknownMember);
                        PartySessionCommandResult leave = application.Leave(intent.MemberId);
                        ClientNetworkRuntime client = _activeClient();
                        if (leave.Accepted && client != null && client.IsConnected) client.Disconnect();
                        return leave;
                    }
                    default:
                        return PartySessionCommandResult.Reject(PartySessionCommandFailure.InvalidRequest);
                }
            }
        }
    }

    public sealed class KentridgeClientMultiplayerApplication : IDisposable
    {
        private readonly Func<GameplayStateClientPacketHandler, IServerSessionAdmissionHandler, ClientNetworkRuntime> _clientFactory;
        private GameplayStateClientPacketHandler _packetHandler;
        private bool _disposed;

        public KentridgeClientMultiplayerApplication(
            KentridgeMultiplayerApplicationDependencies dependencies,
            IApplicationSessionPlanProvider plans,
            Func<NetworkEndpoint> authorityEndpoint,
            Func<GameplayStateClientPacketHandler, IServerSessionAdmissionHandler, ClientNetworkRuntime> clientFactory)
        {
            if (dependencies == null) throw new ArgumentNullException(nameof(dependencies));
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            ReadState = new GameplayReplicationReadState(new[]
            {
                new GameplayProjectionDescriptor(KentridgeReplicatedPartyState.SessionsProjectionId, 1, true),
                new GameplayProjectionDescriptor(KentridgeSessionApplicationGameplayProjectionSource.ProjectionId, 1, true)
            });
            _packetHandler = new GameplayStateClientPacketHandler(ReadState);
            UtpFormation = new KentridgeUtpSessionFormationService(
                admissionHandler => CreateClient(admissionHandler),
                authorityEndpoint ?? throw new ArgumentNullException(nameof(authorityEndpoint)));
            TrackedFormation = new KentridgeTrackedSessionFormationService(UtpFormation);
            Formation = new KentridgeGameplayReadyFormationService(
                TrackedFormation, ReadState, PumpClient, DisconnectClient);
            Session = new GameSessionOrchestrator(new KentridgeReplicatedClientSessionGraphFactory(ReadState, PumpClient));
            PartyQuery = new KentridgeReplicatedPartyScreenQuery(ReadState);
            PartyIntents = new KentridgeDynamicRemoteSessionIntentRouter(
                () => TrackedFormation.ActiveMemberId,
                () => UtpFormation.ActiveClient);
            Application = new ApplicationFlowCoordinator(
                Session,
                dependencies.Saves,
                Formation,
                PartyQuery,
                PartyIntents,
                dependencies.Outcomes,
                dependencies.InputContexts,
                dependencies.InputBindings,
                dependencies.Preferences,
                dependencies.Audio,
                dependencies.Exit,
                plans ?? throw new ArgumentNullException(nameof(plans)));
        }

        public ApplicationFlowCoordinator Application { get; }
        public GameSessionOrchestrator Session { get; }
        public GameplayReplicationReadState ReadState { get; }
        public KentridgeUtpSessionFormationService UtpFormation { get; }
        public KentridgeTrackedSessionFormationService TrackedFormation { get; }
        public IAsyncSessionFormationService Formation { get; }
        public IPartyScreenPresentationQuery PartyQuery { get; }
        public ISessionPresentationIntentRouter PartyIntents { get; }

        public void TickNetwork()
        {
            ThrowIfDisposed();
            PumpClient();
        }

        private ClientNetworkRuntime CreateClient(IServerSessionAdmissionHandler admissionHandler)
        {
            ClientNetworkRuntime client = _clientFactory(_packetHandler, admissionHandler)
                ?? throw new InvalidOperationException("Client factory returned no production network runtime.");
            _packetHandler.BindRepairRequester(request => client.TryRequestGameplayStateRepair(in request));
            return client;
        }

        private void PumpClient() => UtpFormation.ActiveClient?.PumpTransport();

        private void DisconnectClient()
        {
            ClientNetworkRuntime client = UtpFormation.ActiveClient;
            if (client != null && client.IsConnected) client.Disconnect();
            TrackedFormation.ClearActiveIdentity();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { Application.Dispose(); }
            finally
            {
                TrackedFormation.ClearActiveIdentity();
                UtpFormation.Dispose();
                _packetHandler = null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KentridgeClientMultiplayerApplication));
        }
    }
}
