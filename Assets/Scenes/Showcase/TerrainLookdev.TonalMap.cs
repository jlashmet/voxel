using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Structures;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private bool _tonalOverlayApplied;

        // The original image has a strong atmospheric value structure: a bright yellow-green far
        // valley and much darker mossy foreground shoulders. Apply that structure to the authored
        // ground cells before the production renderer culls its first frame. This still changes
        // semantic voxels only; extraction, materials and shading remain the normal voxel path.
        private void OnPreCull()
        {
            if (!_built || _tonalOverlayApplied) return;

            var writer = new VoxelBrush(_table, _pool, in _palette, 1_500_000);
            for (int z = TerrainZMin; z <= TerrainZMax; z++)
            for (int x = TerrainXMin; x <= TerrainXMax; x++)
            {
                int top = HeightVoxel(x, z);
                byte material = GroundToneMaterial(x, z);
                byte coating = GroundToneCoating(x, z);
                writer.SetStyled(x, top, z, material, SurfaceStyles.Smooth, coating);
            }

            if (writer.BudgetExceeded)
                throw new System.InvalidOperationException("Terrain tonal overlay exceeded voxel authoring budget.");

            _table = writer.Table;
            _pool = writer.Pool;
            using (NativeArray<int3> regions = _table.GetResidentCoords(Allocator.Temp))
                for (int i = 0; i < regions.Length; i++) _changes.PublishRegion(regions[i]);

            _tonalOverlayApplied = true;
        }

        private static byte GroundToneMaterial(int x, int z)
        {
            // Near turf is intrinsically darker. The far basin uses broad warm swaths instead of
            // the previous per-voxel sand confetti so the image reads as sunlit grass at distance.
            if (z < 35)
                return Mat.Moss;

            float depth = math.saturate((z - 75f) / 440f);
            float warmField = 0.50f
                            + 0.24f * math.sin(x * 0.045f + z * 0.016f)
                            + 0.18f * math.sin(z * 0.031f - x * 0.019f + 1.1f);
            float warmThreshold = math.lerp(0.18f, 0.54f, depth);
            if (z > 110 && warmField < warmThreshold)
                return Mat.Sand;

            return Mat.Grass;
        }

        private static byte GroundToneCoating(int x, int z)
        {
            int path = PathCenterVoxel(z);
            int fromPath = math.abs(x - path);

            // Preserve a lighter central route while allowing both near shoulders to fall into
            // the deep moss values visible at the bottom of the reference.
            if (z < 5 && fromPath > 22)
                return Coatings.Moss;
            if (z < 55 && fromPath > 38)
                return Coatings.Moss;
            if (z < 105 && fromPath > 78)
                return Coatings.Moss;

            return Coatings.None;
        }
    }
}
