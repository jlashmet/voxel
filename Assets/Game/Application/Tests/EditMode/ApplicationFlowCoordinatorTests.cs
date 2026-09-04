using System;
using System.Collections.Generic;
using Game.Application.Api;
using Game.Application.Runtime;
using Game.Input.Api;
using Game.Outcomes.Api;
using Game.Persistence.Api;
using Game.SessionOrchestration.Api;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;
using NUnit.Framework;
using GameSessionSnapshot = Game.SessionOrchestration.Api.GameSessionSnapshot;

namespace Game.Application.Tests
{
    public sealed class ApplicationFlowCoordinatorTests
    {
        [Test]
        public void NewGameWaitsForAuthoritativeGameplayReady()
        {
            Fixture f = new Fixture();
            f.Session.ReadyOnEnter = false;
            Assert.That(f.App.CompleteBoot().Succeeded, Is.True);

            Assert.That(f.App.RequestNewGame(Descriptor("new-session")).Succeeded, Is.True);
            Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.StartingSession));
            Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Loading));
            Assert.That(f.Session.LastRequest.Kind, Is.EqualTo(GameSessionStartKind.NewGame));

            f.Session.ReadyOnTick = true;
            Assert.That(f.App.Update(16).Succeeded, Is.True);
            Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.InGame));
            Assert.That(f.App.Snapshot.GameplayReady, Is.True);
        }

        [Test]
        public void InvalidAndDuplicateLifecycleTransitionsAreRejected()
        {
            Fixture f = new Fixture();
            Assert.That(f.App.CompleteBoot().Succeeded, Is.True);
            Assert.That(f.App.CompleteBoot().Failure, Is.EqualTo(ApplicationFailure.InvalidState));
            f.Session.ReadyOnEnter = false;
            Assert.That(f.App.RequestNewGame(Descriptor("one")).Succeeded, Is.True);
            Assert.That(f.App.RequestNewGame(Descriptor("two")).Failure, Is.EqualTo(ApplicationFailure.InvalidState));
        }

        [Test]
        public void ContinueSelectsPublishedSaveAndUsesNormalResumeRequest()
        {
            Fixture f = new Fixture();
            f.Saves.Items.Add(MakeSave("save-a", "restored-session"));
            f.Session.ReadyOnEnter = true;
            f.App.CompleteBoot();

            Assert.That(f.App.RequestContinue("save-a").Succeeded, Is.True);
            Assert.That(f.Session.LastRequest.Kind, Is.EqualTo(GameSessionStartKind.Resume));
            Assert.That(f.Session.LastRequest.RestoreSourceId, Is.EqualTo("save-a"));
            Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.InGame));
            Assert.That(f.App.RequestContinue("missing").Failure, Is.EqualTo(ApplicationFailure.InvalidState));
        }

        [Test]
        public void MissingContinueSaveReturnsUsefulFrontendFailureWithoutPreparingGraph()
        {
            Fixture f = new Fixture();
            f.App.CompleteBoot();
            ApplicationOperationResult result = f.App.RequestContinue("missing");
            Assert.That(result.Failure, Is.EqualTo(ApplicationFailure.SaveUnavailable));
            Assert.That(result.Detail, Does.Contain("missing"));
            Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.FrontEnd));
            Assert.That(f.Session.PrepareCalls, Is.Zero);
        }

        [Test]
        public void HostAndJoinUseFormationAndPartyPresentationSeams()
        {
            Fixture host = new Fixture();
            host.App.CompleteBoot();
            HostSessionRequest hostRequest = new HostSessionRequest(
                new GameSessionId("party-a"),
                new SessionStartupConfiguration(4, "p1", "content", true),
                "local-host");
            Assert.That(host.App.RequestHost(hostRequest).Succeeded, Is.True);
            Assert.That(host.Formation.HostCalls, Is.EqualTo(1));
            Assert.That(host.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Party));
            Assert.That(host.App.TryCapturePartyScreen(out PartyScreenPresentationSnapshot party), Is.True);
            Assert.That(party.SessionId.Value, Is.EqualTo("party-a"));

            Fixture join = new Fixture();
            join.Formation.JoinResult = SessionFormationResult.Success(new GameSessionId("party-b"), new PartyMemberId("joiner"));
            join.Party.SessionId = new GameSessionId("party-b");
            join.App.CompleteBoot();
            JoinSessionRequest joinRequest = new JoinSessionRequest(new JoinRequest(new GameSessionId("party-b"), "joiner-key", "p1", "content"));
            Assert.That(join.App.RequestJoin(joinRequest).Succeeded, Is.True);
            Assert.That(join.Formation.JoinCalls, Is.EqualTo(1));
            Assert.That(join.App.TryCapturePartyScreen(out party), Is.True);
            Assert.That(party.SessionId.Value, Is.EqualTo("party-b"));
        }

        [Test]
        public void PartyStartRoutesSessionPresentationBeforeOrchestration()
        {
            Fixture f = new Fixture();
            f.Session.ReadyOnEnter = true;
            f.App.CompleteBoot();
            f.App.RequestHost(new HostSessionRequest(
                new GameSessionId("party-a"),
                new SessionStartupConfiguration(4, "p1", "content", true),
                "host"));

            Assert.That(f.App.RequestPartyStart().Succeeded, Is.True);
            Assert.That(f.PartyIntent.LastIntent.Kind, Is.EqualTo(SessionPresentationIntentKind.Start));
            Assert.That(f.Session.LastRequest.Kind, Is.EqualTo(GameSessionStartKind.NewGame));
            Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.InGame));
        }

        [Test]
        public void NestedMenusPushAndPopUiContextInDeterministicOrder()
        {
            Fixture f = new Fixture();
            f.App.CompleteBoot();
            Assert.That(f.Input.ActiveContext, Is.EqualTo(InputContextId.Exploration));
            f.App.OpenScreen(ApplicationScreen.Settings);
            f.App.OpenScreen(ApplicationScreen.Multiplayer);
            Assert.That(f.Input.ActiveContext, Is.EqualTo(InputContextId.Ui));
            Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Multiplayer));

            f.App.CloseScreen();
            Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Settings));
            Assert.That(f.Input.ActiveContext, Is.EqualTo(InputContextId.Ui));
            f.App.CloseScreen();
            Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.MainMenu));
            Assert.That(f.Input.ActiveContext, Is.EqualTo(InputContextId.Exploration));
        }

        [Test]
        public void PreferencesAndBindingOverridesSurviveCoordinatorRestart()
        {
            MemoryPreferences store = new MemoryPreferences();
            Fixture first = new Fixture(store);
            first.App.CompleteBoot();
            UserPreferences prefs = new UserPreferences(
                0.35f,
                1.25f,
                new[] { new InputBindingOverride("Confirm", 0, "<Keyboard>/f") });
            Assert.That(first.App.ApplyPreferences(prefs).Succeeded, Is.True);
            Assert.That(store.SaveCalls, Is.EqualTo(1));
            first.App.Dispose();

            Fixture second = new Fixture(store);
            Assert.That(second.App.CompleteBoot().Succeeded, Is.True);
            Assert.That(second.Bindings.Applied, Has.Count.EqualTo(1));
            Assert.That(second.Bindings.Applied[0].OverridePath, Is.EqualTo("<Keyboard>/f"));
            Assert.That(second.Audio.Last.MasterVolume, Is.EqualTo(0.35f));
            Assert.That(second.Audio.Last.UiScale, Is.EqualTo(1.25f));
        }

        [Test]
        public void LeaveReturnsFrontendAndQuitExitsAfterSemanticTeardown()
        {
            Fixture leave = new Fixture();
            leave.Session.ReadyOnEnter = true;
            leave.App.CompleteBoot();
            leave.App.RequestNewGame(Descriptor("leave-session"));
            Assert.That(leave.App.RequestLeaveGame().Succeeded, Is.True);
            Assert.That(leave.Session.ShutdownCalls, Is.EqualTo(1));
            Assert.That(leave.Exit.Calls, Is.Zero);
            Assert.That(leave.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.FrontEnd));

            Fixture quit = new Fixture();
            quit.Session.ReadyOnEnter = true;
            quit.App.CompleteBoot();
            quit.App.RequestNewGame(Descriptor("quit-session"));
            Assert.That(quit.App.RequestQuitApplication().Succeeded, Is.True);
            Assert.That(quit.Session.ShutdownCalls, Is.EqualTo(1));
            Assert.That(quit.Exit.Calls, Is.EqualTo(1));
            Assert.That(quit.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.Exiting));
        }

        [Test]
        public void FormedPartyLeaveRoutesSessionPresentationBeforeReturningFrontend()
        {
            Fixture f = new Fixture();
            f.App.CompleteBoot();
            f.App.RequestHost(new HostSessionRequest(
                new GameSessionId("party-a"),
                new SessionStartupConfiguration(4, "p1", "content", true),
                "host"));
            Assert.That(f.App.RequestLeaveGame().Succeeded, Is.True);
            Assert.That(f.PartyIntent.LastIntent.Kind, Is.EqualTo(SessionPresentationIntentKind.Leave));
            Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.FrontEnd));
        }

        [Test]
        public void FailedStartupReturnsFrontendErrorAndDoesNotLeaveHalfRunningGraph()
        {
            Fixture f = new Fixture();
            f.Session.FailPrepare = true;
            f.App.CompleteBoot();
            ApplicationOperationResult result = f.App.RequestNewGame(Descriptor("bad-session"));
            Assert.That(result.Failure, Is.EqualTo(ApplicationFailure.SessionPrepareFailed));
            Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.FrontEnd));
            Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Error));
            Assert.That(f.Session.ShutdownCalls, Is.EqualTo(1));
        }

        [Test]
        public void ResolvedOutcomeIsReadOnlyPresentationAndReturnsThroughTeardown()
        {
            Fixture f = new Fixture();
            f.Session.ReadyOnEnter = true;
            f.App.CompleteBoot();
            f.App.RequestNewGame(Descriptor("outcome-session"));
            f.Outcomes.Current = new GameOutcomeSnapshot(
                GameOutcomeLifecycle.Resolved,
                GameOutcomeDisposition.Success,
                new OutcomeRef("campaign-won"),
                new OutcomeResolutionId("resolution-1"),
                new OutcomeAuthorityRef("gameplay"),
                7);

            f.App.Update(16);
            Assert.That(f.App.Snapshot.Screen, Is.EqualTo(ApplicationScreen.Outcome));
            Assert.That(f.App.Snapshot.Detail, Does.Contain("campaign-won"));
            Assert.That(f.App.ReturnFromOutcome().Succeeded, Is.True);
            Assert.That(f.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.FrontEnd));
            Assert.That(f.Session.ShutdownCalls, Is.EqualTo(1));
        }

        private static ApplicationSessionDescriptor Descriptor(string sessionId) =>
            new ApplicationSessionDescriptor("campaign", "world", sessionId, "config");

        private static SessionSaveMetadata MakeSave(string saveId, string sessionId)
        {
            var header = new GameSessionSnapshotHeader(
                1,
                new SessionSaveId(saveId),
                sessionId,
                new SessionContentId("content"),
                new SessionWorldId("world"),
                3,
                DateTime.UtcNow.Ticks,
                "Test save");
            return new SessionSaveMetadata(header);
        }

        private sealed class Fixture
        {
            public readonly FakeSession Session = new FakeSession();
            public readonly FakeSaveCatalog Saves = new FakeSaveCatalog();
            public readonly FakeFormation Formation = new FakeFormation();
            public readonly FakePartyPresentation Party = new FakePartyPresentation();
            public readonly FakePartyIntent PartyIntent = new FakePartyIntent();
            public readonly FakeOutcomes Outcomes = new FakeOutcomes();
            public readonly FakeInputContexts Input = new FakeInputContexts();
            public readonly FakeBindings Bindings = new FakeBindings();
            public readonly MemoryPreferences Preferences;
            public readonly FakeAudio Audio = new FakeAudio();
            public readonly FakeExit Exit = new FakeExit();
            public readonly FakePlans Plans = new FakePlans();
            public readonly ApplicationFlowCoordinator App;

            public Fixture(MemoryPreferences preferences = null)
            {
                Preferences = preferences ?? new MemoryPreferences();
                App = new ApplicationFlowCoordinator(
                    Session, Saves, Formation, Party, PartyIntent, Outcomes, Input, Bindings,
                    Preferences, Audio, Exit, Plans);
            }
        }

        private sealed class FakeSession : IGameSessionControl
        {
            private GameSessionSnapshot _snapshot = new GameSessionSnapshot(GameSessionLifecycle.Uninitialized, false, null, GameSessionFailure.None, string.Empty);
            public bool ReadyOnEnter;
            public bool ReadyOnTick;
            public bool FailPrepare;
            public int PrepareCalls;
            public int ShutdownCalls;
            public GameSessionStartRequest LastRequest;
            public GameSessionSnapshot Snapshot => _snapshot;

            public GameSessionOperationResult Prepare(GameSessionStartRequest request)
            {
                PrepareCalls++;
                LastRequest = request;
                if (FailPrepare)
                {
                    _snapshot = new GameSessionSnapshot(GameSessionLifecycle.Failed, false, null, GameSessionFailure.CompositionFailed, "prepare failed");
                    return GameSessionOperationResult.Reject(GameSessionFailure.CompositionFailed, "prepare failed");
                }
                _snapshot = new GameSessionSnapshot(GameSessionLifecycle.Ready, false, null, GameSessionFailure.None, string.Empty);
                return GameSessionOperationResult.Success();
            }

            public GameSessionOperationResult EnterRunning()
            {
                _snapshot = new GameSessionSnapshot(GameSessionLifecycle.Running, ReadyOnEnter, null, GameSessionFailure.None, string.Empty);
                return GameSessionOperationResult.Success();
            }

            public GameSessionOperationResult Tick(int elapsedMilliseconds)
            {
                _snapshot = new GameSessionSnapshot(GameSessionLifecycle.Running, ReadyOnTick || _snapshot.GameplayReady, null, GameSessionFailure.None, string.Empty);
                return GameSessionOperationResult.Success();
            }

            public GameSessionOperationResult Capture() => GameSessionOperationResult.Success();

            public GameSessionOperationResult Shutdown()
            {
                ShutdownCalls++;
                _snapshot = new GameSessionSnapshot(GameSessionLifecycle.Stopped, false, null, GameSessionFailure.None, string.Empty);
                return GameSessionOperationResult.Success();
            }
        }

        private sealed class FakeSaveCatalog : ISessionSaveCatalog
        {
            public readonly List<SessionSaveMetadata> Items = new List<SessionSaveMetadata>();
            public IReadOnlyList<SessionSaveMetadata> ListSaves() => Items;
        }

        private sealed class FakeFormation : ISessionFormationService
        {
            public int HostCalls;
            public int JoinCalls;
            public SessionFormationResult HostResult = SessionFormationResult.Success(new GameSessionId("party-a"), new PartyMemberId("host"));
            public SessionFormationResult JoinResult = SessionFormationResult.Success(new GameSessionId("party-a"), new PartyMemberId("joiner"));
            public SessionFormationResult Host(HostSessionRequest request) { HostCalls++; return HostResult; }
            public SessionFormationResult Join(JoinSessionRequest request) { JoinCalls++; return JoinResult; }
        }

        private sealed class FakePartyPresentation : IPartyScreenPresentationQuery
        {
            public GameSessionId SessionId = new GameSessionId("party-a");
            public PartyScreenPresentationSnapshot CapturePartyScreen(PartyMemberId localMemberId) =>
                new PartyScreenPresentationSnapshot(SessionId, 4, SessionPresentationLifecycle.WaitingForPlayers, false, Array.Empty<PartyMemberPresentationSnapshot>());
        }

        private sealed class FakePartyIntent : ISessionPresentationIntentRouter
        {
            public SessionPresentationIntent LastIntent;
            public PartySessionCommandResult Request(SessionPresentationIntent intent)
            {
                LastIntent = intent;
                return PartySessionCommandResult.Accept();
            }
        }

        private sealed class FakeOutcomes : IGameOutcomeQuery
        {
            public GameOutcomeSnapshot Current = GameOutcomeSnapshot.Running();
            public GameOutcomeSnapshot Snapshot() => Current;
        }

        private sealed class FakeInputContexts : IInputContextService
        {
            private readonly List<Entry> _entries = new List<Entry>();
            private int _next;
            public InputContextId ActiveContext => _entries.Count == 0 ? InputContextId.Exploration : _entries[_entries.Count - 1].Context;
            public IInputContextLease Push(InputContextId context)
            {
                int id = ++_next;
                _entries.Add(new Entry(id, context));
                return new Lease(this, id, context);
            }
            private void Remove(int id)
            {
                for (int i = _entries.Count - 1; i >= 0; i--) if (_entries[i].Id == id) { _entries.RemoveAt(i); return; }
            }
            private readonly struct Entry { public readonly int Id; public readonly InputContextId Context; public Entry(int id, InputContextId context) { Id = id; Context = context; } }
            private sealed class Lease : IInputContextLease
            {
                private FakeInputContexts _owner; private readonly int _id; public InputContextId Context { get; }
                public Lease(FakeInputContexts owner, int id, InputContextId context) { _owner = owner; _id = id; Context = context; }
                public void Dispose() { FakeInputContexts owner = _owner; if (owner == null) return; _owner = null; owner.Remove(_id); }
            }
        }

        private sealed class FakeBindings : IInputBindingOverrideService
        {
            public readonly List<InputBindingOverride> Applied = new List<InputBindingOverride>();
            public IReadOnlyList<InputBindingOverride> SnapshotOverrides() => Applied;
            public bool TryApplyOverride(InputBindingOverride bindingOverride, out string error) { Applied.Add(bindingOverride); error = string.Empty; return true; }
            public void ClearOverrides() => Applied.Clear();
        }

        private sealed class MemoryPreferences : IUserPreferencesStore
        {
            public UserPreferences Value;
            public int SaveCalls;
            public bool TryLoad(out UserPreferences preferences) { preferences = Value; return Value != null; }
            public void Save(UserPreferences preferences) { Value = preferences; SaveCalls++; }
        }

        private sealed class FakeAudio : IAudioPreferencesSink
        {
            public UserPreferences Last = UserPreferences.Default;
            public void Apply(UserPreferences preferences) { Last = preferences; }
        }

        private sealed class FakeExit : IApplicationExitPort
        {
            public int Calls;
            public void RequestExit() { Calls++; }
        }

        private sealed class FakePlans : IApplicationSessionPlanProvider
        {
            public GameSessionStartRequest PlanNewGame(ApplicationSessionDescriptor descriptor) =>
                GameSessionStartRequest.NewGame(new GameSessionIdentity(descriptor.CampaignId, descriptor.WorldId, descriptor.SessionId, descriptor.ConfigurationId));

            public GameSessionStartRequest PlanContinue(SessionSaveMetadata save) =>
                GameSessionStartRequest.Resume(new GameSessionIdentity("campaign", save.WorldId.Value, save.SessionId, "config"), save.SaveId.Value);

            public GameSessionStartRequest PlanMultiplayer(SessionFormationResult formation) =>
                GameSessionStartRequest.NewGame(new GameSessionIdentity("campaign", "world", formation.SessionId.Value, "multiplayer"));
        }
    }
}
