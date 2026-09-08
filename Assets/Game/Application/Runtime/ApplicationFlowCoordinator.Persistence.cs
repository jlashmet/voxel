using System;
using System.Collections.Generic;
using Game.Application.Api;
using Game.Persistence.Api;
using Game.SessionOrchestration.Api;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;

namespace Game.Application.Runtime
{
    public sealed partial class ApplicationFlowCoordinator
    {
        private bool _partyResumePrepared;
        private string _preparedPartyResumeSaveId = string.Empty;

        /// <summary>
        /// Restores the authoritative runtime graph for an already-formed host before remote members
        /// reconnect. The application deliberately remains in the frontend/party lifecycle: Sessions
        /// can therefore restore durable disconnected roster identities first, then Continuity can bind
        /// fresh transport connections, and only the leader's later Start intent enters gameplay.
        /// </summary>
        public ApplicationOperationResult PreparePartyResume(string saveId)
        {
            ThrowIfDisposed();
            if (!CanPreparePartyResume(out ApplicationOperationResult rejected)) return rejected;
            if (_partyResumePrepared)
                return Reject(ApplicationFailure.Busy, "A party resume is already prepared.");
            if (!TryFindPartySave(saveId, out SessionSaveMetadata selected, out ApplicationOperationResult unavailable))
                return unavailable;

            GameSessionStartRequest request;
            try
            {
                request = _plans.PlanContinue(selected);
            }
            catch (Exception ex)
            {
                return Reject(ApplicationFailure.SessionPrepareFailed, ex.Message);
            }

            _operationInProgress = true;
            _joinedPartyAwaitingStart = false;
            ClearFailure();
            GameSessionOperationResult prepare = _session.Prepare(request);
            _operationInProgress = false;
            if (!prepare.Succeeded)
                return Reject(ApplicationFailure.SessionPrepareFailed, prepare.Diagnostic);

            _partyResumePrepared = true;
            _preparedPartyResumeSaveId = selected.SaveId.Value;
            ClearFailure();
            return ApplicationOperationResult.Success();
        }

        /// <summary>
        /// Starts an already-formed multiplayer party from a published authoritative save. When the
        /// resume was prepared early, this only authorizes party Start and enters the already-restored
        /// System14 graph; otherwise it retains the one-step frontend behavior for ordinary callers.
        /// </summary>
        public ApplicationOperationResult RequestPartyResume(string saveId)
        {
            ThrowIfDisposed();
            if (!CanPreparePartyResume(out ApplicationOperationResult rejected)) return rejected;
            if (!TryFindPartySave(saveId, out SessionSaveMetadata selected, out ApplicationOperationResult unavailable))
                return unavailable;

            if (_partyResumePrepared &&
                !string.Equals(_preparedPartyResumeSaveId, selected.SaveId.Value, StringComparison.Ordinal))
                return Reject(ApplicationFailure.SaveUnavailable, "Prepared party resume targets a different save.");

            PartySessionCommandResult party = _partyIntents.Request(SessionPresentationIntent.Start(_localMemberId));
            if (!party.Accepted)
                return Reject(ApplicationFailure.PartyCommandRejected, "Party start was rejected: " + party.Failure);

            if (!_partyResumePrepared)
                return StartSession(_plans.PlanContinue(selected));

            _operationInProgress = true;
            _joinedPartyAwaitingStart = false;
            UnwindUi();
            _lifecycle = ApplicationLifecycle.StartingSession;
            _screen = ApplicationScreen.Loading;
            ClearFailure();

            GameSessionOperationResult enter = _session.EnterRunning();
            _operationInProgress = false;
            ClearPreparedPartyResume();
            if (!enter.Succeeded)
                return FailStartup(ApplicationFailure.SessionStartFailed, enter.Diagnostic);

            PromoteWhenReady();
            return ApplicationOperationResult.Success();
        }

        private bool CanPreparePartyResume(out ApplicationOperationResult rejected)
        {
            if (_pendingFormation != null)
            {
                rejected = Reject(ApplicationFailure.Busy, "Session admission is still pending.");
                return false;
            }
            if (_lifecycle != ApplicationLifecycle.FrontEnd || !_activeFormation.Succeeded || !_localMemberId.IsValid)
            {
                rejected = Reject(ApplicationFailure.InvalidState, "No formed party is available to resume.");
                return false;
            }
            if (_operationInProgress)
            {
                rejected = Reject(ApplicationFailure.Busy, "Another application operation is in progress.");
                return false;
            }
            GameSessionLifecycle sessionLifecycle = _session.Snapshot.Lifecycle;
            if (sessionLifecycle != GameSessionLifecycle.Uninitialized &&
                sessionLifecycle != GameSessionLifecycle.Stopped)
            {
                rejected = Reject(ApplicationFailure.InvalidState, "Party resume requires an idle gameplay session.");
                return false;
            }
            rejected = default;
            return true;
        }

        private bool TryFindPartySave(
            string saveId,
            out SessionSaveMetadata selected,
            out ApplicationOperationResult rejected)
        {
            selected = default;
            if (string.IsNullOrWhiteSpace(saveId))
            {
                rejected = Reject(ApplicationFailure.SaveUnavailable, "A save id is required.");
                return false;
            }
            IReadOnlyList<SessionSaveMetadata> saves = _saves.ListSaves();
            for (int i = 0; i < saves.Count; i++)
            {
                if (!string.Equals(saves[i].SaveId.Value, saveId, StringComparison.Ordinal)) continue;
                selected = saves[i];
                rejected = default;
                return true;
            }
            rejected = Reject(ApplicationFailure.SaveUnavailable, "The selected save is unavailable.");
            return false;
        }

        private void ClearPreparedPartyResume()
        {
            _partyResumePrepared = false;
            _preparedPartyResumeSaveId = string.Empty;
        }
    }
}