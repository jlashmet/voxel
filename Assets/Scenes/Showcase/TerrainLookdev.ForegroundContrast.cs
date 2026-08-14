using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering;
using VoxelEngine.Structures;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private bool _foregroundContrastApplied;

        /// <summary>
        /// The reference foreground has roughly twice the luma variance of the current render:
        /// dark mossy banks sit directly beside pale limestone and the bright path. Add a small
        /// number of coherent foreground masses instead of uniformly sprinkling more detail.
        /// </summary>
        private void ApplyForegroundContrastAccents()
        {
            if (!_built || _foregroundContrastApplied) return;

            var writer = new VoxelBrush(_table, _pool, in _palette, 650_000);
            var rng = new Unity.Mathematics.Random(Seed ^ 0xF09Eu);

            for (int cluster = 0; cluster < 42; cluster++)
            {
                int z = rng.NextInt(-58, 128);
                int side = rng.NextBool() ? -1 : 1;
                int x = side * rng.NextInt(58, 152);
                if (x <= TerrainXMin + 12 || x >= TerrainXMax - 12) continue;
                if (math.abs(x - PathCenterVoxel(z)) < 30) continue;

                int rx = rng.NextInt(6, 14);
                int rz = rng.NextInt(5, 12);
                int ry = rng.NextFloat() < 0.32f ? 2 : 1;
                int top = HeightVoxel(x, z);
                StampEllipsoid(ref writer, new int3(x, top + ry, z),
                    new int3(rx, ry, rz), Mat.Moss, SurfaceStyles.Smooth);

                int rocks = rng.NextInt(2, 6);
                for (int r = 0; r < rocks; r++)
                {
                    int xx = x + rng.NextInt(-rx, rx + 1);
                    int zz = z + rng.NextInt(-rz, rz + 1);
                    int hx = rng.NextInt(2, 6);
                    int hz = rng.NextInt(2, 6);
                    int hy = rng.NextInt(1, 4);
                    int y = HeightVoxel(xx, zz) + hy - 1;
                    StampRoundedBox(ref writer, new int3(xx, y, zz), new int3(hx, hy, hz),
                        1, Mat.TerrainLimestone, SurfaceStyles.Planar,
                        rng.NextFloat() < 0.58f);
                }
            }

            if (writer.BudgetExceeded)
                throw new System.InvalidOperationException("Terrain foreground contrast pass exceeded voxel authoring budget.");

            _table = writer.Table;
            _pool = writer.Pool;
            using (NativeArray<int3> regions = _table.GetResidentCoords(Allocator.Temp))
                for (int i = 0; i < regions.Length; i++) _changes.PublishRegion(regions[i]);
            _foregroundContrastApplied = true;
        }
    }
}
