using System;
using System.Collections.Generic;

namespace MountingForce.CombatPrototype
{
    /// <summary>
    /// Multiplayer/application coordination for ending a round. Ready means a player is done taking proactive actions;
    /// it never disables that player's reactions or ability to reserve a physical event.
    /// </summary>
    public sealed class ChainRoundReadinessCoordinator
    {
        private readonly ChainCombatBoard _board;
        private readonly HashSet<int> _readyGroups = new HashSet<int>();
        private int _trackedRound;
        private string _lastMessage;

        public ChainRoundReadinessCoordinator(ChainCombatBoard board)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _trackedRound = board.Round;
        }

        public string LastMessage => string.IsNullOrEmpty(_lastMessage) ? _board.LastMessage : _lastMessage;

        public bool IsReady(int commandGroup)
        {
            Synchronize();
            return _readyGroups.Contains(commandGroup);
        }

        public bool CanUseProactive(int commandGroup)
        {
            Synchronize();
            return HasLivingFriendly(commandGroup) && !_readyGroups.Contains(commandGroup);
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
                return Fail("Resolve or pass the current physical event before the enemy phase.");
            if (!AllLivingPlayersReady)
                return Fail("Every living player group must be Ready before enemies act.");

            int previousRound = _board.Round;
            if (!_board.EndRound())
            {
                _lastMessage = _board.LastMessage;
                return false;
            }

            if (_board.Round != previousRound)
            {
                _readyGroups.Clear();
                _trackedRound = _board.Round;
                _lastMessage = $"Enemy phase resolved. Round {_board.Round} begins; all player Ready states cleared.";
            }
            else
            {
                // Battle may have ended during the enemy phase.
                _lastMessage = _board.LastMessage;
            }
            return true;
        }

        public void Reset()
        {
            _readyGroups.Clear();
            _trackedRound = _board.Round;
            _lastMessage = string.Empty;
        }

        public void Synchronize()
        {
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
