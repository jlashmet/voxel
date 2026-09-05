using System;
using System.IO;
using Game.Application.Api;
using Game.Application.Runtime;
using Game.Composition.Kentridge.Playable;
using Game.Input.Runtime;
using Game.Outcomes.Api;
using Game.Persistence.Api;
using Game.Persistence.Runtime;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;
using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Application-owned production composition for the Kentridge built-player slice. It owns the
    /// one physical input adapter, the one session orchestrator, the Application coordinator and the
    /// durable persistence bridge. Gameplay/domain authority remains in the composed runtime graph.
    /// </summary>
    [AddComponentMenu("Game/Kentridge Production Composition Root")]
    [DefaultExecutionOrder(-20000)]
    public sealed class KentridgeProductionCompositionRoot : MonoBehaviour
    {
        internal const string CampaignId = "main-campaign";
        internal const string WorldId = "kentridge-production-world";
        internal const string ConfigurationId = "kentridge-production-v1";
        internal const string ContentId = "kentridge-opening-campaign-v1";
        public const string DefaultSaveId = "kentridge-production-latest";

        private KentridgePlayableSlice _slice;
        private KentridgeForestBanditEncounter _forest;
        private KentridgeGameplayHudInstaller _hud;
        private InputContextService _inputContexts;
        private UnityPlayerInputReader _input;
        private KentridgeProductionPersistenceBridge _persistence;
        private GameSessionOrchestrator _session;
        private ApplicationFlowCoordinator _flow;
        private ApplicationFrontendView _frontend;
        private bool _started;
        private bool _pendingComposition;
        private bool _loggedUpdateFailure;

        public bool IsComposed => _flow != null && _session != null;
        public ApplicationFlowSnapshot FlowSnapshot =>
            _flow == null
                ? new ApplicationFlowSnapshot(
                    ApplicationLifecycle.Boot,
                    ApplicationScreen.Boot,
                    false,
                    ApplicationFailure.None,
                    string.Empty)
                : _flow.Snapshot;
        public GameSessionSnapshot SessionSnapshot =>
            _session == null
                ? new GameSessionSnapshot(
                    GameSessionLifecycle.Uninitialized,
                    false,
                    null,
                    GameSessionFailure.None,
                    string.Empty)
                : _session.Snapshot;
        public string LastPublishedSaveId => _persistence?.LastPublishedSaveId ?? string.Empty;

        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying) return;
            BindProductionInput();
            if (_started) _pendingComposition = true;
        }

        private void Start()
        {
            if (!UnityEngine.Application.isPlaying) return;
            _started = true;
            _pendingComposition = true;
        }

        private void Update()
        {
            if (!UnityEngine.Application.isPlaying) return;
            if (_pendingComposition)
            {
                if (_slice == null || _slice.SessionFactory == null) return;
                ComposeApplication();
                _pendingComposition = false;
            }

            if (_flow == null) return;
            if (!_slice.OpeningPresentationReady) return;

            int elapsedMilliseconds = Mathf.Max(0, Mathf.RoundToInt(Time.unscaledDeltaTime * 1000f));
            ApplicationOperationResult update = _flow.Update(elapsedMilliseconds);
            if (!update.Succeeded && !_loggedUpdateFailure)
            {
                _loggedUpdateFailure = true;
                Debug.LogError(
                    "SYSTEM24 failure: application update rejected " + update.Failure + ": " + update.Detail);
            }
        }

        private void OnDisable()
        {
            if (!UnityEngine.Application.isPlaying) return;
            TeardownApplication();
            _input?.Dispose();
            _input = null;
            _inputContexts = null;
        }

        public ApplicationOperationResult RequestNewGame()
        {
            RequireApplication();
            return _flow.RequestNewGame(NewGameDescriptor());
        }

        public GameSessionOperationResult RequestSave(string saveId = DefaultSaveId)
        {
            RequireApplication();
            if (string.IsNullOrWhiteSpace(saveId))
                throw new ArgumentException("A semantic save id is required.", nameof(saveId));
            _persistence.ArmCapture(new SessionSaveId(saveId), "Kentridge production vertical slice");
            GameSessionOperationResult result = _session.Capture();
            if (result.Succeeded)
                Debug.Log("SYSTEM24 save: id=" + saveId + " lifecycle=" + _session.Snapshot.Lifecycle);
            return result;
        }

        public ApplicationOperationResult RequestLeaveGame()
        {
            RequireApplication();
            ApplicationOperationResult result = _flow.RequestLeaveGame();
            if (result.Succeeded)
                Debug.Log("SYSTEM24 teardown: lifecycle=" + _flow.Snapshot.Lifecycle + " session=" + _session.Snapshot.Lifecycle);
            return result;
        }

        public ApplicationOperationResult RequestContinue(string saveId = DefaultSaveId)
        {
            RequireApplication();
            ApplicationOperationResult result = _flow.RequestContinue(saveId);
            if (result.Succeeded)
                Debug.Log(
                    "SYSTEM24 continue: id=" + saveId + " lifecycle=" + _flow.Snapshot.Lifecycle +
                    " restored=" + (_slice.SessionFactory.Current != null && _slice.SessionFactory.Current.RestoredFromPersistence));
            return result;
        }

        private void BindProductionInput()
        {
            _slice = GetComponent<KentridgePlayableSlice>()
                ?? throw new InvalidOperationException("Kentridge production root requires KentridgePlayableSlice on the same object.");
            _forest = GetComponent<KentridgeForestBanditEncounter>()
                ?? gameObject.AddComponent<KentridgeForestBanditEncounter>();
            _hud = GetComponent<KentridgeGameplayHudInstaller>()
                ?? gameObject.AddComponent<KentridgeGameplayHudInstaller>();

            _inputContexts = new InputContextService();
            _input = new UnityPlayerInputReader(_inputContexts);
            _slice.BindProductionInput(_input, _input);
            _forest.BindProductionInput(_inputContexts, _input);
            _hud.BindInput(_input, _input);
        }

        private void ComposeApplication()
        {
            if (_flow != null || _session != null)
                throw new InvalidOperationException("Kentridge Application composition is already active.");

            string saveRoot = Path.Combine(UnityEngine.Application.persistentDataPath, "KentridgeProductionSaves");
            _persistence = new KentridgeProductionPersistenceBridge(
                _slice,
                _forest,
                new FileSessionSaveStore(saveRoot));
            _session = new GameSessionOrchestrator(_slice.SessionFactory, _persistence);
            _slice.BindSessionControl(_session);

            var saveCatalog = new SessionSaveCatalog(_persistence.Service);
            _flow = new ApplicationFlowCoordinator(
                _session,
                saveCatalog,
                new UnsupportedSessionFormationService(),
                new UnavailablePartyPresentation(),
                new UnavailablePartyIntents(),
                new RunningOutcomeQuery(),
                _inputContexts,
                _input,
                new PlayerPrefsUserPreferencesStore(),
                new UnityAudioPreferencesSink(),
                new UnityApplicationExitPort(),
                new KentridgeSessionPlanProvider());

            ApplicationOperationResult boot = _flow.CompleteBoot();
            if (!boot.Succeeded)
                throw new InvalidOperationException("Kentridge Application boot failed: " + boot.Failure + " " + boot.Detail);

            _frontend = GetComponent<ApplicationFrontendView>() ?? gameObject.AddComponent<ApplicationFrontendView>();
            _frontend.Bind(
                _flow,
                new ApplicationFrontendConfiguration(
                    NewGameDescriptor(),
                    DefaultSaveId,
                    default,
                    default));

            Debug.Log("SYSTEM24 frontend: lifecycle=" + _flow.Snapshot.Lifecycle + " screen=" + _flow.Snapshot.Screen);
            _loggedUpdateFailure = false;
        }

        private void TeardownApplication()
        {
            if (_flow != null)
            {
                ApplicationLifecycle lifecycle = _flow.Snapshot.Lifecycle;
                if (lifecycle == ApplicationLifecycle.InGame || lifecycle == ApplicationLifecycle.StartingSession)
                {
                    ApplicationOperationResult leave = _flow.RequestLeaveGame();
                    if (!leave.Succeeded)
                        Debug.LogError("SYSTEM24 failure: ordered leave rejected " + leave.Failure + ": " + leave.Detail);
                }
                _flow.Dispose();
                _flow = null;
            }

            if (_session != null &&
                _session.Snapshot.Lifecycle != GameSessionLifecycle.Uninitialized &&
                _session.Snapshot.Lifecycle != GameSessionLifecycle.Stopped)
            {
                GameSessionOperationResult shutdown = _session.Shutdown();
                if (!shutdown.Succeeded)
                    Debug.LogError("SYSTEM24 failure: ordered shutdown rejected " + shutdown.Failure + ": " + shutdown.Diagnostic);
            }

            _session = null;
            _persistence = null;
            _frontend = null;
            _pendingComposition = false;
        }

        private void RequireApplication()
        {
            if (_flow == null || _session == null || _persistence == null)
                throw new InvalidOperationException("Kentridge production Application composition is not active.");
        }

        private static ApplicationSessionDescriptor NewGameDescriptor() =>
            new ApplicationSessionDescriptor(CampaignId, WorldId, "kentridge-local-session", ConfigurationId);

        private sealed class KentridgeSessionPlanProvider : IApplicationSessionPlanProvider
        {
            public GameSessionStartRequest PlanNewGame(ApplicationSessionDescriptor descriptor) =>
                GameSessionStartRequest.NewGame(new GameSessionIdentity(
                    descriptor.CampaignId,
                    descriptor.WorldId,
                    descriptor.SessionId,
                    descriptor.ConfigurationId));

            public GameSessionStartRequest PlanContinue(SessionSaveMetadata save) =>
                GameSessionStartRequest.Resume(
                    new GameSessionIdentity(CampaignId, save.WorldId.Value, save.SessionId, ConfigurationId),
                    save.SaveId.Value);

            public GameSessionStartRequest PlanMultiplayer(SessionFormationResult formation) =>
                GameSessionStartRequest.NewGame(new GameSessionIdentity(
                    CampaignId,
                    WorldId,
                    formation.SessionId.IsValid ? formation.SessionId.Value : "unsupported-multiplayer",
                    ConfigurationId));
        }

        private sealed class UnsupportedSessionFormationService : ISessionFormationService
        {
            public SessionFormationResult Host(HostSessionRequest request) =>
                SessionFormationResult.Reject(
                    SessionFormationFailure.ProviderUnavailable,
                    "Kentridge production slice has no multiplayer provider composition.");

            public SessionFormationResult Join(JoinSessionRequest request) =>
                SessionFormationResult.Reject(
                    SessionFormationFailure.ProviderUnavailable,
                    "Kentridge production slice has no multiplayer provider composition.");
        }

        private sealed class UnavailablePartyPresentation : IPartyScreenPresentationQuery
        {
            public PartyScreenPresentationSnapshot CapturePartyScreen(PartyMemberId localMemberId) =>
                throw new InvalidOperationException("Party presentation is unavailable without a formed multiplayer session.");
        }

        private sealed class UnavailablePartyIntents : ISessionPresentationIntentRouter
        {
            public PartySessionCommandResult Request(SessionPresentationIntent intent) =>
                PartySessionCommandResult.Reject(PartySessionCommandFailure.InvalidRequest);
        }

        private sealed class RunningOutcomeQuery : IGameOutcomeQuery
        {
            public GameOutcomeSnapshot Snapshot() => GameOutcomeSnapshot.Running();
        }
    }
}
