using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Storage;
using VoxelEngine.Structures;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private bool _visibleDetailsApplied;

        private void ApplyVisibleDetails()
        {
            if (!_built || _visibleDetailsApplied) return;

            var writer = new VoxelBrush(_table, _pool, in _palette, 1_200_000);
            BuildReadableFlowerClumps(ref writer);
            BuildVisibleRockAccents(ref writer);

            if (writer.BudgetExceeded)
                throw new System.InvalidOperationException("Terrain visible detail pass exceeded voxel authoring budget.");

            _table = writer.Table;
            _pool = writer.Pool;
            using (NativeArray<int3> regions = _table.GetResidentCoords(Allocator.Temp))
                for (int i = 0; i < regions.Length; i++) _changes.PublishRegion(regions[i]);
            _visibleDetailsApplied = true;
        }

        private static void BuildReadableFlowerClumps(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0xE47Du);

            // Stylized nature reads through clumps and silhouette. A thousand isolated one-voxel
            // dots looked like rendering noise; a few hundred small colonies read as actual plants.
            for (int clump = 0; clump < 430; clump++)
            {
                int z = rng.NextInt(-50, 390);
                float depth = math.saturate((z + 50f) / 440f);
                if (rng.NextFloat() < depth * 0.38f) continue;

                int centreX = rng.NextInt(TerrainXMin + 9, TerrainXMax - 9);
                int path = PathCenterVoxel(z);
                if (z < 235 && math.abs(centreX - path) < 14)
                    centreX += centreX < path ? -18 : 18;

                byte flower = PickFlower(ref rng);
                int stems = z < 95 ? rng.NextInt(4, 8)
                          : z < 225 ? rng.NextInt(3, 6)
                                    : rng.NextInt(1, 4);

                for (int stem = 0; stem < stems; stem++)
                {
                    int x = centreX + rng.NextInt(-4, 5);
                    int zz = z + rng.NextInt(-3, 4);
                    if (x <= TerrainXMin + 4 || x >= TerrainXMax - 4) continue;
                    if (zz < 220 && math.abs(x - PathCenterVoxel(zz)) < 9) continue;

                    int top = FinalTerrainTopVoxel(x, zz);
                    if (zz < 245)
                    {
                        writer.SetStyled(x, top + 1, zz, Mat.Moss, SurfaceStyles.Smooth);
                        if (rng.NextFloat() < 0.42f)
                        {
                            int leafX = x + (rng.NextBool() ? 1 : -1);
                            writer.SetStyled(leafX, top + 1, zz, Mat.Grass, SurfaceStyles.Smooth);
                        }
                    }

                    int headY = top + (zz < 245 ? 2 : 1);
                    writer.SetStyled(x, headY, zz, flower, SurfaceStyles.Rounded);

                    // Foreground flowers get a tiny stylized cross-shaped head so they survive
                    // projection and read like the large simple blossoms in the reference/kit.
                    if (zz < 100)
                    {
                        if (rng.NextFloat() < 0.72f)
                            writer.SetStyled(x + 1, headY, zz, flower, SurfaceStyles.Rounded);
                        if (rng.NextFloat() < 0.55f)
                            writer.SetStyled(x, headY, zz + 1, flower, SurfaceStyles.Rounded);
                    }
                }
            }
        }

        private static byte PickFlower(ref Unity.Mathematics.Random rng)
        {
            float colour = rng.NextFloat();
            if (colour < 0.70f) return Mat.FlowerWhite;
            if (colour < 0.89f) return Mat.FlowerYellow;
            if (colour < 0.97f) return Mat.FlowerPink;
            return Mat.FlowerBlue;
        }

        private static void BuildVisibleRockAccents(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0x9A31u);

            // The target has broad limestone coverage on both valley shoulders. These accents are
            // placed after the ground presentation pass so they can never be buried by a later
            // terrain rewrite, while still using the normal voxel material + rounded surface path.
            for (int cluster = 0; cluster < 88; cluster++)
            {
                int z = rng.NextInt(-38, 385);
                float zm = z * 0.1f;
                int valley = (int)math.round(ValleyCenterMetres(zm) * 10f);
                int side = rng.NextBool() ? -1 : 1;
                int distance = rng.NextInt(42, z < 130 ? 142 : 126);
                int centreX = valley + side * distance;
                if (centreX < TerrainXMin + 10 || centreX > TerrainXMax - 10) continue;

                int pieces = z < 120 ? rng.NextInt(3, 7) : rng.NextInt(2, 5);
                for (int piece = 0; piece < pieces; piece++)
                {
                    int x = centreX + rng.NextInt(-8, 9);
                    int zz = z + rng.NextInt(-6, 7);
                    if (x < TerrainXMin + 6 || x > TerrainXMax - 6) continue;

                    int maxHalf = z < 100 ? 7 : (z < 240 ? 6 : 5);
                    int hx = rng.NextInt(2, maxHalf + 1);
                    int hz = rng.NextInt(2, maxHalf + 1);
                    int hy = rng.NextInt(2, z < 130 ? 5 : 4);
                    int y = FinalTerrainTopVoxel(x, zz) + hy - 1;
                    bool moss = rng.NextFloat() < (z < 160 ? 0.30f : 0.16f);

                    StampRoundedBox(ref writer, new int3(x, y, zz), new int3(hx, hy, hz),
                        math.min(2, math.min(hx, hz)), Mat.TerrainLimestone,
                        SurfaceStyles.Rounded, moss);
                }
            }
        }
    }
}
