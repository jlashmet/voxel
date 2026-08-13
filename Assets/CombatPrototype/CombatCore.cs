using System;
using System.Collections.Generic;

namespace MountingForce.CombatPrototype
{
    public enum CombatTeam
    {
        Friendly,
        Enemy
    }

    public enum RecruitKind
    {
        Stephen,
        Mira,
        Weldon,
        Madeline,
        Grom,
        Ogre,
        Goblin
    }

    public enum ReactionKind
    {
        None,
        Airborne,
        Collision,
        TreeImpact
    }

    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Z;

        public GridPos(int x, int z)
        {
            X = x;
            Z = z;
        }

        public static GridPos operator +(GridPos a, GridPos b)
        {
            return new GridPos(a.X + b.X, a.Z + b.Z);
        }

        public static GridPos operator -(GridPos a, GridPos b)
        {
            return new GridPos(a.X - b.X, a.Z - b.Z);
        }

        public static GridPos operator *(GridPos a, int scalar)
        {
            return new GridPos(a.X * scalar, a.Z * scalar);
        }

        public bool Equals(GridPos other)
        {
            return X == other.X && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPos other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Z;
            }
        }

        public override string ToString()
        {
            return $"({X}, {Z})";
        }
    }

    public sealed class UnitState
    {
        internal UnitState(int id, string name, RecruitKind kind, CombatTeam team, int commandGroup, GridPos position, int maxHp)
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
        public RecruitKind Kind { get; }
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

    public sealed class TreeState
    {
        internal TreeState(int id, GridPos position)
        {
            Id = id;
            Position = position;
            FallDirection = new GridPos(0, 0);
        }

        public int Id { get; }
        public GridPos Position { get; }
        public bool Standing { get; internal set; } = true;
        public GridPos FallDirection { get; internal set; }
    }

    public sealed class ReactionState
    {
        internal ReactionState(ReactionKind kind, int primaryUnitId, int secondaryUnitId, int treeId, GridPos position, string description)
        {
            Kind = kind;
            PrimaryUnitId = primaryUnitId;
            SecondaryUnitId = secondaryUnitId;
            TreeId = treeId;
            Position = position;
            Description = description;
        }

        public ReactionKind Kind { get; }
        public int PrimaryUnitId { get; }
        public int SecondaryUnitId { get; }
        public int TreeId { get; }
        public GridPos Position { get; }
        public string Description { get; }
    }

    internal sealed class MotionState
    {
        public int UnitId;
        public GridPos Direction;
        public int RemainingForce;
        public bool Airborne;
    }

    /// <summary>
    /// Tiny authoritative combat sandbox. All gameplay-relevant state is integer/grid based;
    /// Unity transforms and physics are deliberately kept out of this class.
    /// </summary>
    public sealed class CombatBoard
    {
        public const int Width = 14;
        public const int Depth = 10;

        private const int MoveRange = 3;
        private const int PortalPlacementRange = 6;
        private const int PortalPairMaxDistance = 9;
        private const int CrosswindRange = 6;
        private const int RepulseRange = 5;
        private const int TimberRange = 5;
        private const int MaximumMotionForce = 12;

        private readonly List<UnitState> _units = new List<UnitState>();
        private readonly List<TreeState> _trees = new List<TreeState>();
        private readonly HashSet<GridPos> _amplifiers = new HashSet<GridPos>();
        private readonly List<string> _log = new List<string>();
        private MotionState _motion;
        private int _nextUnitId;
        private int _nextTreeId;

        public CombatBoard()
        {
            Reset();
        }

        public IReadOnlyList<UnitState> Units => _units;
        public IReadOnlyList<TreeState> Trees => _trees;
        public IReadOnlyCollection<GridPos> Amplifiers => _amplifiers;
        public IReadOnlyList<string> Log => _log;
        public GridPos? PortalA { get; private set; }
        public GridPos? PortalB { get; private set; }
        public ReactionState PendingReaction { get; private set; }
        public int Round { get; private set; }
        public string LastMessage { get; private set; }
        public bool BattleOver { get; private set; }
        public string BattleResult { get; private set; }

        public void Reset()
        {
            _units.Clear();
            _trees.Clear();
            _amplifiers.Clear();
            _log.Clear();
            _motion = null;
            _nextUnitId = 1;
            _nextTreeId = 1;
            PortalA = null;
            PortalB = null;
            PendingReaction = null;
            Round = 1;
            LastMessage = string.Empty;
            BattleOver = false;
            BattleResult = string.Empty;

            AddFriendly("Stephen", RecruitKind.Stephen, 1, new GridPos(2, 4), 6);
            AddFriendly("Mira", RecruitKind.Mira, 1, new GridPos(1, 2), 5);
            AddFriendly("Weldon", RecruitKind.Weldon, 2, new GridPos(4, 2), 5);
            AddFriendly("Madeline", RecruitKind.Madeline, 2, new GridPos(6, 5), 6);
            AddFriendly("Grom", RecruitKind.Grom, 2, new GridPos(9, 7), 7);

            AddEnemy("Ogre", RecruitKind.Ogre, new GridPos(3, 4), 7);
            AddEnemy("Goblin A", RecruitKind.Goblin, new GridPos(8, 4), 4);
            AddEnemy("Goblin B", RecruitKind.Goblin, new GridPos(10, 6), 4);

            AddTree(new GridPos(11, 4));
            AddTree(new GridPos(11, 8));

            WriteLog("Round 1. No combo hints: inspect the recruits, positions, and world pieces.");
        }

        public UnitState GetUnit(int id)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].Id == id)
                {
                    return _units[i];
                }
            }

            return null;
        }

        public TreeState GetTree(int id)
        {
            for (int i = 0; i < _trees.Count; i++)
            {
                if (_trees[i].Id == id)
                {
                    return _trees[i];
                }
            }

            return null;
        }

        public UnitState FindUnitAt(GridPos position)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                UnitState unit = _units[i];
                if (unit.IsAlive && unit.Position.Equals(position))
                {
                    return unit;
                }
            }

            return null;
        }

        public TreeState FindStandingTreeAt(GridPos position)
        {
            for (int i = 0; i < _trees.Count; i++)
            {
                TreeState tree = _trees[i];
                if (tree.Standing && tree.Position.Equals(position))
                {
                    return tree;
                }
            }

            return null;
        }

        public bool TryMove(int unitId, GridPos destination)
        {
            if (!TryGetNormalActor(unitId, out UnitState unit))
            {
                return false;
            }

            if (!IsInBounds(destination))
            {
                return Fail("That cell is outside the battle board.");
            }

            if (Distance(unit.Position, destination) > MoveRange)
            {
                return Fail($"Move reaches at most {MoveRange} cells.");
            }

            if (!IsCellOpen(destination))
            {
                return Fail("That cell is occupied.");
            }

            unit.Position = destination;
            unit.ActionSpent = true;
            WriteLog($"{unit.Name} moved to {destination}.");
            return true;
        }

        public bool TryBasicHit(int unitId, int targetId)
        {
            if (!TryGetNormalActor(unitId, out UnitState unit))
            {
                return false;
            }

            UnitState target = GetUnit(targetId);
            if (target == null || !target.IsAlive || target.Team == unit.Team)
            {
                return Fail("Strike needs a living enemy target.");
            }

            if (Distance(unit.Position, target.Position) != 1)
            {
                return Fail("Strike only reaches an adjacent enemy.");
            }

            unit.ActionSpent = true;
            Damage(target, 1, $"{unit.Name} struck {target.Name}");
            CheckBattleEnd();
            return true;
        }

        public bool TryUppercut(int stephenId, int targetId)
        {
            if (!TryGetNormalActor(stephenId, out UnitState stephen))
            {
                return false;
            }

            if (stephen.Kind != RecruitKind.Stephen)
            {
                return Fail("Only Stephen has Uppercut in this prototype.");
            }

            UnitState target = GetUnit(targetId);
            if (target == null || !target.IsAlive || target.Team == stephen.Team)
            {
                return Fail("Uppercut needs a living enemy target.");
            }

            if (Distance(stephen.Position, target.Position) != 1)
            {
                return Fail("Uppercut needs an adjacent enemy.");
            }

            GridPos direction = CardinalDirection(stephen.Position, target.Position);
            if (IsZero(direction))
            {
                return Fail("Uppercut could not determine a launch direction.");
            }

            stephen.ActionSpent = true;
            target.Airborne = true;
            _motion = new MotionState
            {
                UnitId = target.Id,
                Direction = direction,
                RemainingForce = 5,
                Airborne = true
            };

            PendingReaction = new ReactionState(
                ReactionKind.Airborne,
                target.Id,
                0,
                0,
                target.Position,
                $"{target.Name} was launched away from {stephen.Name}.");
            WriteLog($"{stephen.Name} uppercut {target.Name}. The simulation paused while {target.Name} is airborne.");
            return true;
        }

        public bool TryPlacePortalPair(int miraId, GridPos entrance, GridPos exit)
        {
            if (!TryGetNormalActor(miraId, out UnitState mira))
            {
                return false;
            }

            if (mira.Kind != RecruitKind.Mira)
            {
                return Fail("Only Mira can place portals in this prototype.");
            }

            if (!IsInBounds(entrance) || !IsInBounds(exit))
            {
                return Fail("Both portals must be on the battle board.");
            }

            if (entrance.Equals(exit))
            {
                return Fail("Portal entrance and exit must be different cells.");
            }

            if (Distance(mira.Position, entrance) > PortalPlacementRange || Distance(mira.Position, exit) > PortalPlacementRange)
            {
                return Fail($"Mira can place each portal at most {PortalPlacementRange} cells away.");
            }

            if (Distance(entrance, exit) > PortalPairMaxDistance)
            {
                return Fail($"The portal pair can span at most {PortalPairMaxDistance} cells.");
            }

            if (!IsCellOpenForConstruct(entrance) || !IsCellOpenForConstruct(exit))
            {
                return Fail("A portal cannot be placed on a creature, tree, or other construct.");
            }

            PortalA = entrance;
            PortalB = exit;
            mira.ActionSpent = true;
            WriteLog($"{mira.Name} linked portals at {entrance} and {exit}. Moving bodies keep their direction and force through them.");
            return true;
        }

        public bool TryPlaceAmplifier(int miraId, GridPos position)
        {
            if (!TryGetNormalActor(miraId, out UnitState mira))
            {
                return false;
            }

            if (mira.Kind != RecruitKind.Mira)
            {
                return Fail("Only Mira can place the force multiplier in this prototype.");
            }

            if (!IsInBounds(position) || Distance(mira.Position, position) > PortalPlacementRange)
            {
                return Fail($"The multiplier must be within {PortalPlacementRange} cells of Mira.");
            }

            if (!IsCellOpenForConstruct(position))
            {
                return Fail("The multiplier needs an empty cell.");
            }

            _amplifiers.Add(position);
            mira.ActionSpent = true;
            WriteLog($"{mira.Name} placed a force multiplier at {position}. Anything driven through it gains momentum.");
            return true;
        }

        public bool TryCrosswind(int weldonId, GridPos aim)
        {
            UnitState weldon = GetUnit(weldonId);
            if (!TryGetReactionActor(weldon, RecruitKind.Weldon))
            {
                return false;
            }

            if (PendingReaction == null || PendingReaction.Kind != ReactionKind.Airborne || _motion == null)
            {
                return Fail("Crosswind needs an airborne creature currently in flight.");
            }

            UnitState target = GetUnit(PendingReaction.PrimaryUnitId);
            if (target == null || !target.IsAlive || Distance(weldon.Position, target.Position) > CrosswindRange)
            {
                return Fail($"The airborne creature must be within {CrosswindRange} cells of Weldon.");
            }

            GridPos direction = CardinalDirection(target.Position, aim);
            if (IsZero(direction))
            {
                return Fail("Aim Crosswind away from the airborne creature to choose a direction.");
            }

            weldon.ReactionSpent = true;
            _motion.Direction = direction;
            PendingReaction = null;
            WriteLog($"{weldon.Name} redirected {target.Name} toward {DirectionName(direction)}.");
            ResolveMotion();
            CheckBattleEnd();
            return true;
        }

        public bool TryRepulse(int madelineId, int targetId, GridPos aim)
        {
            UnitState madeline = GetUnit(madelineId);
            if (!TryGetReactionActor(madeline, RecruitKind.Madeline))
            {
                return false;
            }

            ReactionState reaction = PendingReaction;
            if (reaction == null || reaction.Kind != ReactionKind.Collision)
            {
                return Fail("Repulse needs a collision that just happened.");
            }

            if (Distance(madeline.Position, reaction.Position) > RepulseRange)
            {
                return Fail($"The collision must be within {RepulseRange} cells of Madeline.");
            }

            if (targetId != reaction.PrimaryUnitId && targetId != reaction.SecondaryUnitId)
            {
                return Fail("Choose one of the two creatures involved in the collision.");
            }

            UnitState target = GetUnit(targetId);
            if (target == null || !target.IsAlive)
            {
                return Fail("That collision participant is no longer able to be repulsed.");
            }

            GridPos direction = CardinalDirection(target.Position, aim);
            if (IsZero(direction))
            {
                return Fail("Aim away from the collision to choose the blast direction.");
            }

            madeline.ReactionSpent = true;
            PendingReaction = null;
            _motion = new MotionState
            {
                UnitId = target.Id,
                Direction = direction,
                RemainingForce = 4,
                Airborne = false
            };
            WriteLog($"{madeline.Name} blasted {target.Name} toward {DirectionName(direction)} after the collision.");
            ResolveMotion();
            CheckBattleEnd();
            return true;
        }

        public bool TryTimber(int gromId, GridPos aim)
        {
            UnitState grom = GetUnit(gromId);
            if (!TryGetReactionActor(grom, RecruitKind.Grom))
            {
                return false;
            }

            ReactionState reaction = PendingReaction;
            if (reaction == null || reaction.Kind != ReactionKind.TreeImpact)
            {
                return Fail("Timber needs a tree that was just struck hard enough to react to.");
            }

            TreeState tree = GetTree(reaction.TreeId);
            if (tree == null || !tree.Standing)
            {
                return Fail("That tree is no longer standing.");
            }

            if (Distance(grom.Position, tree.Position) > TimberRange)
            {
                return Fail($"Grom must be within {TimberRange} cells of the struck tree.");
            }

            GridPos direction = CardinalDirection(tree.Position, aim);
            if (IsZero(direction))
            {
                return Fail("Aim away from the tree to choose its fall direction.");
            }

            grom.ReactionSpent = true;
            PendingReaction = null;
            tree.Standing = false;
            tree.FallDirection = direction;
            WriteLog($"{grom.Name} finished the damaged tree and sent it falling {DirectionName(direction)}.");

            for (int step = 1; step <= 4; step++)
            {
                GridPos cell = tree.Position + direction * step;
                if (!IsInBounds(cell))
                {
                    break;
                }

                UnitState hit = FindUnitAt(cell);
                if (hit != null)
                {
                    Damage(hit, 5, $"The falling tree crushed {hit.Name}");
                }
            }

            CheckBattleEnd();
            return true;
        }

        public bool PassReaction()
        {
            if (PendingReaction == null)
            {
                return Fail("There is no reaction window to pass.");
            }

            ReactionKind kind = PendingReaction.Kind;
            string description = PendingReaction.Description;
            PendingReaction = null;
            WriteLog($"No recruit changed the event: {description}");

            if (kind == ReactionKind.Airborne)
            {
                ResolveMotion();
            }
            else
            {
                _motion = null;
            }

            CheckBattleEnd();
            return true;
        }

        public bool EndRound()
        {
            if (BattleOver)
            {
                return Fail("The battle is already over. Reset to play again.");
            }

            if (PendingReaction != null)
            {
                return Fail("Resolve or pass the current reaction before ending the round.");
            }

            RunEnemyTurn();
            CheckBattleEnd();
            if (BattleOver)
            {
                return true;
            }

            Round++;
            for (int i = 0; i < _units.Count; i++)
            {
                UnitState unit = _units[i];
                if (unit.Team == CombatTeam.Friendly && unit.IsAlive)
                {
                    unit.ActionSpent = false;
                    unit.ReactionSpent = false;
                }
            }

            WriteLog($"Round {Round} begins. Friendly actions and reactions refreshed.");
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

        private void ResolveMotion()
        {
            while (_motion != null && PendingReaction == null)
            {
                UnitState mover = GetUnit(_motion.UnitId);
                if (mover == null || !mover.IsAlive || _motion.RemainingForce <= 0)
                {
                    StopMotion(mover);
                    return;
                }

                GridPos next = mover.Position + _motion.Direction;
                if (!IsInBounds(next))
                {
                    Damage(mover, 1, $"{mover.Name} slammed into the edge of the arena");
                    StopMotion(mover);
                    return;
                }

                TreeState tree = FindStandingTreeAt(next);
                if (tree != null)
                {
                    Damage(mover, 1, $"{mover.Name} struck a tree");
                    mover.Airborne = false;
                    _motion = null;
                    PendingReaction = new ReactionState(
                        ReactionKind.TreeImpact,
                        mover.Id,
                        0,
                        tree.Id,
                        tree.Position,
                        $"{mover.Name} slammed into the tree at {tree.Position}.");
                    WriteLog($"Tree impact at {tree.Position}. The simulation paused on the impact.");
                    return;
                }

                UnitState occupant = FindUnitAt(next);
                if (occupant != null && occupant.Id != mover.Id)
                {
                    Damage(mover, 1, $"{mover.Name} collided with {occupant.Name}");
                    Damage(occupant, 1, $"{occupant.Name} took the collision");
                    mover.Airborne = false;
                    _motion = null;
                    PendingReaction = new ReactionState(
                        ReactionKind.Collision,
                        mover.Id,
                        occupant.Id,
                        0,
                        next,
                        $"{mover.Name} collided with {occupant.Name}.");
                    WriteLog($"{mover.Name} collided with {occupant.Name}. The simulation paused on the collision.");
                    return;
                }

                mover.Position = next;
                _motion.RemainingForce--;

                if (TryTeleport(mover))
                {
                    // Portal traversal preserves the current direction and remaining integer force.
                }

                if (_amplifiers.Contains(mover.Position))
                {
                    int before = _motion.RemainingForce;
                    _motion.RemainingForce = Math.Min(MaximumMotionForce, Math.Max(before + 1, before * 2));
                    WriteLog($"{mover.Name} crossed a force multiplier: remaining momentum {before} -> {_motion.RemainingForce}.");
                }

                if (_motion.RemainingForce <= 0)
                {
                    StopMotion(mover);
                }
            }
        }

        private bool TryTeleport(UnitState mover)
        {
            if (!PortalA.HasValue || !PortalB.HasValue)
            {
                return false;
            }

            GridPos destination;
            if (mover.Position.Equals(PortalA.Value))
            {
                destination = PortalB.Value;
            }
            else if (mover.Position.Equals(PortalB.Value))
            {
                destination = PortalA.Value;
            }
            else
            {
                return false;
            }

            UnitState occupant = FindUnitAt(destination);
            TreeState tree = FindStandingTreeAt(destination);
            if ((occupant != null && occupant.Id != mover.Id) || tree != null)
            {
                WriteLog($"{mover.Name} entered a portal, but the paired exit was obstructed.");
                return false;
            }

            GridPos source = mover.Position;
            mover.Position = destination;
            WriteLog($"{mover.Name} teleported from {source} to {destination} without losing direction or momentum.");
            return true;
        }

        private void StopMotion(UnitState mover)
        {
            if (mover != null)
            {
                mover.Airborne = false;
            }

            _motion = null;
        }

        private void RunEnemyTurn()
        {
            List<UnitState> enemies = new List<UnitState>();
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].Team == CombatTeam.Enemy && _units[i].IsAlive)
                {
                    enemies.Add(_units[i]);
                }
            }

            enemies.Sort((a, b) => a.Id.CompareTo(b.Id));
            for (int i = 0; i < enemies.Count; i++)
            {
                UnitState enemy = enemies[i];
                UnitState target = FindNearestFriendly(enemy.Position);
                if (target == null)
                {
                    return;
                }

                if (Distance(enemy.Position, target.Position) == 1)
                {
                    Damage(target, 1, $"{enemy.Name} attacked {target.Name}");
                    continue;
                }

                GridPos xStep = new GridPos(Sign(target.Position.X - enemy.Position.X), 0);
                GridPos zStep = new GridPos(0, Sign(target.Position.Z - enemy.Position.Z));
                GridPos first = xStep;
                GridPos second = zStep;
                if ((Round + enemy.Id) % 2 != 0)
                {
                    first = zStep;
                    second = xStep;
                }

                if (TryEnemyStep(enemy, first) || TryEnemyStep(enemy, second))
                {
                    WriteLog($"{enemy.Name} advanced toward {target.Name}.");
                }
            }
        }

        private bool TryEnemyStep(UnitState enemy, GridPos step)
        {
            if (IsZero(step))
            {
                return false;
            }

            GridPos destination = enemy.Position + step;
            if (!IsInBounds(destination) || !IsCellOpen(destination))
            {
                return false;
            }

            enemy.Position = destination;
            return true;
        }

        private UnitState FindNearestFriendly(GridPos from)
        {
            UnitState best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < _units.Count; i++)
            {
                UnitState candidate = _units[i];
                if (candidate.Team != CombatTeam.Friendly || !candidate.IsAlive)
                {
                    continue;
                }

                int distance = Distance(from, candidate.Position);
                if (distance < bestDistance || (distance == bestDistance && (best == null || candidate.Id < best.Id)))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private bool TryGetNormalActor(int unitId, out UnitState unit)
        {
            unit = GetUnit(unitId);
            if (BattleOver)
            {
                return Fail("The battle is over. Reset to play again.");
            }

            if (PendingReaction != null)
            {
                return Fail("A physical event is unresolved. React to it or pass before taking a normal action.");
            }

            if (unit == null || !unit.IsAlive || unit.Team != CombatTeam.Friendly)
            {
                return Fail("Choose a living friendly recruit.");
            }

            if (unit.ActionSpent)
            {
                return Fail($"{unit.Name} already used a normal action this round.");
            }

            return true;
        }

        private bool TryGetReactionActor(UnitState unit, RecruitKind requiredKind)
        {
            if (BattleOver)
            {
                return Fail("The battle is over. Reset to play again.");
            }

            if (unit == null || !unit.IsAlive || unit.Team != CombatTeam.Friendly || unit.Kind != requiredKind)
            {
                return Fail("That recruit cannot perform this reaction.");
            }

            if (unit.ReactionSpent)
            {
                return Fail($"{unit.Name} already used a reaction this round.");
            }

            return true;
        }

        private bool IsCellOpen(GridPos position)
        {
            return FindUnitAt(position) == null && FindStandingTreeAt(position) == null;
        }

        private bool IsCellOpenForConstruct(GridPos position)
        {
            if (!IsCellOpen(position) || _amplifiers.Contains(position))
            {
                return false;
            }

            if (PortalA.HasValue && PortalA.Value.Equals(position))
            {
                return false;
            }

            if (PortalB.HasValue && PortalB.Value.Equals(position))
            {
                return false;
            }

            return true;
        }

        private void CheckBattleEnd()
        {
            bool anyFriendly = false;
            bool anyEnemy = false;
            for (int i = 0; i < _units.Count; i++)
            {
                UnitState unit = _units[i];
                if (!unit.IsAlive)
                {
                    continue;
                }

                if (unit.Team == CombatTeam.Friendly)
                {
                    anyFriendly = true;
                }
                else
                {
                    anyEnemy = true;
                }
            }

            if (!anyEnemy)
            {
                BattleOver = true;
                BattleResult = "Victory — all enemies are down.";
                PendingReaction = null;
                _motion = null;
                WriteLog(BattleResult);
            }
            else if (!anyFriendly)
            {
                BattleOver = true;
                BattleResult = "Defeat — the command groups were wiped out.";
                PendingReaction = null;
                _motion = null;
                WriteLog(BattleResult);
            }
        }

        private void Damage(UnitState target, int amount, string reason)
        {
            if (target == null || !target.IsAlive)
            {
                return;
            }

            target.Hp = Math.Max(0, target.Hp - amount);
            WriteLog($"{reason} for {amount} damage. {target.Name}: {target.Hp}/{target.MaxHp} HP.");
            if (!target.IsAlive)
            {
                target.Airborne = false;
                WriteLog($"{target.Name} is down.");
            }
        }

        private void AddFriendly(string name, RecruitKind kind, int commandGroup, GridPos position, int hp)
        {
            _units.Add(new UnitState(_nextUnitId++, name, kind, CombatTeam.Friendly, commandGroup, position, hp));
        }

        private void AddEnemy(string name, RecruitKind kind, GridPos position, int hp)
        {
            _units.Add(new UnitState(_nextUnitId++, name, kind, CombatTeam.Enemy, 0, position, hp));
        }

        private void AddTree(GridPos position)
        {
            _trees.Add(new TreeState(_nextTreeId++, position));
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
            if (_log.Count > 80)
            {
                _log.RemoveAt(0);
            }
        }

        private static GridPos CardinalDirection(GridPos from, GridPos toward)
        {
            int dx = toward.X - from.X;
            int dz = toward.Z - from.Z;
            if (Math.Abs(dx) >= Math.Abs(dz) && dx != 0)
            {
                return new GridPos(Sign(dx), 0);
            }

            if (dz != 0)
            {
                return new GridPos(0, Sign(dz));
            }

            return new GridPos(0, 0);
        }

        private static int Sign(int value)
        {
            if (value > 0)
            {
                return 1;
            }

            if (value < 0)
            {
                return -1;
            }

            return 0;
        }

        private static bool IsZero(GridPos value)
        {
            return value.X == 0 && value.Z == 0;
        }

        private static string DirectionName(GridPos direction)
        {
            if (direction.X > 0)
            {
                return "east";
            }

            if (direction.X < 0)
            {
                return "west";
            }

            if (direction.Z > 0)
            {
                return "north";
            }

            return "south";
        }
    }
}
