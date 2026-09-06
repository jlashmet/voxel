using System;
using System.Collections.Generic;
using Game.Application.Api;
using Game.Application.Runtime;
using Game.Input.Api;
using Game.Outcomes.Api;
using Game.Persistence.Api;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;
using NUnit.Framework;
using GameSessionSnapshot = Game.SessionOrchestration.Api.GameSessionSnapshot;

namespace Game.Application.Tests
{
    public sealed class ApplicationJoinedPartyStartupTests
    {
        [Test]
        public void ProductionOrchestratorRejectsBindingsThatAreNotReadyBeforePrepare()
        {
            using (var f = new Fixture())
            {
                f.Session.ReadyBeforePrepare = false;
                GameSessionOperationResult result = f.Session.Prepare(GameSessionStartRequest.NewGame(
                    new GameSessionIdentity("campaign", "world", "party-a", "multiplayer")));
                Assert.That(result.Failure, Is.EqualTo(GameSessionFailure.BindingsNotReady));
                Assert.That(f.Session.Snapshot.GameplayReady, Is.False);
                Assert.That(f.Session.InitializeCalls, Is.Zero);
            }
        }

        [Test]
        public void ActivePartyWaitsForLocalSynchronizationBeforeComposingProductionGraph()
        {
            using (var f = new Fixture())
            {
                f.Party = Party(SessionPresentationLifecycle.Active, replicationReady: false);
                f.Session.ReadyBeforePrepare = false;
                f.BootAndJoin();
                for (int i = 0; i < 3; i++) Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.FrontEnd));
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Party));
                Assert.That(f.Session.PrepareCalls, Is.Zero);
                Assert.That(f.PlanCalls, Is.Zero);

                f.Session.ReadyBeforePrepare = true;
                f.Session.ReadyOnEnter = true;
                f.Party = Party(SessionPresentationLifecycle.Active);
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.InGame));
                Assert.That(f.Session.PrepareCalls, Is.EqualTo(1));
                Assert.That(f.Session.InitializeCalls, Is.EqualTo(1));
                Assert.That(f.StartCommands, Is.Zero);
            }
        }

        [Test]
        public void ReadyPartyProjectionCannotBypassProductionGraphBindingValidation()
        {
            using (var f = new Fixture())
            {
                f.Party = Party(SessionPresentationLifecycle.Active);
                f.Session.ReadyBeforePrepare = false;
                f.BootAndJoin();
                Assert.That(f.App.Update(16).Failure, Is.EqualTo(ApplicationFailure.SessionPrepareFailed));
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Error));
                Assert.That(f.Session.InitializeCalls, Is.Zero);
                f.Session.ReadyBeforePrepare = true;
                for (int i = 0; i < 3; i++) Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Session.PrepareCalls, Is.EqualTo(1));
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Error));
            }
        }

        [TestCase(MemberConnectionPresentationState.Joined)]
        [TestCase(MemberConnectionPresentationState.Interrupted)]
        [TestCase(MemberConnectionPresentationState.Reconnecting)]
        [TestCase(MemberConnectionPresentationState.Resynchronizing)]
        [TestCase(MemberConnectionPresentationState.Expired)]
        [TestCase(MemberConnectionPresentationState.Left)]
        public void StaleReadyProjectionWithoutConnectedLocalMembershipDoesNotStart(MemberConnectionPresentationState connection)
        {
            using (var f = new Fixture())
            {
                f.Party = Party(SessionPresentationLifecycle.Active, connection: connection);
                f.BootAndJoin();
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Session.PrepareCalls, Is.Zero);
                Assert.That(f.PlanCalls, Is.Zero);
                f.Party = Party(SessionPresentationLifecycle.Active);
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Session.PrepareCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public void JoinedClientObservesLeaderStartAndWaitsForLocalGraphReadiness()
        {
            using (var f = new Fixture())
            {
                f.BootAndJoin();
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.FrontEnd));
                Assert.That(f.Session.PrepareCalls, Is.Zero);

                f.Party = Party(SessionPresentationLifecycle.Active);
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.StartingSession));
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Loading));
                Assert.That(f.App.Snapshot.GameplayReady, Is.False);
                Assert.That(f.Session.PrepareCalls, Is.EqualTo(1));
                Assert.That(f.PlannedFormation.SessionId.Value, Is.EqualTo("party-a"));
                Assert.That(f.PlannedFormation.LocalMemberId.Value, Is.EqualTo("joiner"));
                Assert.That(f.StartCommands, Is.Zero, "A non-leader observes start; it cannot authorize it.");

                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.GameplayReady, Is.False);
                f.Session.ReadyOnTick = true;
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.InGame));
                Assert.That(f.App.Snapshot.GameplayReady, Is.True);
                for (int i = 0; i < 8; i++) Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Session.PrepareCalls, Is.EqualTo(1));
                Assert.That(f.PlanCalls, Is.EqualTo(1));
                Assert.That(f.StartCommands, Is.Zero);
            }
        }

        [TestCase(SessionPresentationLifecycle.Empty)]
        [TestCase(SessionPresentationLifecycle.WaitingForPlayers)]
        [TestCase(SessionPresentationLifecycle.Synchronizing)]
        [TestCase(SessionPresentationLifecycle.ReadyToStart)]
        public void ReadyMembersDoNotAuthorizeStart(SessionPresentationLifecycle lifecycle)
        {
            using (var f = new Fixture())
            {
                f.Party = Party(lifecycle);
                f.Session.ReadyOnEnter = true;
                f.BootAndJoin();
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.FrontEnd));
                Assert.That(f.Session.PrepareCalls, Is.Zero);
                Assert.That(f.PlanCalls, Is.Zero);
                Assert.That(f.StartCommands, Is.Zero);
            }
        }

        [TestCase("other-session", "joiner", true)]
        [TestCase("party-a", "other-member", true)]
        [TestCase("party-a", "joiner", false)]
        [TestCase("party-a", null, true)]
        public void ActiveSnapshotMustMatchFormedSessionAndLocalMembership(string sessionId, string memberId, bool isLocal)
        {
            using (var f = new Fixture())
            {
                f.Party = Party(SessionPresentationLifecycle.Active, sessionId, memberId, isLocal);
                f.BootAndJoin();
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Session.PrepareCalls, Is.Zero);
                Assert.That(f.PlanCalls, Is.Zero);

                f.Party = Party(SessionPresentationLifecycle.Active);
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Session.PrepareCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public void MissingProjectionWaitsWithoutConsumingStart()
        {
            using (var f = new Fixture())
            {
                f.Party = null;
                f.BootAndJoin();
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Session.PrepareCalls, Is.Zero);
                f.Party = Party(SessionPresentationLifecycle.Active);
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Session.PrepareCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public void JoinInProgressStartsOnFirstUpdateWithoutAnotherPartyStartCommand()
        {
            using (var f = new Fixture())
            {
                f.Party = Party(SessionPresentationLifecycle.Active);
                f.Session.ReadyOnEnter = true;
                f.BootAndJoin();
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.InGame));
                Assert.That(f.StartCommands, Is.Zero);
                Assert.That(f.Session.PrepareCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public void FailedPrepareRemainsAnErrorUntilExplicitLeaveAndRejoin()
        {
            using (var f = new Fixture())
            {
                f.Party = Party(SessionPresentationLifecycle.Active);
                f.Session.FailPrepare = true;
                f.BootAndJoin();
                Assert.That(f.App.Update(16).Failure, Is.EqualTo(ApplicationFailure.SessionPrepareFailed));
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Error));
                Assert.That(f.Session.ShutdownCalls, Is.EqualTo(1));

                f.Session.FailPrepare = false;
                for (int i = 0; i < 8; i++) Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Error));
                Assert.That(f.Session.PrepareCalls, Is.EqualTo(1));
                Assert.That(f.PlanCalls, Is.EqualTo(1));

                Assert.That(f.App.RequestLeaveGame().Succeeded, Is.True);
                f.Session.ReadyOnEnter = true;
                Assert.That(f.App.RequestJoin(JoinRequest()).Succeeded, Is.True);
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.InGame));
                Assert.That(f.Session.PrepareCalls, Is.EqualTo(2));
                Assert.That(f.PlanCalls, Is.EqualTo(2));
            }
        }

        [Test]
        public void FailedPlanDoesNotRetryOnEveryFrontendFrame()
        {
            using (var f = new Fixture())
            {
                f.Party = Party(SessionPresentationLifecycle.Active);
                f.ThrowOnPlan = true;
                f.BootAndJoin();
                ApplicationOperationResult result = f.App.Update(16);
                Assert.That(result.Failure, Is.EqualTo(ApplicationFailure.SessionPrepareFailed));
                Assert.That(result.Detail, Does.Contain("multiplayer plan rejected"));
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Error));
                for (int i = 0; i < 8; i++) Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.PlanCalls, Is.EqualTo(1));
                Assert.That(f.Session.PrepareCalls, Is.Zero);
                Assert.That(f.StartCommands, Is.Zero);
            }
        }

        [Test]
        public void LeaveBeforeStartDisarmsStaleProjectionAndRejoinArmsNewAttempt()
        {
            using (var f = new Fixture())
            {
                f.BootAndJoin();
                Assert.That(f.App.RequestLeaveGame().Succeeded, Is.True);
                f.Party = Party(SessionPresentationLifecycle.Active);
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Session.PrepareCalls, Is.Zero);
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.MainMenu));
                Assert.That(f.LeaveCommands, Is.EqualTo(1));

                Assert.That(f.App.RequestJoin(JoinRequest()).Succeeded, Is.True);
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Session.PrepareCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public void LeaveDuringReadinessWaitDoesNotRestartFromStillActiveParty()
        {
            using (var f = new Fixture())
            {
                f.Party = Party(SessionPresentationLifecycle.Active);
                f.BootAndJoin();
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.StartingSession));
                Assert.That(f.App.RequestLeaveGame().Succeeded, Is.True);
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.FrontEnd));
                Assert.That(f.Session.PrepareCalls, Is.EqualTo(1));
                Assert.That(f.Session.ShutdownCalls, Is.EqualTo(1));
                Assert.That(f.LeaveCommands, Is.EqualTo(1));
            }
        }

        [Test]
        public void RejectedJoinCannotObserveAStaleActiveParty()
        {
            using (var f = new Fixture())
            {
                f.Party = Party(SessionPresentationLifecycle.Active);
                f.JoinResult = SessionFormationResult.Reject(SessionFormationFailure.Rejected, "not admitted");
                Assert.That(f.App.CompleteBoot().Succeeded, Is.True);
                Assert.That(f.App.RequestJoin(JoinRequest()).Failure, Is.EqualTo(ApplicationFailure.SessionFormationFailed));
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Session.PrepareCalls, Is.Zero);
                Assert.That(f.ProjectionReads, Is.Zero);
            }
        }

        [Test]
        public void HostStillUsesExplicitPartyStartRatherThanClientObservation()
        {
            using (var f = new Fixture())
            {
                f.Party = Party(SessionPresentationLifecycle.Active, memberId: "host");
                f.Session.ReadyOnEnter = true;
                f.AllowStartCommand = true;
                Assert.That(f.App.CompleteBoot().Succeeded, Is.True);
                Assert.That(f.App.RequestHost(new HostSessionRequest(new GameSessionId("party-a"),
                    new SessionStartupConfiguration(4, "p1", "content", true), "host-key")).Succeeded, Is.True);
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Session.PrepareCalls, Is.Zero);
                Assert.That(f.App.RequestPartyStart().Succeeded, Is.True);
                Assert.That(f.Session.PrepareCalls, Is.EqualTo(1));
                Assert.That(f.StartCommands, Is.EqualTo(1));
            }
        }

        private static JoinSessionRequest JoinRequest() => new JoinSessionRequest(
            new Game.Sessions.Api.JoinRequest(new GameSessionId("party-a"), "join-key", "p1", "content"));

        private static PartyScreenPresentationSnapshot Party(SessionPresentationLifecycle lifecycle,
            string sessionId = "party-a", string memberId = "joiner", bool isLocal = true, bool replicationReady = true,
            MemberConnectionPresentationState connection = MemberConnectionPresentationState.Connected)
        {
            var members = memberId == null ? Array.Empty<PartyMemberPresentationSnapshot>() : new[]
            {
                new PartyMemberPresentationSnapshot(new PartyMemberId(memberId), default, default, default,
                    isLocal, connection,
                    replicationReady ? MemberReadinessPresentationState.GameplayReady : MemberReadinessPresentationState.Synchronizing,
                    replicationReady, replicationReady, default)
            };
            return new PartyScreenPresentationSnapshot(new GameSessionId(sessionId), 4, lifecycle, false, members);
        }

        // Bounded external inputs isolate Application; lifecycle computation uses both production
        // ApplicationFlowCoordinator and GameSessionOrchestrator, never a replacement state machine.
        // These tests do not claim transport or separate-process multiplayer acceptance.
        private sealed class Fixture : IDisposable, ISessionSaveCatalog, ISessionFormationService,
            IPartyScreenPresentationQuery, ISessionPresentationIntentRouter, IGameOutcomeQuery,
            IInputContextService, IInputBindingOverrideService, IUserPreferencesStore, IAudioPreferencesSink,
            IApplicationExitPort, IApplicationSessionPlanProvider
        {
            public readonly SessionBoundary Session = new SessionBoundary();
            public readonly ApplicationFlowCoordinator App;
            public PartyScreenPresentationSnapshot Party = ApplicationJoinedPartyStartupTests.Party(SessionPresentationLifecycle.WaitingForPlayers);
            public SessionFormationResult JoinResult = SessionFormationResult.Success(new GameSessionId("party-a"), new PartyMemberId("joiner"));
            public SessionFormationResult PlannedFormation;
            public int PlanCalls;
            public int ProjectionReads;
            public int StartCommands;
            public int LeaveCommands;
            public bool ThrowOnPlan;
            public bool AllowStartCommand;

            public Fixture() => App = new ApplicationFlowCoordinator(Session, this, this, this, this, this,
                this, this, this, this, this, this);
            public void BootAndJoin()
            {
                Assert.That(App.CompleteBoot().Succeeded, Is.True);
                Assert.That(App.RequestJoin(JoinRequest()).Succeeded, Is.True);
            }
            public void Dispose() { App.Dispose(); Session.Shutdown(); }
            public IReadOnlyList<SessionSaveMetadata> ListSaves() => Array.Empty<SessionSaveMetadata>();
            public SessionFormationResult Host(HostSessionRequest request) =>
                SessionFormationResult.Success(request.SessionId, new PartyMemberId("host"));
            public SessionFormationResult Join(JoinSessionRequest request) => JoinResult;
            public PartyScreenPresentationSnapshot CapturePartyScreen(PartyMemberId localMemberId) { ProjectionReads++; return Party; }
            public PartySessionCommandResult Request(SessionPresentationIntent intent)
            {
                if (intent.Kind == SessionPresentationIntentKind.Start)
                {
                    StartCommands++;
                    if (!AllowStartCommand) return PartySessionCommandResult.Reject(PartySessionCommandFailure.NotLeader);
                }
                if (intent.Kind == SessionPresentationIntentKind.Leave) LeaveCommands++;
                return PartySessionCommandResult.Accept();
            }
            public GameSessionStartRequest PlanMultiplayer(SessionFormationResult formation)
            {
                PlanCalls++;
                PlannedFormation = formation;
                if (ThrowOnPlan) throw new InvalidOperationException("multiplayer plan rejected");
                return GameSessionStartRequest.NewGame(new GameSessionIdentity("campaign", "world", formation.SessionId.Value, "multiplayer"));
            }
            public GameSessionStartRequest PlanNewGame(ApplicationSessionDescriptor descriptor) => throw new NotSupportedException();
            public GameSessionStartRequest PlanContinue(SessionSaveMetadata save) => throw new NotSupportedException();
            public GameOutcomeSnapshot Snapshot() => GameOutcomeSnapshot.Running();
            public InputContextId ActiveContext => InputContextId.Exploration;
            public IInputContextLease Push(InputContextId context) => throw new NotSupportedException();
            public IReadOnlyList<InputBindingOverride> SnapshotOverrides() => Array.Empty<InputBindingOverride>();
            public bool TryApplyOverride(InputBindingOverride bindingOverride, out string error) { error = string.Empty; return true; }
            public void ClearOverrides() { }
            public bool TryLoad(out UserPreferences preferences) { preferences = null; return false; }
            public void Save(UserPreferences preferences) { }
            public void Apply(UserPreferences preferences) { }
            public void RequestExit() { }
        }

        private sealed class SessionBoundary : IGameSessionControl, ISessionRuntimeGraphFactory
        {
            private readonly GameSessionOrchestrator _runtime;
            public bool ReadyBeforePrepare = true;
            public bool ReadyOnEnter;
            public bool ReadyOnTick;
            public bool FailPrepare;
            public int PrepareCalls;
            public int ShutdownCalls;
            public int InitializeCalls;
            public SessionBoundary() => _runtime = new GameSessionOrchestrator(this);
            public GameSessionSnapshot Snapshot => _runtime.Snapshot;
            public GameSessionOperationResult Prepare(GameSessionStartRequest request) { PrepareCalls++; return _runtime.Prepare(request); }
            public GameSessionOperationResult EnterRunning() => _runtime.EnterRunning();
            public GameSessionOperationResult Tick(int elapsedMilliseconds) => _runtime.Tick(elapsedMilliseconds);
            public GameSessionOperationResult Capture() => _runtime.Capture();
            public GameSessionOperationResult Shutdown() { ShutdownCalls++; return _runtime.Shutdown(); }
            public ISessionRuntimeGraph Compose(GameSessionIdentity identity)
            {
                if (FailPrepare) throw new SessionCompositionException(GameSessionFailure.CompositionFailed, "prepare failed");
                return new ReadinessInputs(this);
            }

            private sealed class ReadinessInputs : ISessionRuntimeGraph, ISessionUpdateStep
            {
                private readonly SessionBoundary _inputs;
                private bool _ready;
                private bool _disposed;
                public ReadinessInputs(SessionBoundary inputs)
                {
                    _inputs = inputs;
                    _ready = inputs.ReadyBeforePrepare;
                    UpdateSteps = Array.AsReadOnly(new ISessionUpdateStep[] { this });
                }
                public bool GameplayBindingsReady => !_disposed && _ready;
                public IReadOnlyList<ISessionUpdateStep> UpdateSteps { get; }
                public IGameOutcomeQuery OutcomeQuery => null;
                public SessionUpdatePhase Phase => SessionUpdatePhase.Replication;
                public int Order => 0;
                public string SemanticId => "application.validation.readiness";
                public void InitializeNewGame() { _inputs.InitializeCalls++; _ready = _inputs.ReadyOnEnter; }
                public void Tick(int elapsedMilliseconds) { _ready |= _inputs.ReadyOnTick; }
                public void StartCommands() { }
                public void StopCommands() { }
                public void SettleAuthoritativeState() { }
                public void DetachExternalAdapters() { }
                public void Dispose() { _disposed = true; }
            }
        }
    }
}
