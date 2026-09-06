using System;
using System.Collections;
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
using UnityEngine;

namespace Game.Application.Validation
{
    /// <summary>
    /// Module-local lifecycle discriminator. Real Unity frames drive the production frontend view,
    /// Application coordinator and Orchestrator. Only external admission/readiness inputs are supplied;
    /// this is not a provider/transport implementation or separate-process multiplayer acceptance.
    /// </summary>
    public sealed class ApplicationPendingFormationValidation : MonoBehaviour
    {
        private GameObject _viewObject;
        private ApplicationFrontendView _view;
        private ApplicationFlowCoordinator _app;
        private GameSessionOrchestrator _session;

        private IEnumerator Start()
        {
            IEnumerator validation = Validate();
            try
            {
                while (true)
                {
                    bool more;
                    object current;
                    try
                    {
                        more = validation.MoveNext();
                        current = more ? validation.Current : null;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError("APPLICATION_VALIDATION failure: pending formation " + exception);
                        throw;
                    }
                    if (!more) yield break;
                    yield return current;
                }
            }
            finally
            {
                (validation as IDisposable)?.Dispose();
                Cleanup();
            }
        }

        private IEnumerator Validate()
        {
            var inputs = new BoundaryInputs();
            var provider = new AdmissionInputs();
            _session = new GameSessionOrchestrator(inputs);
            _app = new ApplicationFlowCoordinator(_session, inputs, provider, inputs, inputs, inputs,
                inputs, inputs, inputs, inputs, inputs, inputs);
            Require(_app.CompleteBoot().Succeeded, "boot");
            Require(_app.RequestJoin(inputs.JoinRequest).Succeeded, "begin join");
            _viewObject = new GameObject("PendingFormationFrontendProbe");
            _view = _viewObject.AddComponent<ApplicationFrontendView>();
            _view.Bind(_app, new ApplicationFrontendConfiguration(
                new ApplicationSessionDescriptor("application-campaign", "application-world", "application-run", "application-config"),
                string.Empty,
                new HostSessionRequest(inputs.SessionId, new SessionStartupConfiguration(3, "v1", "content", true), "host"),
                inputs.JoinRequest));

            // Semantic waits, bounded by the unchanged ten-second owning scenario. Do not call
            // Application.Update here: a view that forgets FrontEnd must fail this discriminator.
            float deadline = Time.realtimeSinceStartup + 2f;
            while (provider.Current.PollCalls == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Require(provider.Current.PollCalls > 0, "production view polls FrontEnd");
            Require(_app.Snapshot.Screen == ApplicationScreen.Loading && !_app.TryCapturePartyScreen(out _) &&
                inputs.ComposeCalls == 0, "pending admission has no member or graph");
            Debug.Log("APPLICATION_VALIDATION pending-admission: viewPumped=True memberAdopted=False composes=0");

            AdmissionInput old = provider.Current;
            Require(_app.RequestLeaveGame().Succeeded && old.CancelCalls == 1, "cancel pending attempt");
            Require(_app.RequestJoin(inputs.JoinRequest).Succeeded, "fresh same-session attempt");
            Require(!ReferenceEquals(old, provider.Current), "attempt-local result ownership");
            old.Complete(SessionFormationResult.Success(inputs.SessionId, new PartyMemberId("abandoned-member")));
            deadline = Time.realtimeSinceStartup + 2f;
            while (provider.Current.PollCalls == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Require(provider.Current.PollCalls > 0 && _app.Snapshot.Screen == ApplicationScreen.Loading &&
                !_app.TryCapturePartyScreen(out _) && inputs.ComposeCalls == 0 && inputs.LeaveCalls == 0,
                "old completion cannot admit fresh request or issue member Leave");
            Debug.Log("APPLICATION_VALIDATION pending-cancel: staleIgnored=True leaves=0");

            provider.Current.Complete(SessionFormationResult.Success(inputs.SessionId, inputs.MemberId));
            deadline = Time.realtimeSinceStartup + 2f;
            while (_app.Snapshot.Screen == ApplicationScreen.Loading && Time.realtimeSinceStartup < deadline) yield return null;
            Require(_app.Snapshot.Screen == ApplicationScreen.Party && inputs.ComposeCalls == 0,
                "admission does not grant local synchronization");
            inputs.MemberReady = true;
            deadline = Time.realtimeSinceStartup + 2f;
            while (!_app.Snapshot.GameplayReady && Time.realtimeSinceStartup < deadline) yield return null;
            Require(_app.Snapshot.Lifecycle == ApplicationLifecycle.InGame && _app.Snapshot.GameplayReady &&
                inputs.ComposeCalls == 1 && inputs.InitializeCalls == 1 && inputs.StartCalls == 0,
                "view advances admitted join through real Orchestration exactly once");
            Debug.Log("APPLICATION_VALIDATION pending-start: lifecycle=InGame composes=1 startCommands=0 viewPumped=True");
            Require(_app.RequestLeaveGame().Succeeded && inputs.LeaveCalls == 1 && inputs.DisposeCalls == 1,
                "adopted party uses normal Leave and graph teardown");
            Require(provider.Current.CancelCalls == 0, "normal party Leave does not cancel an adopted operation");
            Debug.Log("APPLICATION_VALIDATION pending-leave: lifecycle=FrontEnd leaves=1 disposedGraphs=1");
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("Pending-formation invariant failed: " + detail);
        }

        private void OnDestroy() => Cleanup();
        private void Cleanup()
        {
            if (_view != null) _view.enabled = false;
            if (_viewObject != null) Destroy(_viewObject);
            _view = null;
            _viewObject = null;
            _app?.Dispose();
            _app = null;
            _session?.Shutdown();
            _session = null;
        }

        // Adversarial external boundary input, not a networking/runtime replacement. It intentionally
        // permits a terminal value after cancellation to exercise caller-side attempt isolation.
        private sealed class AdmissionInput : ISessionFormationOperation
        {
            public int PollCalls, CancelCalls;
            private bool _completed;
            private SessionFormationResult _result;
            public void Complete(SessionFormationResult result) { _result = result; _completed = true; }
            public bool TryGetResult(out SessionFormationResult result) { PollCalls++; result = _result; return _completed; }
            public void Cancel() { CancelCalls++; }
        }
        private sealed class AdmissionInputs : IAsyncSessionFormationService
        {
            public AdmissionInput Current;
            public ISessionFormationOperation BeginHost(HostSessionRequest request) => throw new NotSupportedException();
            public ISessionFormationOperation BeginJoin(JoinSessionRequest request) { Current = new AdmissionInput(); return Current; }
            public SessionFormationResult Host(HostSessionRequest request) => throw new NotSupportedException();
            public SessionFormationResult Join(JoinSessionRequest request) => throw new NotSupportedException();
        }

        private sealed class BoundaryInputs : ISessionSaveCatalog, IPartyScreenPresentationQuery,
            ISessionPresentationIntentRouter, IGameOutcomeQuery, IInputContextService,
            IInputBindingOverrideService, IUserPreferencesStore, IAudioPreferencesSink,
            IApplicationExitPort, IApplicationSessionPlanProvider, ISessionRuntimeGraphFactory
        {
            public readonly GameSessionId SessionId = new GameSessionId("application-pending-formation");
            public readonly PartyMemberId MemberId = new PartyMemberId("application-admitted-member");
            public bool MemberReady;
            public int ComposeCalls, InitializeCalls, DisposeCalls, StartCalls, LeaveCalls;
            public JoinSessionRequest JoinRequest => new JoinSessionRequest(new JoinRequest(SessionId, "applicant", "v1", "content"));
            public IReadOnlyList<SessionSaveMetadata> ListSaves() => Array.Empty<SessionSaveMetadata>();
            public PartyScreenPresentationSnapshot CapturePartyScreen(PartyMemberId localMemberId) =>
                new PartyScreenPresentationSnapshot(SessionId, 3, SessionPresentationLifecycle.Active, false, new[]
                {
                    new PartyMemberPresentationSnapshot(MemberId, default, default, PartyLeadershipRole.Member,
                        localMemberId == MemberId, MemberConnectionPresentationState.Connected,
                        MemberReady ? MemberReadinessPresentationState.GameplayReady : MemberReadinessPresentationState.Synchronizing,
                        false, MemberReady, default)
                });
            public PartySessionCommandResult Request(SessionPresentationIntent intent)
            {
                if (intent.Kind == SessionPresentationIntentKind.Start)
                {
                    StartCalls++;
                    return PartySessionCommandResult.Reject(PartySessionCommandFailure.NotLeader);
                }
                if (intent.Kind == SessionPresentationIntentKind.Leave) LeaveCalls++;
                return PartySessionCommandResult.Accept();
            }
            public ISessionRuntimeGraph Compose(GameSessionIdentity identity) { ComposeCalls++; return new ReadinessInputs(this); }
            public GameSessionStartRequest PlanMultiplayer(SessionFormationResult formation) =>
                GameSessionStartRequest.NewGame(new GameSessionIdentity("application-campaign", "application-world", formation.SessionId.Value, "application-config"));
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

            private sealed class ReadinessInputs : ISessionRuntimeGraph, ISessionUpdateStep
            {
                private readonly BoundaryInputs _inputs;
                private bool _ready = true, _disposed;
                public ReadinessInputs(BoundaryInputs inputs) { _inputs = inputs; UpdateSteps = Array.AsReadOnly(new ISessionUpdateStep[] { this }); }
                public bool GameplayBindingsReady => !_disposed && _ready;
                public IReadOnlyList<ISessionUpdateStep> UpdateSteps { get; }
                public IGameOutcomeQuery OutcomeQuery => null;
                public SessionUpdatePhase Phase => SessionUpdatePhase.Replication;
                public int Order => 0;
                public string SemanticId => "application.pending-formation.readiness";
                public void InitializeNewGame() { _inputs.InitializeCalls++; _ready = false; }
                public void Tick(int elapsedMilliseconds) { _ready = true; }
                public void StartCommands() { }
                public void StopCommands() { }
                public void SettleAuthoritativeState() { }
                public void DetachExternalAdapters() { }
                public void Dispose() { if (_disposed) return; _disposed = true; _inputs.DisposeCalls++; }
            }
        }
    }
}
