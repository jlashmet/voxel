using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.StructuralIntegrity.Runtime
{
    /// <summary>Identifies unsupported logical blocks; applying resulting mutations belongs to Edits.</summary>
    public static class CollapseDetection
    {
        public const byte DefaultThreshold = 2;
        private const int BlocksPerRegion = VoxelReadGrid.BlocksPerRegionEdge
                                          * VoxelReadGrid.BlocksPerRegionEdge
                                          * VoxelReadGrid.BlocksPerRegionEdge;

        public static NativeList<int3> FindCollapseTargets(
            IRegionReadSource storage,
            int3 regionCoord,
            in NativeArray<byte> supportValues,
            byte threshold)
        {
            if (supportValues.Length != BlocksPerRegion)
                throw new ArgumentException(
                    $"supportValues must have length {BlocksPerRegion}.", nameof(supportValues));

            var result = new NativeList<int3>(64, Allocator.Temp);
            if (storage == null || !storage.TryAcquireRegion(regionCoord, out RegionReadView region))
                return result;

            for (int i = 0; i < BlocksPerRegion; i++)
            {
                int3 blockCoord = BlockCoords(i);
                if (!region.TryGetBlock(blockCoord, out VoxelReadBlock block)
                    || block.Kind != VoxelReadBlockKind.Mixed
                    || supportValues[i] > threshold)
                    continue;
                result.Add(blockCoord);
            }
            return result;
        }

        public static NativeList<int3> FindUnsupportedBuilds(
            IRegionReadSource storage,
            int3 regionCoord,
            in NativeArray<byte> supportValues) =>
            FindCollapseTargets(storage, regionCoord, in supportValues, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 BlockCoords(int index) => new int3(
            index & VoxelReadGrid.BlocksPerRegionEdgeMask,
            (index >> VoxelReadGrid.BlocksPerRegionEdgeLog2) & VoxelReadGrid.BlocksPerRegionEdgeMask,
            (index >> (VoxelReadGrid.BlocksPerRegionEdgeLog2 * 2)) & VoxelReadGrid.BlocksPerRegionEdgeMask);
    }
}
