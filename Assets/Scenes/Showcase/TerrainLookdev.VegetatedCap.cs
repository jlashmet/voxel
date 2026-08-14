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
        /// The heightfield is authored as nine-voxel-deep earth columns. A shallow four-voxel cap
        /// still leaves earth visible wherever neighbouring columns differ sharply, producing the
        /// long parallel contour risers that dominate the current capture. The reference is an
        /// almost completely vegetated valley, so cover the full authored shell with turf. Rocks,
        /// path stones and flowers are authored separately and remain on the production voxel path.
        /// </summary>
        private void ApplyVegetatedCap()
        {
            if (!_built || _vegetatedCapApplied) return;

            var writer = new VoxelBrush(_table, _pool, in _palette, 2_250_000);
            for (int z = TerrainZMin; z <= TerrainZMax; z++)
            for (int x = TerrainXMin; x <= TerrainXMax; x++)
            {
                int top = HeightVoxel(x, z);
                byte material = GroundToneMaterial(x, z);
                byte coating = GroundToneCoating(x, z);

                for (int y = top - 8; y <= top; y++)
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
