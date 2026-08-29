using System;

namespace MountingForce.CombatPrototype
{
    /// <summary>
    /// Multiplayer coordination layer above the deterministic combat board.
    ///
    /// A physical event is first reserved by a player/command group without proving that the player has a valid answer.
    /// That is intentional: reservation is a social/ownership action, not a hint system. Once reserved, only recruits
    /// belonging to that player may attempt to claim the board event with a concrete capability. Releasing a concrete
    /// recruit claim keeps the player reservation so the player can reconsider without reopening a click race.
    ///
    /// The coordinator automatically forgets ownership whenever the board advances to a different physical event.
    /// </summary>
    public sealed class ChainReactionReservationCoordinator
    {
        private readonly ChainCombatBoard _board;
        private int _trackedOpportunityId;
        private int _reservedByCommandGroup;
        private string _lastMessage;

        public ChainReactionReservationCoordinator(ChainCombatBoard board)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            Synchronize();
        }

        public int ReservedByCommandGroup
        {
            get
            {
                Synchronize();
                return _reservedByCommandGroup;
            }
        }

        public int OpportunityId
        {
            get
            {
                Synchronize();
                return _trackedOpportunityId;
            }
        }

        public bool IsReserved => ReservedByCommandGroup != 0;

        public string LastMessage
        {
            get
            {
                Synchronize();
                return string.IsNullOrEmpty(_lastMessage) ? _board.LastMessage : _lastMessage;
            }
        }

        /// <summary>
        /// Synchronize after a real board reset/round advance. If the board refused a command and the same physical
        /// event is still pending, ownership is intentionally preserved; failed commands must never erase reservations.
        /// </summary>
        public void Reset()
        {
            ChainReactionOpportunity reaction = _board.PendingReaction;
            if (reaction != null && reaction.Id == _trackedOpportunityId)
            {
                _lastMessage = string.Empty;
                return;
            }

            _trackedOpportunityId = 0;
            _reservedByCommandGroup = 0;
            _lastMessage = string.Empty;
            Synchronize();
        }

        public bool TryReserve(int commandGroup)
        {
            Synchronize();
            ChainReactionOpportunity reaction = _board.PendingReaction;
            if (reaction == null)
                return Fail("There is no unresolved physical event to reserve.");
            if (reaction.IsClaimed)
                return Fail("That event already has a concrete recruit claim.");
            if (!HasLivingFriendly(commandGroup))
                return Fail($"P{commandGroup} has no living recruits and cannot reserve the event.");

            if (_reservedByCommandGroup == commandGroup)
            {
                _lastMessage = $"P{commandGroup} already owns event #{reaction.Id}. Choose a recruit and try the reaction you think applies.";
                return true;
            }

            if (_reservedByCommandGroup != 0)
                return Fail($"P{_reservedByCommandGroup} already reserved event #{reaction.Id}.");

            _reservedByCommandGroup = commandGroup;
            _lastMessage = $"P{commandGroup} reserved event #{reaction.Id}. The game still will not tell P{commandGroup} which recruit or ability can answer it.";
            return true;
        }

        public bool TryClaim(int unitId, ChainReactionAbility ability)
        {
            Synchronize();
            ChainReactionOpportunity reaction = _board.PendingReaction;
            if (reaction == null)
                return Fail("There is no unresolved physical event to answer.");
            if (_reservedByCommandGroup == 0)
                return Fail("A player must reserve the physical event before choosing a recruit or reaction.");

            ChainUnitState unit = _board.GetUnit(unitId);
            if (unit == null || !unit.IsAlive || unit.Team != CombatTeam.Friendly)
                return Fail("Choose a living friendly recruit from the player who reserved this event.");
            if (unit.CommandGroup != _reservedByCommandGroup)
                return Fail($"P{_reservedByCommandGroup} owns event #{reaction.Id}. P{unit.CommandGroup} cannot take the concrete claim unless the reservation is released.");

            bool success = _board.TryClaimReaction(unitId, ability);
            _lastMessage = _board.LastMessage;
            return success;
        }

        /// <summary>
        /// Gives up only the selected recruit/capability. The player-level reservation remains intact.
        /// </summary>
        public bool TryReleaseClaim(int unitId)
        {
            Synchronize();
            ChainReactionOpportunity reaction = _board.PendingReaction;
            if (reaction == null || !reaction.IsClaimed)
                return Fail("There is no concrete reaction claim to release.");
            if (_reservedByCommandGroup == 0)
                return Fail("The event has no player reservation to retain.");
            if (reaction.ClaimedByCommandGroup != _reservedByCommandGroup)
                return Fail("The concrete claim does not belong to the player holding this reservation.");

            bool success = _board.TryReleaseClaim(unitId);
            if (!success)
            {
                _lastMessage = _board.LastMessage;
                return false;
            }

            _lastMessage = $"P{_reservedByCommandGroup} still owns event #{reaction.Id}, but released the recruit/ability choice. P{_reservedByCommandGroup} may try another answer.";
            return true;
        }

        /// <summary>
        /// Gives the physical event back to the whole party. Any concrete claim is released first.
        /// </summary>
        public bool TryReleaseReservation(int commandGroup)
        {
            Synchronize();
            ChainReactionOpportunity reaction = _board.PendingReaction;
            if (reaction == null || _reservedByCommandGroup == 0)
                return Fail("There is no player reservation to release.");
            if (_reservedByCommandGroup != commandGroup)
                return Fail($"Only P{_reservedByCommandGroup} can release this reservation.");

            if (reaction.IsClaimed)
            {
                if (reaction.ClaimedByCommandGroup != commandGroup)
                    return Fail("Reservation/claim ownership is inconsistent; refusing to release another player's claim.");
                if (!_board.TryReleaseClaim(reaction.ClaimedByUnitId))
                {
                    _lastMessage = _board.LastMessage;
                    return false;
                }
            }

            int opportunityId = reaction.Id;
            _reservedByCommandGroup = 0;
            _lastMessage = $"P{commandGroup} released event #{opportunityId}. Any player may reserve it now.";
            return true;
        }

        public bool TryPass()
        {
            Synchronize();
            if (_board.PendingReaction == null)
                return Fail("There is no physical event to pass.");
            if (_reservedByCommandGroup != 0)
                return Fail($"P{_reservedByCommandGroup} reserved this event. That player must release it before the group can pass.");

            bool success = _board.PassReaction();
            _lastMessage = _board.LastMessage;
            Synchronize();
            return success;
        }

        /// <summary>
        /// Call after a reaction execution or any board operation that may replace/consume PendingReaction.
        /// Properties and all coordinator commands call this automatically as well.
        /// </summary>
        public void Synchronize()
        {
            ChainReactionOpportunity reaction = _board.PendingReaction;
            int currentId = reaction == null ? 0 : reaction.Id;
            if (currentId == _trackedOpportunityId) return;

            _trackedOpportunityId = currentId;
            _reservedByCommandGroup = 0;
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
