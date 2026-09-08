using System;
using System.Collections.Generic;
using Game.Application.Api;
using Game.Persistence.Api;
using Game.SessionOrchestration.Api;
using Game.SessionPresentation.Api;

namespace Game.Application.Runtime
{
    public sealed partial class ApplicationFlowCoordinator
    {
        /// <summary>
        /// Starts an already-formed multiplayer party from a published authoritative save.
        /// Formation/admission remains owned by Sessions; this method only chooses the normal
        /// SessionOrchestration resume request at the same frontend transition used by party start.
        /// </summary>
        public ApplicationOperationResult RequestPartyResume(string saveId)
        {
            ThrowIfDisposed();
            if (_pendingFormation != null)
                return Reject(ApplicationFailure.Busy, "Session admission is still pending.");
            if (_lifecycle != ApplicationLifecycle.FrontEnd || !_activeFormation.Succeeded || !_localMemberId.IsValid)
                return Reject(ApplicationFailure.InvalidState, "No formed party is available to resume.");
            if (_operationInProgress)
                return Reject(ApplicationFailure.Busy, "Another application operation is in progress.");
            if (string.IsNullOrWhiteSpace(saveId))
                return Reject(ApplicationFailure.SaveUnavailable, "A save id is required.");

            SessionSaveMetadata selected = default;
            bool found = false;
            IReadOnlyList<SessionSaveMetadata> saves = _saves.ListSaves();
            for (int i = 0; i < saves.Count; i++)
            {
                SessionSaveMetadata candidate = saves[i];
                if (!string.Equals(candidate.SaveId.Value, saveId, StringComparison.Ordinal)) continue;
                selected = candidate;
                found = true;
                break;
            }
            if (!found)
                return Reject(ApplicationFailure.SaveUnavailable, "Save is not available: " + saveId);
            if (!string.Equals(selected.SessionId, _activeFormation.SessionId.Value, StringComparison.Ordinal))
                return Reject(
                    ApplicationFailure.SaveUnavailable,
                    "Save session does not match the formed multiplayer party.");

            PartySessionCommandResult party = _partyIntents.Request(SessionPresentationIntent.Start(_localMemberId));
            if (!party.Accepted)
                return Reject(ApplicationFailure.PartyCommandRejected, "Party start was rejected: " + party.Failure);

            return StartSession(_plans.PlanContinue(selected));
        }
    }
}
