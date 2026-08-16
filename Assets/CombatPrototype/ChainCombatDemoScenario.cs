using System;

namespace MountingForce.CombatPrototype
{
    /// <summary>
    /// Deterministic four-player showcase used by the playable combat demo and its regression test.
    /// The board remains authoritative for combat causality; an optional environment bridge mirrors
    /// semantic consequences into production world systems.
    /// </summary>
    public sealed class ChainCombatDemoScenario
    {
        private static readonly GridPos East = new GridPos(1, 0);
        private static readonly GridPos West = new GridPos(-1, 0);

        private readonly ChainCombatBoard _board;
        private readonly ChainReactionReservationCoordinator _reservations;
        private readonly IChainCombatEnvironmentBridge _environment;

        public ChainCombatDemoScenario(
            ChainCombatBoard board,
            ChainReactionReservationCoordinator reservations,
            IChainCombatEnvironmentBridge environment = null)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
            _environment = environment;
        }

        public int StepIndex { get; private set; }
        public bool IsComplete => StepIndex >= 4;
        public string LastMessage { get; private set; } = "Ready to demonstrate a four-player physical cascade.";

        public string CurrentStepLabel
        {
            get
            {
                switch (StepIndex)
                {
                    case 0: return "P1 Stephen: Uppercut the Ogre to create an airborne event.";
                    case 1: return "P2 Weldon: claim the airborne event and redirect it east.";
                    case 2: return "P3 Madeline: claim the collision and repulse the Goblin into the tree.";
                    case 3: return "P4 Grom: claim the tree impact and drop the tree back through the enemies.";
                    default: return "Showcase complete: four players, one causal chain.";
                }
            }
        }

        public bool TryAdvance()
        {
            if (IsComplete)
            {
                LastMessage = "The showcase cascade is already complete.";
                return false;
            }

            bool success;
            switch (StepIndex)
            {
                case 0: success = TryLaunch(); break;
                case 1: success = TryRedirect(); break;
                case 2: success = TryRepulseIntoTree(); break;
                case 3: success = TryDropTree(); break;
                default: success = false; break;
            }

            if (success) StepIndex++;
            return success;
        }

        private bool TryLaunch()
        {
            ChainUnitState stephen = FindUnit(ChainRecruitKind.Stephen, CombatTeam.Friendly);
            ChainUnitState ogre = FindUnit(ChainRecruitKind.Ogre, CombatTeam.Enemy);
            if (stephen == null || ogre == null) return Fail("The showcase requires living Stephen and Ogre units.");

            if (!_board.TryUppercut(stephen.Id, ogre.Id)) return Fail(_board.LastMessage);
            if (_board.PendingReaction == null || _board.PendingReaction.Kind != ChainReactionKind.Airborne)
                return Fail("Uppercut did not create the expected airborne event.");

            LastMessage = "P1 launched the Ogre. The airborne event is now available for a teammate handoff.";
            return true;
        }

        private bool TryRedirect()
        {
            ChainUnitState weldon = FindUnit(ChainRecruitKind.Weldon, CombatTeam.Friendly);
            ChainReactionOpportunity reaction = _board.PendingReaction;
            if (weldon == null || reaction == null || reaction.Kind != ChainReactionKind.Airborne)
                return Fail("The redirect step requires Weldon and a live airborne event.");

            ChainUnitState target = _board.GetUnit(reaction.PrimaryUnitId);
            if (target == null) return Fail("The airborne showcase target no longer exists.");

            if (!_reservations.TryReserve(weldon.CommandGroup) ||
                !_reservations.TryClaim(weldon.Id, ChainReactionAbility.Crosswind) ||
                !_board.TryCrosswind(weldon.Id, target.Position + East))
                return Fail(_reservations.LastMessage);

            _reservations.Synchronize();
            if (_board.PendingReaction == null || _board.PendingReaction.Kind != ChainReactionKind.Collision)
                return Fail("Crosswind did not produce the expected creature collision.");

            LastMessage = "P2 redirected the Ogre into a Goblin. The collision is now a new decision for the party.";
            return true;
        }

        private bool TryRepulseIntoTree()
        {
            ChainUnitState madeline = FindUnit(ChainRecruitKind.Madeline, CombatTeam.Friendly);
            ChainReactionOpportunity reaction = _board.PendingReaction;
            if (madeline == null || reaction == null || reaction.Kind != ChainReactionKind.Collision)
                return Fail("The repulse step requires Madeline and a live collision event.");

            ChainUnitState target = ChooseGoblinParticipant(reaction);
            if (target == null) return Fail("The showcase collision no longer contains a living Goblin target.");

            GridPos targetBeforeRepulse = target.Position;
            if (!_reservations.TryReserve(madeline.CommandGroup) ||
                !_reservations.TryClaim(madeline.Id, ChainReactionAbility.Repulse) ||
                !_board.TryRepulse(madeline.Id, target.Id, target.Position + East))
                return Fail(_reservations.LastMessage);

            _reservations.Synchronize();
            if (_board.PendingReaction == null || _board.PendingReaction.Kind != ChainReactionKind.TreeImpact)
                return Fail("Repulse did not drive the Goblin into the expected tree-impact event.");

            ChainTreeState tree = _board.GetTree(_board.PendingReaction.TreeId);
            if (tree != null)
            {
                GridPos incoming = new GridPos(tree.Position.X - targetBeforeRepulse.X, tree.Position.Z - targetBeforeRepulse.Z);
                _environment?.NotifyTreeImpact(tree.Position, incoming, 4);
            }

            LastMessage = "P3 repulsed the Goblin into the tree. The environment has become the next link in the chain.";
            return true;
        }

        private bool TryDropTree()
        {
            ChainUnitState grom = FindUnit(ChainRecruitKind.Grom, CombatTeam.Friendly);
            ChainReactionOpportunity reaction = _board.PendingReaction;
            if (grom == null || reaction == null || reaction.Kind != ChainReactionKind.TreeImpact)
                return Fail("The finisher requires Grom and a live tree-impact event.");

            ChainTreeState tree = _board.GetTree(reaction.TreeId);
            if (tree == null || !tree.Standing) return Fail("The impacted showcase tree is no longer standing.");

            if (!_reservations.TryReserve(grom.CommandGroup) ||
                !_reservations.TryClaim(grom.Id, ChainReactionAbility.Timber) ||
                !_board.TryTimber(grom.Id, tree.Position + West))
                return Fail(_reservations.LastMessage);

            _reservations.Synchronize();
            _environment?.NotifyTreeFelled(tree.Position, tree.FallDirection, tree.IsNotched ? 7 : 5);
            LastMessage = $"Cascade complete: {_board.LastCascadeSteps} deliberate steps, {_board.LastCascadePlayers} players, {_board.LastHandoffs} handoffs.";
            return true;
        }

        private ChainUnitState ChooseGoblinParticipant(ChainReactionOpportunity reaction)
        {
            ChainUnitState primary = _board.GetUnit(reaction.PrimaryUnitId);
            ChainUnitState secondary = _board.GetUnit(reaction.SecondaryUnitId);
            if (primary != null && primary.IsAlive && primary.Kind == ChainRecruitKind.Goblin) return primary;
            if (secondary != null && secondary.IsAlive && secondary.Kind == ChainRecruitKind.Goblin) return secondary;
            return null;
        }

        private ChainUnitState FindUnit(ChainRecruitKind kind, CombatTeam team)
        {
            for (int i = 0; i < _board.Units.Count; i++)
            {
                ChainUnitState unit = _board.Units[i];
                if (unit.Kind == kind && unit.Team == team && unit.IsAlive) return unit;
            }
            return null;
        }

        private bool Fail(string message)
        {
            LastMessage = string.IsNullOrEmpty(message) ? "The showcase step could not be completed." : message;
            return false;
        }
    }
}
