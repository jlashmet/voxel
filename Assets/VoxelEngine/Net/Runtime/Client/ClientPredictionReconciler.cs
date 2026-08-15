using System;
using VoxelEngine.Net.Runtime.Protocol;

namespace VoxelEngine.Net.Runtime.Client
{
    /// <summary>
    /// Game-owned prediction adapter. Networking supplies the authoritative rewind point and the
    /// exact still-unacknowledged input samples; the gameplay movement code remains the only place
    /// that simulates player motion.
    /// </summary>
    public interface IClientPredictionAdapter
    {
        void ApplyAuthoritativeState(in S_PlayerState state);
        void ReplayInput(in C_PlayerInput input);
    }

    /// <summary>
    /// Bounded sent-input history used for local-player rewind/replay. Sequences are compared with
    /// ushort modular ordering, matching the EPHEMERAL receiver and redundant input bundle contract.
    /// </summary>
    public sealed class ClientPredictionReconciler
    {
        public const int DefaultHistoryCapacity = 256;

        private readonly C_PlayerInput[] _history;
        private int _head;
        private int _count;

        public int Count => _count;
        public long DroppedInputs { get; private set; }
        public long Reconciliations { get; private set; }
        public long ReplayedInputs { get; private set; }

        public ClientPredictionReconciler(int capacity = DefaultHistoryCapacity)
        {
            if (capacity < 8 || capacity > 4096)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _history = new C_PlayerInput[capacity];
        }

        /// <summary>Record an input only after it has been accepted by the transport send path.</summary>
        public bool RecordSentInput(in C_PlayerInput input)
        {
            if (_count > 0)
            {
                C_PlayerInput newest = At(_count - 1);
                if (!IsNewer(input.sequence, newest.sequence))
                    return false;
            }

            if (_count == _history.Length)
            {
                _head = (_head + 1) % _history.Length;
                _count--;
                DroppedInputs++;
            }

            int tail = (_head + _count) % _history.Length;
            _history[tail] = input;
            _count++;
            return true;
        }

        /// <summary>
        /// Snap prediction to the server state, discard inputs the server confirms it consumed,
        /// then replay the remaining local inputs in original sequence order.
        /// </summary>
        public int Reconcile(in S_PlayerState state, IClientPredictionAdapter adapter)
        {
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));

            if (state.HasInputAck)
            {
                while (_count > 0 && IsAtOrBefore(At(0).sequence, state.ackInputSequence))
                    PopFront();
            }

            adapter.ApplyAuthoritativeState(in state);

            int replayed = 0;
            for (int i = 0; i < _count; i++)
            {
                C_PlayerInput input = At(i);
                adapter.ReplayInput(in input);
                replayed++;
            }

            Reconciliations++;
            ReplayedInputs += replayed;
            return replayed;
        }

        public void Reset()
        {
            Array.Clear(_history, 0, _history.Length);
            _head = 0;
            _count = 0;
        }

        private C_PlayerInput At(int logicalIndex) =>
            _history[(_head + logicalIndex) % _history.Length];

        private void PopFront()
        {
            _history[_head] = default;
            _head = (_head + 1) % _history.Length;
            _count--;
        }

        private static bool IsNewer(ushort candidate, ushort reference)
        {
            ushort delta = unchecked((ushort)(candidate - reference));
            return delta != 0 && delta < 0x8000;
        }

        private static bool IsAtOrBefore(ushort candidate, ushort acknowledged)
        {
            if (candidate == acknowledged)
                return true;
            ushort delta = unchecked((ushort)(acknowledged - candidate));
            return delta != 0 && delta < 0x8000;
        }
    }
}
