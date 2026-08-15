using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;
using VoxelEngine.Rendering;
using VoxelEngine.Structures;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private bool _vegetatedCapApplied;

        /// <summary>
        /// Normalize the visible heightfield shell into vegetation before the dedicated
        /// presentation/detail passes run. This pass must never add height to the terrain: filling
        /// above HeightVoxel turns every integer height step into a broad raised terrace in the
        /// portrait camera. Later detail passes are responsible for adding visible relief.
        /// </summary>
        private void ApplyVegetatedCap()
        {
            if (!_built || _vegetatedCapApplied) return;

            var reads = new RegionReadSource(in _table, in _pool);
            var mutations = new RegionMutationStore(in _table, in _pool);
            var writer = new VoxelBrush(reads, mutations, _palette, 3_000_000);
            for (int z = TerrainZMin; z <= TerrainZMax; z++)
            for (int x = TerrainXMin; x <= TerrainXMax; x++)
            {
                int top = HeightVoxel(x, z);
                byte material = GroundToneMaterial(x, z);
                byte coating = GroundToneCoating(x, z);

                // Re-skin only the existing terrain column. The previous version wrote solid turf
                // through top + 6 in an attempt to hide legacy shelves. That accidentally raised
                // the whole terrain by six voxels and made quantized height contours dominate the
                // image. Keep the cap at the authored surface; discrete rocks/tufts are added later.
                for (int y = top - 8; y <= top; y++)
                    writer.SetStyled(x, y, z, material, SurfaceStyles.Smooth, coating);
            }

            // Re-author the semantic features after the shell recolour so the path and flower
            // rhythm remain legible. These still use the normal production voxel authoring path.
            BuildPath(ref writer);
            BuildFlowers(ref writer);

            if (writer.BudgetExceeded)
                throw new System.InvalidOperationException("Terrain vegetated cap exceeded voxel authoring budget.");

            using (NativeArray<int3> regions = _table.GetResidentCoords(Allocator.Temp))
                for (int i = 0; i < regions.Length; i++) _changes.PublishRegion(regions[i]);
            _vegetatedCapApplied = true;
        }
    }
}
