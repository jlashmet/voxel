using VoxelEngine.Net.Server;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Reconciliation engine that replays client inputs against historical world state
    /// to resolve divergence between client speculative predictions and server authority.
    /// Historical snapshots are logical block-state data; reconciliation never depends on
    /// Storage's physical Region/brick representation.
    /// </summary>
    public sealed class Reconciliation : IDisposable
    {
        private int _fromTick;
        private int _toTick;
        private bool _initialized;
        private NativeHashMap<int3, BrickReconResult> _modifiedBricks;
        private bool _hadRollback;

        public Reconciliation()
        {
            _fromTick = 0;
            _toTick = 0;
            _initialized = false;
            _modifiedBricks = new NativeHashMap<int3, BrickReconResult>(64, Allocator.Persistent);
            _hadRollback = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Initialize(int fromTick, int toTick)
        {
            _fromTick = fromTick;
            _toTick = toTick;
            _initialized = true;
            _hadRollback = false;
            if (_modifiedBricks.IsCreated) _modifiedBricks.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Replay(int fromTick, int toTick, ref WorldHistory history)
        {
            _hadRollback = false;
            var affectedRegions = new NativeList<int3>(8, Allocator.Temp);

            if (_modifiedBricks.IsCreated && _modifiedBricks.Count > 0)
            {
                foreach (var kvp in _modifiedBricks)
                {
                    int3 blockCoord = kvp.Key;
                    int3 regionCoord = blockCoord >> VoxelReadGrid.BlocksPerRegionEdgeLog2;

                    bool found = false;
                    foreach (int3 existing in affectedRegions)
                    {
                        if (math.all(existing == regionCoord))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        affectedRegions.Add(regionCoord);
                }
            }

            for (int tick = fromTick; tick < toTick; tick++)
            {
                foreach (int3 regionCoord in affectedRegions)
                {
                    if (!history.TrySnapshot((uint)tick, regionCoord, out var snapshot))
                        continue;

                    ReplayTickForRegion(regionCoord, snapshot);
                }
            }

            affectedRegions.Dispose();
        }

        public (int fromTick, int toTick) GetCurrentRange() => (_fromTick, _toTick);

        public ReconciliationResult GetResult()
        {
            return new ReconciliationResult
            {
                ModifiedBricks = _modifiedBricks,
                HadRollback = _hadRollback
            };
        }

        public bool HadRollback => _hadRollback;

        private void ReplayTickForRegion(int3 regionCoord, in NativeArray<byte> snapshot)
        {
            int blocksPerAxis = VoxelReadGrid.BlocksPerRegionEdge;

            for (int bx = 0; bx < blocksPerAxis; bx++)
            {
                for (int by = 0; by < blocksPerAxis; by++)
                {
                    for (int bz = 0; bz < blocksPerAxis; bz++)
                    {
                        int blockIndex = BlockIndex(bx, by, bz);
                        if ((uint)blockIndex >= (uint)snapshot.Length)
                            continue;

                        byte serverMaterial = snapshot[blockIndex];
                        if (serverMaterial == VoxelGrid.MaterialEmpty)
                            continue;

                        int3 blockCoord = new int3(
                            regionCoord.x * blocksPerAxis + bx,
                            regionCoord.y * blocksPerAxis + by,
                            regionCoord.z * blocksPerAxis + bz);

                        if (!_modifiedBricks.TryGetValue(blockCoord, out var result))
                            continue;

                        if (result.ClientMaterial != serverMaterial)
                        {
                            _hadRollback = true;
                            result.MatchesServer = false;
                            result.ServerMaterial = serverMaterial;
                            _modifiedBricks[blockCoord] = result;
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BlockIndex(int x, int y, int z) =>
            x
            | (y << VoxelReadGrid.BlocksPerRegionEdgeLog2)
            | (z << (VoxelReadGrid.BlocksPerRegionEdgeLog2 * 2));

        public void Dispose()
        {
            if (_modifiedBricks.IsCreated) _modifiedBricks.Dispose();
        }
    }
}
