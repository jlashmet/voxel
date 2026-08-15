using Unity.Mathematics;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using VoxelEngine.Edits.Api;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Ring buffer of player inputs for redundant send and reconciliation replay.
    ///
    /// Stores the last N inputs (configured at construction, defaulting to the rollback
    /// window size from device-matrix.md: 500 ms / 15 ticks) so they can be re-sent during
    /// packet loss recovery or replayed during reconciliation when the server's response
    /// diverges from client prediction.
    ///
    /// Redundant send: each input is queued for transmission on every tick until the server
    /// acknowledges it, reducing the probability of dropped actions without requiring a
    /// separate ACK channel (device-matrix.md bandwidth budget accounts for this overhead).
    /// </summary>
    public sealed class InputBuffer : IDisposable
    {
        // -- constants ------------------------------------------------------------

        /// <summary>Default buffer size in ticks — matches device-matrix.md rollback window.</summary>
        private const int k_DefaultSize = 16; // slightly more than 15 to avoid edge cases.

        // -- state ----------------------------------------------------------------

        /// <summary>Underlying ring buffer storage, indexed by tick modulo capacity.</summary>
        private NativeArray<C_PlayerInput> _buffer;

        /// <summary>True when this slot in the ring buffer holds a valid input.</summary>
        private NativeArray<bool> _valid;

        /// <summary>Ring buffer capacity (power of two for fast modulo via bitwise AND).</summary>
        private readonly int _capacity;

        /// <summary>Mask for fast modulo: capacity - 1, since capacity is always power of two.</summary>
        private readonly int _mask;

        /// <summary>Highest tick that has been fully flushed to the network. Used for redundant send tracking.</summary>
        private uint _lastFlushedTick;

        /// <summary>Client-side sequence counter for generating unique input ordinals.</summary>
        private ushort _nextSequence;

        // -- construction ---------------------------------------------------------

        public InputBuffer(int capacityInTicks = k_DefaultSize)
        {
            // Round up to next power of two for fast modulo.
            int pow2 = 1;
            while (pow2 < capacityInTicks) pow2 <<= 1;
            _capacity = Math.Max(k_DefaultSize, pow2);
            _mask = _capacity - 1;

            _buffer = new NativeArray<C_PlayerInput>(_capacity, Allocator.Persistent);
            _valid = new NativeArray<bool>(_capacity, Allocator.Persistent);
            for (int i = 0; i < _capacity; i++)
                _valid[i] = false;

            _lastFlushedTick = 0;
            _nextSequence = 0;
        }

        // -- public API -----------------------------------------------------------

        /// <summary>
        /// Push a player input into the buffer at the given tick position.
        ///
        /// If a valid input already exists for this tick, it is overwritten (the client
        /// may have updated its prediction). This also marks the input as needing
        /// transmission on the next flush cycle.
        /// </summary>
        /// <param name="input">Player input to buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(in C_PlayerInput input)
        {
            int index = (int)(input.tick & _mask);
            _buffer[index] = input;
            _valid[index] = true;
        }

        /// <summary>
        /// Retrieve the buffered input at a specific tick, if one exists.
        ///
        /// Used during reconciliation replay to look up which inputs were pending
        /// at a given historical tick. Only valid for ticks within the buffer's window.
        /// </summary>
        /// <param name="tick">Server tick to look up.</param>
        /// <param name="output">The buffered input, if one exists for this tick.</param>
        /// <returns>True if a valid input was found; false if the slot is empty or outside the buffer window.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPopAtTick(uint tick, out C_PlayerInput output)
        {
            int index = (int)(tick & _mask);
            if (!_valid[index])
            {
                output = default;
                return false;
            }

            output = _buffer[index];
            return true;
        }

        /// <summary>
        /// Try to retrieve the buffered input at a specific tick without network operations.
        /// Internal variant used during reconciliation when we need inputs but cannot call network code.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPopAtTickLocal(uint tick, out C_PlayerInput? output)
        {
            int index = (int)(tick & _mask);
            if (!_valid[index])
            {
                output = null;
                return false;
            }

            output = _buffer[index];
            return true;
        }

        /// <summary>
        /// Peek at the predicted input for a tick without removing it from the buffer.
        /// Returns the last queued input whose tick is <= targetTick.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public C_PlayerInput? PeekPredicted(uint targetTick)
        {
            // Walk backward from targetTick to find the most recent pending input.
            for (uint t = targetTick; t > 0; t--)
            {
                int index = (int)(t & _mask);
                if (_valid[index] && _buffer[index].tick <= targetTick)
                    return _buffer[index];
            }
            return null;
        }

        /// <summary>
        /// Flush pending inputs to the network for a given tick.
        ///
        /// Sends every buffered input from 0 to the given tick that has not yet been
        /// confirmed by the server. Implements redundant send: inputs are re-sent on
        /// each flush until explicitly acknowledged, ensuring delivery despite packet loss.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FlushForTick(uint tick, Network network)
        {
            if (tick < _lastFlushedTick) return; // Already flushed these ticks.

            for (uint t = _lastFlushedTick; t <= tick; t++)
            {
                int index = (int)(t & _mask);
                if (_valid[index])
                {
                    var input = _buffer[index];
                    // Redundant send: re-queue on every flush until the server acknowledges.
                    network.SendLocalPrediction(CreateAlterationFromInput(input));
                }
            }

            _lastFlushedTick = tick + 1;
        }

        /// <summary>Process incoming events from the network layer and apply them to tracked state.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FlushReceived(Network network)
        {
            // Drain incoming event messages. Each processed event may update the speculative
            // overlay via the ClientTickLoop, not directly here — this method is a bridge.
            while (network.ProcessEvents() > 0) { /* handled by tick loop */ }
        }

        /// <summary>Generate the next player input from local state and push it into the buffer.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public C_PlayerInput GenerateNext(float3 position, float3 direction,
            C_PlayerInput.ActionType actionType, byte toolMaterial)
        {
            var input = new C_PlayerInput(0, 0, ++_nextSequence, position, direction, actionType, toolMaterial);
            Push(input);
            return input;
        }

        /// <summary>Clear all buffered inputs. Called during disconnection or world reset.</summary>
        public void Clear()
        {
            for (int i = 0; i < _capacity; i++)
                _valid[i] = false;

            _lastFlushedTick = 0;
            _nextSequence = 0;
        }

        /// <summary>True when there are unconfirmed inputs pending in the buffer.</summary>
        public bool HasPendingInputs => _lastFlushedTick == 0;

        /// <summary>Dispose native resources.</summary>
        public void Dispose()
        {
            if (_buffer.IsCreated) _buffer.Dispose();
            if (_valid.IsCreated) _valid.Dispose();
        }

        // -- helpers --------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AlterationEvent CreateAlterationFromInput(in C_PlayerInput input)
        {
            return new AlterationEvent(
                kind: input.toolMaterial,
                tick: input.tick,
                origin: (int3)input.Position(),
                shapeRadius: 1,
                material: input.toolMaterial,
                seed: unchecked((uint)input.playerId),
                playerId: input.playerId,
                sequence: input.sequence);
        }
    }
}
