using System;
using System.Collections.Generic;
using Game.Combat.Api;
using Game.Input.Api;
using Game.Vitality.Api;

namespace Game.Combat.Runtime
{
    public readonly struct CombatGridPosition : IEquatable<CombatGridPosition>
    {
        public int X { get; }
        public int Z { get; }

        public CombatGridPosition(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(CombatGridPosition other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is CombatGridPosition other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Z;
        public override string ToString() => "(" + X + "," + Z + ")";
    }

    public abstract class CombatCommand
    {
        public CombatParticipantId Participant { get; }

        protected CombatCommand(CombatParticipantId participant)
        {
            if (!participant.IsValid) throw new ArgumentException("Participant is required.", nameof(participant));
            Participant = participant;
        }
    }

    public sealed class MoveCombatantCommand : CombatCommand
    {
        public int DeltaX { get; }
        public int DeltaZ { get; }

        public MoveCombatantCommand(CombatParticipantId participant, int deltaX, int deltaZ)
            : base(participant)
        {
            DeltaX = deltaX;
            DeltaZ = deltaZ;
        }
    }

    public sealed class AttackCombatantCommand : CombatCommand
    {
        public CombatParticipantId Target { get; }

        public AttackCombatantCommand(CombatParticipantId participant, CombatParticipantId target)
            : base(participant)
        {
            if (!target.IsValid) throw new ArgumentException("Target is required.", nameof(target));
            Target = target;
        }
    }

    public readonly struct CombatCommandResult
    {
        public bool Succeeded { get; }
        public string RejectReason { get; }

        private CombatCommandResult(bool succeeded, string rejectReason)
        {
            Succeeded = succeeded;
            RejectReason = rejectReason ?? string.Empty;
        }

        public static CombatCommandResult Accept() => new CombatCommandResult(true, string.Empty);
        public static CombatCommandResult Reject(string reason) => new CombatCommandResult(false, reason);
    }

    /// <summary>
    /// Engine-independent authority for the production combat slice. Movement remains a free positioning command for
    /// compatibility with the first Kentridge integration, while attacks are turn-consuming battle actions. Actor life
    /// truth is owned by Vitality; Combat retains only positioning, turn, team, and winner policy.
    /// </summary>
    public sealed class CombatService : ICombatService
    {
        private const int AttackDamage = 2;

        private static readonly IReadOnlyList<CombatParticipant> EmptyParticipants = Array.AsReadOnly(new CombatParticipant[0]);
        private readonly Dictionary<CombatParticipantId, CombatGridPosition> _positions =
            new Dictionary<CombatParticipantId, CombatGridPosition>();
        private readonly CombatVitalityAdapter _vitality;
        private IReadOnlyList<CombatParticipant> _participants = EmptyParticipants;
        private int _nextSessionId = 1;
        private int _turnIndex;

        public CombatService(IVitalityService vitality)
        {
            _vitality = new CombatVitalityAdapter(vitality);
        }

        public bool IsActive => State == CombatLifecycleState.Active;
        public CombatLifecycleState State { get; private set; } = CombatLifecycleState.Idle;
        public CombatSessionId ActiveSessionId { get; private set; }
        public IReadOnlyList<CombatParticipant> ActiveParticipants => _participants;
        public CombatParticipantId ActiveParticipant { get; private set; }
        public int ActionCount { get; private set; }
        public int TurnNumber { get; private set; }
        public CombatTeam? WinningTeam { get; private set; }
        public bool HasPendingBattleWork => IsActive;

        public CombatSessionId BeginCombat(CombatEncounterRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (IsActive) throw new InvalidOperationException("A combat session is already active.");

            var seen = new HashSet<CombatParticipantId>();
            bool hasPlayer = false;
            bool hasEnemy = false;
            int enemyIndex = 0;
            _positions.Clear();

            for (int i = 0; i < request.Participants.Count; i++)
            {
                CombatParticipant participant = request.Participants[i];
                if (!seen.Add(participant.Id))
                    throw new ArgumentException("Duplicate combat participant '" + participant.Id + "'.", nameof(request));
                if (!participant.IsCharacterBacked)
                    throw new ArgumentException("Combat participant '" + participant.Id + "' is not backed by a CharacterId.", nameof(request));
                if (!_vitality.TryGetState(participant, out _))
                    throw new ArgumentException("Combat participant '" + participant.Id + "' has no registered vitality state.", nameof(request));

                if (participant.Team == CombatTeam.Player)
                {
                    hasPlayer = true;
                    _positions.Add(participant.Id, new CombatGridPosition(0, 0));
                }
                else
                {
                    hasEnemy = true;
                    int lane = (enemyIndex % 2 == 0) ? -1 : 1;
                    _positions.Add(participant.Id, new CombatGridPosition(3 + enemyIndex / 2, lane));
                    enemyIndex++;
                }
            }

            if (!hasPlayer || !hasEnemy)
                throw new ArgumentException("Combat requires at least one player and one enemy.", nameof(request));

            _participants = request.Participants;
            ActiveSessionId = new CombatSessionId(_nextSessionId++);
            State = CombatLifecycleState.Active;
            WinningTeam = null;
            ActionCount = 0;
            TurnNumber = 1;
            _turnIndex = 0;
            ActiveParticipant = _participants[0].Id;
            AdvancePastDefeatedParticipants();
            return ActiveSessionId;
        }

        public CombatCommandResult TryExecute(CombatCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!IsActive) return CombatCommandResult.Reject("No active combat session.");

            var move = command as MoveCombatantCommand;
            if (move != null) return TryMove(move);

            var attack = command as AttackCombatantCommand;
            if (attack != null) return TryAttack(attack);

            return CombatCommandResult.Reject("Unsupported combat command.");
        }

        private CombatCommandResult TryMove(MoveCombatantCommand move)
        {
            CombatGridPosition current;
            if (!_positions.TryGetValue(move.Participant, out current))
                return CombatCommandResult.Reject("Participant is not in the active encounter.");
            if (!IsAlive(move.Participant))
                return CombatCommandResult.Reject("Defeated participants cannot move.");

            int distance = Math.Abs(move.DeltaX) + Math.Abs(move.DeltaZ);
            if (distance != 1)
                return CombatCommandResult.Reject("Move commands must advance exactly one grid cell.");

            var target = new CombatGridPosition(current.X + move.DeltaX, current.Z + move.DeltaZ);
            foreach (KeyValuePair<CombatParticipantId, CombatGridPosition> pair in _positions)
            {
                if (!pair.Key.Equals(move.Participant) && IsAlive(pair.Key) && pair.Value.Equals(target))
                    return CombatCommandResult.Reject("Target grid cell is occupied.");
            }

            _positions[move.Participant] = target;
            return CombatCommandResult.Accept();
        }

        private CombatCommandResult TryAttack(AttackCombatantCommand attack)
        {
            if (!attack.Participant.Equals(ActiveParticipant))
                return CombatCommandResult.Reject("Only the active combatant may attack.");
            if (!IsAlive(attack.Participant))
                return CombatCommandResult.Reject("The active combatant is defeated.");
            if (!IsAlive(attack.Target))
                return CombatCommandResult.Reject("Attack target is already defeated or absent.");

            CombatParticipant attacker = FindParticipant(attack.Participant);
            CombatParticipant target = FindParticipant(attack.Target);
            if (attacker == null || target == null)
                return CombatCommandResult.Reject("Attack participants must belong to the active encounter.");
            if (attacker.Team == target.Team)
                return CombatCommandResult.Reject("Combatants cannot attack their own team.");

            DamageResult damage = _vitality.ApplyDamage(target, AttackDamage);
            if (!damage.Accepted)
                return CombatCommandResult.Reject("Vitality rejected combat damage: " + damage.RejectionReason + ".");
            ActionCount++;

            CombatTeam? winner = EvaluateWinner();
            if (winner.HasValue)
            {
                SettleCombat(winner.Value);
                return CombatCommandResult.Accept();
            }

            AdvanceTurn();
            return CombatCommandResult.Accept();
        }

        public bool TryGetGridPosition(CombatParticipantId participant, out CombatGridPosition position) =>
            _positions.TryGetValue(participant, out position);

        public bool TryGetHitPoints(CombatParticipantId participant, out int hitPoints)
        {
            CombatParticipant actor = FindParticipant(participant);
            if (actor != null && _vitality.TryGetState(actor, out VitalitySnapshot state))
            {
                hitPoints = state.Current;
                return true;
            }

            hitPoints = 0;
            return false;
        }

        public bool IsAlive(CombatParticipantId participant)
        {
            CombatParticipant actor = FindParticipant(participant);
            return actor != null && _vitality.IsAlive(actor);
        }

        public void CompleteCombat()
        {
            if (!IsActive) return;
            State = CombatLifecycleState.Completed;
            ActiveParticipant = default(CombatParticipantId);
            WinningTeam = null;
        }

        private CombatParticipant FindParticipant(CombatParticipantId id)
        {
            for (int i = 0; i < _participants.Count; i++)
                if (_participants[i].Id.Equals(id)) return _participants[i];
            return null;
        }

        private CombatTeam? EvaluateWinner()
        {
            bool playerAlive = false;
            bool enemyAlive = false;
            for (int i = 0; i < _participants.Count; i++)
            {
                CombatParticipant participant = _participants[i];
                if (!IsAlive(participant.Id)) continue;
                if (participant.Team == CombatTeam.Player) playerAlive = true;
                else enemyAlive = true;
            }

            if (!enemyAlive) return CombatTeam.Player;
            if (!playerAlive) return CombatTeam.Enemy;
            return null;
        }

        private void AdvanceTurn()
        {
            if (_participants.Count == 0) return;
            int previousIndex = _turnIndex;
            do
            {
                _turnIndex = (_turnIndex + 1) % _participants.Count;
                if (_turnIndex == 0) TurnNumber++;
                if (IsAlive(_participants[_turnIndex].Id))
                {
                    ActiveParticipant = _participants[_turnIndex].Id;
                    return;
                }
            }
            while (_turnIndex != previousIndex);

            CombatTeam? winner = EvaluateWinner();
            if (winner.HasValue) SettleCombat(winner.Value);
        }

        private void AdvancePastDefeatedParticipants()
        {
            if (_participants.Count == 0) return;
            if (IsAlive(_participants[_turnIndex].Id))
            {
                ActiveParticipant = _participants[_turnIndex].Id;
                return;
            }
            AdvanceTurn();
        }

        private void SettleCombat(CombatTeam winner)
        {
            WinningTeam = winner;
            State = CombatLifecycleState.Completed;
            ActiveParticipant = default(CombatParticipantId);
        }
    }

    /// <summary>
    /// Deterministic, allocation-light battle driver used by autonomous actors and regression validation. It has no
    /// hidden wait state: one Step either executes one legal attack and advances authority state, or throws with a
    /// high-signal diagnostic. The seeded PRNG is a tiny fixed LCG so results do not depend on framework RNG details.
    /// </summary>
    public sealed class CombatAiBattleDriver
    {
        private readonly CombatService _combat;
        private uint _rngState;

        public CombatAiBattleDriver(CombatService combat, int seed)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            Seed = seed;
            _rngState = unchecked((uint)seed);
            if (_rngState == 0u) _rngState = 0x6d2b79f5u;
        }

        public int Seed { get; }
        public int StepCount { get; private set; }
        public CombatParticipantId PendingTarget { get; private set; }
        public string LastAction { get; private set; } = string.Empty;
        public bool HasPendingAction => PendingTarget.IsValid;

        public bool Step()
        {
            PendingTarget = default(CombatParticipantId);
            if (!_combat.IsActive) return false;

            CombatParticipant actor = FindParticipant(_combat.ActiveParticipant);
            if (actor == null || !_combat.IsAlive(actor.Id))
                throw new InvalidOperationException(Diagnostic("Active combatant is absent or defeated."));

            var candidates = new List<CombatParticipantId>(_combat.ActiveParticipants.Count);
            for (int i = 0; i < _combat.ActiveParticipants.Count; i++)
            {
                CombatParticipant candidate = _combat.ActiveParticipants[i];
                if (candidate.Team == actor.Team || !_combat.IsAlive(candidate.Id)) continue;
                candidates.Add(candidate.Id);
            }

            if (candidates.Count == 0)
                throw new InvalidOperationException(Diagnostic("No living opposing target exists while combat is still active."));

            PendingTarget = candidates[NextIndex(candidates.Count)];
            int beforeActions = _combat.ActionCount;
            CombatParticipantId beforeActor = _combat.ActiveParticipant;
            CombatCommandResult result = _combat.TryExecute(new AttackCombatantCommand(actor.Id, PendingTarget));
            if (!result.Succeeded)
                throw new InvalidOperationException(Diagnostic("AI attack rejected: " + result.RejectReason));

            StepCount++;
            LastAction = "action=" + _combat.ActionCount +
                         " turn=" + _combat.TurnNumber +
                         " actor=" + actor.Id +
                         " team=" + actor.Team +
                         " target=" + PendingTarget +
                         " state=" + _combat.State +
                         " winner=" + (_combat.WinningTeam.HasValue ? _combat.WinningTeam.Value.ToString() : "none");
            PendingTarget = default(CombatParticipantId);

            bool authorityProgressed = _combat.ActionCount > beforeActions &&
                                       (!_combat.IsActive || !_combat.ActiveParticipant.Equals(beforeActor));
            if (!authorityProgressed)
                throw new InvalidOperationException(Diagnostic("Successful AI action made no turn/terminal progress."));
            return true;
        }

        public CombatTeam RunToCompletion(int maximumActions)
        {
            if (maximumActions <= 0) throw new ArgumentOutOfRangeException(nameof(maximumActions));
            while (_combat.IsActive && StepCount < maximumActions)
                Step();

            if (_combat.IsActive)
                throw new InvalidOperationException(Diagnostic("Battle exceeded the bounded action watchdog of " + maximumActions + "."));
            if (!_combat.WinningTeam.HasValue)
                throw new InvalidOperationException(Diagnostic("Battle completed without a terminal winning team."));
            if (HasPendingAction || _combat.HasPendingBattleWork)
                throw new InvalidOperationException(Diagnostic("Battle completed with unresolved AI/combat work."));
            return _combat.WinningTeam.Value;
        }

        public string Diagnostic(string reason)
        {
            return reason +
                   " seed=" + Seed +
                   " step=" + StepCount +
                   " action=" + _combat.ActionCount +
                   " turn=" + _combat.TurnNumber +
                   " active=" + _combat.ActiveParticipant +
                   " pendingTarget=" + PendingTarget +
                   " state=" + _combat.State +
                   " winner=" + (_combat.WinningTeam.HasValue ? _combat.WinningTeam.Value.ToString() : "none");
        }

        private CombatParticipant FindParticipant(CombatParticipantId id)
        {
            for (int i = 0; i < _combat.ActiveParticipants.Count; i++)
                if (_combat.ActiveParticipants[i].Id.Equals(id)) return _combat.ActiveParticipants[i];
            return null;
        }

        private int NextIndex(int count)
        {
            _rngState = unchecked(_rngState * 1664525u + 1013904223u);
            return (int)(_rngState % (uint)count);
        }
    }

    public sealed class CombatInputController
    {
        private readonly CombatService _combat;
        private readonly IPlayerInputReader _input;
        private readonly LocalPlayerId _localPlayer;
        private readonly CombatParticipantId _participant;
        private float _repeatCooldown;

        public CombatInputController(
            CombatService combat,
            IPlayerInputReader input,
            LocalPlayerId localPlayer,
            CombatParticipantId participant)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _localPlayer = localPlayer;
            _participant = participant;
        }

        public CombatCommandResult Tick(float deltaTime)
        {
            if (!_combat.IsActive) return CombatCommandResult.Reject("No active combat session.");

            _repeatCooldown = Math.Max(0f, _repeatCooldown - Math.Max(0f, deltaTime));
            PlayerInputSnapshot snapshot = _input.Read(_localPlayer);
            if (_repeatCooldown > 0f)
                return CombatCommandResult.Reject("Input repeat cooldown is active.");

            float absX = Math.Abs(snapshot.MoveX);
            float absY = Math.Abs(snapshot.MoveY);
            if (absX < 0.5f && absY < 0.5f)
                return CombatCommandResult.Reject("No movement intent.");

            int dx = 0;
            int dz = 0;
            if (absX >= absY)
                dx = snapshot.MoveX >= 0f ? 1 : -1;
            else
                dz = snapshot.MoveY >= 0f ? 1 : -1;

            CombatCommandResult result = _combat.TryExecute(new MoveCombatantCommand(_participant, dx, dz));
            if (result.Succeeded) _repeatCooldown = 0.18f;
            return result;
        }
    }
}
