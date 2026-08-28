using System;
using System.Collections.Generic;
using Game.Combat.Api;
using Game.Input.Api;

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

    public sealed class CombatService : ICombatService
    {
        private static readonly IReadOnlyList<CombatParticipant> EmptyParticipants = Array.AsReadOnly(new CombatParticipant[0]);
        private readonly Dictionary<CombatParticipantId, CombatGridPosition> _positions =
            new Dictionary<CombatParticipantId, CombatGridPosition>();
        private IReadOnlyList<CombatParticipant> _participants = EmptyParticipants;
        private int _nextSessionId = 1;

        public bool IsActive => State == CombatLifecycleState.Active;
        public CombatLifecycleState State { get; private set; } = CombatLifecycleState.Idle;
        public CombatSessionId ActiveSessionId { get; private set; }
        public IReadOnlyList<CombatParticipant> ActiveParticipants => _participants;

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
            return ActiveSessionId;
        }

        public CombatCommandResult TryExecute(CombatCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!IsActive) return CombatCommandResult.Reject("No active combat session.");

            var move = command as MoveCombatantCommand;
            if (move == null) return CombatCommandResult.Reject("Unsupported combat command.");

            CombatGridPosition current;
            if (!_positions.TryGetValue(move.Participant, out current))
                return CombatCommandResult.Reject("Participant is not in the active encounter.");

            int distance = Math.Abs(move.DeltaX) + Math.Abs(move.DeltaZ);
            if (distance != 1)
                return CombatCommandResult.Reject("Move commands must advance exactly one grid cell.");

            var target = new CombatGridPosition(current.X + move.DeltaX, current.Z + move.DeltaZ);
            foreach (KeyValuePair<CombatParticipantId, CombatGridPosition> pair in _positions)
            {
                if (!pair.Key.Equals(move.Participant) && pair.Value.Equals(target))
                    return CombatCommandResult.Reject("Target grid cell is occupied.");
            }

            _positions[move.Participant] = target;
            return CombatCommandResult.Accept();
        }

        public bool TryGetGridPosition(CombatParticipantId participant, out CombatGridPosition position) =>
            _positions.TryGetValue(participant, out position);

        public void CompleteCombat()
        {
            if (!IsActive) return;
            State = CombatLifecycleState.Completed;
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
