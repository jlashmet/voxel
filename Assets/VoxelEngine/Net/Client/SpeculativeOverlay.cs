using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Pending voxel modification keyed by logical world block coordinate.
    /// </summary>
    public struct PendingVoxel
    {
        public byte material;
        public uint tick;
        public bool confirmed;
    }

    /// <summary>
    /// Client-local speculative overlay. The overlay owns only speculative metadata; promotion of a
    /// confirmed block goes through Storage.Api so Net never owns physical region/brick allocation.
    /// </summary>
    public sealed class SpeculativeOverlay : IDisposable
    {
        private NativeHashMap<int3, PendingVoxel> _pending;
        private uint _highestTick;
        private ReconciliationResult _reconResult;

        public SpeculativeOverlay()
        {
            _pending = new NativeHashMap<int3, PendingVoxel>(64, Allocator.Persistent);
            _highestTick = 0;
            _reconResult = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyPending(in AlterationEvent evt)
        {
            int3 startBlock = new int3(
                evt.origin.x >> VoxelReadGrid.BlockEdgeLog2,
                evt.origin.y >> VoxelReadGrid.BlockEdgeLog2,
                evt.origin.z >> VoxelReadGrid.BlockEdgeLog2);

            ushort radius = evt.kind == AlterationEvent.KindExplosion ? evt.Radius() : (ushort)1;

            for (int bx = -radius; bx <= radius; bx++)
            for (int by = -radius; by <= radius; by++)
            for (int bz = -radius; bz <= radius; bz++)
            {
                int3 blockCoord = startBlock + new int3(bx, by, bz);
                int dist2 = bx * bx + by * by + bz * bz;
                if (evt.kind == AlterationEvent.KindExplosion && dist2 > radius * radius)
                    continue;

                _pending[blockCoord] = new PendingVoxel
                {
                    material = evt.material,
                    tick = evt.tick,
                    confirmed = false,
                };
            }

            _highestTick = math.max(_highestTick, evt.tick);
        }

        /// <summary>
        /// Promote every pending block through <see cref="IRegionMutationStore"/> up to the
        /// confirmed tick. A confirmed entry for a non-resident region is dropped, matching the
        /// previous behavior: authoritative state will arrive with the region when it is streamed.
        /// </summary>
        public void ConfirmTick(uint tick, IRegionMutationStore storage)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));

            var keys = _pending.GetKeyArray(Allocator.Temp);
            foreach (int3 blockCoord in keys)
            {
                if (!_pending.TryGetValue(blockCoord, out PendingVoxel entry) || entry.tick > tick)
                    continue;

                int3 regionCoord = new int3(
                    blockCoord.x >> VoxelReadGrid.BlocksPerRegionEdgeLog2,
                    blockCoord.y >> VoxelReadGrid.BlocksPerRegionEdgeLog2,
                    blockCoord.z >> VoxelReadGrid.BlocksPerRegionEdgeLog2);

                if (storage.IsRegionResident(regionCoord))
                    storage.SetWholeBlock(blockCoord, entry.material, markHardSurface: false);

                _pending.Remove(blockCoord);
            }
            keys.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RejectTick(uint tick, Span<byte> reason)
        {
            var keys = _pending.GetKeyArray(Allocator.Temp);
            foreach (int3 blockCoord in keys)
            {
                if (_pending.TryGetValue(blockCoord, out PendingVoxel entry) && entry.tick <= tick)
                    _pending.Remove(blockCoord);
            }
            keys.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeArray<int3> GetRenderedState(Allocator allocator) => _pending.GetKeyArray(allocator);

        public bool HasPending => _pending.Count > 0;
        public int PendingCount => _pending.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPendingMaterial(int3 blockCoord, out byte material)
        {
            if (_pending.TryGetValue(blockCoord, out PendingVoxel entry))
            {
                material = entry.material;
                return true;
            }
            material = 0;
            return false;
        }

        public void Clear() => _pending.Clear();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyReconciliationResult(in ReconciliationResult result)
        {
            foreach (var kvp in result.ModifiedBricks)
            {
                if (!kvp.Value.MatchesServer)
                    _pending.Remove(kvp.Key);
            }
        }

        public void Dispose()
        {
            if (_pending.IsCreated) _pending.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTick(uint tick) => _highestTick = math.max(_highestTick, tick);

        public ReconciliationResult GetResult() => _reconResult;
    }

    public struct ReconciliationResult
    {
        public NativeHashMap<int3, BrickReconResult> ModifiedBricks;
        public bool HadRollback { get; set; }
    }

    public struct BrickReconResult
    {
        public bool MatchesServer;
        public byte ServerMaterial;
        public byte ClientMaterial;
    }
}
