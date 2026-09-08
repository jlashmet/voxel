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
using OrchestrationSnapshot = Game.SessionOrchestration.Api.GameSessionSnapshot;

namespace Game.Application.Tests
{
    public sealed class ApplicationPartyResumeTests
    {
        [Test]
        public void FormedHostCanStartPartyFromMatchingPublishedSave()
        {
            var fixture = new Fixture(MakeSave("rehost-save", "rehost-session"));
            Assert.That(fixture.App.CompleteBoot().Succeeded, Is.True);
            Assert.That(fixture.App.RequestHost(new HostSessionRequest(
                new GameSessionId("rehost-session"),
                new SessionStartupConfiguration(3, "protocol", "content", true),
                "host")).Succeeded, Is.True);

            ApplicationOperationResult result = fixture.App.RequestPartyResume("rehost-save");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.PartyIntents.Calls, Is.EqualTo(1));
            Assert.That(fixture.PartyIntents.Last.Kind, Is.EqualTo(SessionPresentationIntentKind.Start));
            Assert.That(fixture.Session.PrepareCalls, Is.EqualTo(1));
            Assert.That(fixture.Session.LastRequest.Kind, Is.EqualTo(GameSessionStartKind.Resume));
            Assert.That(fixture.Session.LastRequest.RestoreSourceId, Is.EqualTo("rehost-save"));
            Assert.That(fixture.Session.LastRequest.Identity.SessionId, Is.EqualTo("rehost-session"));
            Assert.That(fixture.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.InGame));
        }

        [Test]
        public void PartyResumeRejectsSaveFromDifferentSessionBeforeStartIntent()
        {
            var fixture = new Fixture(MakeSave("wrong-save", "different-session"));
            Assert.That(fixture.App.CompleteBoot().Succeeded, Is.True);
            Assert.That(fixture.App.RequestHost(new HostSessionRequest(
                new GameSessionId("rehost-session"),
                new SessionStartupConfiguration(3, "protocol", "content", true),
                "host")).Succeeded, Is.True);

            ApplicationOperationResult result = fixture.App.RequestPartyResume("wrong-save");

            Assert.That(result.Failure, Is.EqualTo(ApplicationFailure.SaveUnavailable));
            Assert.That(fixture.PartyIntents.Calls, Is.Zero);
            Assert.That(fixture.Session.PrepareCalls, Is.Zero);
            Assert.That(fixture.App.Snapshot.Lifecycle, Is.EqualTo(ApplicationLifecycle.FrontEnd));
        }

        private static SessionSaveMetadata MakeSave(string saveId, string sessionId)
        {
            return new SessionSaveMetadata(new GameSessionSnapshotHeader(
                1,
                new SessionSaveId(saveId),
                sessionId,
                new SessionContentId("campaign"),
                new SessionWorldId("world"),
                7,
                DateTime.UtcNow.Ticks,
                "multiplayer rehost"));
        }

        private sealed class Fixture
        {
            public readonly FakeSession Session = new FakeSession();
            public readonly SaveCatalog Saves;
            public readonly FakePartyIntents PartyIntents = new FakePartyIntents();
            public readonly ApplicationFlowCoordinator App;

            public Fixture(SessionSaveMetadata save)
            {
                Saves = new SaveCatalog(save);
                App = new ApplicationFlowCoordinator(
                    Session,
                    Saves,
                    new HostFormation(),
                    NoPartyQuery.Instance,
                    PartyIntents,
                    RunningOutcome.Instance,
                    NoInputContexts.Instance,
                    NoBindings.Instance,
                    DefaultPreferences.Instance,
                    NoAudio.Instance,
                    NoExit.Instance,
                    new Plans());
            }
        }

        private sealed class FakeSession : IGameSessionControl
        {
            private OrchestrationSnapshot _snapshot = new OrchestrationSnapshot(
                GameSessionLifecycle.Uninitialized, false, null, GameSessionFailure.None, string.Empty);

            public int PrepareCalls { get; private set; }
            public GameSessionStartRequest LastRequest { get; private set; }
            public OrchestrationSnapshot Snapshot => _snapshot;

            public GameSessionOperationResult Prepare(GameSessionStartRequest request)
            {
                PrepareCalls++;
                LastRequest = request;
                _snapshot = new OrchestrationSnapshot(
                    GameSessionLifecycle.Ready, true, null, GameSessionFailure.None, string.Empty);
                return GameSessionOperationResult.Success();
            }

            public GameSessionOperationResult EnterRunning()
            {
                _snapshot = new OrchestrationSnapshot(
                    GameSessionLifecycle.Running, true, null, GameSessionFailure.None, string.Empty);
                return GameSessionOperationResult.Success();
            }

            public GameSessionOperationResult Tick(int elapsedMilliseconds) => GameSessionOperationResult.Success();
            public GameSessionOperationResult Capture() => GameSessionOperationResult.Success();
            public GameSessionOperationResult Shutdown()
            {
                _snapshot = new OrchestrationSnapshot(
                    GameSessionLifecycle.Stopped, false, null, GameSessionFailure.None, string.Empty);
                return GameSessionOperationResult.Success();
            }
        }

        private sealed class SaveCatalog : ISessionSaveCatalog
        {
            private readonly SessionSaveMetadata[] _saves;
            public SaveCatalog(SessionSaveMetadata save) => _saves = new[] { save };
            public IReadOnlyList<SessionSaveMetadata> ListSaves() => _saves;
        }

        private sealed class HostFormation : ISessionFormationService
        {
            public SessionFormationResult Host(HostSessionRequest request) =>
                SessionFormationResult.Success(request.SessionId, new PartyMemberId("host"));

            public SessionFormationResult Join(JoinSessionRequest request) =>
                SessionFormationResult.Reject(SessionFormationFailure.ProviderUnavailable, "not used");
        }

        private sealed class FakePartyIntents : ISessionPresentationIntentRouter
        {
            public int Calls { get; private set; }
            public SessionPresentationIntent Last { get; private set; }
            public PartySessionCommandResult Request(SessionPresentationIntent intent)
            {
                Calls++;
                Last = intent;
                return PartySessionCommandResult.Accept();
            }
        }

        private sealed class Plans : IApplicationSessionPlanProvider
        {
            public GameSessionStartRequest PlanNewGame(ApplicationSessionDescriptor descriptor) =>
                GameSessionStartRequest.NewGame(new GameSessionIdentity(
                    descriptor.CampaignId, descriptor.WorldId, descriptor.SessionId, descriptor.ConfigurationId));

            public GameSessionStartRequest PlanContinue(SessionSaveMetadata save) =>
                GameSessionStartRequest.Resume(
                    new GameSessionIdentity(
                        save.ContentId.Value, save.WorldId.Value, save.SessionId, "fixture-config"),
                    save.SaveId.Value);

            public GameSessionStartRequest PlanMultiplayer(SessionFormationResult formation) =>
                GameSessionStartRequest.NewGame(new GameSessionIdentity(
                    "campaign", "world", formation.SessionId.Value, "fixture-config"));
        }

        private sealed class NoPartyQuery : IPartyScreenPresentationQuery
        {
            public static readonly NoPartyQuery Instance = new NoPartyQuery();
            public PartyScreenPresentationSnapshot CapturePartyScreen(PartyMemberId localMemberId) => null;
        }

        private sealed class RunningOutcome : IGameOutcomeQuery
        {
            public static readonly RunningOutcome Instance = new RunningOutcome();
            public GameOutcomeSnapshot Snapshot() => GameOutcomeSnapshot.Running();
        }

        private sealed class NoInputContexts : IInputContextService
        {
            public static readonly NoInputContexts Instance = new NoInputContexts();
            public InputContextId ActiveContext => InputContextId.Exploration;
            public IInputContextLease Push(InputContextId context) => new Lease(context);

            private sealed class Lease : IInputContextLease
            {
                public Lease(InputContextId context) => Context = context;
                public InputContextId Context { get; }
                public void Dispose() { }
            }
        }

        private sealed class NoBindings : IInputBindingOverrideService
        {
            public static readonly NoBindings Instance = new NoBindings();
            public IReadOnlyList<InputBindingOverride> SnapshotOverrides() => Array.Empty<InputBindingOverride>();
            public bool TryApplyOverride(InputBindingOverride bindingOverride, out string error)
            {
                error = string.Empty;
                return true;
            }
            public void ClearOverrides() { }
        }

        private sealed class DefaultPreferences : IUserPreferencesStore
        {
            public static readonly DefaultPreferences Instance = new DefaultPreferences();
            public bool TryLoad(out UserPreferences preferences)
            {
                preferences = UserPreferences.Default;
                return true;
            }
            public void Save(UserPreferences preferences) { }
        }

        private sealed class NoAudio : IAudioPreferencesSink
        {
            public static readonly NoAudio Instance = new NoAudio();
            public void Apply(UserPreferences preferences) { }
        }

        private sealed class NoExit : IApplicationExitPort
        {
            public static readonly NoExit Instance = new NoExit();
            public void RequestExit() { }
        }
    }
}
