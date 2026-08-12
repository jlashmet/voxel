using System;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Buffers one ordered semantic region repair. Network callbacks only copy bytes here; region
    /// mutation happens later through TryApplyCompleted on the client world-update path.
    /// </summary>
    public sealed class ClientRegionRepairAssembler
    {
        private byte[] _snapshot;
        private int _received;
        private int3 _regionCoord;
        private uint _snapshotTick;
        private uint _semanticHash;
        private bool _complete;

        public bool IsActive => _snapshot != null;
        public bool IsComplete => _complete;
        public int3 RegionCoord => _regionCoord;
        public uint SnapshotTick => _snapshotTick;
        public uint SemanticHash => _semanticHash;
        public int ReceivedBytes => _received;
        public int TotalBytes => _snapshot?.Length ?? 0;

        public bool TryAcceptPacket(ReadOnlySpan<byte> packet)
        {
            if (!RegionRepairChunkPacket.TryDecode(packet, out var header, out ReadOnlySpan<byte> chunk))
                return false;
            if (header.TotalLength > SemanticRegionSnapshotCodec.DefaultMaxSnapshotBytes)
                return false;

            if (_snapshot == null)
            {
                if (header.Offset != 0)
                    return false;

                _snapshot = new byte[header.TotalLength];
                _regionCoord = header.RegionCoord;
                _snapshotTick = header.SnapshotTick;
                _semanticHash = header.SemanticHash;
                _received = 0;
                _complete = false;
            }
            else if (!_regionCoord.Equals(header.RegionCoord) ||
                     _snapshotTick != header.SnapshotTick ||
                     _semanticHash != header.SemanticHash ||
                     _snapshot.Length != header.TotalLength ||
                     header.Offset != _received)
            {
                return false;
            }

            chunk.CopyTo(_snapshot.AsSpan(_received, chunk.Length));
            _received += chunk.Length;
            _complete = _received == _snapshot.Length;
            return true;
        }

        /// <summary>
        /// Apply an assembled snapshot only when the paused authority queue expects exactly this
        /// region/tick/hash checkpoint. After replacement, recompute the semantic hash before resume.
        /// </summary>
        public bool TryApplyCompleted(
            ref RegionTable table,
            ref BrickPool pool,
            ClientAuthoritativeEventQueue authorityQueue)
        {
            if (!_complete || authorityQueue == null || !authorityQueue.RepairPending ||
                !authorityQueue.RepairRegion.Equals(_regionCoord) ||
                authorityQueue.RepairTick != _snapshotTick ||
                authorityQueue.RepairHash != _semanticHash)
                return false;

            if (!SemanticRegionSnapshotCodec.TryApply(
                    ref table,
                    ref pool,
                    _regionCoord,
                    _snapshot))
                return false;

            if (!table.TryGetRegion(_regionCoord, out Region region) ||
                SemanticRegionHasher.HashRegion(in region, in pool) != _semanticHash ||
                !authorityQueue.CompleteRepair(_regionCoord, _snapshotTick, _semanticHash))
                return false;

            Reset();
            return true;
        }

        public void Reset()
        {
            _snapshot = null;
            _received = 0;
            _regionCoord = default;
            _snapshotTick = 0;
            _semanticHash = 0;
            _complete = false;
        }
    }
}
