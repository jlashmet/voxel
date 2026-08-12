using System;
using System.Collections.Generic;
using System.Reflection;

namespace MountingForce.CombatPrototype
{
    public enum ChainEnemyIntentKind
    {
        Wait,
        Advance,
        Attack,
        Shove,
        Charge
    }

    public sealed class ChainEnemyIntent
    {
        internal ChainEnemyIntent(int enemyId, ChainEnemyIntentKind kind, int targetUnitId, GridPos direction, GridPos destination, int score, string description)
        {
            EnemyId = enemyId;
            Kind = kind;
            TargetUnitId = targetUnitId;
            Direction = direction;
            Destination = destination;
            Score = score;
            Description = description;
        }

        public int EnemyId { get; }
        public ChainEnemyIntentKind Kind { get; }
        public int TargetUnitId { get; }
        public GridPos Direction { get; }
        public GridPos Destination { get; }
        public int Score { get; }
        public string Description { get; }
    }

    /// <summary>
    /// Small deterministic tactical opponent for the cascade lab.
    ///
    /// The AI plans once at the beginning of a player round and commits to those intentions. It does not secretly
    /// re-target after the players move. That makes enemy decisions readable physical promises the party can evade,
    /// disrupt, or deliberately exploit. Enemy shoves and ogre charges reuse the board's normal motion/collision/
    /// environment rules, so enemy actions can create the same reaction opportunities as player-created impacts.
    ///
    /// This class currently bridges a few private board operations through reflection so the experiment can evolve
    /// without widening the production-facing board API. If the mechanic survives playtesting, those operations should
    /// move behind explicit authoritative enemy-command methods on ChainCombatBoard.
    /// </summary>
    public sealed class ChainEnemyTacticalAI
    {
        private static readonly GridPos[] Cardinal =
        {
            new GridPos(1, 0),
            new GridPos(-1, 0),
            new GridPos(0, 1),
            new GridPos(0, -1)
        };

        private static readonly FieldInfo MotionField = typeof(ChainCombatBoard).GetField("_motion", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ActiveRecruitField = typeof(ChainCombatBoard).GetField("_activeRecruitByGroup", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ResolveMotionMethod = typeof(ChainCombatBoard).GetMethod("ResolveMotion", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CheckBattleEndMethod = typeof(ChainCombatBoard).GetMethod("CheckBattleEnd", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo WriteLogMethod = typeof(ChainCombatBoard).GetMethod("WriteLog", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo RoundProperty = typeof(ChainCombatBoard).GetProperty("Round", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private readonly ChainCombatBoard _board;
        private readonly List<ChainEnemyIntent> _intents = new List<ChainEnemyIntent>();
        private int _plannedRound;
        private int _nextIntentIndex;
        private bool _enemyPhaseActive;

        public ChainEnemyTacticalAI(ChainCombatBoard board)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            PlanRound();
        }

        public IReadOnlyList<ChainEnemyIntent> Intents => _intents;
        public bool EnemyPhaseActive => _enemyPhaseActive;
        public int PlannedRound => _plannedRound;

        public void Synchronize()
        {
            if (_enemyPhaseActive) return;
            if (_plannedRound != _board.Round) PlanRound();
        }

        public void PlanRound()
        {
            _intents.Clear();
            _nextIntentIndex = 0;
            _enemyPhaseActive = false;
            _plannedRound = _board.Round;

            var enemies = new List<ChainUnitState>();
            for (int i = 0; i < _board.Units.Count; i++)
            {
                ChainUnitState unit = _board.Units[i];
                if (unit.Team == CombatTeam.Enemy && unit.IsAlive) enemies.Add(unit);
            }
            enemies.Sort((a, b) => a.Id.CompareTo(b.Id));

            for (int i = 0; i < enemies.Count; i++)
                _intents.Add(ChooseIntent(enemies[i]));

            for (int i = 0; i < _intents.Count; i++)
            {
                ChainEnemyIntent intent = _intents[i];
                ChainUnitState enemy = _board.GetUnit(intent.EnemyId);
                if (enemy != null) Log($"AI intent: {enemy.Name} — {intent.Description}");
            }
        }

        public bool BeginOrContinueEnemyPhase()
        {
            Synchronize();
            if (_board.BattleOver) return false;
            if (_board.PendingReaction != null) return false;

            _enemyPhaseActive = true;
            while (_nextIntentIndex < _intents.Count)
            {
                ChainEnemyIntent intent = _intents[_nextIntentIndex++];
                Execute(intent);
                CheckBattleEnd();

                if (_board.BattleOver)
                {
                    _enemyPhaseActive = false;
                    return true;
                }

                if (_board.PendingReaction != null)
                {
                    Log("Enemy phase paused on a physical event. Players may reserve/react before the remaining enemy intentions continue.");
                    return true;
                }
            }

            FinishEnemyPhaseAndAdvanceRound();
            return true;
        }

        private ChainEnemyIntent ChooseIntent(ChainUnitState enemy)
        {
            ChainEnemyIntent best = new ChainEnemyIntent(enemy.Id, ChainEnemyIntentKind.Wait, 0, Zero, enemy.Position, 0, "waits and watches");

            for (int i = 0; i < _board.Units.Count; i++)
            {
                ChainUnitState friendly = _board.Units[i];
                if (friendly.Team != CombatTeam.Friendly || !friendly.IsAlive) continue;

                int distance = ChainCombatBoard.Distance(enemy.Position, friendly.Position);
                if (distance != 1) continue;

                GridPos direction = Direction(enemy.Position, friendly.Position);
                int vulnerability = friendly.MaxHp - friendly.Hp;

                if (enemy.Kind == ChainRecruitKind.Goblin)
                {
                    int physicalPayoff = ScoreShovePayoff(friendly, direction, 4);
                    int shoveScore = physicalPayoff <= 0 ? 0 : 108 + physicalPayoff + vulnerability * 2;
                    if (shoveScore > best.Score)
                    {
                        best = new ChainEnemyIntent(enemy.Id, ChainEnemyIntentKind.Shove, friendly.Id,
                            direction, friendly.Position + direction, shoveScore,
                            $"commits to shove {friendly.Name} {DirectionName(direction)} with force 4");
                    }
                }

                int attackScore = 120 + vulnerability * 4 + (enemy.Kind == ChainRecruitKind.Ogre ? 10 : 0);
                if (attackScore > best.Score)
                {
                    best = new ChainEnemyIntent(enemy.Id, ChainEnemyIntentKind.Attack, friendly.Id,
                        direction, friendly.Position, attackScore,
                        $"commits to attack {friendly.Name} if {friendly.Name} is still adjacent");
                }
            }

            if (enemy.Kind == ChainRecruitKind.Ogre)
            {
                ChainEnemyIntent charge = FindBestOgreCharge(enemy);
                if (charge.Score > best.Score) best = charge;
            }

            ChainEnemyIntent advance = FindBestAdvance(enemy);
            if (advance.Score > best.Score) best = advance;
            return best;
        }

        private ChainEnemyIntent FindBestOgreCharge(ChainUnitState ogre)
        {
            ChainEnemyIntent best = new ChainEnemyIntent(ogre.Id, ChainEnemyIntentKind.Wait, 0, Zero, ogre.Position, 0, "holds position");

            for (int d = 0; d < Cardinal.Length; d++)
            {
                GridPos direction = Cardinal[d];
                for (int step = 1; step <= 6; step++)
                {
                    GridPos cell = ogre.Position + direction * step;
                    if (!_board.IsInBounds(cell)) break;

                    ChainTreeState tree = _board.FindStandingTreeAt(cell);
                    ChainUnitState occupant = _board.FindUnitAt(cell);
                    if (tree == null && occupant == null) continue;

                    if (occupant != null && occupant.Id != ogre.Id)
                    {
                        if (occupant.Team == CombatTeam.Friendly)
                        {
                            int vulnerability = occupant.MaxHp - occupant.Hp;
                            int score = 85 + vulnerability * 3 + (7 - step) * 3;
                            if (score > best.Score)
                            {
                                best = new ChainEnemyIntent(ogre.Id, ChainEnemyIntentKind.Charge, occupant.Id, direction, cell, score,
                                    $"winds up a force-6 charge {DirectionName(direction)} toward {occupant.Name}");
                            }
                        }
                        break;
                    }

                    if (tree != null)
                    {
                        ChainUnitState beyond = FindFriendlyBeyond(cell, direction, 2);
                        if (beyond != null)
                        {
                            int score = 72;
                            if (score > best.Score)
                            {
                                best = new ChainEnemyIntent(ogre.Id, ChainEnemyIntentKind.Charge, beyond.Id, direction, cell, score,
                                    $"winds up a force-6 charge {DirectionName(direction)} through tree #{tree.Id}");
                            }
                        }
                        break;
                    }
                }
            }

            return best;
        }

        private ChainEnemyIntent FindBestAdvance(ChainUnitState enemy)
        {
            ChainEnemyIntent best = new ChainEnemyIntent(enemy.Id, ChainEnemyIntentKind.Wait, 0, Zero, enemy.Position, 0, "holds position");

            for (int d = 0; d < Cardinal.Length; d++)
            {
                GridPos direction = Cardinal[d];
                GridPos destination = enemy.Position + direction;
                if (!_board.IsInBounds(destination)) continue;
                if (_board.FindUnitAt(destination) != null || _board.FindStandingTreeAt(destination) != null) continue;

                ChainUnitState target = BestPressureTarget(destination);
                if (target == null) continue;

                int distance = ChainCombatBoard.Distance(destination, target.Position);
                int vulnerability = target.MaxHp - target.Hp;
                int score = 45 - distance * 4 + vulnerability * 2;

                if (enemy.Kind == ChainRecruitKind.Ogre)
                {
                    GridPos line = Direction(destination, target.Position);
                    if (IsAligned(destination, target.Position) && !IsZero(line)) score += 14;
                }
                else
                {
                    score += AdjacentEnemyCount(destination) == 0 ? 5 : 0;
                }

                if (score > best.Score)
                {
                    best = new ChainEnemyIntent(enemy.Id, ChainEnemyIntentKind.Advance, target.Id, direction, destination, score,
                        $"commits to advance {DirectionName(direction)} toward {target.Name}");
                }
            }

            return best;
        }

        private int ScoreShovePayoff(ChainUnitState target, GridPos direction, int force)
        {
            int score = 0;
            for (int step = 1; step <= force; step++)
            {
                GridPos cell = target.Position + direction * step;
                if (!_board.IsInBounds(cell))
                {
                    score += Math.Max(6, 14 - step * 2);
                    break;
                }

                ChainTreeState tree = _board.FindStandingTreeAt(cell);
                if (tree != null)
                {
                    score += 34 + Math.Max(0, 5 - step) * 3;
                    break;
                }

                ChainUnitState occupant = _board.FindUnitAt(cell);
                if (occupant != null && occupant.Id != target.Id)
                {
                    score += occupant.Team == CombatTeam.Friendly ? 32 : 12;
                    break;
                }

                if (HasAmplifier(cell)) score += 14;
                if (( _board.PortalA.HasValue && _board.PortalA.Value.Equals(cell)) ||
                    ( _board.PortalB.HasValue && _board.PortalB.Value.Equals(cell))) score += 8;
            }
            return score;
        }

        private void Execute(ChainEnemyIntent intent)
        {
            ChainUnitState enemy = _board.GetUnit(intent.EnemyId);
            if (enemy == null || !enemy.IsAlive)
            {
                Log("A committed enemy intent fizzled because its actor was removed before the enemy phase.");
                return;
            }

            switch (intent.Kind)
            {
                case ChainEnemyIntentKind.Attack:
                    ExecuteAttack(enemy, intent);
                    break;
                case ChainEnemyIntentKind.Shove:
                    ExecuteShove(enemy, intent);
                    break;
                case ChainEnemyIntentKind.Advance:
                    ExecuteAdvance(enemy, intent);
                    break;
                case ChainEnemyIntentKind.Charge:
                    ExecuteCharge(enemy, intent);
                    break;
                default:
                    Log($"{enemy.Name} waits.");
                    break;
            }
        }

        private void ExecuteAttack(ChainUnitState enemy, ChainEnemyIntent intent)
        {
            ChainUnitState target = _board.GetUnit(intent.TargetUnitId);
            if (target == null || !target.IsAlive || target.Team != CombatTeam.Friendly || ChainCombatBoard.Distance(enemy.Position, target.Position) != 1)
            {
                Log($"{enemy.Name}'s committed attack misses because its target is no longer adjacent.");
                return;
            }

            int damage = enemy.Kind == ChainRecruitKind.Ogre ? 2 : 1;
            target.Hp = Math.Max(0, target.Hp - damage);
            target.Airborne = false;
            Log($"{enemy.Name} executes its committed attack on {target.Name} for {damage}. {target.Name}: {target.Hp}/{target.MaxHp} HP.");
        }

        private void ExecuteShove(ChainUnitState enemy, ChainEnemyIntent intent)
        {
            ChainUnitState target = _board.GetUnit(intent.TargetUnitId);
            if (target == null || !target.IsAlive || target.Team != CombatTeam.Friendly ||
                ChainCombatBoard.Distance(enemy.Position, target.Position) != 1)
            {
                Log($"{enemy.Name}'s committed shove misses because its target is no longer adjacent.");
                return;
            }

            GridPos currentDirection = Direction(enemy.Position, target.Position);
            if (!currentDirection.Equals(intent.Direction))
            {
                Log($"{enemy.Name}'s committed shove loses its angle after the board changes.");
                return;
            }

            ExecutePhysicalMotion(target, intent.Direction, 4,
                $"{enemy.Name} shoves {target.Name} {DirectionName(intent.Direction)} with force 4");
        }

        private void ExecuteAdvance(ChainUnitState enemy, ChainEnemyIntent intent)
        {
            GridPos destination = enemy.Position + intent.Direction;
            if (!_board.IsInBounds(destination) || _board.FindUnitAt(destination) != null || _board.FindStandingTreeAt(destination) != null)
            {
                Log($"{enemy.Name}'s committed advance is blocked. The AI does not secretly re-route after the players alter the board.");
                return;
            }

            enemy.Position = destination;
            Log($"{enemy.Name} executes its committed advance to {destination}.");
        }

        private void ExecuteCharge(ChainUnitState enemy, ChainEnemyIntent intent)
        {
            if (IsZero(intent.Direction))
            {
                Log($"{enemy.Name}'s charge had no valid direction and fizzled.");
                return;
            }

            ExecutePhysicalMotion(enemy, intent.Direction, 6,
                $"{enemy.Name} executes its committed force-6 charge {DirectionName(intent.Direction)}. Anything now in that lane can be hit");
        }

        private void ExecutePhysicalMotion(ChainUnitState mover, GridPos direction, int force, string message)
        {
            if (MotionField == null || ResolveMotionMethod == null)
            {
                Log("Enemy physical-motion bridge is unavailable; the intent fizzled.");
                return;
            }

            var motion = new ChainMotionState
            {
                UnitId = mover.Id,
                Direction = direction,
                RemainingForce = force,
                Airborne = false
            };
            MotionField.SetValue(_board, motion);
            Log(message + ".");
            ResolveMotionMethod.Invoke(_board, null);
        }

        private void FinishEnemyPhaseAndAdvanceRound()
        {
            _enemyPhaseActive = false;
            CheckBattleEnd();
            if (_board.BattleOver) return;

            if (RoundProperty != null)
                RoundProperty.SetValue(_board, _board.Round + 1);

            if (ActiveRecruitField?.GetValue(_board) is Dictionary<int, int> active)
                active.Clear();

            for (int i = 0; i < _board.Units.Count; i++)
            {
                ChainUnitState unit = _board.Units[i];
                if (unit.Team != CombatTeam.Friendly || !unit.IsAlive) continue;
                unit.MoveSpent = false;
                unit.ActionSpent = false;
                unit.ReactionSpent = false;
            }

            Log($"Round {_board.Round} begins. Enemy AI is choosing new committed intentions from the new board state.");
            PlanRound();
        }

        private ChainUnitState BestPressureTarget(GridPos from)
        {
            ChainUnitState best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < _board.Units.Count; i++)
            {
                ChainUnitState candidate = _board.Units[i];
                if (candidate.Team != CombatTeam.Friendly || !candidate.IsAlive) continue;
                int distance = ChainCombatBoard.Distance(from, candidate.Position);
                int missingHp = candidate.MaxHp - candidate.Hp;
                int score = -distance * 5 + missingHp * 3;
                if (score > bestScore || (score == bestScore && (best == null || candidate.Id < best.Id)))
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        private ChainUnitState FindFriendlyBeyond(GridPos start, GridPos direction, int maxDistance)
        {
            for (int step = 1; step <= maxDistance; step++)
            {
                GridPos cell = start + direction * step;
                if (!_board.IsInBounds(cell)) return null;
                ChainUnitState unit = _board.FindUnitAt(cell);
                if (unit == null) continue;
                return unit.Team == CombatTeam.Friendly && unit.IsAlive ? unit : null;
            }
            return null;
        }

        private bool HasAmplifier(GridPos position)
        {
            foreach (GridPos amplifier in _board.Amplifiers)
                if (amplifier.Equals(position)) return true;
            return false;
        }

        private int AdjacentEnemyCount(GridPos position)
        {
            int count = 0;
            for (int i = 0; i < Cardinal.Length; i++)
            {
                ChainUnitState unit = _board.FindUnitAt(position + Cardinal[i]);
                if (unit != null && unit.Team == CombatTeam.Enemy && unit.IsAlive) count++;
            }
            return count;
        }

        private void CheckBattleEnd()
        {
            CheckBattleEndMethod?.Invoke(_board, null);
        }

        private void Log(string message)
        {
            WriteLogMethod?.Invoke(_board, new object[] { message });
        }

        private static bool IsAligned(GridPos a, GridPos b)
        {
            return a.X == b.X || a.Z == b.Z;
        }

        private static GridPos Direction(GridPos from, GridPos toward)
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
            if (direction.Z < 0) return "south";
            return "nowhere";
        }

        private static readonly GridPos Zero = new GridPos(0, 0);
    }
}
