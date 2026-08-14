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
        /// Normalize the base authored terrain into one continuous vegetated shell before the
        /// dedicated presentation/detail passes run. Earlier base rock/turf fields were authored
        /// before the reference-directed passes and still left hundreds of low planar boxes above
        /// the heightfield. In the portrait camera those boxes read as dark horizontal contour
        /// stripes even when recoloured green. Cover a shallow volume above the heightfield too,
        /// then rebuild the path and flowers. The later foreground/detail passes add the intended
        /// discrete limestone accents back on top using the normal production voxel path.
        /// </summary>
        private void ApplyVegetatedCap()
        {
            if (!_built || _vegetatedCapApplied) return;

            var writer = new VoxelBrush(_table, _pool, in _palette, 4_000_000);
            for (int z = TerrainZMin; z <= TerrainZMax; z++)
            for (int x = TerrainXMin; x <= TerrainXMax; x++)
            {
                int top = HeightVoxel(x, z);
                byte material = GroundToneMaterial(x, z);
                byte coating = GroundToneCoating(x, z);

                // Erase the legacy base-detail layer as well as the shell itself. Six voxels is
                // enough to remove the shallow planar shelf boxes responsible for the striping,
                // while leaving room for the later intentional rock/outcrop passes.
                for (int y = top - 8; y <= top + 6; y++)
                    writer.SetStyled(x, y, z, material, SurfaceStyles.Smooth, coating);
            }

            // The normalization pass intentionally covers the original early path/flowers too.
            // Re-author those semantic features after the clean cap so they remain readable.
            BuildPath(ref writer);
            BuildFlowers(ref writer);

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
