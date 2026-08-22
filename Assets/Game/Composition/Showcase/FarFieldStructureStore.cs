using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// A coarse, permanent record of authored world surfaces — the castle, Kentridge, terrain
    /// sculpts, and other generated content — so they can be drawn at the same distance terrain is.
    ///
    /// <para><b>The problem this solves.</b> Terrain renders to the horizon because
    /// <see cref="TerrainSampler"/> is a pure function: any coordinate can be answered with no
    /// storage. Authored content is not fully described by that function, and resident voxel
    /// regions are too expensive to retain to the horizon.</para>
    ///
    /// <para><b>What this stores instead.</b> One coarse sample per 3.2 m column. Tall authored
    /// surfaces retain the old positive silhouette record. Authored terrain that ends below the
    /// analytic height field is recorded separately as a surface override, so a gorge, cliff cut,
    /// or moat is not filled back in by the fallback terrain proxy.</para>
    ///
    /// <para>The trade is honest and deliberate: at range a structure becomes a silhouette with
    /// no overhangs, no interior, and no destruction detail. Runtime destruction is intentionally
    /// not captured; this store is refreshed only at the world's existing authored-content capture
    /// boundaries.</para>
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
        private readonly Dictionary<int2, int[]> _authoredTerrain = new();

        /// <summary>Regions holding positive built-content silhouettes.</summary>
        public int RecordedRegionCount => _columns.Count;

        /// <summary>Regions holding authored surfaces lower than the analytic terrain field.</summary>
        public int RecordedTerrainRegionCount => _authoredTerrain.Count;

        /// <summary>
        /// Bumped whenever recorded content changes. Consumers cache meshes built from this
        /// data and would otherwise never show content that finished generating after their last
        /// rebuild — the castle completes long after the rings first build.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// Scans an authored region after its current generation stage and records both tall built
        /// silhouettes and any surface that was deliberately sculpted below the analytic terrain.
        ///
        /// The caller already invokes this only at authored-content boundaries (terrain/features,
        /// then again when a landmark finishes). It is not a mutation listener, so ordinary player
        /// destruction never leaks into the permanent far representation.
        /// </summary>
        public void CaptureRegion(int3 regionCoord, IRegionReadSource storage, uint seed)
        {
            if (storage == null || !storage.TryAcquireRegion(regionCoord, out RegionReadView region))
                return;

            int3 originVoxel = regionCoord * ShowcaseWorld.RegionVoxelEdge;
            int[] structureHeights = null;
            int[] loweredTerrain = null;

            for (int cz = 0; cz < ColumnsPerRegion; cz++)
            for (int cx = 0; cx < ColumnsPerRegion; cx++)
            {
                int voxelX = originVoxel.x + cx * VoxelsPerColumn + VoxelsPerColumn / 2;
                int voxelZ = originVoxel.z + cz * VoxelsPerColumn + VoxelsPerColumn / 2;

                int top = TopSolidVoxel(in region, voxelX, voxelZ,
                                        originVoxel.y, ShowcaseWorld.RegionVoxelEdge);
                if (top == int.MinValue) continue;

                int terrain = TerrainSampler.HeightAt(voxelX, voxelZ, seed);
                int index = cx + cz * ColumnsPerRegion;

                // A post-authoring surface below the deterministic height field is authoritative
                // world data, not destruction detail. Keep the lowest authored value ever captured
                // so later eviction/regeneration of plain terrain cannot erase the distant sculpt.
                if (top < terrain)
                {
                    loweredTerrain ??= NewOverrideArray();
                    loweredTerrain[index] = top;
                }

                if (top - terrain < MinRiseVoxels) continue;

                structureHeights ??= NewColumnArray();
                if (top > structureHeights[index]) structureHeights[index] = top;
            }

            var key = new int2(regionCoord.x, regionCoord.z);
            bool changed = MergeStructureColumns(key, structureHeights);
            changed |= MergeLoweredTerrain(key, loweredTerrain);
            if (changed) Version++;
        }

        /// <summary>
        /// Built-surface height in voxels at a world column, or <c>int.MinValue</c> where no
        /// structure was recorded and the caller should use the terrain surface.
        /// </summary>
        public int HeightAt(int worldVoxelX, int worldVoxelZ)
        {
            int regionX = FloorDiv(worldVoxelX, ShowcaseWorld.RegionVoxelEdge);
            int regionZ = FloorDiv(worldVoxelZ, ShowcaseWorld.RegionVoxelEdge);
            if (!_columns.TryGetValue(new int2(regionX, regionZ), out int[] heights))
                return int.MinValue;

            int index = ColumnIndex(worldVoxelX, worldVoxelZ, regionX, regionZ);
            int value = heights[index];
            return value == 0 ? int.MinValue : value;
        }

        /// <summary>
        /// Authored terrain surface in voxels where generation lowered the analytic height field,
        /// or <c>int.MinValue</c> when the ordinary terrain sampler remains authoritative.
        /// </summary>
        public int AuthoredTerrainHeightAt(int worldVoxelX, int worldVoxelZ)
        {
            int regionX = FloorDiv(worldVoxelX, ShowcaseWorld.RegionVoxelEdge);
            int regionZ = FloorDiv(worldVoxelZ, ShowcaseWorld.RegionVoxelEdge);
            if (!_authoredTerrain.TryGetValue(new int2(regionX, regionZ), out int[] heights))
                return int.MinValue;

            int index = ColumnIndex(worldVoxelX, worldVoxelZ, regionX, regionZ);
            return heights[index];
        }

        public void Clear()
        {
            _columns.Clear();
            _authoredTerrain.Clear();
            Version++;
        }

        private static int[] NewColumnArray() => new int[ColumnsPerRegion * ColumnsPerRegion];

        private static int[] NewOverrideArray()
        {
            var values = new int[ColumnsPerRegion * ColumnsPerRegion];
            for (int i = 0; i < values.Length; i++) values[i] = int.MinValue;
            return values;
        }

        private bool MergeStructureColumns(int2 key, int[] heights)
        {
            if (heights == null) return false;
            if (!_columns.TryGetValue(key, out int[] existing))
            {
                _columns[key] = heights;
                return true;
            }

            bool changed = false;
            for (int i = 0; i < existing.Length; i++)
            {
                if (heights[i] <= existing[i]) continue;
                existing[i] = heights[i];
                changed = true;
            }
            return changed;
        }

        private bool MergeLoweredTerrain(int2 key, int[] heights)
        {
            if (heights == null) return false;
            if (!_authoredTerrain.TryGetValue(key, out int[] existing))
            {
                _authoredTerrain[key] = heights;
                return true;
            }

            bool changed = false;
            for (int i = 0; i < existing.Length; i++)
            {
                int incoming = heights[i];
                if (incoming == int.MinValue) continue;
                if (existing[i] != int.MinValue && incoming >= existing[i]) continue;
                existing[i] = incoming;
                changed = true;
            }
            return changed;
        }

        private static int ColumnIndex(int worldVoxelX, int worldVoxelZ, int regionX, int regionZ)
        {
            int localX = worldVoxelX - regionX * ShowcaseWorld.RegionVoxelEdge;
            int localZ = worldVoxelZ - regionZ * ShowcaseWorld.RegionVoxelEdge;
            int cx = localX / VoxelsPerColumn;
            int cz = localZ / VoxelsPerColumn;
            return cx + cz * ColumnsPerRegion;
        }

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
