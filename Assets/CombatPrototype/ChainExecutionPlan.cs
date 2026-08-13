using System;
using System.Collections.Generic;
using System.Reflection;

namespace MountingForce.CombatPrototype
{
    public enum ChainPlannedActionKind
    {
        Move,
        BasicHit,
        Uppercut,
        Gust,
        ShoulderHurl,
        PlacePortalPair,
        PlaceAmplifier,
        Converge,
        Harpoon,
        NotchTree,
        Reaction,
        PassReaction
    }

    /// <summary>
    /// A deterministic, network-friendly instruction authored during planning. The instruction contains only player
    /// intent and concrete targets/aim points; the normal ChainCombatBoard remains the single source of combat rules.
    /// </summary>
    public sealed class ChainPlannedAction
    {
        internal ChainPlannedAction(
            ChainPlannedActionKind kind,
            int commandGroup,
            int unitId,
            int targetId,
            int secondaryTargetId,
            int treeId,
            GridPos positionA,
            GridPos positionB,
            ChainReactionAbility reactionAbility,
            ChainReactionKind expectedReactionKind)
        {
            Kind = kind;
            CommandGroup = commandGroup;
            UnitId = unitId;
            TargetId = targetId;
            SecondaryTargetId = secondaryTargetId;
            TreeId = treeId;
            PositionA = positionA;
            PositionB = positionB;
            ReactionAbility = reactionAbility;
            ExpectedReactionKind = expectedReactionKind;
        }

        public int PlanId { get; internal set; }
        public ChainPlannedActionKind Kind { get; }
        public int CommandGroup { get; }
        public int UnitId { get; }
        public int TargetId { get; }
        public int SecondaryTargetId { get; }
        public int TreeId { get; }
        public GridPos PositionA { get; }
        public GridPos PositionB { get; }
        public ChainReactionAbility ReactionAbility { get; }
        public ChainReactionKind ExpectedReactionKind { get; }
        public bool IsReaction => Kind == ChainPlannedActionKind.Reaction || Kind == ChainPlannedActionKind.PassReaction;

        public static ChainPlannedAction Move(int group, int unitId, GridPos destination) =>
            New(ChainPlannedActionKind.Move, group, unitId, positionA: destination);

        public static ChainPlannedAction BasicHit(int group, int unitId, int targetId) =>
            New(ChainPlannedActionKind.BasicHit, group, unitId, targetId: targetId);

        public static ChainPlannedAction Uppercut(int group, int unitId, int targetId) =>
            New(ChainPlannedActionKind.Uppercut, group, unitId, targetId: targetId);

        public static ChainPlannedAction Gust(int group, int unitId, int targetId) =>
            New(ChainPlannedActionKind.Gust, group, unitId, targetId: targetId);

        public static ChainPlannedAction ShoulderHurl(int group, int unitId, int targetId, GridPos aim) =>
            New(ChainPlannedActionKind.ShoulderHurl, group, unitId, targetId: targetId, positionA: aim);

        public static ChainPlannedAction PortalPair(int group, int unitId, GridPos entrance, GridPos exit) =>
            New(ChainPlannedActionKind.PlacePortalPair, group, unitId, positionA: entrance, positionB: exit);

        public static ChainPlannedAction Amplifier(int group, int unitId, GridPos position) =>
            New(ChainPlannedActionKind.PlaceAmplifier, group, unitId, positionA: position);

        public static ChainPlannedAction Converge(int group, int unitId, int movingTargetId, int anchorTargetId) =>
            New(ChainPlannedActionKind.Converge, group, unitId, targetId: movingTargetId, secondaryTargetId: anchorTargetId);

        public static ChainPlannedAction Harpoon(int group, int unitId, int targetId) =>
            New(ChainPlannedActionKind.Harpoon, group, unitId, targetId: targetId);

        public static ChainPlannedAction NotchTree(int group, int unitId, int treeId, GridPos aim) =>
            New(ChainPlannedActionKind.NotchTree, group, unitId, treeId: treeId, positionA: aim);

        public static ChainPlannedAction React(
            int group,
            int unitId,
            ChainReactionAbility ability,
            ChainReactionKind expectedKind,
            int targetId,
            GridPos aim) =>
            New(ChainPlannedActionKind.Reaction, group, unitId, targetId: targetId, positionA: aim,
                reactionAbility: ability, expectedReactionKind: expectedKind);

        public static ChainPlannedAction Pass(ChainReactionKind expectedKind) =>
            New(ChainPlannedActionKind.PassReaction, 0, 0, expectedReactionKind: expectedKind);

        public ChainPlannedAction Copy()
        {
            return new ChainPlannedAction(
                Kind, CommandGroup, UnitId, TargetId, SecondaryTargetId, TreeId,
                PositionA, PositionB, ReactionAbility, ExpectedReactionKind)
            {
                PlanId = PlanId
            };
        }

        public string Describe(ChainCombatBoard board)
        {
            ChainUnitState actor = board?.GetUnit(UnitId);
            ChainUnitState target = board?.GetUnit(TargetId);
            ChainUnitState secondary = board?.GetUnit(SecondaryTargetId);
            string actorName = actor?.Name ?? (UnitId == 0 ? "Team" : $"unit #{UnitId}");
            string targetName = target?.Name ?? (TargetId == 0 ? "" : $"unit #{TargetId}");

            switch (Kind)
            {
                case ChainPlannedActionKind.Move: return $"P{CommandGroup} {actorName} — Move to {PositionA}";
                case ChainPlannedActionKind.BasicHit: return $"P{CommandGroup} {actorName} — Strike {targetName}";
                case ChainPlannedActionKind.Uppercut: return $"P{CommandGroup} {actorName} — Uppercut {targetName}";
                case ChainPlannedActionKind.Gust: return $"P{CommandGroup} {actorName} — Gust {targetName}";
                case ChainPlannedActionKind.ShoulderHurl: return $"P{CommandGroup} {actorName} — Hurl {targetName} toward {PositionA}";
                case ChainPlannedActionKind.PlacePortalPair: return $"P{CommandGroup} {actorName} — Portals {PositionA} ↔ {PositionB}";
                case ChainPlannedActionKind.PlaceAmplifier: return $"P{CommandGroup} {actorName} — Force ×2 at {PositionA}";
                case ChainPlannedActionKind.Converge: return $"P{CommandGroup} {actorName} — Converge {targetName} toward {secondary?.Name ?? $"unit #{SecondaryTargetId}"}";
                case ChainPlannedActionKind.Harpoon: return $"P{CommandGroup} {actorName} — Harpoon {targetName}";
                case ChainPlannedActionKind.NotchTree: return $"P{CommandGroup} {actorName} — Notch tree #{TreeId} toward {PositionA}";
                case ChainPlannedActionKind.Reaction: return $"↳ P{CommandGroup} {actorName} — {ChainCombatBoard.AbilityName(ReactionAbility)} on {ExpectedReactionKind}";
                case ChainPlannedActionKind.PassReaction: return $"↳ Let {ExpectedReactionKind} continue without a reaction";
                default: return Kind.ToString();
            }
        }

        private static ChainPlannedAction New(
            ChainPlannedActionKind kind,
            int commandGroup,
            int unitId,
            int targetId = 0,
            int secondaryTargetId = 0,
            int treeId = 0,
            GridPos positionA = default(GridPos),
            GridPos positionB = default(GridPos),
            ChainReactionAbility reactionAbility = ChainReactionAbility.None,
            ChainReactionKind expectedReactionKind = ChainReactionKind.None)
        {
            return new ChainPlannedAction(
                kind, commandGroup, unitId, targetId, secondaryTargetId, treeId,
                positionA, positionB, reactionAbility, expectedReactionKind);
        }
    }

    /// <summary>
    /// Collaborative ordered plan. Root/proactive actions are reorderable blocks; the reactions immediately following
    /// a root remain attached to that root so a drag cannot put a reaction before the event that is supposed to create it.
    /// </summary>
    public sealed class ChainExecutionPlan
    {
        private sealed class Snapshot
        {
            public readonly List<ChainPlannedAction> Actions;
            public readonly int NextPlanId;

            public Snapshot(List<ChainPlannedAction> actions, int nextPlanId)
            {
                Actions = actions;
                NextPlanId = nextPlanId;
            }
        }

        private readonly List<ChainPlannedAction> _actions = new List<ChainPlannedAction>();
        private readonly Stack<Snapshot> _undo = new Stack<Snapshot>();
        private readonly Stack<Snapshot> _redo = new Stack<Snapshot>();
        private int _nextPlanId = 1;
        private Snapshot _compoundStart;
        private bool _compoundChanged;

        public IReadOnlyList<ChainPlannedAction> Actions => _actions;
        public int Revision { get; private set; }
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public bool HasActions => _actions.Count > 0;

        public int Add(ChainPlannedAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            RecordBeforeEdit();
            ChainPlannedAction stored = action.Copy();
            stored.PlanId = _nextPlanId++;
            _actions.Add(stored);
            MarkEdited();
            return stored.PlanId;
        }

        public bool Remove(int planId)
        {
            int index = FindIndex(planId);
            if (index < 0) return false;
            RecordBeforeEdit();

            if (!_actions[index].IsReaction)
            {
                int end = index + 1;
                while (end < _actions.Count && _actions[end].IsReaction) end++;
                _actions.RemoveRange(index, end - index);
            }
            else
            {
                _actions.RemoveAt(index);
            }

            MarkEdited();
            return true;
        }

        public void Clear()
        {
            if (_actions.Count == 0) return;
            RecordBeforeEdit();
            _actions.Clear();
            MarkEdited();
        }

        public int RootCount()
        {
            int count = 0;
            for (int i = 0; i < _actions.Count; i++)
                if (!_actions[i].IsReaction) count++;
            return count;
        }

        public int RootOrdinalOf(int planId)
        {
            int ordinal = 0;
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].IsReaction) continue;
                if (_actions[i].PlanId == planId) return ordinal;
                ordinal++;
            }
            return -1;
        }

        public bool MoveRootAction(int planId, int targetRootOrdinal)
        {
            int sourceOrdinal = RootOrdinalOf(planId);
            if (sourceOrdinal < 0) return false;

            List<List<ChainPlannedAction>> blocks = BuildBlocks();
            if (blocks.Count <= 1) return false;
            targetRootOrdinal = Math.Max(0, Math.Min(blocks.Count - 1, targetRootOrdinal));
            if (sourceOrdinal == targetRootOrdinal) return false;

            RecordBeforeEdit();
            List<ChainPlannedAction> moving = blocks[sourceOrdinal];
            blocks.RemoveAt(sourceOrdinal);
            blocks.Insert(targetRootOrdinal, moving);

            _actions.Clear();
            for (int i = 0; i < blocks.Count; i++) _actions.AddRange(blocks[i]);
            MarkEdited();
            return true;
        }

        public void BeginCompoundEdit()
        {
            if (_compoundStart != null) return;
            _compoundStart = Capture();
            _compoundChanged = false;
        }

        public void EndCompoundEdit()
        {
            if (_compoundStart == null) return;
            if (_compoundChanged)
            {
                _undo.Push(_compoundStart);
                _redo.Clear();
            }
            _compoundStart = null;
            _compoundChanged = false;
        }

        public void CancelCompoundEdit()
        {
            if (_compoundStart == null) return;
            if (_compoundChanged) Restore(_compoundStart);
            _compoundStart = null;
            _compoundChanged = false;
            Revision++;
        }

        public bool Undo()
        {
            EndCompoundEdit();
            if (_undo.Count == 0) return false;
            _redo.Push(Capture());
            Restore(_undo.Pop());
            Revision++;
            return true;
        }

        public bool Redo()
        {
            EndCompoundEdit();
            if (_redo.Count == 0) return false;
            _undo.Push(Capture());
            Restore(_redo.Pop());
            Revision++;
            return true;
        }

        public void ResetWithoutHistory()
        {
            _actions.Clear();
            _undo.Clear();
            _redo.Clear();
            _nextPlanId = 1;
            _compoundStart = null;
            _compoundChanged = false;
            Revision++;
        }

        private int FindIndex(int planId)
        {
            for (int i = 0; i < _actions.Count; i++)
                if (_actions[i].PlanId == planId) return i;
            return -1;
        }

        private List<List<ChainPlannedAction>> BuildBlocks()
        {
            var blocks = new List<List<ChainPlannedAction>>();
            for (int i = 0; i < _actions.Count; i++)
            {
                ChainPlannedAction action = _actions[i];
                if (!action.IsReaction || blocks.Count == 0)
                    blocks.Add(new List<ChainPlannedAction>());
                blocks[blocks.Count - 1].Add(action);
            }
            return blocks;
        }

        private void RecordBeforeEdit()
        {
            if (_compoundStart != null)
            {
                _compoundChanged = true;
                return;
            }
            _undo.Push(Capture());
            _redo.Clear();
        }

        private void MarkEdited()
        {
            Revision++;
        }

        private Snapshot Capture()
        {
            var copy = new List<ChainPlannedAction>(_actions.Count);
            for (int i = 0; i < _actions.Count; i++) copy.Add(_actions[i].Copy());
            return new Snapshot(copy, _nextPlanId);
        }

        private void Restore(Snapshot snapshot)
        {
            _actions.Clear();
            for (int i = 0; i < snapshot.Actions.Count; i++) _actions.Add(snapshot.Actions[i].Copy());
            _nextPlanId = snapshot.NextPlanId;
        }
    }

    public sealed class ChainPlanActionResult
    {
        public ChainPlanActionResult(int planId, bool succeeded, string message)
        {
            PlanId = planId;
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public int PlanId { get; }
        public bool Succeeded { get; }
        public string Message { get; }
    }

    public sealed class ChainPlanPreviewFrame
    {
        internal ChainPlanPreviewFrame(int actionIndex, int planId, string label, ChainCombatBoard board)
        {
            ActionIndex = actionIndex;
            PlanId = planId;
            Label = label;
            Board = board;
        }

        public int ActionIndex { get; }
        public int PlanId { get; }
        public string Label { get; }
        public ChainCombatBoard Board { get; }
    }

    public sealed class ChainExecutionPreview
    {
        internal readonly Dictionary<int, ChainPlanActionResult> ResultByPlanId = new Dictionary<int, ChainPlanActionResult>();
        internal readonly List<ChainPlanPreviewFrame> MutableFrames = new List<ChainPlanPreviewFrame>();

        public IReadOnlyList<ChainPlanPreviewFrame> Frames => MutableFrames;
        public ChainCombatBoard FinalBoard => MutableFrames.Count == 0 ? null : MutableFrames[MutableFrames.Count - 1].Board;
        public bool HasFailure { get; internal set; }
        public int FailedPlanId { get; internal set; }
        public string FailureMessage { get; internal set; }
        public int ExecutedActionCount { get; internal set; }

        public ChainPlanActionResult ResultFor(int planId)
        {
            ResultByPlanId.TryGetValue(planId, out ChainPlanActionResult result);
            return result;
        }
    }

    public static class ChainExecutionPlanSimulator
    {
        public static ChainExecutionPreview Simulate(ChainCombatBoard source, IReadOnlyList<ChainPlannedAction> actions)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var preview = new ChainExecutionPreview();
            ChainCombatBoard board = ChainCombatBoardClone.Clone(source);
            preview.MutableFrames.Add(new ChainPlanPreviewFrame(-1, 0, "Start of shared plan", ChainCombatBoardClone.Clone(board)));

            if (actions == null) return preview;
            for (int i = 0; i < actions.Count; i++)
            {
                ChainPlannedAction action = actions[i];
                bool succeeded = Execute(board, action, out string message);
                preview.ResultByPlanId[action.PlanId] = new ChainPlanActionResult(action.PlanId, succeeded, message);
                preview.MutableFrames.Add(new ChainPlanPreviewFrame(i, action.PlanId, action.Describe(source), ChainCombatBoardClone.Clone(board)));

                if (!succeeded)
                {
                    preview.HasFailure = true;
                    preview.FailedPlanId = action.PlanId;
                    preview.FailureMessage = message;
                    break;
                }
                preview.ExecutedActionCount++;
            }

            return preview;
        }

        public static bool ExecuteAll(ChainCombatBoard board, IReadOnlyList<ChainPlannedAction> actions, out string message)
        {
            message = string.Empty;
            if (board == null) return false;
            if (actions == null) return true;

            for (int i = 0; i < actions.Count; i++)
            {
                if (!Execute(board, actions[i], out message)) return false;
            }
            return true;
        }

        private static bool Execute(ChainCombatBoard board, ChainPlannedAction action, out string message)
        {
            bool succeeded;
            if (action.Kind == ChainPlannedActionKind.Reaction)
            {
                ChainReactionOpportunity pending = board.PendingReaction;
                if (pending == null)
                {
                    message = $"{ChainCombatBoard.AbilityName(action.ReactionAbility)} has no physical event to react to at this point in the plan.";
                    return false;
                }
                if (action.ExpectedReactionKind != ChainReactionKind.None && pending.Kind != action.ExpectedReactionKind)
                {
                    message = $"Expected {action.ExpectedReactionKind}, but the ghost reached {pending.Kind}. Reorder or change the plan.";
                    return false;
                }
                if (!board.TryClaimReaction(action.UnitId, action.ReactionAbility))
                {
                    message = board.LastMessage;
                    return false;
                }

                switch (action.ReactionAbility)
                {
                    case ChainReactionAbility.Crosswind:
                        succeeded = board.TryCrosswind(action.UnitId, action.PositionA);
                        break;
                    case ChainReactionAbility.CatchThrow:
                        succeeded = board.TryCatchThrow(action.UnitId, action.PositionA);
                        break;
                    case ChainReactionAbility.Repulse:
                        succeeded = board.TryRepulse(action.UnitId, action.TargetId, action.PositionA);
                        break;
                    case ChainReactionAbility.FollowThrough:
                        succeeded = board.TryFollowThrough(action.UnitId, action.TargetId, action.PositionA);
                        break;
                    case ChainReactionAbility.HookYank:
                        succeeded = board.TryHookYank(action.UnitId, action.TargetId, action.PositionA);
                        break;
                    case ChainReactionAbility.Timber:
                        succeeded = board.TryTimber(action.UnitId, action.PositionA);
                        break;
                    default:
                        message = "That planned reaction has no executable capability.";
                        return false;
                }

                message = board.LastMessage;
                return succeeded;
            }

            if (action.Kind == ChainPlannedActionKind.PassReaction)
            {
                ChainReactionOpportunity pending = board.PendingReaction;
                if (pending == null)
                {
                    message = "There is no physical event to pass at this point in the plan.";
                    return false;
                }
                if (action.ExpectedReactionKind != ChainReactionKind.None && pending.Kind != action.ExpectedReactionKind)
                {
                    message = $"Expected to pass {action.ExpectedReactionKind}, but the ghost reached {pending.Kind}.";
                    return false;
                }
                succeeded = board.PassReaction();
                message = board.LastMessage;
                return succeeded;
            }

            switch (action.Kind)
            {
                case ChainPlannedActionKind.Move:
                    succeeded = board.TryMove(action.UnitId, action.PositionA);
                    break;
                case ChainPlannedActionKind.BasicHit:
                    succeeded = board.TryBasicHit(action.UnitId, action.TargetId);
                    break;
                case ChainPlannedActionKind.Uppercut:
                    succeeded = board.TryUppercut(action.UnitId, action.TargetId);
                    break;
                case ChainPlannedActionKind.Gust:
                    succeeded = board.TryGust(action.UnitId, action.TargetId);
                    break;
                case ChainPlannedActionKind.ShoulderHurl:
                    succeeded = board.TryShoulderHurl(action.UnitId, action.TargetId, action.PositionA);
                    break;
                case ChainPlannedActionKind.PlacePortalPair:
                    succeeded = board.TryPlacePortalPair(action.UnitId, action.PositionA, action.PositionB);
                    break;
                case ChainPlannedActionKind.PlaceAmplifier:
                    succeeded = board.TryPlaceAmplifier(action.UnitId, action.PositionA);
                    break;
                case ChainPlannedActionKind.Converge:
                    succeeded = board.TryConverge(action.UnitId, action.TargetId, action.SecondaryTargetId);
                    break;
                case ChainPlannedActionKind.Harpoon:
                    succeeded = board.TryHarpoon(action.UnitId, action.TargetId);
                    break;
                case ChainPlannedActionKind.NotchTree:
                    succeeded = board.TryNotchTree(action.UnitId, action.TreeId, action.PositionA);
                    break;
                default:
                    message = "Unknown planned action.";
                    return false;
            }

            message = board.LastMessage;
            return succeeded;
        }
    }

    /// <summary>
    /// Prototype-only deep copy bridge. The combat board predates planning snapshots, so this keeps V9 isolated while
    /// ensuring the preview runs the exact production prototype rules. If planning survives playtest, move this into
    /// explicit snapshot/restore APIs on the authoritative combat state rather than keeping reflection at the seam.
    /// </summary>
    internal static class ChainCombatBoardClone
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Type BoardType = typeof(ChainCombatBoard);

        public static ChainCombatBoard Clone(ChainCombatBoard source)
        {
            var copy = new ChainCombatBoard();

            List<ChainUnitState> units = GetField<List<ChainUnitState>>(copy, "_units");
            units.Clear();
            for (int i = 0; i < source.Units.Count; i++)
            {
                ChainUnitState src = source.Units[i];
                units.Add(new ChainUnitState(src.Id, src.Name, src.Kind, src.Team, src.CommandGroup, src.Position, src.MaxHp)
                {
                    Position = src.Position,
                    Hp = src.Hp,
                    MoveSpent = src.MoveSpent,
                    ActionSpent = src.ActionSpent,
                    ReactionSpent = src.ReactionSpent,
                    Airborne = src.Airborne
                });
            }

            List<ChainTreeState> trees = GetField<List<ChainTreeState>>(copy, "_trees");
            trees.Clear();
            for (int i = 0; i < source.Trees.Count; i++)
            {
                ChainTreeState src = source.Trees[i];
                trees.Add(new ChainTreeState(src.Id, src.Position)
                {
                    Standing = src.Standing,
                    FallDirection = src.FallDirection,
                    Stress = src.Stress,
                    NotchedDirection = src.NotchedDirection,
                    NotchedByUnitId = src.NotchedByUnitId
                });
            }

            HashSet<GridPos> amplifiers = GetField<HashSet<GridPos>>(copy, "_amplifiers");
            amplifiers.Clear();
            foreach (GridPos position in source.Amplifiers) amplifiers.Add(position);

            List<string> log = GetField<List<string>>(copy, "_log");
            log.Clear();
            for (int i = 0; i < source.Log.Count; i++) log.Add(source.Log[i]);

            HashSet<int> cascadeGroups = GetField<HashSet<int>>(copy, "_cascadeGroups");
            cascadeGroups.Clear();
            HashSet<int> sourceCascadeGroups = GetField<HashSet<int>>(source, "_cascadeGroups");
            foreach (int group in sourceCascadeGroups) cascadeGroups.Add(group);

            Dictionary<int, int> active = GetField<Dictionary<int, int>>(copy, "_activeRecruitByGroup");
            active.Clear();
            Dictionary<int, int> sourceActive = GetField<Dictionary<int, int>>(source, "_activeRecruitByGroup");
            foreach (KeyValuePair<int, int> pair in sourceActive) active[pair.Key] = pair.Value;

            ChainMotionState sourceMotion = GetField<ChainMotionState>(source, "_motion");
            SetField(copy, "_motion", sourceMotion == null ? null : new ChainMotionState
            {
                UnitId = sourceMotion.UnitId,
                Direction = sourceMotion.Direction,
                RemainingForce = sourceMotion.RemainingForce,
                Airborne = sourceMotion.Airborne
            });

            CopyField<int>(source, copy, "_nextUnitId");
            CopyField<int>(source, copy, "_nextTreeId");
            CopyField<int>(source, copy, "_nextOpportunityId");
            CopyField<bool>(source, copy, "_cascadeActive");
            CopyField<int>(source, copy, "_lastCascadeCommandGroup");

            SetProperty(copy, "PortalA", source.PortalA);
            SetProperty(copy, "PortalB", source.PortalB);
            SetProperty(copy, "Round", source.Round);
            SetProperty(copy, "LastMessage", source.LastMessage);
            SetProperty(copy, "BattleOver", source.BattleOver);
            SetProperty(copy, "BattleResult", source.BattleResult);
            SetProperty(copy, "CurrentCascadeSteps", source.CurrentCascadeSteps);
            SetProperty(copy, "LastCascadeSteps", source.LastCascadeSteps);
            SetProperty(copy, "LastCascadePlayers", source.LastCascadePlayers);
            SetProperty(copy, "BestCascadeSteps", source.BestCascadeSteps);
            SetProperty(copy, "BestCascadePlayers", source.BestCascadePlayers);
            SetProperty(copy, "CurrentHandoffs", source.CurrentHandoffs);
            SetProperty(copy, "LastHandoffs", source.LastHandoffs);
            SetProperty(copy, "BestHandoffs", source.BestHandoffs);

            ChainReactionOpportunity pending = source.PendingReaction;
            SetProperty(copy, "PendingReaction", pending == null ? null : new ChainReactionOpportunity(
                pending.Id,
                pending.Kind,
                pending.PrimaryUnitId,
                pending.SecondaryUnitId,
                pending.TreeId,
                pending.Position,
                pending.ImpactForce,
                pending.Description)
            {
                ClaimedByUnitId = pending.ClaimedByUnitId,
                ClaimedByCommandGroup = pending.ClaimedByCommandGroup,
                ClaimedAbility = pending.ClaimedAbility
            });

            return copy;
        }

        private static T GetField<T>(object target, string name)
        {
            FieldInfo field = BoardType.GetField(name, InstancePrivate);
            if (field == null) throw new MissingFieldException(BoardType.FullName, name);
            return (T)field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = BoardType.GetField(name, InstancePrivate);
            if (field == null) throw new MissingFieldException(BoardType.FullName, name);
            field.SetValue(target, value);
        }

        private static void CopyField<T>(object source, object target, string name)
        {
            SetField(target, name, GetField<T>(source, name));
        }

        private static void SetProperty(object target, string name, object value)
        {
            PropertyInfo property = BoardType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo setter = property?.GetSetMethod(true);
            if (setter == null) throw new MissingMemberException(BoardType.FullName, name);
            setter.Invoke(target, new[] { value });
        }
    }
}
