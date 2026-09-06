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

namespace Game.Application.Tests
{
    public sealed class ApplicationPendingFormationTests
    {
        private static readonly GameSessionId PartyId = new GameSessionId("pending-party");
        private static readonly PartyMemberId MemberId = new PartyMemberId("authority-issued-member");
        private static JoinSessionRequest JoinRequest() => new JoinSessionRequest(
            new JoinRequest(PartyId, "applicant", "v1", "content"));
        private static HostSessionRequest HostRequest() => new HostSessionRequest(
            PartyId, new SessionStartupConfiguration(3, "v1", "content", true), "host");
        private static SessionFormationResult Admitted() => SessionFormationResult.Success(PartyId, MemberId);

        [Test]
        public void PendingJoinDoesNotExposeIdentityReadPartyOrComposeGraph()
        {
            using (var f = new Fixture())
            {
                f.BeginJoin();
                // Even a success-shaped out value is untrusted when TryGetResult returns false.
                f.Provider.Next.Result = Admitted();
                for (int i = 0; i < 20; i++) Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.FrontEnd));
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Loading));
                Assert.That(f.App.Snapshot.GameplayReady, Is.False);
                Assert.That(f.App.TryCapturePartyScreen(out _), Is.False);
                Assert.That(f.ProjectionReads, Is.Zero);
                Assert.That(f.ComposeCalls, Is.Zero);
                Assert.That(f.Provider.Next.PollCalls, Is.EqualTo(20));
                Assert.That(f.Provider.JoinBegins, Is.EqualTo(1));
                Assert.That(f.Provider.LegacyCalls, Is.Zero);
            }
        }

        [Test]
        public void AcceptedJoinStillWaitsForLeaderLocalReadinessAndProductionGraphReadiness()
        {
            using (var f = new Fixture())
            {
                f.ReadyAfterInitialize = false;
                f.BeginJoin();
                f.Provider.Next.Complete(Admitted());
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Party));
                Assert.That(f.App.TryCapturePartyScreen(out var party), Is.True);
                Assert.That(party.SessionId, Is.EqualTo(PartyId));
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.ComposeCalls, Is.Zero);

                f.PartyLifecycle = SessionPresentationLifecycle.Active;
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.ComposeCalls, Is.Zero, "Active is not local synchronization.");
                f.MemberReady = true;
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Session.Snapshot.Lifecycle, Is.EqualTo(GameSessionLifecycle.Running));
                Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.StartingSession));
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.GameplayReady, Is.False);
                f.ReadyOnTick = true;
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.GameplayReady, Is.True);
                for (int i = 0; i < 3; i++) f.App.Update(16);
                Assert.That(f.ComposeCalls, Is.EqualTo(1));
                Assert.That(f.InitializeCalls, Is.EqualTo(1));
                Assert.That(f.PlannedFormation.LocalMemberId, Is.EqualTo(MemberId));
                Assert.That(f.StartCommands, Is.Zero);
                Assert.That(f.Provider.Next.PollCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public void AcceptedHostRequiresExplicitStartNotClientAutoStart()
        {
            using (var f = new Fixture())
            {
                f.PartyLifecycle = SessionPresentationLifecycle.Active;
                f.MemberReady = true;
                f.AllowStart = true;
                f.Boot();
                Assert.That(f.App.RequestHost(HostRequest()).Succeeded, Is.True);
                f.Provider.Next.Complete(Admitted());
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.ComposeCalls, Is.Zero);
                Assert.That(f.App.RequestPartyStart().Succeeded, Is.True);
                Assert.That(f.ComposeCalls, Is.EqualTo(1));
                Assert.That(f.StartCommands, Is.EqualTo(1));
                Assert.That(f.Provider.HostBegins, Is.EqualTo(1));
                Assert.That(f.Provider.LegacyCalls, Is.Zero);
            }
        }

        [TestCase("host")]
        [TestCase("join")]
        [TestCase("new")]
        [TestCase("continue")]
        [TestCase("start")]
        [TestCase("open-screen")]
        [TestCase("close-screen")]
        public void PendingAdmissionRejectsOverlappingIntent(string intent)
        {
            using (var f = new Fixture())
            {
                f.BeginJoin();
                ApplicationOperationResult result;
                switch (intent)
                {
                    case "host": result = f.App.RequestHost(HostRequest()); break;
                    case "join": result = f.App.RequestJoin(JoinRequest()); break;
                    case "new": result = f.App.RequestNewGame(default); break;
                    case "continue": result = f.App.RequestContinue("save"); break;
                    case "start": result = f.App.RequestPartyStart(); break;
                    case "open-screen": result = f.App.OpenScreen(ApplicationScreen.Settings); break;
                    default: result = f.App.CloseScreen(); break;
                }
                Assert.That(result.Failure, Is.EqualTo(ApplicationFailure.Busy));
                Assert.That(f.Provider.JoinBegins, Is.EqualTo(1));
                Assert.That(f.Provider.HostBegins, Is.Zero);
                Assert.That(f.StartCommands, Is.Zero);
                Assert.That(f.ComposeCalls, Is.Zero);
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Loading));
            }
        }

        [Test]
        public void LeaveCancelsAttemptAndLateReplyCannotReplaceFreshSameSessionAdmission()
        {
            using (var f = new Fixture())
            {
                f.BeginJoin();
                Operation old = f.Provider.Next;
                Assert.That(f.App.RequestLeaveGame().Succeeded, Is.True);
                Assert.That(old.CancelCalls, Is.EqualTo(1));
                Assert.That(f.LeaveCommands, Is.Zero, "Pending operation is not an adopted member.");
                Assert.That(f.DisposeCalls, Is.Zero, "No graph exists yet.");
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.MainMenu));

                f.Provider.Next = new Operation();
                Assert.That(f.App.RequestJoin(JoinRequest()).Succeeded, Is.True);
                old.Complete(SessionFormationResult.Success(PartyId, new PartyMemberId("stale-member")));
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(old.PollCalls, Is.Zero);
                Assert.That(f.App.TryCapturePartyScreen(out _), Is.False);
                f.Provider.Next.Complete(Admitted());
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                f.PartyLifecycle = SessionPresentationLifecycle.Active;
                f.MemberReady = true;
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.PlannedFormation.LocalMemberId, Is.EqualTo(MemberId));
                Assert.That(f.ComposeCalls, Is.EqualTo(1));
                Assert.That(f.App.RequestLeaveGame().Succeeded, Is.True);
                Assert.That(f.LeaveCommands, Is.EqualTo(1));
                Assert.That(f.Provider.Next.CancelCalls, Is.Zero, "Adopted party owns normal Leave.");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void QuitCancelsPendingAdmissionAndExitsEvenWhenCleanupFails(bool cancellationThrows)
        {
            using (var f = new Fixture())
            {
                f.BeginJoin();
                f.Provider.Next.ThrowOnCancel = cancellationThrows;
                ApplicationOperationResult result = f.App.RequestQuitApplication();
                Assert.That(result.Succeeded, Is.EqualTo(!cancellationThrows));
                if (cancellationThrows) Assert.That(result.Failure, Is.EqualTo(ApplicationFailure.TeardownFailed));
                Assert.That(f.ExitCalls, Is.EqualTo(1));
                Assert.That(f.Provider.Next.CancelCalls, Is.EqualTo(1));
                Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.Exiting));
                f.Provider.Next.Complete(Admitted());
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Provider.Next.PollCalls, Is.Zero);
                Assert.That(f.LeaveCommands, Is.Zero);
                Assert.That(f.ComposeCalls, Is.Zero);
            }
        }

        [Test]
        public void DisposeCancelsOnlyOnceAndNeverAdoptsLateResult()
        {
            using (var f = new Fixture())
            {
                f.BeginJoin();
                f.App.Dispose();
                f.App.Dispose();
                f.Provider.Next.Complete(Admitted());
                Assert.That(f.Provider.Next.CancelCalls, Is.EqualTo(1));
                Assert.Throws<ObjectDisposedException>(() => f.App.Update(16));
                Assert.That(f.ComposeCalls, Is.Zero);
                Assert.That(f.LeaveCommands, Is.Zero);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NullOrThrowingBeginFailsClosedWithoutSynchronousFallback(bool throws)
        {
            using (var f = new Fixture())
            {
                f.Boot();
                f.Provider.ThrowOnBegin = throws;
                f.Provider.ReturnNull = !throws;
                ApplicationOperationResult result = f.App.RequestJoin(JoinRequest());
                Assert.That(result.Failure, Is.EqualTo(ApplicationFailure.SessionFormationFailed));
                Assert.That(result.Detail, Does.Not.Contain("private-admission-token"));
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Error));
                Assert.That(f.App.TryCapturePartyScreen(out _), Is.False);
                Assert.That(f.Provider.LegacyCalls, Is.Zero);
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Provider.JoinBegins, Is.EqualTo(1));
                f.Provider.ThrowOnBegin = f.Provider.ReturnNull = false;
                Assert.That(f.App.RequestJoin(JoinRequest()).Succeeded, Is.True, "User may explicitly retry.");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DefaultOrWrongSessionSuccessIsCancelledNotAdopted(bool wrongSession)
        {
            using (var f = new Fixture())
            {
                f.BeginJoin();
                f.Provider.Next.Complete(wrongSession
                    ? SessionFormationResult.Success(new GameSessionId("other-party"), MemberId)
                    : default);
                Assert.That(f.App.Update(16).Failure, Is.EqualTo(ApplicationFailure.SessionFormationFailed));
                Assert.That(f.Provider.Next.CancelCalls, Is.EqualTo(1));
                Assert.That(f.App.TryCapturePartyScreen(out _), Is.False);
                Assert.That(f.ComposeCalls, Is.Zero);
                Assert.That(f.ProjectionReads, Is.Zero);
                for (int i = 0; i < 3; i++) f.App.Update(16);
                Assert.That(f.Provider.Next.PollCalls, Is.EqualTo(1));
            }
        }

        [TestCase(SessionFormationFailure.SessionFull)]
        [TestCase(SessionFormationFailure.ProviderUnavailable)]
        [TestCase(SessionFormationFailure.ProtocolMismatch)]
        [TestCase(SessionFormationFailure.Rejected)]
        public void RejectionOrProviderDeadlineReturnsToMenuWithoutLeavingAnInventedMember(SessionFormationFailure failure)
        {
            using (var f = new Fixture())
            {
                f.BeginJoin();
                f.Provider.Next.Complete(SessionFormationResult.Reject(failure, "admission unavailable"));
                ApplicationOperationResult result = f.App.Update(16);
                Assert.That(result.Failure, Is.EqualTo(ApplicationFailure.SessionFormationFailed));
                Assert.That(result.Detail, Does.Contain(failure.ToString()));
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Error));
                Assert.That(f.App.RequestLeaveGame().Succeeded, Is.True);
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.MainMenu));
                Assert.That(f.LeaveCommands, Is.Zero);
                Assert.That(f.ComposeCalls, Is.Zero);
                Assert.That(f.Provider.Next.CancelCalls, Is.EqualTo(1));
                Assert.That(f.Provider.JoinBegins, Is.EqualTo(1));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PollFailureDetachesOperationEvenWhenCancellationAlsoThrows(bool cancelThrows)
        {
            using (var f = new Fixture())
            {
                f.BeginJoin();
                f.Provider.Next.ThrowOnPoll = true;
                f.Provider.Next.ThrowOnCancel = cancelThrows;
                ApplicationOperationResult result = f.App.Update(16);
                Assert.That(result.Failure, Is.EqualTo(ApplicationFailure.SessionFormationFailed));
                Assert.That(result.Detail, Does.Not.Contain("private-admission-token"));
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Provider.Next.PollCalls, Is.EqualTo(1));
                Assert.That(f.Provider.Next.CancelCalls, Is.EqualTo(1));
                Assert.That(f.App.TryCapturePartyScreen(out _), Is.False);
                Assert.That(f.ComposeCalls, Is.Zero);
            }
        }

        [Test]
        public void FailedCancellationReportsTeardownFailureButOldReplyStaysDetached()
        {
            using (var f = new Fixture())
            {
                f.BeginJoin();
                Operation old = f.Provider.Next;
                old.ThrowOnCancel = true;
                ApplicationOperationResult result = f.App.RequestLeaveGame();
                Assert.That(result.Failure, Is.EqualTo(ApplicationFailure.TeardownFailed));
                Assert.That(result.Detail, Does.Not.Contain("private-admission-token"));
                f.Provider.Next = new Operation();
                Assert.That(f.App.RequestJoin(JoinRequest()).Succeeded, Is.True);
                old.Complete(Admitted());
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(old.PollCalls, Is.Zero);
                Assert.That(f.App.TryCapturePartyScreen(out _), Is.False);
            }
        }

        [Test]
        public void ProviderCannotReenterPollingOrStartAnotherRequestDuringCancellation()
        {
            using (var f = new Fixture())
            {
                f.BeginJoin();
                f.Provider.Next.OnPoll = () => Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                Assert.That(f.Provider.Next.PollCalls, Is.EqualTo(1));
                f.Provider.Next.OnCancel = () => Assert.That(f.App.RequestJoin(JoinRequest()).Failure,
                    Is.EqualTo(ApplicationFailure.Busy));
                Assert.That(f.App.RequestLeaveGame().Succeeded, Is.True);
                Assert.That(f.Provider.JoinBegins, Is.EqualTo(1));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReentrantDisposalDuringBeginOrPollCannotAdoptReply(bool duringPoll)
        {
            using (var f = new Fixture())
            {
                f.Boot();
                if (duringPoll)
                {
                    Assert.That(f.App.RequestJoin(JoinRequest()).Succeeded, Is.True);
                    f.Provider.Next.Complete(Admitted());
                    f.Provider.Next.OnPoll = f.App.Dispose;
                    Assert.That(f.App.Update(16).Succeeded, Is.True);
                }
                else
                {
                    f.Provider.OnBegin = f.App.Dispose;
                    Assert.That(f.App.RequestJoin(JoinRequest()).Failure, Is.EqualTo(ApplicationFailure.InvalidState));
                }
                Assert.That(f.Provider.Next.CancelCalls, Is.EqualTo(1));
                Assert.That(f.ComposeCalls, Is.Zero);
                Assert.That(f.ProjectionReads, Is.Zero);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdoptedPartyCannotBeSilentlyReplacedByAnotherAsyncFormation(bool host)
        {
            using (var f = new Fixture())
            {
                f.BeginJoin();
                f.Provider.Next.Complete(Admitted());
                Assert.That(f.App.Update(16).Succeeded, Is.True);
                ApplicationOperationResult result = host ? f.App.RequestHost(HostRequest()) : f.App.RequestJoin(JoinRequest());
                Assert.That(result.Failure, Is.EqualTo(ApplicationFailure.InvalidState));
                Assert.That(f.Provider.JoinBegins, Is.EqualTo(1));
                Assert.That(f.Provider.HostBegins, Is.Zero);
                Assert.That(f.Provider.Next.CancelCalls, Is.Zero);
                Assert.That(f.App.TryCapturePartyScreen(out _), Is.True);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SynchronousProvidersRetainImmediateFormationBehavior(bool host)
        {
            var provider = new SynchronousProvider();
            using (var f = new Fixture(provider))
            {
                f.Boot();
                Assert.That((host ? f.App.RequestHost(HostRequest()) : f.App.RequestJoin(JoinRequest())).Succeeded, Is.True);
                Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Party));
                Assert.That(f.App.TryCapturePartyScreen(out _), Is.True);
                Assert.That(f.ComposeCalls, Is.Zero);
                Assert.That(provider.Calls, Is.EqualTo(1));
                Assert.That(f.Provider.JoinBegins + f.Provider.HostBegins, Is.Zero);
            }
        }

        [Test]
        public void DefaultSynchronousResultCannotInventAnAdmittedParty()
        {
            using (var f = new Fixture(new SynchronousProvider { Result = default }))
            {
                f.Boot();
                Assert.That(f.App.RequestJoin(JoinRequest()).Failure, Is.EqualTo(ApplicationFailure.SessionFormationFailed));
                Assert.That(f.App.TryCapturePartyScreen(out _), Is.False);
                Assert.That(f.ComposeCalls, Is.Zero);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidSessionRequestCannotStartProviderWork(bool host)
        {
            using (var f = new Fixture())
            {
                f.Boot();
                Assert.That((host ? f.App.RequestHost(default) : f.App.RequestJoin(default)).Failure,
                    Is.EqualTo(ApplicationFailure.SessionFormationFailed));
                Assert.That(f.Provider.JoinBegins + f.Provider.HostBegins, Is.Zero);
            }
        }

        [Test]
        public void AdmissionUnwindsNavigationAndCancelAllowsNormalNewGame()
        {
            using (var f = new Fixture())
            {
                f.Boot();
                Assert.That(f.App.OpenScreen(ApplicationScreen.Multiplayer).Succeeded, Is.True);
                Assert.That(f.ActiveLeases, Is.EqualTo(1));
                Assert.That(f.App.RequestJoin(JoinRequest()).Succeeded, Is.True);
                Assert.That(f.ActiveLeases, Is.Zero);
                Assert.That(f.App.RequestLeaveGame().Succeeded, Is.True);
                Assert.That(f.App.RequestNewGame(default).Succeeded, Is.True);
                Assert.That(f.ComposeCalls, Is.EqualTo(1));
                Assert.That(f.LeaveCommands, Is.Zero);
            }
        }

        [Test]
        public void NegativeElapsedTimeCannotPollPendingProvider()
        {
            using (var f = new Fixture())
            {
                f.BeginJoin();
                Assert.Throws<ArgumentOutOfRangeException>(() => f.App.Update(-1));
                Assert.That(f.Provider.Next.PollCalls, Is.Zero);
            }
        }

        // Only external formation/readiness inputs are controlled. All application and graph lifecycle
        // transitions execute the production coordinator and Orchestrator. No transport is simulated;
        // these assertions are not authority/client process or gameplay-convergence acceptance.
        private sealed class Fixture : IDisposable, ISessionSaveCatalog, IPartyScreenPresentationQuery,
            ISessionPresentationIntentRouter, IGameOutcomeQuery, IInputContextService,
            IInputBindingOverrideService, IUserPreferencesStore, IAudioPreferencesSink, IApplicationExitPort,
            IApplicationSessionPlanProvider, ISessionRuntimeGraphFactory
        {
            public readonly Provider Provider = new Provider();
            public readonly ApplicationFlowCoordinator App;
            public readonly GameSessionOrchestrator Session;
            public SessionPresentationLifecycle PartyLifecycle = SessionPresentationLifecycle.WaitingForPlayers;
            public bool MemberReady;
            public bool ReadyAfterInitialize = true;
            public bool ReadyOnTick;
            public bool AllowStart;
            public int ComposeCalls, InitializeCalls, DisposeCalls, ProjectionReads, StartCommands, LeaveCommands, ExitCalls, ActiveLeases;
            public SessionFormationResult PlannedFormation;
            public Fixture(ISessionFormationService formation = null)
            {
                Session = new GameSessionOrchestrator(this);
                App = new ApplicationFlowCoordinator(Session, this, formation ?? Provider, this, this, this,
                    this, this, this, this, this, this);
            }
            public void Boot() => Assert.That(App.CompleteBoot().Succeeded, Is.True);
            public void BeginJoin() { Boot(); Assert.That(App.RequestJoin(JoinRequest()).Succeeded, Is.True); }
            public void Dispose() { App.Dispose(); Session.Shutdown(); }
            public ISessionRuntimeGraph Compose(GameSessionIdentity identity) { ComposeCalls++; return new Graph(this); }
            public IReadOnlyList<SessionSaveMetadata> ListSaves() => Array.Empty<SessionSaveMetadata>();
            public PartyScreenPresentationSnapshot CapturePartyScreen(PartyMemberId localMemberId)
            {
                ProjectionReads++;
                return new PartyScreenPresentationSnapshot(PartyId, 3, PartyLifecycle, false, new[]
                {
                    new PartyMemberPresentationSnapshot(MemberId, default, default, PartyLeadershipRole.Member,
                        localMemberId == MemberId, MemberConnectionPresentationState.Connected,
                        MemberReady ? MemberReadinessPresentationState.GameplayReady : MemberReadinessPresentationState.Synchronizing,
                        MemberReady, MemberReady, default)
                });
            }
            public PartySessionCommandResult Request(SessionPresentationIntent intent)
            {
                if (intent.Kind == SessionPresentationIntentKind.Start)
                {
                    StartCommands++;
                    if (!AllowStart) return PartySessionCommandResult.Reject(PartySessionCommandFailure.NotLeader);
                }
                if (intent.Kind == SessionPresentationIntentKind.Leave) LeaveCommands++;
                return PartySessionCommandResult.Accept();
            }
            public GameSessionStartRequest PlanMultiplayer(SessionFormationResult formation)
            {
                PlannedFormation = formation;
                return GameSessionStartRequest.NewGame(new GameSessionIdentity("campaign", "world", formation.SessionId.Value, "configuration"));
            }
            public GameSessionStartRequest PlanNewGame(ApplicationSessionDescriptor descriptor) =>
                GameSessionStartRequest.NewGame(new GameSessionIdentity("campaign", "world", "new-session", "configuration"));
            public GameSessionStartRequest PlanContinue(SessionSaveMetadata save) => throw new NotSupportedException();
            public GameOutcomeSnapshot Snapshot() => GameOutcomeSnapshot.Running();
            public InputContextId ActiveContext => ActiveLeases == 0 ? InputContextId.Exploration : InputContextId.Ui;
            public IInputContextLease Push(InputContextId context) { ActiveLeases++; return new Lease(this, context); }
            public IReadOnlyList<InputBindingOverride> SnapshotOverrides() => Array.Empty<InputBindingOverride>();
            public bool TryApplyOverride(InputBindingOverride bindingOverride, out string error) { error = string.Empty; return true; }
            public void ClearOverrides() { }
            public bool TryLoad(out UserPreferences preferences) { preferences = null; return false; }
            public void Save(UserPreferences preferences) { }
            public void Apply(UserPreferences preferences) { }
            public void RequestExit() { ExitCalls++; }

            private sealed class Lease : IInputContextLease
            {
                private Fixture _owner;
                public InputContextId Context { get; }
                public Lease(Fixture owner, InputContextId context) { _owner = owner; Context = context; }
                public void Dispose() { if (_owner == null) return; _owner.ActiveLeases--; _owner = null; }
            }
            private sealed class Graph : ISessionRuntimeGraph, ISessionUpdateStep
            {
                private readonly Fixture _owner;
                private bool _ready = true, _disposed;
                public Graph(Fixture owner) { _owner = owner; UpdateSteps = Array.AsReadOnly(new ISessionUpdateStep[] { this }); }
                public bool GameplayBindingsReady => !_disposed && _ready;
                public IReadOnlyList<ISessionUpdateStep> UpdateSteps { get; }
                public IGameOutcomeQuery OutcomeQuery => null;
                public SessionUpdatePhase Phase => SessionUpdatePhase.Replication;
                public int Order => 0;
                public string SemanticId => "application.pending-formation.readiness";
                public void InitializeNewGame() { _owner.InitializeCalls++; _ready = _owner.ReadyAfterInitialize; }
                public void Tick(int elapsedMilliseconds) { _ready |= _owner.ReadyOnTick; }
                public void StartCommands() { }
                public void StopCommands() { }
                public void SettleAuthoritativeState() { }
                public void DetachExternalAdapters() { }
                public void Dispose() { if (_disposed) return; _disposed = true; _owner.DisposeCalls++; }
            }
        }

        private sealed class Provider : IAsyncSessionFormationService
        {
            public Operation Next = new Operation();
            public int JoinBegins, HostBegins, LegacyCalls;
            public bool ThrowOnBegin, ReturnNull;
            public Action OnBegin;
            public ISessionFormationOperation BeginHost(HostSessionRequest request) { HostBegins++; return Begin(); }
            public ISessionFormationOperation BeginJoin(JoinSessionRequest request) { JoinBegins++; return Begin(); }
            private ISessionFormationOperation Begin()
            {
                OnBegin?.Invoke();
                if (ThrowOnBegin) throw new InvalidOperationException("private-admission-token");
                return ReturnNull ? null : Next;
            }
            public SessionFormationResult Host(HostSessionRequest request) { LegacyCalls++; throw new NotSupportedException(); }
            public SessionFormationResult Join(JoinSessionRequest request) { LegacyCalls++; throw new NotSupportedException(); }
        }

        // Deliberately permits hostile late results after Cancel to verify caller-side isolation.
        private sealed class Operation : ISessionFormationOperation
        {
            public bool Completed, ThrowOnPoll, ThrowOnCancel;
            public SessionFormationResult Result;
            public int PollCalls, CancelCalls;
            public Action OnPoll, OnCancel;
            public void Complete(SessionFormationResult result) { Result = result; Completed = true; }
            public bool TryGetResult(out SessionFormationResult result)
            {
                PollCalls++;
                OnPoll?.Invoke();
                if (ThrowOnPoll) throw new InvalidOperationException("private-admission-token");
                result = Result;
                return Completed;
            }
            public void Cancel()
            {
                CancelCalls++;
                OnCancel?.Invoke();
                if (ThrowOnCancel) throw new InvalidOperationException("private-admission-token");
            }
        }

        private sealed class SynchronousProvider : ISessionFormationService
        {
            public int Calls;
            public SessionFormationResult Result = Admitted();
            public SessionFormationResult Host(HostSessionRequest request) { Calls++; return Result; }
            public SessionFormationResult Join(JoinSessionRequest request) { Calls++; return Result; }
        }
    }
}
