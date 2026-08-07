using VoxelEngine.Net.Server;
using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Client-side tick loop, aligned to the server's authoritative 30 Hz clock.
    ///
    /// Drives the full client simulation pipeline each server tick: applies received
    /// <see cref="AlterationEvent"/>s in deterministic order, advances the speculative
    /// overlay, flushes buffered player inputs, and triggers reconciliation when the
    /// server catches up past a buffered input.
    ///
    /// Server tick alignment is achieved by comparing the client's local time to the
    /// timestamp carried in every <see cref="SyncPacket"/> received from the server.
    /// The client adjusts its internal tick counter to match, so all client-local
    /// simulation derives from a single authoritative timeline (Constitution Principle III).
    /// </summary>
    public sealed class ClientTickLoop : IDisposable
    {
        // -- constants ------------------------------------------------------------

        /// <summary>Server tick rate in Hz — 30 on every tier (device-matrix.md).</summary>
        private const int k_ServerTickRate = 30;

        /// <summary>Duration of one server tick in seconds.</summary>
        private const float k_TickDuration = 1.0f / k_ServerTickRate;

        // -- state ----------------------------------------------------------------

        /// <summary>Server-confirmed current tick number.</summary>
        private uint _serverTick;

        /// <summary>Client-local predicted tick, lagged behind server during roll-forward.</summary>
        private uint _clientTick;

        /// <summary>Time (seconds) spent in the current simulated step. Accumulates via deltaTime.</summary>
        private float _tickAccumulator;

        /// <summary>Timestamp of the last sync packet from the server, in seconds from session start.</summary>
        private float _syncTimestamp;

        /// <summary>Offset between client and server time, derived from sync packets.</summary>
        private float _timeOffset;

        /// <summary>
        /// Last server tick reported in a sync packet. Used to detect jumps
        /// and trigger reconciliation when the server has moved ahead of client prediction.
        /// </summary>
        private uint _lastSyncTick;

        // -- subsystems -----------------------------------------------------------

        /// <summary>Buffered player inputs keyed by tick for replay during reconciliation.</summary>
        private readonly InputBuffer _inputBuffer;

        /// <summary>Client-local speculative voxel overlay over the authoritative grid.</summary>
        private readonly SpeculativeOverlay _overlay;

        /// <summary>Reconciliation engine, driven when server state diverges from client prediction.</summary>
        private readonly Reconciliation _reconciliation;

        /// <summary>Server-confirmed world history for replay queries during reconciliation.</summary>
        private WorldHistory _worldHistory;

        /// <summary>Whether the last sync packet indicated a tick jump requiring reconciliation.</summary>
        private bool _needsReconciliation;

        /// <summary>Ticks to skip after reconciliation before resuming normal forward-simulation. Prevents thrash.</summary>
        private uint _recoveryTicks;

        // -- construction ---------------------------------------------------------

        public ClientTickLoop(int inputBufferSize, int historyCapacity)
        {
            _serverTick = 0;
            _clientTick = 0;
            _tickAccumulator = 0f;
            _syncTimestamp = 0f;
            _timeOffset = 0f;
            _lastSyncTick = 0;
            _needsReconciliation = false;
            _recoveryTicks = 0;

            _inputBuffer = new InputBuffer(inputBufferSize);
            _overlay = new SpeculativeOverlay();
            _reconciliation = new Reconciliation();
        }

        // -- public API -----------------------------------------------------------

        /// <summary>
        /// Main update driven from the Unity Update loop. Should be called every frame
        /// with the frame's deltaTime and a reference to the active network session.
        ///
        /// Advances client simulation in discrete 30 Hz steps, applying any events that
        /// have been received from the server and flushing any buffered player inputs
        /// that are now ready to send.
        /// </summary>
        /// <param name="deltaTime">Frame time in seconds, from Time.deltaTime or equivalent.</param>
        /// <param name="network">Active network session for sending inputs and receiving events.</param>
        public void Update(float deltaTime, Network network)
        {
            // Process any incoming messages from the network (events, sync packets).
            _inputBuffer.FlushReceived(network);

            // Handle reconciliation if needed — this takes priority over forward-simulation.
            if (_needsReconciliation)
            {
                DoReconciliation(network);
                return;
            }

            // Forward-simulate client ticks by accumulating deltaTime.
            _tickAccumulator += deltaTime;

            while (_tickAccumulator >= k_TickDuration && _recoveryTicks == 0)
            {
                _tickAccumulator -= k_TickDuration;
                _clientTick++;

                // Flush pending inputs for this tick.
                _inputBuffer.FlushForTick(_clientTick, network);

                // Advance the speculative overlay by one tick.
                _overlay.AdvanceTick(_clientTick);

                // Predict local changes for this tick (no-op if no input was queued).
                var predicted = _inputBuffer.PeekPredicted(_clientTick);
                if (predicted.HasValue)
                {
                    ApplyLocalPrediction(predicted.Value, network);
                }
            }

            // Decrement recovery ticks after reconciliation completes.
            if (_recoveryTicks > 0)
            {
                _recoveryTicks--;
            }
        }

        /// <summary>
        /// Record a sync packet from the server for clock alignment.
        ///
        /// The server timestamp anchors the client's tick counter to the authoritative
        /// timeline. The time offset is computed as the difference between the server's
        /// reported time and the client's local time at receipt. This allows the client
        /// to maintain a consistent view even when frames are dropped or jittered.
        /// </summary>
        /// <param name="sync">Sync packet received from the server.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnSyncPacket(in SyncPacket sync)
        {
            // Update time offset from the sync packet's timestamp delta.
            float serverTime = sync.ServerTime;
            float clientReceiptTime = sync.ClientReceiptTime;
            _timeOffset = serverTime - clientReceiptTime;

            // Track server tick for jump detection (triggers reconciliation).
            uint previousServerTick = _serverTick;
            _serverTick = sync.ServerTick;

            if (_lastSyncTick > 0 && _serverTick != _lastSyncTick + 1)
            {
                // Server has advanced by more than one tick — possible lag or replay.
                // The client needs to reconcile its state against the server's confirmed history.
                int fromTick = (int)(_clientTick > previousServerTick ? previousServerTick : _clientTick);
                int toTick = (int)_serverTick;

                if (toTick - fromTick >= 1)
                {
                    _needsReconciliation = true;
                    _reconciliation.Initialize(fromTick, toTick);
                }
            }

            // Align client tick to server if the server has moved ahead significantly.
            uint tickDelta = (uint)Math.Abs((long)_serverTick - _clientTick);
            if (tickDelta > k_ServerTickRate)
            {
                // Client is more than one second behind: fast-advance without simulation.
                _clientTick = _serverTick;
                _tickAccumulator = 0f;
            }

            _lastSyncTick = _serverTick;
        }

        /// <summary>
        /// Push a player input into the buffer for sending and future reconciliation.
        ///
        /// The input is stored redundantly so it can be replayed during reconciliation if
        /// the server's response diverges from the client's prediction (Constitution Principle III).
        /// </summary>
        /// <param name="input">Player input to buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushInput(in C_PlayerInput input)
        {
            _inputBuffer.Push(input);
        }

        /// <summary>
        /// Get a reference to the speculative overlay for rendering and collision queries.
        ///
        /// The returned overlay is live — changes from incoming events and reconciliation
        /// are reflected immediately. This is what both the render and collision subsystems
        /// read during a frame.
        /// </summary>
        public SpeculativeOverlay Overlay => _overlay;

        /// <summary>Current server-confirmed tick.</summary>
        public uint ServerTick => _serverTick;

        /// <summary>Current client-predicted tick (may lag behind server during reconciliation).</summary>
        public uint ClientTick => _clientTick;

        /// <summary>True when reconciliation is in progress and the client should not simulate forward.</summary>
        public bool IsReconciling => _needsReconciliation;

        /// <summary>Set or update the world history reference used for reconciliation replay queries.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetWorldHistory(ref WorldHistory history) => _worldHistory = history;

        // -- internal helpers -----------------------------------------------------

        private void ApplyLocalPrediction(in C_PlayerInput input, Network network)
        {
            if (input.actionType == (byte)C_PlayerInput.ActionType.None)
                return;

            // Convert the player's action into an AlterationEvent for overlay application.
            var evt = CreateAlterationFromInput(input);
            _overlay.ApplyPending(in evt);
            network.SendLocalPrediction(evt);
        }

        private void DoReconciliation(Network network)
        {
            var (fromTick, toTick) = _reconciliation.GetCurrentRange();

            // Fetch historical world state for each region that changed.
            if (_worldHistory.OldestTick <= (uint)fromTick)
            {
                _reconciliation.Replay(fromTick, toTick, ref _worldHistory);
                _overlay.ApplyReconciliationResult(_reconciliation.GetResult());

                // Re-apply any client inputs that occurred after the reconciliation point.
                uint recoveryStart = (uint)toTick;
                for (uint t = recoveryStart; t <= _clientTick; t++)
                {
                    var input = _inputBuffer.TryPopAtTickLocal(t, out var result);
                    if (input && result.HasValue)
                    {
                        var evt = CreateAlterationFromInput(result.Value);
                        _overlay.ApplyPending(in evt);
                    }
                }

                _needsReconciliation = false;
                _recoveryTicks = 3; // Brief recovery period to prevent thrash.
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AlterationEvent CreateAlterationFromInput(in C_PlayerInput input)
        {
            // Convert a player action into an AlterationEvent for speculative overlay.
            return new AlterationEvent(
                kind: input.toolMaterial,          // material index as alteration kind for simplicity.
                tick: input.tick,
                origin: (int3)input.Position(),     // quantized position to voxel coordinates.
                shapeRadius: 1,                     // single-brick radius for direct placement.
                material: input.toolMaterial,
                seed: unchecked((uint)input.playerId),
                playerId: input.playerId,
                sequence: input.sequence);
        }

        /// <summary>Dispose all managed and native resources owned by this tick loop.</summary>
        public void Dispose()
        {
            _overlay.Dispose();
            _reconciliation.Dispose();
            _inputBuffer.Dispose();
        }
    }

    /// <summary>
    /// Sync packet received from the server for client clock alignment.
    /// Carries the server's authoritative time and tick so the client can maintain a
    /// consistent simulation timeline (Constitution Principle III).
    /// </summary>
    public struct SyncPacket
    {
        /// <summary>Authoritative server time in seconds from session start.</summary>
        public float ServerTime;

        /// <summary>Authoritative server tick number at the time this packet was authored.</summary>
        public uint ServerTick;

        /// <summary>Client's local time (seconds) when this packet was received, set by the client layer.</summary>
        public float ClientReceiptTime;
    }

    /// <summary>
    /// Minimal network interface required by ClientTickLoop for sending inputs and receiving events.
    /// Concrete implementations are provided by the transport layer (Unity Transport or custom).
    /// </summary>
    public abstract class Network
    {
        /// <summary>Flush any pending outbound messages that were queued by PushInput calls.</summary>
        public abstract void Flush();

        /// <summary>Process all incoming messages on the event channel.</summary>
        public abstract int ProcessEvents();

        /// <summary>Sends a speculative overlay prediction to the server for acceptance.</summary>
        public abstract void SendLocalPrediction(in AlterationEvent evt);
    }
}
