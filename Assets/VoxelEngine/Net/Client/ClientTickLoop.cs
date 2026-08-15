using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Edits;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Server;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Legacy client tick scaffold retained for source compatibility. New sessions use
    /// ClientNetworkRuntime and ClientPredictionReconciler. This scaffold consumes the canonical
    /// intent-only C_PlayerInput and never assumes client-owned player identity or world position.
    /// </summary>
    [Obsolete("Use ClientNetworkRuntime for live networking and prediction.")]
    public sealed class ClientTickLoop : IDisposable
    {
        private const int k_ServerTickRate = 30;
        private const float k_TickDuration = 1f / k_ServerTickRate;

        private uint _serverTick;
        private uint _clientTick;
        private float _tickAccumulator;
        private float _timeOffset;
        private uint _lastSyncTick;
        private readonly InputBuffer _inputBuffer;
        private readonly SpeculativeOverlay _overlay;
        private readonly Reconciliation _reconciliation;
        private WorldHistory _worldHistory;
        private bool _needsReconciliation;
        private uint _recoveryTicks;

        public ClientTickLoop(int inputBufferSize, int historyCapacity)
        {
            _inputBuffer = new InputBuffer(inputBufferSize);
            _overlay = new SpeculativeOverlay();
            _reconciliation = new Reconciliation();
        }

        public void Update(float deltaTime, Network network)
        {
            _inputBuffer.FlushReceived(network);
            if (_needsReconciliation)
            {
                DoReconciliation(network);
                return;
            }

            _tickAccumulator += deltaTime;
            while (_tickAccumulator >= k_TickDuration && _recoveryTicks == 0)
            {
                _tickAccumulator -= k_TickDuration;
                _clientTick++;
                _inputBuffer.FlushForTick(_clientTick, network);
                _overlay.AdvanceTick(_clientTick);
                C_PlayerInput? predicted = _inputBuffer.PeekPredicted(_clientTick);
                if (predicted.HasValue)
                    ApplyLocalPrediction(predicted.Value, network);
            }

            if (_recoveryTicks > 0)
                _recoveryTicks--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnSyncPacket(in SyncPacket sync)
        {
            _timeOffset = sync.ServerTime - sync.ClientReceiptTime;
            uint previousServerTick = _serverTick;
            _serverTick = sync.ServerTick;

            if (_lastSyncTick > 0 && _serverTick != _lastSyncTick + 1)
            {
                int fromTick = (int)(_clientTick > previousServerTick ? previousServerTick : _clientTick);
                int toTick = (int)_serverTick;
                if (toTick - fromTick >= 1)
                {
                    _needsReconciliation = true;
                    _reconciliation.Initialize(fromTick, toTick);
                }
            }

            uint tickDelta = (uint)Math.Abs((long)_serverTick - _clientTick);
            if (tickDelta > k_ServerTickRate)
            {
                _clientTick = _serverTick;
                _tickAccumulator = 0f;
            }
            _lastSyncTick = _serverTick;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushInput(in C_PlayerInput input) => _inputBuffer.Push(input);

        public SpeculativeOverlay Overlay => _overlay;
        public uint ServerTick => _serverTick;
        public uint ClientTick => _clientTick;
        public bool IsReconciling => _needsReconciliation;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetWorldHistory(ref WorldHistory history) => _worldHistory = history;

        private void ApplyLocalPrediction(in C_PlayerInput input, Network network)
        {
            C_PlayerInput.ActionBits actions = input.Actions;
            if ((actions & (C_PlayerInput.ActionBits.UseMain | C_PlayerInput.ActionBits.UseAlt)) == 0)
                return;

            AlterationEvent evt = CreateAlterationFromInput(input);
            _overlay.ApplyPending(in evt);
            network.SendLocalPrediction(evt);
        }

        private void DoReconciliation(Network network)
        {
            var range = _reconciliation.GetCurrentRange();
            int fromTick = range.fromTick;
            int toTick = range.toTick;
            if (_worldHistory.OldestTick > (uint)fromTick)
                return;

            _reconciliation.Replay(fromTick, toTick, ref _worldHistory);
            _overlay.ApplyReconciliationResult(_reconciliation.GetResult());

            for (uint tick = (uint)toTick; tick <= _clientTick; tick++)
            {
                bool found = _inputBuffer.TryPopAtTickLocal(tick, out C_PlayerInput? input);
                if (found && input.HasValue)
                {
                    AlterationEvent evt = CreateAlterationFromInput(input.Value);
                    _overlay.ApplyPending(in evt);
                }
                if (tick == uint.MaxValue) break;
            }

            _needsReconciliation = false;
            _recoveryTicks = 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AlterationEvent CreateAlterationFromInput(in C_PlayerInput input)
        {
            float2 movement = input.Movement();
            byte kind = (input.Actions & C_PlayerInput.ActionBits.UseAlt) != 0
                ? AlterationEvent.KindExplosion
                : AlterationEvent.KindBrush;

            return new AlterationEvent(
                kind,
                input.tick,
                new int3((int)math.round(movement.x), 0, (int)math.round(movement.y)),
                1,
                input.toolMaterial,
                input.sequence,
                0,
                input.sequence);
        }

        public void Dispose()
        {
            _overlay.Dispose();
            _reconciliation.Dispose();
            _inputBuffer.Dispose();
        }
    }

    public struct SyncPacket
    {
        public float ServerTime;
        public uint ServerTick;
        public float ClientReceiptTime;
    }

    public abstract class Network
    {
        public abstract void Flush();
        public abstract int ProcessEvents();
        public abstract void SendLocalPrediction(in AlterationEvent evt);
    }
}
