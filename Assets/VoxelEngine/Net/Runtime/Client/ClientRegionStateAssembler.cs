using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Transport-side assembler for fragmented-reliable BULK region state. It only copies/validates
    /// bytes; authoritative world replacement happens later from ClientNetworkRuntime's explicit
    /// world-update call.
    /// </summary>
    public sealed class ClientRegionStateAssembler
    {
        public const int MaxCompletedSnapshots = 4;
        public const int MaxCompletedSnapshotBytes = 32 * 1024 * 1024;

        private readonly Queue<CompletedRegionState> _completed = new Queue<CompletedRegionState>(2);
        private uint _activeTransferId;
        private int3 _activeRegion;
        private uint _activeTick;
        private uint _activeHash;
        private byte[] _activeBytes;
        private int _activeReceived;
        private int _completedBytes;

        public bool IsReceiving => _activeTransferId != 0;
        public int ActiveBytesReceived => _activeReceived;
        public int CompletedCount => _completed.Count;
        public int CompletedBytes => _completedBytes;

        public bool TryAcceptPacket(ReadOnlySpan<byte> packet)
        {
            if (!RegionStateChunkPacket.TryDecode(packet, out var header, out ReadOnlySpan<byte> chunk))
                return false;

            if (_activeTransferId == 0)
            {
                if (header.Offset != 0 || _completed.Count >= MaxCompletedSnapshots ||
                    _completedBytes + header.TotalLength > MaxCompletedSnapshotBytes)
                    return false;

                _activeTransferId = header.TransferId;
                _activeRegion = header.RegionCoord;
                _activeTick = header.SnapshotTick;
                _activeHash = header.SemanticHash;
                _activeBytes = new byte[header.TotalLength];
                _activeReceived = 0;
            }
            else if (header.TransferId != _activeTransferId ||
                     !header.RegionCoord.Equals(_activeRegion) ||
                     header.SnapshotTick != _activeTick ||
                     header.SemanticHash != _activeHash ||
                     header.TotalLength != _activeBytes.Length ||
                     header.Offset != _activeReceived)
            {
                return false;
            }

            chunk.CopyTo(_activeBytes.AsSpan(_activeReceived, chunk.Length));
            _activeReceived += chunk.Length;

            if (_activeReceived != _activeBytes.Length)
                return !header.IsFinal;
            if (!header.IsFinal)
                return false;

            byte[] completedBytes = _activeBytes;
            _completed.Enqueue(new CompletedRegionState(
                _activeTransferId,
                _activeRegion,
                _activeTick,
                _activeHash,
                completedBytes));
            _completedBytes += completedBytes.Length;
            ClearActive();
            return true;
        }

        public bool TryDequeue(out CompletedRegionState completed)
        {
            if (_completed.Count == 0)
            {
                completed = default;
                return false;
            }

            completed = _completed.Dequeue();
            _completedBytes -= completed.Snapshot.Length;
            if (_completedBytes < 0) _completedBytes = 0;
            return true;
        }

        public void Reset()
        {
            ClearActive();
            _completed.Clear();
            _completedBytes = 0;
        }

        private void ClearActive()
        {
            _activeTransferId = 0;
            _activeRegion = default;
            _activeTick = 0;
            _activeHash = 0;
            _activeBytes = null;
            _activeReceived = 0;
        }

        public readonly struct CompletedRegionState
        {
            public readonly uint TransferId;
            public readonly int3 RegionCoord;
            public readonly uint SnapshotTick;
            public readonly uint SemanticHash;
            public readonly byte[] Snapshot;

            public CompletedRegionState(
                uint transferId,
                int3 regionCoord,
                uint snapshotTick,
                uint semanticHash,
                byte[] snapshot)
            {
                TransferId = transferId;
                RegionCoord = regionCoord;
                SnapshotTick = snapshotTick;
                SemanticHash = semanticHash;
                Snapshot = snapshot;
            }
        }
    }
}
