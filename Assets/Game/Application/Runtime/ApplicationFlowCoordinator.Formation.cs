using System;
using Game.Application.Api;
using Game.Sessions.Api;

namespace Game.Application.Runtime
{
    public sealed partial class ApplicationFlowCoordinator
    {
        // This is local frontend intent, not session authority. One operation owns one reply;
        // there is no shared latest-result slot that a cancelled request could overwrite.
        private ISessionFormationOperation _pendingFormation;
        private GameSessionId _pendingFormationSession;
        private bool _pendingFormationIsJoin;

        private ApplicationOperationResult BeginFormation(Func<ISessionFormationOperation> begin,
            GameSessionId expectedSession, bool isJoin)
        {
            if (!expectedSession.IsValid)
                return Reject(ApplicationFailure.SessionFormationFailed, "A valid session id is required.");
            if (_localMemberId.IsValid)
                return Reject(ApplicationFailure.InvalidState, "Leave the current party before forming another.");

            _operationInProgress = true;
            ISessionFormationOperation operation = null;
            try
            {
                operation = begin();
                if (operation == null)
                    return FailFormation("Provider returned no admission operation.");
                if (_disposed)
                {
                    CancelFormationOperation(operation);
                    return Reject(ApplicationFailure.InvalidState, "Application closed during session formation.");
                }

                UnwindUi();
                if (_disposed)
                {
                    CancelFormationOperation(operation);
                    return Reject(ApplicationFailure.InvalidState, "Application closed during session formation.");
                }
                _pendingFormation = operation;
                _pendingFormationSession = expectedSession;
                _pendingFormationIsJoin = isJoin;
                _joinedPartyAwaitingStart = false;
                _activeFormation = default;
                _localMemberId = default;
                _screen = ApplicationScreen.Loading;
                ClearFailure();
                // Success acknowledges the local request only. No member/graph exists until a
                // matching authority result is adopted by Update on the owning thread.
                return ApplicationOperationResult.Success();
            }
            catch (Exception)
            {
                ClearPendingFormation();
                CancelFormationOperation(operation);
                // Provider exceptions can contain endpoint credentials. Keep public diagnostics semantic.
                return FailFormation("Session formation provider could not begin admission.");
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        private ApplicationOperationResult PollPendingFormation()
        {
            if (_operationInProgress) return ApplicationOperationResult.Success();
            ISessionFormationOperation operation = _pendingFormation;
            _operationInProgress = true;
            try
            {
                bool completed = operation.TryGetResult(out SessionFormationResult formation);
                // Disposal can be reentrant through an external provider. Never adopt its reply.
                if (_disposed || !ReferenceEquals(operation, _pendingFormation))
                    return ApplicationOperationResult.Success();
                if (!completed) return ApplicationOperationResult.Success();

                bool isJoin = _pendingFormationIsJoin;
                GameSessionId expectedSession = _pendingFormationSession;
                ClearPendingFormation();
                if (!formation.Succeeded)
                {
                    CancelFormationOperation(operation);
                    return FailFormation(formation.Failure + ": " + formation.Detail);
                }
                if (!formation.SessionId.IsValid || !formation.LocalMemberId.IsValid ||
                    formation.SessionId != expectedSession)
                {
                    CancelFormationOperation(operation);
                    return FailFormation("Provider returned an invalid or mismatched admitted identity.");
                }

                ApplicationOperationResult result = FormSession(formation);
                _joinedPartyAwaitingStart = result.Succeeded && isJoin;
                // Host waits for explicit Start. Join still waits for the separate connected,
                // local GameplayReady projection before the existing Orchestrator startup path.
                return result;
            }
            catch (Exception)
            {
                CancelPendingFormation();
                return FailFormation("Session formation provider failed while awaiting admission.");
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        private ApplicationOperationResult LeavePendingFormation()
        {
            _operationInProgress = true;
            try
            {
                string warning = CancelPendingFormation();
                UnwindUi();
                _screen = ApplicationScreen.MainMenu;
                ClearFailure();
                // There is no adopted member to Leave or composed graph to shut down.
                return string.IsNullOrEmpty(warning)
                    ? ApplicationOperationResult.Success()
                    : Reject(ApplicationFailure.TeardownFailed, warning);
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        private string CancelPendingFormation()
        {
            ISessionFormationOperation operation = _pendingFormation;
            // Detach before external cleanup: reentrant/late completion cannot revive this attempt.
            ClearPendingFormation();
            return CancelFormationOperation(operation);
        }

        private void ClearPendingFormation()
        {
            _pendingFormation = null;
            _pendingFormationSession = default;
            _pendingFormationIsJoin = false;
        }

        private static string CancelFormationOperation(ISessionFormationOperation operation)
        {
            if (operation == null) return string.Empty;
            try { operation.Cancel(); return string.Empty; }
            catch (Exception) { return "Session formation cancellation failed."; }
        }

        private ApplicationOperationResult FailFormation(string detail)
        {
            _joinedPartyAwaitingStart = false;
            _screen = ApplicationScreen.Error;
            return Reject(ApplicationFailure.SessionFormationFailed, detail);
        }
    }
}
