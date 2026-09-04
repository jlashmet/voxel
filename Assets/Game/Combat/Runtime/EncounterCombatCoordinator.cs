using System;
using Game.Combat.Api;
using Game.Encounters.Api;

namespace Game.Combat.Runtime
{
    /// <summary>
    /// Thin semantic adapter over the existing CombatService. It remembers which Encounter owns the active
    /// combat session and emits one terminal CombatResolved fact; it does not own encounter outcome policy.
    /// </summary>
    public sealed class EncounterCombatCoordinator : IEncounterCombatCoordinator
    {
        private readonly CombatService _combat;
        private EncounterId _encounterId;
        private CombatSessionId _sessionId;
        private bool _resolutionTaken;

        public EncounterCombatCoordinator(CombatService combat)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public CombatStartResult Start(CombatStartRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (_combat.IsActive)
                throw new InvalidOperationException("A combat session is already active.");

            CombatSessionId sessionId = _combat.BeginCombat(
                new CombatEncounterRequest(request.EncounterId.Value, request.Participants));

            _encounterId = request.EncounterId;
            _sessionId = sessionId;
            _resolutionTaken = false;
            return new CombatStartResult(_encounterId, _sessionId);
        }

        public bool TryTakeResolved(out CombatResolved resolved)
        {
            if (!_encounterId.IsValid || !_sessionId.IsValid || _resolutionTaken ||
                _combat.State != CombatLifecycleState.Completed || !_combat.WinningTeam.HasValue)
            {
                resolved = default;
                return false;
            }

            resolved = new CombatResolved(_encounterId, _sessionId, _combat.WinningTeam.Value);
            _resolutionTaken = true;
            return true;
        }
    }
}
