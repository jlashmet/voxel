using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using Mat = Game.Materials.Api.GameMaterialIds;   // engine-side Mat constants were removed

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private bool _foregroundContrastApplied;

        /// <summary>
        /// The reference uses dark moss banks beside discrete pale blocks. The base lookdev's
        /// partially buried limestone shelves read as broad horizontal geological strata, so the
        /// base limestone presentation is subdued elsewhere and this pass restores pale stone as
        /// smaller, clearly separated blocks across the depth of the valley.
        /// </summary>
        private void ApplyForegroundContrastAccents()
        {
            if (!_built || _foregroundContrastApplied) return;

            var writer = CreateWriter(1_050_000);
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
                StampEllipsoid(writer, new int3(x, top + ry, z),
                    new int3(rx, ry, rz), Mat.Moss, SurfaceStyles.Smooth);

                int rocks = rng.NextInt(2, 6);
                for (int r = 0; r < rocks; r++)
                {
                    int xx = x + rng.NextInt(-rx, rx + 1);
                    int zz = z + rng.NextInt(-rz, rz + 1);
                    int hx = rng.NextInt(2, 6);
                    int hz = rng.NextInt(2, 6);
                    int hy = rng.NextInt(2, 5);
                    int y = HeightVoxel(xx, zz) + hy;
                    StampRoundedBox(writer, new int3(xx, y, zz), new int3(hx, hy, hz),
                        1, TerrainLimestoneAccent, SurfaceStyles.Planar,
                        rng.NextFloat() < 0.58f);
                }
            }

            // Carry distinct stone marks into the middle and far valley. Keep them smaller with
            // distance and place their centres above the local surface so they read as cuboids,
            // not contour slices cut through the hill.
            for (int cluster = 0; cluster < 120; cluster++)
            {
                int z = rng.NextInt(90, 535);
                int side = rng.NextBool() ? -1 : 1;
                int x = (int)math.round(ValleyCenterMetres(z * 0.1f) * 10f)
                      + side * rng.NextInt(36, 150);
                if (x <= TerrainXMin + 7 || x >= TerrainXMax - 7) continue;
                if (z < 280 && math.abs(x - PathCenterVoxel(z)) < 16) continue;

                int pieces = rng.NextInt(1, 4);
                for (int p = 0; p < pieces; p++)
                {
                    int xx = x + rng.NextInt(-6, 7);
                    int zz = z + rng.NextInt(-5, 6);
                    int maxHalf = z > 360 ? 4 : 5;
                    int hx = rng.NextInt(2, maxHalf + 1);
                    int hz = rng.NextInt(2, maxHalf + 1);
                    int hy = rng.NextInt(1, 3);
                    int y = HeightVoxel(xx, zz) + hy;
                    StampRoundedBox(writer, new int3(xx, y, zz), new int3(hx, hy, hz),
                        1, TerrainLimestoneAccent, SurfaceStyles.Planar,
                        rng.NextFloat() < 0.38f);
                }
            }

            if (writer.BudgetExceeded)
                throw new System.InvalidOperationException("Terrain foreground contrast pass exceeded voxel authoring budget.");

            PublishAllResidentRegions();
            _foregroundContrastApplied = true;
        }
    }
}
