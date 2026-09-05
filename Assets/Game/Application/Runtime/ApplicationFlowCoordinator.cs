using System;
using System.Collections.Generic;
using Game.Application.Api;
using Game.Input.Api;
using Game.Outcomes.Api;
using Game.Persistence.Api;
using Game.SessionOrchestration.Api;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;

namespace Game.Application.Runtime
{
    public interface IApplicationSessionPlanProvider
    {
        GameSessionStartRequest PlanNewGame(ApplicationSessionDescriptor descriptor);
        GameSessionStartRequest PlanContinue(SessionSaveMetadata save);
        GameSessionStartRequest PlanMultiplayer(SessionFormationResult formation);
    }

    /// <summary>
    /// Local application coordinator. It serializes frontend intent and delegates all gameplay/session
    /// authority to owning systems. It never loads scenes, opens sockets, pauses simulation time or resolves outcomes.
    /// </summary>
    public sealed class ApplicationFlowCoordinator : IDisposable
    {
        private readonly IGameSessionControl _session;
        private readonly ISessionSaveCatalog _saves;
        private readonly ISessionFormationService _formation;
        private readonly IPartyScreenPresentationQuery _partyPresentation;
        private readonly ISessionPresentationIntentRouter _partyIntents;
        private readonly IGameOutcomeQuery _outcomes;
        private readonly IInputContextService _inputContexts;
        private readonly IInputBindingOverrideService _bindings;
        private readonly IUserPreferencesStore _preferencesStore;
        private readonly IAudioPreferencesSink _audio;
        private readonly IApplicationExitPort _exit;
        private readonly IApplicationSessionPlanProvider _plans;
        private readonly List<ApplicationScreen> _screenStack = new List<ApplicationScreen>();
        private readonly List<IInputContextLease> _uiLeases = new List<IInputContextLease>();

        private ApplicationLifecycle _lifecycle = ApplicationLifecycle.Boot;
        private ApplicationScreen _screen = ApplicationScreen.Boot;
        private ApplicationFailure _lastFailure;
        private string _detail = string.Empty;
        private bool _operationInProgress;
        private SessionFormationResult _activeFormation;
        private PartyMemberId _localMemberId;
        private bool _disposed;

        public ApplicationFlowCoordinator(
            IGameSessionControl session,
            ISessionSaveCatalog saves,
            ISessionFormationService formation,
            IPartyScreenPresentationQuery partyPresentation,
            ISessionPresentationIntentRouter partyIntents,
            IGameOutcomeQuery outcomes,
            IInputContextService inputContexts,
            IInputBindingOverrideService bindings,
            IUserPreferencesStore preferencesStore,
            IAudioPreferencesSink audio,
            IApplicationExitPort exit,
            IApplicationSessionPlanProvider plans)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _saves = saves ?? throw new ArgumentNullException(nameof(saves));
            _formation = formation ?? throw new ArgumentNullException(nameof(formation));
            _partyPresentation = partyPresentation ?? throw new ArgumentNullException(nameof(partyPresentation));
            _partyIntents = partyIntents ?? throw new ArgumentNullException(nameof(partyIntents));
            _outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
            _inputContexts = inputContexts ?? throw new ArgumentNullException(nameof(inputContexts));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _preferencesStore = preferencesStore ?? throw new ArgumentNullException(nameof(preferencesStore));
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _exit = exit ?? throw new ArgumentNullException(nameof(exit));
            _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        }

        public ApplicationFlowSnapshot Snapshot => new ApplicationFlowSnapshot(
            _lifecycle,
            _screen,
            _lifecycle == ApplicationLifecycle.InGame && _session.Snapshot.GameplayReady,
            _lastFailure,
            _detail);

        public ApplicationOperationResult CompleteBoot()
        {
            ThrowIfDisposed();
            if (_lifecycle != ApplicationLifecycle.Boot)
                return Reject(ApplicationFailure.InvalidState, "Boot has already completed.");

            UserPreferences preferences;
            if (!_preferencesStore.TryLoad(out preferences) || preferences == null)
                preferences = UserPreferences.Default;
            ApplicationOperationResult apply = ApplyPreferencesInternal(preferences, false);
            if (!apply.Succeeded) return apply;

            _lifecycle = ApplicationLifecycle.FrontEnd;
            _screen = ApplicationScreen.MainMenu;
            ClearFailure();
            return ApplicationOperationResult.Success();
        }

        public ApplicationOperationResult RequestNewGame(ApplicationSessionDescriptor descriptor)
        {
            ThrowIfDisposed();
            if (!CanStartFrontendOperation(out ApplicationOperationResult rejected)) return rejected;
            return StartSession(_plans.PlanNewGame(descriptor));
        }

        public ApplicationOperationResult RequestContinue(string saveId)
        {
            ThrowIfDisposed();
            if (!CanStartFrontendOperation(out ApplicationOperationResult rejected)) return rejected;
            if (string.IsNullOrWhiteSpace(saveId)) return Reject(ApplicationFailure.SaveUnavailable, "A save id is required.");

            IReadOnlyList<SessionSaveMetadata> saves = _saves.ListSaves();
            for (int i = 0; i < saves.Count; i++)
            {
                if (!string.Equals(saves[i].SaveId.Value, saveId, StringComparison.Ordinal)) continue;
                return StartSession(_plans.PlanContinue(saves[i]));
            }
            return Reject(ApplicationFailure.SaveUnavailable, "Save is not available: " + saveId);
        }

        public ApplicationOperationResult RequestHost(HostSessionRequest request)
        {
            ThrowIfDisposed();
            if (!CanStartFrontendOperation(out ApplicationOperationResult rejected)) return rejected;
            return FormSession(_formation.Host(request));
        }

        public ApplicationOperationResult RequestJoin(JoinSessionRequest request)
        {
            ThrowIfDisposed();
            if (!CanStartFrontendOperation(out ApplicationOperationResult rejected)) return rejected;
            return FormSession(_formation.Join(request));
        }

        public ApplicationOperationResult RequestPartyStart()
        {
            ThrowIfDisposed();
            if (_lifecycle != ApplicationLifecycle.FrontEnd || !_activeFormation.Succeeded || !_localMemberId.IsValid)
                return Reject(ApplicationFailure.InvalidState, "No formed party is available to start.");
            if (_operationInProgress) return Reject(ApplicationFailure.Busy, "Another application operation is in progress.");

            PartySessionCommandResult party = _partyIntents.Request(SessionPresentationIntent.Start(_localMemberId));
            if (!party.Accepted)
                return Reject(ApplicationFailure.PartyCommandRejected, "Party start was rejected: " + party.Failure);
            return StartSession(_plans.PlanMultiplayer(_activeFormation));
        }

        public ApplicationOperationResult RequestLeaveGame()
        {
            ThrowIfDisposed();
            if (_operationInProgress) return Reject(ApplicationFailure.Busy, "Another application operation is in progress.");
            if (_lifecycle != ApplicationLifecycle.InGame &&
                _lifecycle != ApplicationLifecycle.StartingSession &&
                !(_lifecycle == ApplicationLifecycle.FrontEnd && _activeFormation.Succeeded))
            {
                return Reject(ApplicationFailure.InvalidState, "There is no active game or party to leave.");
            }

            _operationInProgress = true;
            _lifecycle = ApplicationLifecycle.ReturningToFrontEnd;
            UnwindUi();
            string warning = LeavePartyIfPresent();
            GameSessionOperationResult shutdown = ShutdownIfActive();
            _activeFormation = default;
            _localMemberId = default;
            _lifecycle = ApplicationLifecycle.FrontEnd;
            _screen = ApplicationScreen.MainMenu;
            _operationInProgress = false;

            if (!shutdown.Succeeded)
                return Reject(ApplicationFailure.TeardownFailed, shutdown.Diagnostic);
            ClearFailure();
            if (!string.IsNullOrEmpty(warning)) _detail = warning;
            return ApplicationOperationResult.Success();
        }

        public ApplicationOperationResult RequestQuitApplication()
        {
            ThrowIfDisposed();
            if (_operationInProgress) return Reject(ApplicationFailure.Busy, "Another application operation is in progress.");

            _operationInProgress = true;
            UnwindUi();
            string warning = LeavePartyIfPresent();
            GameSessionOperationResult shutdown = ShutdownIfActive();
            _lifecycle = ApplicationLifecycle.Exiting;
            _screen = ApplicationScreen.MainMenu;
            _operationInProgress = false;
            _exit.RequestExit();

            if (!shutdown.Succeeded)
                return Reject(ApplicationFailure.TeardownFailed, shutdown.Diagnostic);
            ClearFailure();
            if (!string.IsNullOrEmpty(warning)) _detail = warning;
            return ApplicationOperationResult.Success();
        }

        public ApplicationOperationResult Update(int elapsedMilliseconds)
        {
            ThrowIfDisposed();
            if (elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));

            if (_lifecycle == ApplicationLifecycle.StartingSession ||
                _lifecycle == ApplicationLifecycle.InGame)
            {
                GameSessionOperationResult tick = _session.Tick(elapsedMilliseconds);
                if (!tick.Succeeded)
                {
                    if (_lifecycle == ApplicationLifecycle.StartingSession)
                        return FailStartup(ApplicationFailure.SessionUpdateFailed, tick.Diagnostic);
                    return Reject(ApplicationFailure.SessionUpdateFailed, tick.Diagnostic);
                }
                PromoteWhenReady();
            }

            if (_lifecycle == ApplicationLifecycle.InGame)
            {
                GameOutcomeSnapshot outcome = _outcomes.Snapshot();
                if (outcome.Lifecycle == GameOutcomeLifecycle.Resolved)
                {
                    _screen = ApplicationScreen.Outcome;
                    _detail = outcome.Disposition + ": " + outcome.Outcome;
                }
            }

            return ApplicationOperationResult.Success();
        }

        public ApplicationOperationResult ReturnFromOutcome()
        {
            ThrowIfDisposed();
            if (_lifecycle != ApplicationLifecycle.InGame || _screen != ApplicationScreen.Outcome)
                return Reject(ApplicationFailure.InvalidState, "No resolved outcome is being presented.");
            return RequestLeaveGame();
        }

        public ApplicationOperationResult OpenScreen(ApplicationScreen screen)
        {
            ThrowIfDisposed();
            if (_lifecycle != ApplicationLifecycle.FrontEnd && _lifecycle != ApplicationLifecycle.InGame)
                return Reject(ApplicationFailure.InvalidState, "Navigation is unavailable in the current lifecycle.");
            if (screen == ApplicationScreen.Boot || screen == ApplicationScreen.Loading || screen == ApplicationScreen.Gameplay)
                return Reject(ApplicationFailure.InvalidState, "The requested screen is lifecycle-owned.");

            _screenStack.Add(_screen);
            _uiLeases.Add(_inputContexts.Push(InputContextId.Ui));
            _screen = screen;
            return ApplicationOperationResult.Success();
        }

        public ApplicationOperationResult CloseScreen()
        {
            ThrowIfDisposed();
            if (_screenStack.Count == 0 || _uiLeases.Count == 0)
                return Reject(ApplicationFailure.InvalidState, "No nested screen is open.");

            int last = _uiLeases.Count - 1;
            _uiLeases[last].Dispose();
            _uiLeases.RemoveAt(last);
            int screenLast = _screenStack.Count - 1;
            _screen = _screenStack[screenLast];
            _screenStack.RemoveAt(screenLast);
            return ApplicationOperationResult.Success();
        }

        public bool TryCapturePartyScreen(out PartyScreenPresentationSnapshot snapshot)
        {
            ThrowIfDisposed();
            if (!_activeFormation.Succeeded || !_localMemberId.IsValid)
            {
                snapshot = null;
                return false;
            }
            snapshot = _partyPresentation.CapturePartyScreen(_localMemberId);
            return snapshot != null;
        }

        public ApplicationOperationResult ApplyPreferences(UserPreferences preferences)
        {
            ThrowIfDisposed();
            if (preferences == null) return Reject(ApplicationFailure.InvalidPreferences, "Preferences are required.");
            return ApplyPreferencesInternal(preferences, true);
        }

        private ApplicationOperationResult ApplyPreferencesInternal(UserPreferences preferences, bool persist)
        {
            try
            {
                _bindings.ClearOverrides();
                for (int i = 0; i < preferences.BindingOverrides.Count; i++)
                {
                    if (!_bindings.TryApplyOverride(preferences.BindingOverrides[i], out string error))
                        return Reject(ApplicationFailure.InvalidPreferences, error);
                }
                _audio.Apply(preferences);
                if (persist) _preferencesStore.Save(preferences);
                return ApplicationOperationResult.Success();
            }
            catch (Exception ex)
            {
                return Reject(ApplicationFailure.InvalidPreferences, ex.Message);
            }
        }

        private ApplicationOperationResult FormSession(SessionFormationResult formation)
        {
            if (!formation.Succeeded)
                return Reject(ApplicationFailure.SessionFormationFailed, formation.Failure + ": " + formation.Detail);
            _activeFormation = formation;
            _localMemberId = formation.LocalMemberId;
            _screen = ApplicationScreen.Party;
            ClearFailure();
            return ApplicationOperationResult.Success();
        }

        private ApplicationOperationResult StartSession(GameSessionStartRequest request)
        {
            if (_operationInProgress) return Reject(ApplicationFailure.Busy, "Another application operation is in progress.");
            if (_lifecycle != ApplicationLifecycle.FrontEnd)
                return Reject(ApplicationFailure.InvalidState, "A session can only start from the frontend.");

            _operationInProgress = true;
            UnwindUi();
            _lifecycle = ApplicationLifecycle.StartingSession;
            _screen = ApplicationScreen.Loading;
            ClearFailure();

            GameSessionOperationResult prepare = _session.Prepare(request);
            if (!prepare.Succeeded)
            {
                _operationInProgress = false;
                return FailStartup(ApplicationFailure.SessionPrepareFailed, prepare.Diagnostic);
            }

            GameSessionOperationResult enter = _session.EnterRunning();
            _operationInProgress = false;
            if (!enter.Succeeded)
                return FailStartup(ApplicationFailure.SessionStartFailed, enter.Diagnostic);

            PromoteWhenReady();
            return ApplicationOperationResult.Success();
        }

        private void PromoteWhenReady()
        {
            if (_lifecycle != ApplicationLifecycle.StartingSession || !_session.Snapshot.GameplayReady) return;
            _lifecycle = ApplicationLifecycle.InGame;
            _screen = ApplicationScreen.Gameplay;
            ClearFailure();
        }

        private ApplicationOperationResult FailStartup(ApplicationFailure failure, string detail)
        {
            ShutdownIfActive();
            _lifecycle = ApplicationLifecycle.FrontEnd;
            _screen = ApplicationScreen.Error;
            _operationInProgress = false;
            return Reject(failure, detail);
        }

        private string LeavePartyIfPresent()
        {
            if (!_activeFormation.Succeeded || !_localMemberId.IsValid) return string.Empty;
            PartySessionCommandResult leave = _partyIntents.Request(SessionPresentationIntent.Leave(_localMemberId));
            return leave.Accepted ? string.Empty : "Party leave was rejected: " + leave.Failure;
        }

        private GameSessionOperationResult ShutdownIfActive()
        {
            GameSessionLifecycle lifecycle = _session.Snapshot.Lifecycle;
            if (lifecycle == GameSessionLifecycle.Uninitialized || lifecycle == GameSessionLifecycle.Stopped)
                return GameSessionOperationResult.Success();
            return _session.Shutdown();
        }

        private bool CanStartFrontendOperation(out ApplicationOperationResult rejected)
        {
            if (_operationInProgress)
            {
                rejected = Reject(ApplicationFailure.Busy, "Another application operation is in progress.");
                return false;
            }
            if (_lifecycle != ApplicationLifecycle.FrontEnd)
            {
                rejected = Reject(ApplicationFailure.InvalidState, "Frontend intent is unavailable in the current lifecycle.");
                return false;
            }
            rejected = ApplicationOperationResult.Success();
            return true;
        }

        private ApplicationOperationResult Reject(ApplicationFailure failure, string detail)
        {
            _lastFailure = failure;
            _detail = detail ?? string.Empty;
            return ApplicationOperationResult.Reject(failure, _detail);
        }

        private void ClearFailure()
        {
            _lastFailure = ApplicationFailure.None;
            _detail = string.Empty;
        }

        private void UnwindUi()
        {
            for (int i = _uiLeases.Count - 1; i >= 0; i--) _uiLeases[i].Dispose();
            _uiLeases.Clear();
            _screenStack.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            UnwindUi();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ApplicationFlowCoordinator));
        }
    }
}
