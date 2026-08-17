using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// A coarse, permanent record of built content — the castle, Kentridge, anything authored or
    /// edited into the voxel world — so it can be drawn at the same distance terrain is.
    ///
    /// <para><b>The problem this solves.</b> Terrain renders to the horizon because
    /// <see cref="TerrainSampler"/> is a pure function: any coordinate can be answered with no
    /// storage. Built content is not a function, it is voxels, and resident voxel regions are too
    /// expensive to retain to the horizon.</para>
    ///
    /// <para><b>What this stores instead.</b> One height per coarse column — 16x16 columns per
    /// region, about 3.2 m each — capturing how far the built surface rises above the terrain
    /// the height function already describes. The capture path consumes only Storage.Api's
    /// borrowed read view; the far-field cache never sees physical region/brick representation.</para>
    ///
    /// <para>The trade is honest and deliberate: at range a structure becomes a silhouette with
    /// no overhangs, no interior, and no destruction detail. That is the same trade every voxel
    /// game makes for distant chunks, and it is invisible at the distances this is used.</para>
    /// </summary>
    public sealed class FarFieldStructureStore
    {
        /// <summary>Coarse columns per region edge. 16 gives 3.2 m columns at 10 cm voxels.</summary>
        public const int ColumnsPerRegion = 16;

        /// <summary>Voxels spanned by one coarse column.</summary>
        public const int VoxelsPerColumn = ShowcaseWorld.RegionVoxelEdge / ColumnsPerRegion;

        /// <summary>
        /// A column must rise this far above the analytic terrain before it counts as built
        /// content. Terrain generation rounds and the coarse column takes a maximum, so a small
        /// margin keeps ordinary ground from being recorded as structure everywhere.
        /// </summary>
        public const int MinRiseVoxels = 24;

        private readonly Dictionary<int2, int[]> _columns = new();

        /// <summary>Regions holding built content. Sparse by construction.</summary>
        public int RecordedRegionCount => _columns.Count;

        /// <summary>
        /// Bumped whenever recorded content changes. Consumers cache meshes built from this
        /// data and would otherwise never show a structure that finished generating after
        /// their last rebuild — the castle completes long after the rings first build.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// Scans a freshly generated region and records any coarse column whose solid surface
        /// stands above the terrain height field. Called once per region, after generation.
        /// </summary>
        public void CaptureRegion(int3 regionCoord, IRegionReadSource storage, uint seed)
        {
            if (storage == null || !storage.TryAcquireRegion(regionCoord, out RegionReadView region))
                return;

            int3 originVoxel = regionCoord * ShowcaseWorld.RegionVoxelEdge;
            int[] heights = null;

            for (int cz = 0; cz < ColumnsPerRegion; cz++)
            for (int cx = 0; cx < ColumnsPerRegion; cx++)
            {
                int voxelX = originVoxel.x + cx * VoxelsPerColumn + VoxelsPerColumn / 2;
                int voxelZ = originVoxel.z + cz * VoxelsPerColumn + VoxelsPerColumn / 2;

                int top = TopSolidVoxel(in region, voxelX, voxelZ,
                                        originVoxel.y, ShowcaseWorld.RegionVoxelEdge);
                if (top == int.MinValue) continue;

                int terrain = TerrainSampler.HeightAt(voxelX, voxelZ, seed);
                if (top - terrain < MinRiseVoxels) continue;

                heights ??= NewColumnArray();
                int index = cx + cz * ColumnsPerRegion;
                if (top > heights[index]) heights[index] = top;
            }

            if (heights == null) return;

            var key = new int2(regionCoord.x, regionCoord.z);
            if (_columns.TryGetValue(key, out int[] existing))
            {
                for (int i = 0; i < existing.Length; i++)
                    if (heights[i] > existing[i]) existing[i] = heights[i];
                Version++;
                return;
            }
            _columns[key] = heights;
            Version++;
        }

        /// <summary>
        /// Built-surface height in voxels at a world column, or <c>int.MinValue</c> where no
        /// structure was recorded and the caller should use the terrain height field.
        /// </summary>
        public int HeightAt(int worldVoxelX, int worldVoxelZ)
        {
            int regionX = FloorDiv(worldVoxelX, ShowcaseWorld.RegionVoxelEdge);
            int regionZ = FloorDiv(worldVoxelZ, ShowcaseWorld.RegionVoxelEdge);
            if (!_columns.TryGetValue(new int2(regionX, regionZ), out int[] heights))
                return int.MinValue;

            int localX = worldVoxelX - regionX * ShowcaseWorld.RegionVoxelEdge;
            int localZ = worldVoxelZ - regionZ * ShowcaseWorld.RegionVoxelEdge;
            int cx = localX / VoxelsPerColumn;
            int cz = localZ / VoxelsPerColumn;
            int value = heights[cx + cz * ColumnsPerRegion];
            return value == 0 ? int.MinValue : value;
        }

        public void Clear() { _columns.Clear(); Version++; }

        private static int[] NewColumnArray() => new int[ColumnsPerRegion * ColumnsPerRegion];

        /// <summary>
        /// Topmost solid voxel in a column within one region's vertical span. Skip empty 8^3
        /// blocks through the compact block-ref/occupancy summary first; only the top occupied
        /// mixed block needs up to eight cell reads. This keeps terminal castle far-field capture
        /// bounded to roughly 64 block checks per coarse column instead of 512 cell reads.
        /// </summary>
        private static int TopSolidVoxel(in RegionReadView region,
                                         int worldX, int worldZ, int baseY, int height)
        {
            int3 originVoxel = region.RegionCoord * ShowcaseWorld.RegionVoxelEdge;
            int localX = worldX - originVoxel.x;
            int localZ = worldZ - originVoxel.z;
            int maxLocalY = height - 1;
            int blockX = localX >> VoxelReadGrid.BlockEdgeLog2;
            int blockZ = localZ >> VoxelReadGrid.BlockEdgeLog2;
            int topBlockY = maxLocalY >> VoxelReadGrid.BlockEdgeLog2;

            for (int blockY = topBlockY; blockY >= 0; blockY--)
            {
                int3 localBlock = new(blockX, blockY, blockZ);
                if (!region.IsBlockOccupied(localBlock)
                    || !region.TryGetBlock(localBlock, out VoxelReadBlock block))
                    continue;

                int blockBaseY = blockY << VoxelReadGrid.BlockEdgeLog2;
                int maxInnerY = math.min(VoxelReadGrid.BlockEdge - 1,
                                         maxLocalY - blockBaseY);
                if (block.Kind == VoxelReadBlockKind.Uniform)
                {
                    if (block.UniformMaterial != VoxelGrid.MaterialEmpty)
                        return baseY + blockBaseY + maxInnerY;
                    continue;
                }

                for (int innerY = maxInnerY; innerY >= 0; innerY--)
                {
                    int localY = blockBaseY + innerY;
                    if (!region.TryReadCell(
                            new int3(localX, localY, localZ), out VoxelCell cell))
                        continue;
                    if (cell.BaseMaterialId != VoxelGrid.MaterialEmpty)
                        return baseY + localY;
                }
            }
            return int.MinValue;
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            if (value % divisor != 0 && (value < 0) != (divisor < 0)) quotient--;
            return quotient;
        }
    }
}
