using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering;
using VoxelEngine.Structures;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private bool _vegetatedCapApplied;

        /// <summary>
        /// The heightfield is authored as columns, so a single grass voxel on top leaves earth
        /// exposed anywhere adjacent columns differ in height. In the reference those step faces
        /// are overwhelmingly turf/moss, not bare horizontal soil bands. Give every column a
        /// shallow vegetated cap while keeping the same production voxel/material/rendering path.
        /// </summary>
        private void ApplyVegetatedCap()
        {
            if (!_built || _vegetatedCapApplied) return;

            var writer = new VoxelBrush(_table, _pool, in _palette, 1_100_000);
            for (int z = TerrainZMin; z <= TerrainZMax; z++)
            for (int x = TerrainXMin; x <= TerrainXMax; x++)
            {
                int top = HeightVoxel(x, z);
                byte material = GroundToneMaterial(x, z);
                byte coating = GroundToneCoating(x, z);

                // Four voxels covers the common height deltas in the rolling valley, turning the
                // visible stair risers into grassy/mossy banks while deeper cuts can still expose
                // occasional earth as they do in the target.
                for (int y = top - 3; y <= top; y++)
                    writer.SetStyled(x, y, z, material, SurfaceStyles.Smooth, coating);
            }

            if (writer.BudgetExceeded)
                throw new System.InvalidOperationException("Terrain vegetated cap exceeded voxel authoring budget.");

            _table = writer.Table;
            _pool = writer.Pool;
            using (NativeArray<int3> regions = _table.GetResidentCoords(Allocator.Temp))
                for (int i = 0; i < regions.Length; i++) _changes.PublishRegion(regions[i]);
            _vegetatedCapApplied = true;
        }
    }
}
