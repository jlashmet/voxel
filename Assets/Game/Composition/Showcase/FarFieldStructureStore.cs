using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// A coarse, permanent record of authored world surfaces — anonymous built silhouettes and
    /// terrain sculpts — so they can be drawn at the same distance terrain is. Known semantic
    /// buildings may suppress the positive-silhouette fallback once an independent proxy exists;
    /// terrain lowering/material overrides remain independent and authoritative.
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
        private readonly Dictionary<int2, byte[]> _authoredTerrainMaterials = new();
        private readonly Dictionary<int2, bool[]> _suppressedBuiltColumns = new();

        /// <summary>Regions holding positive built-content silhouettes.</summary>
        public int RecordedRegionCount => _columns.Count;

        /// <summary>Regions holding authored surfaces lower than the analytic terrain field.</summary>
        public int RecordedTerrainRegionCount => _authoredTerrain.Count;

        /// <summary>
        /// Bumped whenever recorded content or fallback suppression changes. Consumers cache meshes
        /// built from this data and need to rebuild when the representation source changes.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// Scans an authored region after its current generation stage and records both tall built
        /// silhouettes and any surface that was deliberately sculpted below the analytic terrain.
        /// The caller invokes this only at authored-content boundaries; ordinary player destruction
        /// does not become permanent far presentation state.
        /// </summary>
        public void CaptureRegion(int3 regionCoord, IRegionReadSource storage, uint seed)
        {
            if (storage == null || !storage.TryAcquireRegion(regionCoord, out RegionReadView region))
                return;

            int3 originVoxel = regionCoord * ShowcaseWorld.RegionVoxelEdge;
            int[] structureHeights = null;
            int[] loweredTerrain = null;
            byte[] loweredMaterials = null;

            for (int cz = 0; cz < ColumnsPerRegion; cz++)
            for (int cx = 0; cx < ColumnsPerRegion; cx++)
            {
                int voxelX = originVoxel.x + cx * VoxelsPerColumn + VoxelsPerColumn / 2;
                int voxelZ = originVoxel.z + cz * VoxelsPerColumn + VoxelsPerColumn / 2;

                int top = TopSurfaceVoxel(in region, voxelX, voxelZ,
                                          originVoxel.y, ShowcaseWorld.RegionVoxelEdge,
                                          out byte topMaterial);
                if (top == int.MinValue) continue;

                int terrain = TerrainSampler.HeightAt(voxelX, voxelZ, seed);
                int index = cx + cz * ColumnsPerRegion;

                if (top < terrain)
                {
                    loweredTerrain ??= NewOverrideArray();
                    loweredMaterials ??= new byte[ColumnsPerRegion * ColumnsPerRegion];
                    loweredTerrain[index] = top;
                    loweredMaterials[index] = topMaterial;
                }

                if (top - terrain < MinRiseVoxels) continue;

                structureHeights ??= NewColumnArray();
                if (top > structureHeights[index]) structureHeights[index] = top;
            }

            var key = new int2(regionCoord.x, regionCoord.z);
            bool changed = MergeStructureColumns(key, structureHeights);
            changed |= MergeLoweredTerrain(key, loweredTerrain, loweredMaterials);
            if (changed) Version++;
        }

        /// <summary>
        /// Suppresses only the positive built-silhouette fallback in the supplied world-voxel XZ
        /// bounds. The bounds are generic presentation-space data; callers decide which known
        /// semantic structures have independent proxies. Lowered terrain/material overrides are not
        /// touched, and anonymous built silhouettes outside these bounds remain available.
        /// </summary>
        public void SuppressBuiltSilhouette(
            int minWorldVoxelX,
            int minWorldVoxelZ,
            int maxWorldVoxelXExclusive,
            int maxWorldVoxelZExclusive)
        {
            if (maxWorldVoxelXExclusive <= minWorldVoxelX)
                throw new ArgumentOutOfRangeException(nameof(maxWorldVoxelXExclusive));
            if (maxWorldVoxelZExclusive <= minWorldVoxelZ)
                throw new ArgumentOutOfRangeException(nameof(maxWorldVoxelZExclusive));

            int minRegionX = FloorDiv(minWorldVoxelX, ShowcaseWorld.RegionVoxelEdge);
            int minRegionZ = FloorDiv(minWorldVoxelZ, ShowcaseWorld.RegionVoxelEdge);
            int maxRegionX = FloorDiv(maxWorldVoxelXExclusive - 1, ShowcaseWorld.RegionVoxelEdge);
            int maxRegionZ = FloorDiv(maxWorldVoxelZExclusive - 1, ShowcaseWorld.RegionVoxelEdge);
            bool changed = false;

            for (int regionZ = minRegionZ; regionZ <= maxRegionZ; regionZ++)
            for (int regionX = minRegionX; regionX <= maxRegionX; regionX++)
            {
                var key = new int2(regionX, regionZ);
                if (!_suppressedBuiltColumns.TryGetValue(key, out bool[] suppressed))
                {
                    suppressed = new bool[ColumnsPerRegion * ColumnsPerRegion];
                    _suppressedBuiltColumns.Add(key, suppressed);
                }

                int regionOriginX = regionX * ShowcaseWorld.RegionVoxelEdge;
                int regionOriginZ = regionZ * ShowcaseWorld.RegionVoxelEdge;
                for (int cz = 0; cz < ColumnsPerRegion; cz++)
                for (int cx = 0; cx < ColumnsPerRegion; cx++)
                {
                    int columnMinX = regionOriginX + cx * VoxelsPerColumn;
                    int columnMinZ = regionOriginZ + cz * VoxelsPerColumn;
                    int columnMaxX = columnMinX + VoxelsPerColumn;
                    int columnMaxZ = columnMinZ + VoxelsPerColumn;
                    if (columnMaxX <= minWorldVoxelX || columnMinX >= maxWorldVoxelXExclusive
                        || columnMaxZ <= minWorldVoxelZ || columnMinZ >= maxWorldVoxelZExclusive)
                        continue;

                    int index = cx + cz * ColumnsPerRegion;
                    if (suppressed[index]) continue;
                    suppressed[index] = true;
                    changed = true;
                }
            }

            if (changed) Version++;
        }

        /// <summary>
        /// Returns whether the built-silhouette fallback is suppressed at this world column.
        /// Terrain lowering/material channels are intentionally independent of this result.
        /// </summary>
        public bool IsBuiltSilhouetteSuppressedAt(int worldVoxelX, int worldVoxelZ)
        {
            int regionX = FloorDiv(worldVoxelX, ShowcaseWorld.RegionVoxelEdge);
            int regionZ = FloorDiv(worldVoxelZ, ShowcaseWorld.RegionVoxelEdge);
            if (!_suppressedBuiltColumns.TryGetValue(
                    new int2(regionX, regionZ), out bool[] suppressed))
                return false;

            int index = ColumnIndex(worldVoxelX, worldVoxelZ, regionX, regionZ);
            return suppressed[index];
        }

        /// <summary>
        /// Built-surface height in voxels at a world column, or <c>int.MinValue</c> where no
        /// anonymous fallback structure should be presented.
        /// </summary>
        public int HeightAt(int worldVoxelX, int worldVoxelZ)
        {
            if (IsBuiltSilhouetteSuppressedAt(worldVoxelX, worldVoxelZ))
                return int.MinValue;

            int regionX = FloorDiv(worldVoxelX, ShowcaseWorld.RegionVoxelEdge);
            int regionZ = FloorDiv(worldVoxelZ, ShowcaseWorld.RegionVoxelEdge);
            if (!_columns.TryGetValue(new int2(regionX, regionZ), out int[] heights))
                return int.MinValue;

            int index = ColumnIndex(worldVoxelX, worldVoxelZ, regionX, regionZ);
            int value = heights[index];
            return value == 0 ? int.MinValue : value;
        }

        /// <summary>
        /// Authored terrain surface in voxels where generation lowered the analytic terrain field,
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

        /// <summary>
        /// Material carried by an authored lowered surface. Callers should first confirm that
        /// <see cref="AuthoredTerrainHeightAt"/> returned a real override; empty means no retained
        /// material and preserves compatibility with terrain-only records.
        /// </summary>
        public byte AuthoredTerrainMaterialAt(int worldVoxelX, int worldVoxelZ)
        {
            int regionX = FloorDiv(worldVoxelX, ShowcaseWorld.RegionVoxelEdge);
            int regionZ = FloorDiv(worldVoxelZ, ShowcaseWorld.RegionVoxelEdge);
            if (!_authoredTerrainMaterials.TryGetValue(
                    new int2(regionX, regionZ), out byte[] materials))
                return VoxelGrid.MaterialEmpty;

            int index = ColumnIndex(worldVoxelX, worldVoxelZ, regionX, regionZ);
            return materials[index];
        }

        public void Clear()
        {
            _columns.Clear();
            _authoredTerrain.Clear();
            _authoredTerrainMaterials.Clear();
            _suppressedBuiltColumns.Clear();
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

        private bool MergeLoweredTerrain(int2 key, int[] heights, byte[] materials)
        {
            if (heights == null) return false;
            if (!_authoredTerrain.TryGetValue(key, out int[] existing))
            {
                _authoredTerrain[key] = heights;
                _authoredTerrainMaterials[key] = materials
                    ?? new byte[ColumnsPerRegion * ColumnsPerRegion];
                return true;
            }

            if (!_authoredTerrainMaterials.TryGetValue(key, out byte[] existingMaterials))
            {
                existingMaterials = new byte[ColumnsPerRegion * ColumnsPerRegion];
                _authoredTerrainMaterials[key] = existingMaterials;
            }

            bool changed = false;
            for (int i = 0; i < existing.Length; i++)
            {
                int incoming = heights[i];
                if (incoming == int.MinValue) continue;

                byte incomingMaterial = materials != null
                    ? materials[i]
                    : VoxelGrid.MaterialEmpty;
                int current = existing[i];
                if (current != int.MinValue && incoming > current) continue;

                if (current == int.MinValue || incoming < current)
                {
                    existing[i] = incoming;
                    existingMaterials[i] = incomingMaterial;
                    changed = true;
                    continue;
                }

                if (existingMaterials[i] == incomingMaterial) continue;
                existingMaterials[i] = incomingMaterial;
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

        private static int TopSurfaceVoxel(in RegionReadView region,
                                           int worldX, int worldZ, int baseY, int height,
                                           out byte material)
        {
            material = VoxelGrid.MaterialEmpty;
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
                    {
                        material = block.UniformMaterial;
                        return baseY + blockBaseY + maxInnerY;
                    }
                    continue;
                }

                for (int innerY = maxInnerY; innerY >= 0; innerY--)
                {
                    int localY = blockBaseY + innerY;
                    if (!region.TryReadCell(
                            new int3(localX, localY, localZ), out VoxelCell cell))
                        continue;
                    if (cell.BaseMaterialId == VoxelGrid.MaterialEmpty) continue;
                    material = cell.BaseMaterialId;
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
