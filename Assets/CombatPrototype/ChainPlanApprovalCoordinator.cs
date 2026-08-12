using System.Reflection;
using UnityEngine;

namespace MountingForce.CombatPrototype
{
    /// <summary>
    /// Binds multiplayer Ready state to an exact shared-plan revision.
    ///
    /// A Ready flag is an approval of the ghost future the player actually saw. If any client edits that future
    /// (add/remove/undo/redo/reorder), every existing approval is revoked. The one deliberate exception is the
    /// planner committing an approved plan to the authoritative board: that transition empties the plan and mutates
    /// the real board, so Ready is preserved long enough for the enemy phase to follow.
    ///
    /// This is intentionally an application-layer coordinator rather than combat-domain state. A network server can
    /// later enforce the same invariant by attaching the approved plan revision/hash to each player's Ready command.
    /// </summary>
    [DefaultExecutionOrder(-1100)]
    [RequireComponent(typeof(ChainCombatLabController))]
    [RequireComponent(typeof(ChainExecutionPlanner))]
    public sealed class ChainPlanApprovalCoordinator : MonoBehaviour
    {
        private static readonly FieldInfo BoardField = typeof(ChainCombatLabController).GetField(
            "_board", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ReadinessField = typeof(ChainCombatLabController).GetField(
            "_roundReadiness", BindingFlags.Instance | BindingFlags.NonPublic);

        private ChainCombatLabController _controller;
        private ChainExecutionPlanner _planner;
        private ChainCombatBoard _board;
        private ChainRoundReadinessCoordinator _readiness;

        private int _trackedRevision = -1;
        private bool _trackedHadActions;
        private int _trackedBoardFingerprint;
        private int _lastInvalidatedRevision = -1;
        private int _lastCommittedRevision = -1;

        public int TrackedRevision => _trackedRevision;
        public int LastInvalidatedRevision => _lastInvalidatedRevision;
        public int LastCommittedRevision => _lastCommittedRevision;

        private void Awake()
        {
            _controller = GetComponent<ChainCombatLabController>();
            _planner = GetComponent<ChainExecutionPlanner>();
        }

        private void Update()
        {
            SynchronizeNow();
        }

        public void SynchronizeNow()
        {
            ResolveDependencies();
            if (_planner == null || _board == null || _readiness == null) return;

            int revision = _planner.Plan.Revision;
            bool hasActions = _planner.Plan.HasActions;
            int boardFingerprint = Fingerprint(_board);

            if (_trackedRevision < 0)
            {
                _trackedRevision = revision;
                _trackedHadActions = hasActions;
                _trackedBoardFingerprint = boardFingerprint;
                return;
            }

            if (revision != _trackedRevision)
            {
                int previousRevision = _trackedRevision;
                bool planBecameEmpty = _trackedHadActions && !hasActions;
                bool authoritativeBoardChanged = boardFingerprint != _trackedBoardFingerprint;
                bool committedApprovedPlan = planBecameEmpty && authoritativeBoardChanged;

                if (committedApprovedPlan)
                {
                    _lastCommittedRevision = previousRevision;
                }
                else
                {
                    int cleared = ClearReadyApprovals();
                    _lastInvalidatedRevision = previousRevision;
                    if (cleared > 0)
                    {
                        Debug.Log($"Chain plan revision changed {previousRevision} -> {revision}; cleared {cleared} stale player approval(s).");
                    }
                }

                _trackedRevision = revision;
            }

            _trackedHadActions = hasActions;
            _trackedBoardFingerprint = boardFingerprint;
        }

        public void ResetTracking()
        {
            _trackedRevision = -1;
            _trackedHadActions = false;
            _trackedBoardFingerprint = 0;
            _lastInvalidatedRevision = -1;
            _lastCommittedRevision = -1;
            ResolveDependencies();
            SynchronizeNow();
        }

        private void ResolveDependencies()
        {
            if (_controller == null) _controller = GetComponent<ChainCombatLabController>();
            if (_planner == null) _planner = GetComponent<ChainExecutionPlanner>();
            if (_controller == null) return;

            if (BoardField != null)
                _board = BoardField.GetValue(_controller) as ChainCombatBoard;
            if (ReadinessField != null)
                _readiness = ReadinessField.GetValue(_controller) as ChainRoundReadinessCoordinator;
        }

        private int ClearReadyApprovals()
        {
            int cleared = 0;
            for (int group = 1; group <= 4; group++)
            {
                if (!_readiness.IsReady(group)) continue;
                if (_readiness.TrySetReady(group, false)) cleared++;
            }
            return cleared;
        }

        private static int Fingerprint(ChainCombatBoard board)
        {
            unchecked
            {
                int hash = board.Round * 397 ^ (board.PendingReaction?.Id ?? 0);
                for (int i = 0; i < board.Units.Count; i++)
                {
                    ChainUnitState unit = board.Units[i];
                    hash = hash * 31 + unit.Id;
                    hash = hash * 31 + unit.Position.X;
                    hash = hash * 31 + unit.Position.Z;
                    hash = hash * 31 + unit.Hp;
                    hash = hash * 31 + (unit.MoveSpent ? 1 : 0);
                    hash = hash * 31 + (unit.ActionSpent ? 1 : 0);
                    hash = hash * 31 + (unit.ReactionSpent ? 1 : 0);
                    hash = hash * 31 + (unit.Airborne ? 1 : 0);
                }
                for (int i = 0; i < board.Trees.Count; i++)
                {
                    ChainTreeState tree = board.Trees[i];
                    hash = hash * 31 + tree.Id;
                    hash = hash * 31 + (tree.Standing ? 1 : 0);
                    hash = hash * 31 + tree.Stress;
                }
                if (board.PortalA.HasValue) hash = hash * 31 + board.PortalA.Value.GetHashCode();
                if (board.PortalB.HasValue) hash = hash * 31 + board.PortalB.Value.GetHashCode();
                hash = hash * 31 + board.Amplifiers.Count;
                return hash;
            }
        }
    }
}
