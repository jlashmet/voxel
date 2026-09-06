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
using UnityEngine;

namespace Game.Application.Validation
{
    /// <summary>
    /// Non-visual Application lifecycle discriminator using production Orchestration. Inputs model
    /// readiness boundaries only; this is not a transport, gameplay, or multi-process implementation.
    /// </summary>
    public sealed class ApplicationJoinedPartyValidation : MonoBehaviour
    {
        private void Start()
        {
            try { Validate(); }
            catch (Exception exception)
            {
                Debug.LogError("APPLICATION_VALIDATION failure: " + exception);
                throw;
            }
        }

        private static void Validate()
        {
            var inputs = new ExternalInputs();
            var graphs = new ReadinessInputsFactory();
            var session = new GameSessionOrchestrator(graphs);
            using (var app = new ApplicationFlowCoordinator(session, inputs, inputs, inputs, inputs,
                inputs, inputs, inputs, inputs, inputs, inputs, inputs))
            {
                try
                {
                    Require(app.CompleteBoot().Succeeded, "joined boot");
                    Require(app.RequestJoin(inputs.JoinRequest).Succeeded, "joined formation input");
                    Require(app.Update(16).Succeeded && graphs.ComposeCalls == 0, "wait for leader start");
                    inputs.Lifecycle = SessionPresentationLifecycle.Active;
                    Require(app.Update(16).Succeeded && graphs.ComposeCalls == 0, "active is not local synchronization");
                    Require(app.Snapshot.Lifecycle == ApplicationLifecycle.FrontEnd, "unready join remains frontend");
                    Debug.Log("APPLICATION_VALIDATION joined-sync-wait: lifecycle=FrontEnd composes=0");

                    graphs.ReadyBeforePrepare = true;
                    inputs.MemberReady = true;
                    Require(app.Update(16).Succeeded, "joined production composition");
                    Require(app.Snapshot.Lifecycle == ApplicationLifecycle.StartingSession, "post-initialization readiness wait");
                    Require(session.Snapshot.Lifecycle == GameSessionLifecycle.Running && !app.Snapshot.GameplayReady,
                        "application retains production graph readiness gate");
                    Require(app.Update(16).Succeeded && !app.Snapshot.GameplayReady, "not ready until graph update");
                    graphs.ReadyOnTick = true;
                    Require(app.Update(16).Succeeded && app.Snapshot.GameplayReady, "production graph ready");
                    Require(app.Update(16).Succeeded && graphs.ComposeCalls == 1 && graphs.InitializeCalls == 1,
                        "production graph initialized exactly once");
                    Require(inputs.StartCalls == 0, "client did not authorize start");
                    Debug.Log("APPLICATION_VALIDATION joined-start: lifecycle=InGame ready=True startCommands=0 prepares=1 productionOrchestration=True");

                    Require(app.RequestLeaveGame().Succeeded, "joined leave");
                    Require(app.Update(16).Succeeded && graphs.ComposeCalls == 1, "no stale active-party restart");
                    Require(app.Snapshot.Lifecycle == ApplicationLifecycle.FrontEnd &&
                        session.Snapshot.Lifecycle == GameSessionLifecycle.Stopped &&
                        inputs.LeaveCalls == 1 && graphs.DisposeCalls == 1, "production teardown");
                    Debug.Log("APPLICATION_VALIDATION joined-leave: lifecycle=FrontEnd noRestart=True");

                    Require(app.RequestJoin(inputs.JoinRequest).Succeeded, "fresh join after explicit leave");
                    Require(app.Update(16).Succeeded && app.Update(16).Succeeded && app.Snapshot.GameplayReady,
                        "fresh composed graph reaches readiness");
                    Require(graphs.ComposeCalls == 2 && graphs.InitializeCalls == 2 && inputs.StartCalls == 0,
                        "one graph per successful formation");
                    Debug.Log("APPLICATION_VALIDATION joined-rejoin: lifecycle=InGame composes=2 startCommands=0");
                    Require(app.RequestLeaveGame().Succeeded && graphs.DisposeCalls == 2, "fresh graph teardown");
                }
                finally
                {
                    session.Shutdown();
                }
            }
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("Joined-party invariant failed: " + detail);
        }

        // Application consumes immutable semantic boundary inputs here. Network/session/domain
        // realization is deliberately not reproduced and must be proven by the multiplayer scenes.
        private sealed class ExternalInputs : ISessionSaveCatalog, ISessionFormationService,
            IPartyScreenPresentationQuery, ISessionPresentationIntentRouter, IGameOutcomeQuery,
            IInputContextService, IInputBindingOverrideService, IUserPreferencesStore,
            IAudioPreferencesSink, IApplicationExitPort, IApplicationSessionPlanProvider
        {
            private readonly GameSessionId _session = new GameSessionId("application-joined-party");
            private readonly PartyMemberId _member = new PartyMemberId("application-joined-member");
            public SessionPresentationLifecycle Lifecycle = SessionPresentationLifecycle.WaitingForPlayers;
            public bool MemberReady;
            public int StartCalls;
            public int LeaveCalls;
            public JoinSessionRequest JoinRequest => new JoinSessionRequest(new JoinRequest(
                _session, "application-join-input", "application-contract", "application-content"));
            public IReadOnlyList<SessionSaveMetadata> ListSaves() => Array.Empty<SessionSaveMetadata>();
            public SessionFormationResult Host(HostSessionRequest request) => throw new NotSupportedException();
            public SessionFormationResult Join(JoinSessionRequest request) => SessionFormationResult.Success(_session, _member);
            public PartyScreenPresentationSnapshot CapturePartyScreen(PartyMemberId localMemberId) =>
                new PartyScreenPresentationSnapshot(_session, 1, Lifecycle, false, new[]
                {
                    new PartyMemberPresentationSnapshot(_member, default, default, PartyLeadershipRole.Member,
                        localMemberId == _member, MemberConnectionPresentationState.Connected,
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
            public GameSessionStartRequest PlanMultiplayer(SessionFormationResult formation) =>
                GameSessionStartRequest.NewGame(new GameSessionIdentity("application-campaign", "application-world",
                    formation.SessionId.Value, "application-configuration"));
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

        private sealed class ReadinessInputsFactory : ISessionRuntimeGraphFactory
        {
            public bool ReadyBeforePrepare;
            public bool ReadyOnTick;
            public int ComposeCalls;
            public int InitializeCalls;
            public int DisposeCalls;
            public ISessionRuntimeGraph Compose(GameSessionIdentity identity)
            {
                ComposeCalls++;
                return new ReadinessInputs(this);
            }

            // This fixture supplies pre/post-initialization readiness inputs and counts lifecycle
            // calls; GameSessionOrchestrator owns every lifecycle transition and ordered update.
            private sealed class ReadinessInputs : ISessionRuntimeGraph, ISessionUpdateStep
            {
                private readonly ReadinessInputsFactory _inputs;
                private bool _ready;
                private bool _disposed;
                public ReadinessInputs(ReadinessInputsFactory inputs)
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
                public void InitializeNewGame() { _inputs.InitializeCalls++; _ready = false; }
                public void Tick(int elapsedMilliseconds) { _ready |= _inputs.ReadyOnTick; }
                public void StartCommands() { }
                public void StopCommands() { }
                public void SettleAuthoritativeState() { }
                public void DetachExternalAdapters() { }
                public void Dispose()
                {
                    if (_disposed) return;
                    _disposed = true;
                    _inputs.DisposeCalls++;
                }
            }
        }
    }
}
