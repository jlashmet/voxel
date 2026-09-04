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
using UnityEngine;

namespace Game.Application.Validation
{
    public sealed class ApplicationFrontendValidationShowcase : MonoBehaviour
    {
        private readonly List<string> _proof = new List<string>();
        private ApplicationFlowCoordinator _app;

        private void Start()
        {
            try { RunValidation(); }
            catch (Exception ex)
            {
                Debug.LogError("APPLICATION_VALIDATION failure: " + ex);
                throw;
            }
        }

        private void RunValidation()
        {
            var session = new SessionStub();
            var saves = new SaveCatalogStub();
            var formation = new FormationStub();
            var party = new PartyPresentationStub();
            var partyIntents = new PartyIntentStub();
            var outcomes = new OutcomeStub();
            var input = new InputContextStub();
            var bindings = new BindingStub();
            var preferences = new PreferencesStub();
            var audio = new AudioStub();
            var exit = new ExitStub();
            var plans = new PlanStub();

            _app = new ApplicationFlowCoordinator(
                session, saves, formation, party, partyIntents, outcomes, input, bindings,
                preferences, audio, exit, plans);
            Require(_app.CompleteBoot().Succeeded, "boot");
            Require(_app.Snapshot.Lifecycle == ApplicationLifecycle.FrontEnd, "frontend lifecycle");
            Proof("APPLICATION_VALIDATION ready: lifecycle=FrontEnd screen=MainMenu");

            session.ReadyOnEnter = false;
            session.ReadyOnTick = true;
            Require(_app.RequestNewGame(new ApplicationSessionDescriptor("campaign", "world", "new-game", "production")).Succeeded, "new request");
            Require(_app.Snapshot.Lifecycle == ApplicationLifecycle.StartingSession, "new waits for readiness");
            Require(_app.Update(16).Succeeded && _app.Snapshot.Lifecycle == ApplicationLifecycle.InGame, "new promotion");
            Proof("APPLICATION_VALIDATION new-game: lifecycle=InGame ready=True");
            Require(_app.RequestLeaveGame().Succeeded, "new leave");

            saves.Items.Add(MakeSave("save-continue", "resume-session"));
            session.ReadyOnEnter = true;
            session.ReadyOnTick = false;
            Require(_app.RequestContinue("save-continue").Succeeded, "continue request");
            Require(session.LastRequest.Kind == GameSessionStartKind.Resume && session.LastRequest.RestoreSourceId == "save-continue", "resume semantics");
            Proof("APPLICATION_VALIDATION continue: restore=save-continue lifecycle=InGame");
            Require(_app.RequestLeaveGame().Succeeded, "continue leave");

            var hostRequest = new HostSessionRequest(new GameSessionId("hosted-party"), new SessionStartupConfiguration(4, "p1", "content", true), "host-key");
            formation.HostResult = SessionFormationResult.Success(new GameSessionId("hosted-party"), new PartyMemberId("host"));
            party.SessionId = new GameSessionId("hosted-party");
            Require(_app.RequestHost(hostRequest).Succeeded, "host");
            Require(_app.TryCapturePartyScreen(out PartyScreenPresentationSnapshot hostParty) && hostParty.SessionId.Value == "hosted-party", "host presentation");
            Require(_app.RequestLeaveGame().Succeeded && partyIntents.LeaveCalls == 1, "host leave");

            formation.JoinResult = SessionFormationResult.Success(new GameSessionId("joined-party"), new PartyMemberId("joiner"));
            party.SessionId = new GameSessionId("joined-party");
            var joinRequest = new JoinSessionRequest(new JoinRequest(new GameSessionId("joined-party"), "join-key", "p1", "content"));
            Require(_app.RequestJoin(joinRequest).Succeeded, "join");
            Require(_app.TryCapturePartyScreen(out PartyScreenPresentationSnapshot joinParty) && joinParty.SessionId.Value == "joined-party", "join presentation");
            Proof("APPLICATION_VALIDATION multiplayer: host=hosted-party join=joined-party partySemantic=True");
            Require(_app.RequestLeaveGame().Succeeded, "join leave");

            Require(_app.OpenScreen(ApplicationScreen.Settings).Succeeded, "settings open");
            Require(_app.OpenScreen(ApplicationScreen.Multiplayer).Succeeded, "nested open");
            Require(input.ActiveContext == InputContextId.Ui, "ui context");
            Require(_app.CloseScreen().Succeeded && _app.CloseScreen().Succeeded, "nested close");
            Require(input.ActiveContext == InputContextId.Exploration, "context unwind");
            var prefs = new UserPreferences(0.4f, 1.2f, new[] { new InputBindingOverride("Confirm", 0, "<Keyboard>/f") });
            Require(_app.ApplyPreferences(prefs).Succeeded && preferences.SaveCalls == 1 && bindings.Applied.Count == 1, "settings persist");
            Proof("APPLICATION_VALIDATION menus-settings: context=Exploration persisted=True binding=Confirm");

            session.ReadyOnEnter = true;
            Require(_app.RequestNewGame(new ApplicationSessionDescriptor("campaign", "world", "outcome-session", "production")).Succeeded, "outcome session");
            outcomes.Current = new GameOutcomeSnapshot(
                GameOutcomeLifecycle.Resolved,
                GameOutcomeDisposition.Success,
                new OutcomeRef("campaign-won"),
                new OutcomeResolutionId("resolution-1"),
                new OutcomeAuthorityRef("gameplay"),
                9);
            Require(_app.Update(16).Succeeded && _app.Snapshot.Screen == ApplicationScreen.Outcome, "outcome presentation");
            Require(_app.ReturnFromOutcome().Succeeded && _app.Snapshot.Lifecycle == ApplicationLifecycle.FrontEnd, "outcome return");
            Proof("APPLICATION_VALIDATION outcome-return: disposition=Success lifecycle=FrontEnd");

            var view = gameObject.AddComponent<ApplicationFrontendView>();
            view.Bind(_app, new ApplicationFrontendConfiguration(
                new ApplicationSessionDescriptor("campaign", "world", "configured-new", "production"),
                "save-continue",
                hostRequest,
                joinRequest));
            Require(view.IsBound, "production frontend view");
            Proof("APPLICATION_FRONTEND_VIEW ready: bound=True lifecycle=FrontEnd");
        }

        private void Proof(string line)
        {
            _proof.Add(line);
            Debug.Log(line);
        }

        private static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Validation invariant failed: " + label);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(480f, 28f, 760f, 420f), GUI.skin.box);
            GUILayout.Label("SYSTEM 23 — APPLICATION FLOW PROOF");
            GUILayout.Label("Semantic ownership: Sessions / Orchestration / Persistence / Outcomes / Input");
            for (int i = 0; i < _proof.Count; i++) GUILayout.Label("✓ " + _proof[i]);
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            _app?.Dispose();
        }

        private static SessionSaveMetadata MakeSave(string saveId, string sessionId) =>
            new SessionSaveMetadata(new GameSessionSnapshotHeader(
                1, new SessionSaveId(saveId), sessionId, new SessionContentId("content"), new SessionWorldId("world"), 5,
                DateTime.UtcNow.Ticks, "Validation save"));

        private sealed class SessionStub : IGameSessionControl
        {
            private GameSessionSnapshot _snapshot = new GameSessionSnapshot(GameSessionLifecycle.Uninitialized, false, null, GameSessionFailure.None, string.Empty);
            public bool ReadyOnEnter;
            public bool ReadyOnTick;
            public GameSessionStartRequest LastRequest;
            public GameSessionSnapshot Snapshot => _snapshot;
            public GameSessionOperationResult Prepare(GameSessionStartRequest request) { LastRequest = request; _snapshot = new GameSessionSnapshot(GameSessionLifecycle.Ready, false, null, GameSessionFailure.None, string.Empty); return GameSessionOperationResult.Success(); }
            public GameSessionOperationResult EnterRunning() { _snapshot = new GameSessionSnapshot(GameSessionLifecycle.Running, ReadyOnEnter, null, GameSessionFailure.None, string.Empty); return GameSessionOperationResult.Success(); }
            public GameSessionOperationResult Tick(int elapsedMilliseconds) { _snapshot = new GameSessionSnapshot(GameSessionLifecycle.Running, ReadyOnTick || _snapshot.GameplayReady, null, GameSessionFailure.None, string.Empty); return GameSessionOperationResult.Success(); }
            public GameSessionOperationResult Capture() => GameSessionOperationResult.Success();
            public GameSessionOperationResult Shutdown() { _snapshot = new GameSessionSnapshot(GameSessionLifecycle.Stopped, false, null, GameSessionFailure.None, string.Empty); return GameSessionOperationResult.Success(); }
        }

        private sealed class SaveCatalogStub : ISessionSaveCatalog
        {
            public readonly List<SessionSaveMetadata> Items = new List<SessionSaveMetadata>();
            public IReadOnlyList<SessionSaveMetadata> ListSaves() => Items;
        }

        private sealed class FormationStub : ISessionFormationService
        {
            public SessionFormationResult HostResult = SessionFormationResult.Success(new GameSessionId("hosted-party"), new PartyMemberId("host"));
            public SessionFormationResult JoinResult = SessionFormationResult.Success(new GameSessionId("joined-party"), new PartyMemberId("joiner"));
            public SessionFormationResult Host(HostSessionRequest request) => HostResult;
            public SessionFormationResult Join(JoinSessionRequest request) => JoinResult;
        }

        private sealed class PartyPresentationStub : IPartyScreenPresentationQuery
        {
            public GameSessionId SessionId = new GameSessionId("hosted-party");
            public PartyScreenPresentationSnapshot CapturePartyScreen(PartyMemberId localMemberId) =>
                new PartyScreenPresentationSnapshot(SessionId, 4, SessionPresentationLifecycle.WaitingForPlayers, false, Array.Empty<PartyMemberPresentationSnapshot>());
        }

        private sealed class PartyIntentStub : ISessionPresentationIntentRouter
        {
            public int LeaveCalls;
            public PartySessionCommandResult Request(SessionPresentationIntent intent) { if (intent.Kind == SessionPresentationIntentKind.Leave) LeaveCalls++; return PartySessionCommandResult.Accept(); }
        }

        private sealed class OutcomeStub : IGameOutcomeQuery
        {
            public GameOutcomeSnapshot Current = GameOutcomeSnapshot.Running();
            public GameOutcomeSnapshot Snapshot() => Current;
        }

        private sealed class InputContextStub : IInputContextService
        {
            private readonly List<Lease> _stack = new List<Lease>();
            public InputContextId ActiveContext => _stack.Count == 0 ? InputContextId.Exploration : _stack[_stack.Count - 1].Context;
            public IInputContextLease Push(InputContextId context) { var lease = new Lease(this, context); _stack.Add(lease); return lease; }
            private void Remove(Lease lease) => _stack.Remove(lease);
            private sealed class Lease : IInputContextLease
            {
                private InputContextStub _owner; public InputContextId Context { get; }
                public Lease(InputContextStub owner, InputContextId context) { _owner = owner; Context = context; }
                public void Dispose() { InputContextStub owner = _owner; if (owner == null) return; _owner = null; owner.Remove(this); }
            }
        }

        private sealed class BindingStub : IInputBindingOverrideService
        {
            public readonly List<InputBindingOverride> Applied = new List<InputBindingOverride>();
            public IReadOnlyList<InputBindingOverride> SnapshotOverrides() => Applied;
            public bool TryApplyOverride(InputBindingOverride bindingOverride, out string error) { Applied.Add(bindingOverride); error = string.Empty; return true; }
            public void ClearOverrides() => Applied.Clear();
        }

        private sealed class PreferencesStub : IUserPreferencesStore
        {
            public UserPreferences Value; public int SaveCalls;
            public bool TryLoad(out UserPreferences preferences) { preferences = Value; return Value != null; }
            public void Save(UserPreferences preferences) { Value = preferences; SaveCalls++; }
        }

        private sealed class AudioStub : IAudioPreferencesSink { public void Apply(UserPreferences preferences) { } }
        private sealed class ExitStub : IApplicationExitPort { public void RequestExit() { } }

        private sealed class PlanStub : IApplicationSessionPlanProvider
        {
            public GameSessionStartRequest PlanNewGame(ApplicationSessionDescriptor descriptor) => GameSessionStartRequest.NewGame(new GameSessionIdentity(descriptor.CampaignId, descriptor.WorldId, descriptor.SessionId, descriptor.ConfigurationId));
            public GameSessionStartRequest PlanContinue(SessionSaveMetadata save) => GameSessionStartRequest.Resume(new GameSessionIdentity("campaign", save.WorldId.Value, save.SessionId, "production"), save.SaveId.Value);
            public GameSessionStartRequest PlanMultiplayer(SessionFormationResult formation) => GameSessionStartRequest.NewGame(new GameSessionIdentity("campaign", "world", formation.SessionId.Value, "multiplayer"));
        }
    }
}
