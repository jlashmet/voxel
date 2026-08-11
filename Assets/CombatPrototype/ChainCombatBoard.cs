using System;
using System.Collections.Generic;

namespace MountingForce.CombatPrototype
{
    public enum ChainRecruitKind
    {
        Stephen,
        Brutus,
        Weldon,
        Madeline,
        Mira,
        Grom,
        Skitter,
        Ogre,
        Goblin
    }

    public enum ChainReactionKind
    {
        None,
        Airborne,
        Collision,
        TreeImpact
    }

    public enum ChainReactionAbility
    {
        None,
        Crosswind,
        CatchThrow,
        Repulse,
        FollowThrough,
        HookYank,
        Timber
    }

    public sealed class ChainUnitState
    {
        internal ChainUnitState(int id, string name, ChainRecruitKind kind, CombatTeam team, int commandGroup, GridPos position, int maxHp)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Team = team;
            CommandGroup = commandGroup;
            Position = position;
            MaxHp = maxHp;
            Hp = maxHp;
        }

        public int Id { get; }
        public string Name { get; }
        public ChainRecruitKind Kind { get; }
        public CombatTeam Team { get; }
        public int CommandGroup { get; }
        public GridPos Position { get; internal set; }
        public int MaxHp { get; }
        public int Hp { get; internal set; }
        public bool ActionSpent { get; internal set; }
        public bool ReactionSpent { get; internal set; }
        public bool Airborne { get; internal set; }
        public bool IsAlive => Hp > 0;
    }

    public sealed class ChainTreeState
    {
        internal ChainTreeState(int id, GridPos position)
        {
            Id = id;
            Position = position;
            FallDirection = new GridPos(0, 0);
        }

        public int Id { get; }
        public GridPos Position { get; }
        public bool Standing { get; internal set; } = true;
        public GridPos FallDirection { get; internal set; }
        public int Stress { get; internal set; }
    }

    public sealed class ChainReactionOpportunity
    {
        internal ChainReactionOpportunity(
            int id,
            ChainReactionKind kind,
            int primaryUnitId,
            int secondaryUnitId,
            int treeId,
            GridPos position,
            int impactForce,
            string description)
        {
            Id = id;
            Kind = kind;
            PrimaryUnitId = primaryUnitId;
            SecondaryUnitId = secondaryUnitId;
            TreeId = treeId;
            Position = position;
            ImpactForce = impactForce;
            Description = description;
        }

        public int Id { get; }
        public ChainReactionKind Kind { get; }
        public int PrimaryUnitId { get; }
        public int SecondaryUnitId { get; }
        public int TreeId { get; }
        public GridPos Position { get; }
        public int ImpactForce { get; }
        public string Description { get; }
        public int ClaimedByUnitId { get; internal set; }
        public int ClaimedByCommandGroup { get; internal set; }
        public ChainReactionAbility ClaimedAbility { get; internal set; }
        public bool IsClaimed => ClaimedByUnitId != 0;
    }

    internal sealed class ChainMotionState
    {
        public int UnitId;
        public GridPos Direction;
        public int RemainingForce;
        public bool Airborne;
    }

    /// <summary>
    /// Deterministic combat sandbox for discovering and executing physical cascades.
    /// The server exposes physical facts, not compatible reactions. Clients attempt to claim a fact with a recruit
    /// and capability they believe applies; the first valid claim owns the next decision until execution/release.
    /// </summary>
    public sealed class ChainCombatBoard
    {
        public const int Width = 14;
        public const int Depth = 10;

        private const int MoveRange = 3;
        private const int ConstructRange = 6;
        private const int PortalPairMaxDistance = 9;
        private const int MaximumMotionForce = 12;
        private const int TreeReactionMinimumForce = 2;
        private const int TreeBreakStress = 7;

        private readonly List<ChainUnitState> _units = new List<ChainUnitState>();
        private readonly List<ChainTreeState> _trees = new List<ChainTreeState>();
        private readonly HashSet<GridPos> _amplifiers = new HashSet<GridPos>();
        private readonly List<string> _log = new List<string>();
        private readonly HashSet<int> _cascadeGroups = new HashSet<int>();

        private ChainMotionState _motion;
        private int _nextUnitId;
        private int _nextTreeId;
        private int _nextOpportunityId;
        private bool _cascadeActive;
        private int _lastCascadeCommandGroup;

        public ChainCombatBoard()
        {
            Reset();
        }

        public IReadOnlyList<ChainUnitState> Units => _units;
        public IReadOnlyList<ChainTreeState> Trees => _trees;
        public IReadOnlyCollection<GridPos> Amplifiers => _amplifiers;
        public IReadOnlyList<string> Log => _log;
        public GridPos? PortalA { get; private set; }
        public GridPos? PortalB { get; private set; }
        public ChainReactionOpportunity PendingReaction { get; private set; }
        public int Round { get; private set; }
        public string LastMessage { get; private set; }
        public bool BattleOver { get; private set; }
        public string BattleResult { get; private set; }
        public int CurrentCascadeSteps { get; private set; }
        public int CurrentCascadePlayers => _cascadeGroups.Count;
        public int LastCascadeSteps { get; private set; }
        public int LastCascadePlayers { get; private set; }
        public int BestCascadeSteps { get; private set; }
        public int BestCascadePlayers { get; private set; }
        public int CurrentHandoffs { get; private set; }
        public int LastHandoffs { get; private set; }
        public int BestHandoffs { get; private set; }

        public void Reset()
        {
            _units.Clear();
            _trees.Clear();
            _amplifiers.Clear();
            _log.Clear();
            _cascadeGroups.Clear();
            _motion = null;
            _nextUnitId = 1;
            _nextTreeId = 1;
            _nextOpportunityId = 1;
            _cascadeActive = false;
            _lastCascadeCommandGroup = 0;
            PortalA = null;
            PortalB = null;
            PendingReaction = null;
            Round = 1;
            LastMessage = string.Empty;
            BattleOver = false;
            BattleResult = string.Empty;
            CurrentCascadeSteps = 0;
            LastCascadeSteps = 0;
            LastCascadePlayers = 0;
            BestCascadeSteps = 0;
            BestCascadePlayers = 0;
            CurrentHandoffs = 0;
            LastHandoffs = 0;
            BestHandoffs = 0;

            AddFriendly("Stephen", ChainRecruitKind.Stephen, 1, new GridPos(2, 4), 7);
            AddFriendly("Brutus", ChainRecruitKind.Brutus, 1, new GridPos(4, 6), 8);
            AddFriendly("Weldon", ChainRecruitKind.Weldon, 2, new GridPos(4, 2), 6);
            AddFriendly("Madeline", ChainRecruitKind.Madeline, 3, new GridPos(6, 5), 6);
            AddFriendly("Mira", ChainRecruitKind.Mira, 3, new GridPos(1, 1), 5);
            AddFriendly("Grom", ChainRecruitKind.Grom, 4, new GridPos(10, 7), 8);
            AddFriendly("Skitter", ChainRecruitKind.Skitter, 4, new GridPos(8, 6), 6);

            AddEnemy("Ogre", ChainRecruitKind.Ogre, new GridPos(3, 4), 11);
            AddEnemy("Goblin A", ChainRecruitKind.Goblin, new GridPos(8, 4), 7);
            AddEnemy("Goblin B", ChainRecruitKind.Goblin, new GridPos(10, 6), 7);

            AddTree(new GridPos(11, 4));
            AddTree(new GridPos(11, 8));

            WriteLog("Round 1. The world reports what physically happened, never which recruit should answer it.");
        }

        public ChainUnitState GetUnit(int id)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].Id == id) return _units[i];
            }
            return null;
        }

        public ChainTreeState GetTree(int id)
        {
            for (int i = 0; i < _trees.Count; i++)
            {
                if (_trees[i].Id == id) return _trees[i];
            }
            return null;
        }

        public ChainUnitState FindUnitAt(GridPos position)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                ChainUnitState unit = _units[i];
                if (unit.IsAlive && unit.Position.Equals(position)) return unit;
            }
            return null;
        }

        public ChainTreeState FindStandingTreeAt(GridPos position)
        {
            for (int i = 0; i < _trees.Count; i++)
            {
                ChainTreeState tree = _trees[i];
                if (tree.Standing && tree.Position.Equals(position)) return tree;
            }
            return null;
        }

        public bool TryMove(int unitId, GridPos destination)
        {
            if (!TryGetNormalActor(unitId, out ChainUnitState unit)) return false;
            if (!IsInBounds(destination)) return Fail("That cell is outside the battle board.");
            if (Distance(unit.Position, destination) > MoveRange) return Fail($"Move reaches at most {MoveRange} cells.");
            if (!IsCellOpen(destination)) return Fail("That cell is occupied.");

            unit.Position = destination;
            unit.ActionSpent = true;
            WriteLog($"P{unit.CommandGroup} moved {unit.Name} to {destination}.");
            return true;
        }

        public bool TryBasicHit(int unitId, int targetId)
        {
            if (!TryGetNormalActor(unitId, out ChainUnitState unit)) return false;
            ChainUnitState target = GetUnit(targetId);
            if (!IsEnemyTarget(unit, target) || Distance(unit.Position, target.Position) != 1)
                return Fail("Strike needs an adjacent living enemy.");

            unit.ActionSpent = true;
            Damage(target, 1, $"{unit.Name} struck {target.Name}");
            CheckBattleEnd();
            return true;
        }

        public bool TryUppercut(int stephenId, int targetId)
        {
            if (!TryGetNormalActor(stephenId, out ChainUnitState stephen) || stephen.Kind != ChainRecruitKind.Stephen)
                return Fail("Stephen is required for Uppercut.");

            ChainUnitState target = GetUnit(targetId);
            if (!IsEnemyTarget(stephen, target) || Distance(stephen.Position, target.Position) != 1)
                return Fail("Uppercut needs an adjacent living enemy.");

            GridPos direction = CardinalDirection(stephen.Position, target.Position);
            stephen.ActionSpent = true;
            target.Airborne = true;
            _motion = NewMotion(target.Id, direction, 5, true);
            BeginCascade(stephen.CommandGroup, $"{stephen.Name} launched {target.Name}");
            OpenOpportunity(ChainReactionKind.Airborne, target.Id, 0, 0, target.Position, 5,
                $"{target.Name} is airborne, moving {DirectionName(direction)}, with force 5 ({ForceWord(5)}).");
            return true;
        }

        public bool TryShoulderHurl(int brutusId, int targetId, GridPos aim)
        {
            if (!TryGetNormalActor(brutusId, out ChainUnitState brutus) || brutus.Kind != ChainRecruitKind.Brutus)
                return Fail("Brutus is required for Shoulder Hurl.");

            ChainUnitState target = GetUnit(targetId);
            if (!IsEnemyTarget(brutus, target) || Distance(brutus.Position, target.Position) != 1)
                return Fail("Shoulder Hurl needs an adjacent living enemy.");

            GridPos direction = CardinalDirection(target.Position, aim);
            if (IsZero(direction)) return Fail("Aim away from the target to choose the hurl direction.");

            brutus.ActionSpent = true;
            _motion = NewMotion(target.Id, direction, 5, false);
            BeginCascade(brutus.CommandGroup, $"{brutus.Name} shoulder-hurled {target.Name}");
            ResolveMotion();
            CheckBattleEnd();
            return true;
        }

        public bool TryGust(int weldonId, int targetId)
        {
            if (!TryGetNormalActor(weldonId, out ChainUnitState weldon) || weldon.Kind != ChainRecruitKind.Weldon)
                return Fail("Weldon is required for Gust.");

            ChainUnitState target = GetUnit(targetId);
            if (!IsEnemyTarget(weldon, target) || Distance(weldon.Position, target.Position) > 4)
                return Fail("Gust needs a living enemy within 4 cells.");

            GridPos direction = CardinalDirection(weldon.Position, target.Position);
            if (IsZero(direction)) return Fail("Gust needs a direction away from Weldon.");

            weldon.ActionSpent = true;
            _motion = NewMotion(target.Id, direction, 3, false);
            BeginCascade(weldon.CommandGroup, $"{weldon.Name} gusted {target.Name}");
            ResolveMotion();
            CheckBattleEnd();
            return true;
        }

        public bool TryPlacePortalPair(int miraId, GridPos entrance, GridPos exit)
        {
            if (!TryGetNormalActor(miraId, out ChainUnitState mira) || mira.Kind != ChainRecruitKind.Mira)
                return Fail("Mira is required to place portals.");
            if (!IsInBounds(entrance) || !IsInBounds(exit) || entrance.Equals(exit))
                return Fail("Choose two different cells on the board for the portals.");
            if (Distance(mira.Position, entrance) > ConstructRange || Distance(mira.Position, exit) > ConstructRange)
                return Fail($"Each portal must be within {ConstructRange} cells of Mira.");
            if (Distance(entrance, exit) > PortalPairMaxDistance)
                return Fail($"The portal pair can span at most {PortalPairMaxDistance} cells.");
            if (!IsCellOpenForConstruct(entrance) || !IsCellOpenForConstruct(exit))
                return Fail("Portals need empty cells.");

            PortalA = entrance;
            PortalB = exit;
            mira.ActionSpent = true;
            WriteLog($"{mira.Name} linked portals at {entrance} and {exit}. Travel through a portal costs no extra force.");
            return true;
        }

        public bool TryPlaceAmplifier(int miraId, GridPos position)
        {
            if (!TryGetNormalActor(miraId, out ChainUnitState mira) || mira.Kind != ChainRecruitKind.Mira)
                return Fail("Mira is required to place a force multiplier.");
            if (!IsInBounds(position) || Distance(mira.Position, position) > ConstructRange || !IsCellOpenForConstruct(position))
                return Fail("The multiplier needs an empty cell within Mira's 6-cell placement range.");

            _amplifiers.Add(position);
            mira.ActionSpent = true;
            WriteLog($"{mira.Name} placed a force multiplier at {position}. Moving bodies crossing it amplify their remaining force.");
            return true;
        }

        public bool TryClaimReaction(int unitId, ChainReactionAbility ability)
        {
            if (BattleOver || PendingReaction == null) return Fail("There is no unresolved physical event to claim.");
            if (PendingReaction.IsClaimed)
            {
                ChainUnitState owner = GetUnit(PendingReaction.ClaimedByUnitId);
                return Fail(owner == null ? "That event is already claimed." : $"P{owner.CommandGroup} already claimed this event with {owner.Name}.");
            }

            ChainUnitState unit = GetUnit(unitId);
            if (unit == null || !unit.IsAlive || unit.Team != CombatTeam.Friendly || unit.ReactionSpent)
                return Fail("That recruit cannot claim a reaction right now.");
            if (!CanClaim(unit, ability, PendingReaction))
                return Fail($"{unit.Name} cannot claim this event from the current position with that capability.");

            PendingReaction.ClaimedByUnitId = unit.Id;
            PendingReaction.ClaimedByCommandGroup = unit.CommandGroup;
            PendingReaction.ClaimedAbility = ability;
            WriteLog($"P{unit.CommandGroup} claimed event #{PendingReaction.Id} with {unit.Name}: {AbilityName(ability)}.");
            return true;
        }

        public bool TryReleaseClaim(int unitId)
        {
            if (PendingReaction == null || !PendingReaction.IsClaimed) return Fail("There is no reaction claim to release.");
            if (PendingReaction.ClaimedByUnitId != unitId) return Fail("Only the recruit holding this claim can release it.");

            ChainUnitState unit = GetUnit(unitId);
            WriteLog($"{unit?.Name ?? "A recruit"} released event #{PendingReaction.Id}. Another player may claim it.");
            PendingReaction.ClaimedByUnitId = 0;
            PendingReaction.ClaimedByCommandGroup = 0;
            PendingReaction.ClaimedAbility = ChainReactionAbility.None;
            return true;
        }

        public bool TryCrosswind(int weldonId, GridPos aim)
        {
            if (!TryGetClaimedActor(weldonId, ChainRecruitKind.Weldon, ChainReactionAbility.Crosswind,
                    out ChainUnitState weldon, out ChainReactionOpportunity reaction)) return false;

            ChainUnitState target = GetUnit(reaction.PrimaryUnitId);
            GridPos direction = target == null ? Zero : CardinalDirection(target.Position, aim);
            if (target == null || IsZero(direction) || _motion == null)
                return Fail("Crosswind needs an aimed direction while the airborne body still has momentum.");

            ConsumeClaim(weldon, $"redirected {target.Name} with Crosswind");
            _motion.Direction = direction;
            ResolveMotion();
            CheckBattleEnd();
            return true;
        }

        public bool TryCatchThrow(int brutusId, GridPos aim)
        {
            if (!TryGetClaimedActor(brutusId, ChainRecruitKind.Brutus, ChainReactionAbility.CatchThrow,
                    out ChainUnitState brutus, out ChainReactionOpportunity reaction)) return false;

            ChainUnitState target = GetUnit(reaction.PrimaryUnitId);
            GridPos direction = target == null ? Zero : CardinalDirection(target.Position, aim);
            if (target == null || IsZero(direction)) return Fail("Catch & Throw needs a throw direction.");
            if (!TryFindCatchCell(brutus, target, out GridPos catchCell)) return Fail("Brutus has no open adjacent cell to complete the catch.");

            target.Position = catchCell;
            target.Airborne = true;
            _motion = NewMotion(target.Id, direction, 7, true);
            ConsumeClaim(brutus, $"caught {target.Name} and rethrew it with force 7");
            ResolveMotion();
            CheckBattleEnd();
            return true;
        }

        public bool TryRepulse(int madelineId, int targetId, GridPos aim)
        {
            if (!TryGetClaimedActor(madelineId, ChainRecruitKind.Madeline, ChainReactionAbility.Repulse,
                    out ChainUnitState madeline, out ChainReactionOpportunity reaction)) return false;
            if (targetId != reaction.PrimaryUnitId && targetId != reaction.SecondaryUnitId)
                return Fail("Repulse must target one of the two collision participants.");

            ChainUnitState target = GetUnit(targetId);
            GridPos direction = target == null ? Zero : CardinalDirection(target.Position, aim);
            if (target == null || !target.IsAlive || IsZero(direction))
                return Fail("Choose a living collision participant and an outward direction.");

            _motion = NewMotion(target.Id, direction, 4, false);
            ConsumeClaim(madeline, $"repulsed {target.Name} with force 4");
            ResolveMotion();
            CheckBattleEnd();
            return true;
        }

        public bool TryFollowThrough(int stephenId, int targetId, GridPos aim)
        {
            if (!TryGetClaimedActor(stephenId, ChainRecruitKind.Stephen, ChainReactionAbility.FollowThrough,
                    out ChainUnitState stephen, out ChainReactionOpportunity reaction)) return false;
            if (targetId != reaction.PrimaryUnitId && targetId != reaction.SecondaryUnitId)
                return Fail("Follow Through must target one of the collision participants.");

            ChainUnitState target = GetUnit(targetId);
            GridPos direction = target == null ? Zero : CardinalDirection(target.Position, aim);
            if (target == null || !target.IsAlive || IsZero(direction))
                return Fail("Choose a living collision participant and a kick direction.");

            _motion = NewMotion(target.Id, direction, 5, false);
            ConsumeClaim(stephen, $"followed the collision with a force-5 kick on {target.Name}");
            ResolveMotion();
            CheckBattleEnd();
            return true;
        }

        public bool TryHookYank(int skitterId, int targetId, GridPos aim)
        {
            if (!TryGetClaimedActor(skitterId, ChainRecruitKind.Skitter, ChainReactionAbility.HookYank,
                    out ChainUnitState skitter, out ChainReactionOpportunity reaction)) return false;

            bool validTarget = targetId == reaction.PrimaryUnitId ||
                               (reaction.Kind == ChainReactionKind.Collision && targetId == reaction.SecondaryUnitId);
            if (!validTarget) return Fail("Hook Yank must grab a creature involved in the claimed event.");

            ChainUnitState target = GetUnit(targetId);
            GridPos direction = target == null ? Zero : CardinalDirection(target.Position, aim);
            if (target == null || !target.IsAlive || IsZero(direction)) return Fail("Hook Yank needs a living event participant and a pull direction.");

            GridPos oneStep = target.Position + direction;
            if (Distance(oneStep, skitter.Position) >= Distance(target.Position, skitter.Position))
                return Fail("Skitter's hook must pull the target closer to Skitter.");

            _motion = NewMotion(target.Id, direction, 5, false);
            ConsumeClaim(skitter, $"hook-yanked {target.Name} with force 5");
            ResolveMotion();
            CheckBattleEnd();
            return true;
        }

        public bool TryTimber(int gromId, GridPos aim)
        {
            if (!TryGetClaimedActor(gromId, ChainRecruitKind.Grom, ChainReactionAbility.Timber,
                    out ChainUnitState grom, out ChainReactionOpportunity reaction)) return false;

            ChainTreeState tree = GetTree(reaction.TreeId);
            GridPos direction = tree == null ? Zero : CardinalDirection(tree.Position, aim);
            if (tree == null || !tree.Standing || IsZero(direction)) return Fail("Timber needs the struck standing tree and a fall direction.");

            tree.Standing = false;
            tree.FallDirection = direction;
            ConsumeClaim(grom, $"committed tree #{tree.Id} to a {DirectionName(direction)} fall");

            ChainUnitState carried = null;
            for (int step = 1; step <= 4; step++)
            {
                GridPos cell = tree.Position + direction * step;
                if (!IsInBounds(cell)) break;
                ChainUnitState hit = FindUnitAt(cell);
                if (hit == null) continue;

                int damage = step <= 2 ? 5 : 4;
                Damage(hit, damage, $"The falling tree crushed {hit.Name}");
                if (carried == null && hit.IsAlive) carried = hit;
            }

            if (carried != null)
            {
                _motion = NewMotion(carried.Id, direction, 4, false);
                WriteLog($"The tree carries {carried.Name} onward with force 4.");
                ResolveMotion();
            }
            else
            {
                _motion = null;
                FinishCascadeIfIdle();
            }

            CheckBattleEnd();
            return true;
        }

        public bool PassReaction()
        {
            if (PendingReaction == null) return Fail("There is no reaction window to pass.");
            if (PendingReaction.IsClaimed)
            {
                ChainUnitState owner = GetUnit(PendingReaction.ClaimedByUnitId);
                return Fail(owner == null
                    ? "A claimed event must be released before the group can pass."
                    : $"P{owner.CommandGroup} {owner.Name} owns this event. Execute it or release the claim before passing.");
            }

            string description = PendingReaction.Description;
            ChainReactionKind kind = PendingReaction.Kind;
            PendingReaction = null;
            WriteLog($"Nobody claimed the event: {description}");

            if (kind == ChainReactionKind.Airborne && _motion != null)
            {
                ResolveMotion();
            }
            else
            {
                _motion = null;
                FinishCascadeIfIdle();
            }

            CheckBattleEnd();
            return true;
        }

        public bool EndRound()
        {
            if (BattleOver) return Fail("The battle is over. Reset to play again.");
            if (PendingReaction != null) return Fail("Resolve or pass the physical event before ending the round.");

            RunEnemyTurn();
            CheckBattleEnd();
            if (BattleOver) return true;

            Round++;
            for (int i = 0; i < _units.Count; i++)
            {
                ChainUnitState unit = _units[i];
                if (unit.Team == CombatTeam.Friendly && unit.IsAlive)
                {
                    unit.ActionSpent = false;
                    unit.ReactionSpent = false;
                }
            }

            WriteLog($"Round {Round} begins. Normal actions and reactions refreshed.");
            return true;
        }

        public bool IsInBounds(GridPos position)
        {
            return position.X >= 0 && position.X < Width && position.Z >= 0 && position.Z < Depth;
        }

        public static int Distance(GridPos a, GridPos b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);
        }

        public static string AbilityName(ChainReactionAbility ability)
        {
            switch (ability)
            {
                case ChainReactionAbility.Crosswind: return "Crosswind";
                case ChainReactionAbility.CatchThrow: return "Catch & Throw";
                case ChainReactionAbility.Repulse: return "Repulse";
                case ChainReactionAbility.FollowThrough: return "Follow Through";
                case ChainReactionAbility.HookYank: return "Hook Yank";
                case ChainReactionAbility.Timber: return "Timber";
                default: return "None";
            }
        }

        public static string ForceWord(int force)
        {
            if (force <= 1) return "weak";
            if (force <= 3) return "solid";
            if (force <= 5) return "hard";
            if (force <= 8) return "violent";
            return "devastating";
        }

        private bool CanClaim(ChainUnitState unit, ChainReactionAbility ability, ChainReactionOpportunity reaction)
        {
            switch (ability)
            {
                case ChainReactionAbility.Crosswind:
                    return unit.Kind == ChainRecruitKind.Weldon && reaction.Kind == ChainReactionKind.Airborne && Distance(unit.Position, reaction.Position) <= 6;
                case ChainReactionAbility.CatchThrow:
                    return unit.Kind == ChainRecruitKind.Brutus && reaction.Kind == ChainReactionKind.Airborne && Distance(unit.Position, reaction.Position) <= 3;
                case ChainReactionAbility.Repulse:
                    return unit.Kind == ChainRecruitKind.Madeline && reaction.Kind == ChainReactionKind.Collision && Distance(unit.Position, reaction.Position) <= 5;
                case ChainReactionAbility.FollowThrough:
                    return unit.Kind == ChainRecruitKind.Stephen && reaction.Kind == ChainReactionKind.Collision && Distance(unit.Position, reaction.Position) <= 4;
                case ChainReactionAbility.HookYank:
                    return unit.Kind == ChainRecruitKind.Skitter &&
                           (reaction.Kind == ChainReactionKind.Collision || reaction.Kind == ChainReactionKind.TreeImpact) &&
                           Distance(unit.Position, reaction.Position) <= 6;
                case ChainReactionAbility.Timber:
                    return unit.Kind == ChainRecruitKind.Grom && reaction.Kind == ChainReactionKind.TreeImpact && Distance(unit.Position, reaction.Position) <= 5;
                default:
                    return false;
            }
        }

        private bool TryGetClaimedActor(
            int unitId,
            ChainRecruitKind kind,
            ChainReactionAbility ability,
            out ChainUnitState unit,
            out ChainReactionOpportunity reaction)
        {
            unit = GetUnit(unitId);
            reaction = PendingReaction;
            if (BattleOver || reaction == null || !reaction.IsClaimed) return Fail("Claim a physical event before executing a reaction.");
            if (unit == null || !unit.IsAlive || unit.Kind != kind || reaction.ClaimedByUnitId != unit.Id || reaction.ClaimedAbility != ability)
                return Fail("That recruit does not own this reaction claim.");
            return true;
        }

        private void ConsumeClaim(ChainUnitState unit, string description)
        {
            int opportunityId = PendingReaction == null ? 0 : PendingReaction.Id;
            unit.ReactionSpent = true;
            PendingReaction = null;
            AddCascadeStep(unit.CommandGroup, description);
            WriteLog($"P{unit.CommandGroup} executed claim #{opportunityId}: {unit.Name} {description}.");
        }

        private void ResolveMotion()
        {
            while (_motion != null && PendingReaction == null)
            {
                ChainUnitState mover = GetUnit(_motion.UnitId);
                if (mover == null || !mover.IsAlive || _motion.RemainingForce <= 0)
                {
                    StopMotion(mover);
                    FinishCascadeIfIdle();
                    return;
                }

                int impactForce = _motion.RemainingForce;
                GridPos next = mover.Position + _motion.Direction;
                if (!IsInBounds(next))
                {
                    int damage = ImpactDamage(impactForce);
                    Damage(mover, damage, $"{mover.Name} slammed into the arena edge at force {impactForce} ({ForceWord(impactForce)})");
                    StopMotion(mover);
                    FinishCascadeIfIdle();
                    return;
                }

                ChainTreeState tree = FindStandingTreeAt(next);
                if (tree != null)
                {
                    int damage = ImpactDamage(impactForce);
                    Damage(mover, damage, $"{mover.Name} struck tree #{tree.Id} at force {impactForce} ({ForceWord(impactForce)})");
                    tree.Stress = Math.Min(TreeBreakStress, tree.Stress + impactForce);
                    mover.Airborne = false;
                    _motion = null;

                    if (impactForce >= TreeReactionMinimumForce && mover.IsAlive)
                    {
                        OpenOpportunity(ChainReactionKind.TreeImpact, mover.Id, 0, tree.Id, tree.Position, impactForce,
                            $"{mover.Name} hit tree #{tree.Id} with force {impactForce} ({ForceWord(impactForce)}). Tree stress: {tree.Stress}/{TreeBreakStress}.");
                    }
                    else
                    {
                        WriteLog($"The tree flexed, but the force was too weak to create a meaningful tree-impact opportunity.");
                        FinishCascadeIfIdle();
                    }
                    return;
                }

                ChainUnitState occupant = FindUnitAt(next);
                if (occupant != null && occupant.Id != mover.Id)
                {
                    int damage = ImpactDamage(impactForce);
                    int moverDamage = Math.Max(1, damage - 1);
                    Damage(mover, moverDamage, $"{mover.Name} collided with {occupant.Name} at force {impactForce} ({ForceWord(impactForce)})");
                    Damage(occupant, damage, $"{occupant.Name} absorbed the impact");
                    mover.Airborne = false;
                    _motion = null;

                    if (mover.IsAlive || occupant.IsAlive)
                    {
                        OpenOpportunity(ChainReactionKind.Collision, mover.Id, occupant.Id, 0, next, impactForce,
                            $"{mover.Name} collided with {occupant.Name} at force {impactForce} ({ForceWord(impactForce)}). Impact damage: {damage}.");
                    }
                    else
                    {
                        FinishCascadeIfIdle();
                    }
                    return;
                }

                mover.Position = next;
                _motion.RemainingForce--;
                TryTeleport(mover);

                if (_motion != null && _amplifiers.Contains(mover.Position))
                {
                    int before = _motion.RemainingForce;
                    _motion.RemainingForce = Math.Min(MaximumMotionForce, Math.Max(before + 1, before * 2));
                    WriteLog($"{mover.Name} crossed a force multiplier: {before} -> {_motion.RemainingForce} ({ForceWord(_motion.RemainingForce)}).");
                }

                if (_motion != null && _motion.RemainingForce <= 0)
                {
                    StopMotion(mover);
                    FinishCascadeIfIdle();
                }
            }
        }

        private void OpenOpportunity(
            ChainReactionKind kind,
            int primaryId,
            int secondaryId,
            int treeId,
            GridPos position,
            int impactForce,
            string description)
        {
            PendingReaction = new ChainReactionOpportunity(
                _nextOpportunityId++, kind, primaryId, secondaryId, treeId, position, impactForce, description);
            WriteLog($"Physical event #{PendingReaction.Id}: {description} It is unclaimed.");
        }

        private bool TryTeleport(ChainUnitState mover)
        {
            if (!PortalA.HasValue || !PortalB.HasValue || _motion == null) return false;

            GridPos destination;
            if (mover.Position.Equals(PortalA.Value)) destination = PortalB.Value;
            else if (mover.Position.Equals(PortalB.Value)) destination = PortalA.Value;
            else return false;

            ChainUnitState occupant = FindUnitAt(destination);
            ChainTreeState tree = FindStandingTreeAt(destination);
            if ((occupant != null && occupant.Id != mover.Id) || tree != null)
            {
                WriteLog($"{mover.Name} reached a portal, but the paired exit was obstructed.");
                return false;
            }

            GridPos source = mover.Position;
            mover.Position = destination;
            WriteLog($"{mover.Name} teleported {source} -> {destination} with force {_motion.RemainingForce} preserved.");
            return true;
        }

        private static int ImpactDamage(int force)
        {
            if (force <= 2) return 1;
            if (force <= 4) return 2;
            if (force <= 6) return 3;
            if (force <= 8) return 4;
            return 5;
        }

        private static ChainMotionState NewMotion(int unitId, GridPos direction, int force, bool airborne)
        {
            return new ChainMotionState
            {
                UnitId = unitId,
                Direction = direction,
                RemainingForce = Math.Max(0, Math.Min(MaximumMotionForce, force)),
                Airborne = airborne
            };
        }

        private void BeginCascade(int commandGroup, string description)
        {
            if (_cascadeActive) FinishCascade();

            _cascadeActive = true;
            _cascadeGroups.Clear();
            _cascadeGroups.Add(commandGroup);
            CurrentCascadeSteps = 1;
            CurrentHandoffs = 0;
            _lastCascadeCommandGroup = commandGroup;
            WriteLog($"Cascade started by P{commandGroup}: {description}.");
        }

        private void AddCascadeStep(int commandGroup, string description)
        {
            if (!_cascadeActive)
            {
                _cascadeActive = true;
                _cascadeGroups.Clear();
                CurrentCascadeSteps = 0;
                CurrentHandoffs = 0;
                _lastCascadeCommandGroup = 0;
            }

            if (_lastCascadeCommandGroup != 0 && _lastCascadeCommandGroup != commandGroup) CurrentHandoffs++;
            _lastCascadeCommandGroup = commandGroup;
            CurrentCascadeSteps++;
            _cascadeGroups.Add(commandGroup);
            WriteLog($"Cascade step {CurrentCascadeSteps}: P{commandGroup} {description}. Handoffs: {CurrentHandoffs}.");
        }

        private void FinishCascadeIfIdle()
        {
            if (_cascadeActive && PendingReaction == null && _motion == null) FinishCascade();
        }

        private void FinishCascade()
        {
            LastCascadeSteps = CurrentCascadeSteps;
            LastCascadePlayers = _cascadeGroups.Count;
            LastHandoffs = CurrentHandoffs;

            bool newBest = LastCascadeSteps > BestCascadeSteps ||
                           (LastCascadeSteps == BestCascadeSteps && LastCascadePlayers > BestCascadePlayers) ||
                           (LastCascadeSteps == BestCascadeSteps && LastCascadePlayers == BestCascadePlayers && LastHandoffs > BestHandoffs);
            if (newBest)
            {
                BestCascadeSteps = LastCascadeSteps;
                BestCascadePlayers = LastCascadePlayers;
                BestHandoffs = LastHandoffs;
            }

            if (LastCascadeSteps > 0)
                WriteLog($"Cascade ended: {LastCascadeSteps} deliberate steps, {LastCascadePlayers} players, {LastHandoffs} handoffs.");

            _cascadeActive = false;
            _cascadeGroups.Clear();
            _lastCascadeCommandGroup = 0;
            CurrentCascadeSteps = 0;
            CurrentHandoffs = 0;
        }

        private void StopMotion(ChainUnitState mover)
        {
            if (mover != null) mover.Airborne = false;
            _motion = null;
        }

        private bool TryFindCatchCell(ChainUnitState brutus, ChainUnitState target, out GridPos catchCell)
        {
            GridPos toward = CardinalDirection(brutus.Position, target.Position);
            GridPos[] candidates =
            {
                brutus.Position + toward,
                brutus.Position + new GridPos(1, 0),
                brutus.Position + new GridPos(-1, 0),
                brutus.Position + new GridPos(0, 1),
                brutus.Position + new GridPos(0, -1)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                GridPos cell = candidates[i];
                ChainUnitState occupant = FindUnitAt(cell);
                if (IsInBounds(cell) && FindStandingTreeAt(cell) == null && (occupant == null || occupant.Id == target.Id))
                {
                    catchCell = cell;
                    return true;
                }
            }

            catchCell = Zero;
            return false;
        }

        private void RunEnemyTurn()
        {
            List<ChainUnitState> enemies = new List<ChainUnitState>();
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].Team == CombatTeam.Enemy && _units[i].IsAlive) enemies.Add(_units[i]);
            }

            enemies.Sort((a, b) => a.Id.CompareTo(b.Id));
            for (int i = 0; i < enemies.Count; i++)
            {
                ChainUnitState enemy = enemies[i];
                ChainUnitState target = FindNearestFriendly(enemy.Position);
                if (target == null) return;

                if (Distance(enemy.Position, target.Position) == 1)
                {
                    Damage(target, 1, $"{enemy.Name} attacked {target.Name}");
                    continue;
                }

                GridPos xStep = new GridPos(Sign(target.Position.X - enemy.Position.X), 0);
                GridPos zStep = new GridPos(0, Sign(target.Position.Z - enemy.Position.Z));
                GridPos first = (Round + enemy.Id) % 2 == 0 ? xStep : zStep;
                GridPos second = (Round + enemy.Id) % 2 == 0 ? zStep : xStep;
                if (TryEnemyStep(enemy, first) || TryEnemyStep(enemy, second)) WriteLog($"{enemy.Name} advanced toward {target.Name}.");
            }
        }

        private bool TryEnemyStep(ChainUnitState enemy, GridPos step)
        {
            if (IsZero(step)) return false;
            GridPos destination = enemy.Position + step;
            if (!IsInBounds(destination) || !IsCellOpen(destination)) return false;
            enemy.Position = destination;
            return true;
        }

        private ChainUnitState FindNearestFriendly(GridPos from)
        {
            ChainUnitState best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < _units.Count; i++)
            {
                ChainUnitState candidate = _units[i];
                if (candidate.Team != CombatTeam.Friendly || !candidate.IsAlive) continue;
                int distance = Distance(from, candidate.Position);
                if (distance < bestDistance || (distance == bestDistance && (best == null || candidate.Id < best.Id)))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private bool TryGetNormalActor(int unitId, out ChainUnitState unit)
        {
            unit = GetUnit(unitId);
            if (BattleOver) return Fail("The battle is over. Reset to play again.");
            if (PendingReaction != null) return Fail("A physical event is unresolved. Someone must claim it or the group must pass.");
            if (unit == null || !unit.IsAlive || unit.Team != CombatTeam.Friendly) return Fail("Choose a living friendly recruit.");
            if (unit.ActionSpent) return Fail($"{unit.Name} already used a normal action this round.");
            return true;
        }

        private bool IsEnemyTarget(ChainUnitState actor, ChainUnitState target)
        {
            return actor != null && target != null && target.IsAlive && actor.Team != target.Team;
        }

        private bool IsCellOpen(GridPos position)
        {
            return FindUnitAt(position) == null && FindStandingTreeAt(position) == null;
        }

        private bool IsCellOpenForConstruct(GridPos position)
        {
            if (!IsCellOpen(position) || _amplifiers.Contains(position)) return false;
            if (PortalA.HasValue && PortalA.Value.Equals(position)) return false;
            if (PortalB.HasValue && PortalB.Value.Equals(position)) return false;
            return true;
        }

        private void CheckBattleEnd()
        {
            bool anyFriendly = false;
            bool anyEnemy = false;
            for (int i = 0; i < _units.Count; i++)
            {
                ChainUnitState unit = _units[i];
                if (!unit.IsAlive) continue;
                if (unit.Team == CombatTeam.Friendly) anyFriendly = true;
                else anyEnemy = true;
            }

            if (!anyEnemy)
            {
                BattleOver = true;
                BattleResult = "Victory — all enemies are down.";
            }
            else if (!anyFriendly)
            {
                BattleOver = true;
                BattleResult = "Defeat — the command groups were wiped out.";
            }
            else return;

            PendingReaction = null;
            _motion = null;
            FinishCascadeIfIdle();
            WriteLog(BattleResult);
        }

        private void Damage(ChainUnitState target, int amount, string reason)
        {
            if (target == null || !target.IsAlive) return;
            target.Hp = Math.Max(0, target.Hp - Math.Max(0, amount));
            WriteLog($"{reason} for {amount}. {target.Name}: {target.Hp}/{target.MaxHp} HP.");
            if (!target.IsAlive)
            {
                target.Airborne = false;
                WriteLog($"{target.Name} is down.");
            }
        }

        private void AddFriendly(string name, ChainRecruitKind kind, int commandGroup, GridPos position, int hp)
        {
            _units.Add(new ChainUnitState(_nextUnitId++, name, kind, CombatTeam.Friendly, commandGroup, position, hp));
        }

        private void AddEnemy(string name, ChainRecruitKind kind, GridPos position, int hp)
        {
            _units.Add(new ChainUnitState(_nextUnitId++, name, kind, CombatTeam.Enemy, 0, position, hp));
        }

        private void AddTree(GridPos position)
        {
            _trees.Add(new ChainTreeState(_nextTreeId++, position));
        }

        private bool Fail(string message)
        {
            LastMessage = message;
            return false;
        }

        private void WriteLog(string message)
        {
            LastMessage = message;
            _log.Add(message);
            if (_log.Count > 140) _log.RemoveAt(0);
        }

        private static GridPos CardinalDirection(GridPos from, GridPos toward)
        {
            int dx = toward.X - from.X;
            int dz = toward.Z - from.Z;
            if (Math.Abs(dx) >= Math.Abs(dz) && dx != 0) return new GridPos(Sign(dx), 0);
            if (dz != 0) return new GridPos(0, Sign(dz));
            return Zero;
        }

        private static int Sign(int value)
        {
            if (value > 0) return 1;
            if (value < 0) return -1;
            return 0;
        }

        private static bool IsZero(GridPos value)
        {
            return value.X == 0 && value.Z == 0;
        }

        private static string DirectionName(GridPos direction)
        {
            if (direction.X > 0) return "east";
            if (direction.X < 0) return "west";
            if (direction.Z > 0) return "north";
            return "south";
        }

        private static readonly GridPos Zero = new GridPos(0, 0);
    }
}
