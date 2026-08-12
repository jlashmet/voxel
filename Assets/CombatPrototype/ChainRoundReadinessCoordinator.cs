using System;
using System.Collections.Generic;

namespace MountingForce.CombatPrototype
{
    /// <summary>
    /// Multiplayer/application coordination for ending a round. Ready means a player is done taking proactive actions;
    /// it never disables that player's reactions or ability to reserve a physical event.
    ///
    /// Once all living player groups are ready, the tactical enemy AI executes its committed intentions. Enemy execution
    /// may pause on the exact same physical reaction opportunities as player actions; after the party resolves/passes
    /// that event, TryAdvanceRound resumes the remaining enemy intents rather than starting a fresh enemy plan.
    /// </summary>
    public sealed class ChainRoundReadinessCoordinator
    {
        private readonly ChainCombatBoard _board;
        private readonly HashSet<int> _readyGroups = new HashSet<int>();
        private readonly ChainEnemyTacticalAI _enemyAI;
        private int _trackedRound;
        private string _lastMessage;

        public ChainRoundReadinessCoordinator(ChainCombatBoard board)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _enemyAI = new ChainEnemyTacticalAI(board);
            _trackedRound = board.Round;
        }

        public string LastMessage => string.IsNullOrEmpty(_lastMessage) ? _board.LastMessage : _lastMessage;
        public ChainEnemyTacticalAI EnemyAI => _enemyAI;
        public IReadOnlyList<ChainEnemyIntent> EnemyIntents
        {
            get
            {
                _enemyAI.Synchronize();
                return _enemyAI.Intents;
            }
        }
        public bool EnemyPhaseActive => _enemyAI.EnemyPhaseActive;

        public bool IsReady(int commandGroup)
        {
            Synchronize();
            return _readyGroups.Contains(commandGroup);
        }

        public bool CanUseProactive(int commandGroup)
        {
            Synchronize();
            return HasLivingFriendly(commandGroup) && !_readyGroups.Contains(commandGroup) && !_enemyAI.EnemyPhaseActive;
        }

        public bool AllLivingPlayersReady
        {
            get
            {
                Synchronize();
                bool foundLivingGroup = false;
                for (int group = 1; group <= 4; group++)
                {
                    if (!HasLivingFriendly(group)) continue;
                    foundLivingGroup = true;
                    if (!_readyGroups.Contains(group)) return false;
                }
                return foundLivingGroup;
            }
        }

        public bool TrySetReady(int commandGroup, bool ready)
        {
            Synchronize();
            if (!HasLivingFriendly(commandGroup))
                return Fail($"P{commandGroup} has no living recruits.");
            if (_board.BattleOver)
                return Fail("The battle is over.");
            if (_enemyAI.EnemyPhaseActive)
                return Fail("The enemy phase has started. Player Ready state is locked until the next round.");

            if (ready)
            {
                _readyGroups.Add(commandGroup);
                _lastMessage = $"P{commandGroup} is ready. Proactive play is closed for P{commandGroup}, but every living P{commandGroup} recruit can still react.";
            }
            else
            {
                _readyGroups.Remove(commandGroup);
                _lastMessage = $"P{commandGroup} is no longer ready and may continue proactive play if its activation still has resources.";
            }
            return true;
        }

        public bool TryAdvanceRound()
        {
            Synchronize();
            if (_board.BattleOver) return Fail("The battle is over.");
            if (_board.PendingReaction != null)
                return Fail("Resolve or pass the current physical event before the enemy phase continues.");
            if (!AllLivingPlayersReady)
                return Fail("Every living player group must be Ready before enemies act.");

            int previousRound = _board.Round;
            bool progressed = _enemyAI.BeginOrContinueEnemyPhase();
            if (!progressed)
                return Fail("The enemy AI could not advance its phase.");

            if (_board.PendingReaction != null)
            {
                _lastMessage = "Enemy phase paused on a physical event. Any player may reserve/react; once it is resolved, continue the enemy phase.";
                return true;
            }

            if (_board.BattleOver)
            {
                _lastMessage = _board.LastMessage;
                return true;
            }

            if (_board.Round != previousRound)
            {
                _readyGroups.Clear();
                _trackedRound = _board.Round;
                _lastMessage = $"Enemy AI finished its committed actions. Round {_board.Round} begins with new visible enemy intentions and all player Ready states cleared.";
                return true;
            }

            _lastMessage = _board.LastMessage;
            return true;
        }

        public void Reset()
        {
            _readyGroups.Clear();
            _trackedRound = _board.Round;
            _lastMessage = string.Empty;
            _enemyAI.PlanRound();
        }

        public void Synchronize()
        {
            _enemyAI.Synchronize();
            if (_board.Round == _trackedRound) return;
            _trackedRound = _board.Round;
            _readyGroups.Clear();
            _lastMessage = string.Empty;
        }

        private bool HasLivingFriendly(int commandGroup)
        {
            if (commandGroup <= 0) return false;
            for (int i = 0; i < _board.Units.Count; i++)
            {
                ChainUnitState unit = _board.Units[i];
                if (unit.Team == CombatTeam.Friendly && unit.IsAlive && unit.CommandGroup == commandGroup)
                    return true;
            }
            return false;
        }

        private bool Fail(string message)
        {
            _lastMessage = message;
            return false;
        }
    }
}
