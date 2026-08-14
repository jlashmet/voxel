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

            var writer = new VoxelBrush(_table, _pool, in _palette, 1_800_000);
            BuildTurfTufts(ref writer);
            BuildReadableFlowerClumps(ref writer);
            BuildVisibleRockAccents(ref writer);
            BuildFarRockShelves(ref writer);

            if (writer.BudgetExceeded)
                throw new System.InvalidOperationException("Terrain visible detail pass exceeded voxel authoring budget.");

            _table = writer.Table;
            _pool = writer.Pool;
            using (NativeArray<int3> regions = _table.GetResidentCoords(Allocator.Temp))
                for (int i = 0; i < regions.Length; i++) _changes.PublishRegion(regions[i]);
            _visibleDetailsApplied = true;
        }

        private static void BuildTurfTufts(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0xA77Du);

            // The reference is a carpet of overlapping small vegetation masses. Small ellipsoidal
            // tufts break long voxel-height contour bands with real geometry instead of texture
            // noise and give the foreground/middle distance the clustered Quaternius-like rhythm.
            for (int i = 0; i < 980; i++)
            {
                int z = rng.NextInt(-48, 525);
                int x = rng.NextInt(TerrainXMin + 5, TerrainXMax - 5);
                if (z < 235 && math.abs(x - PathCenterVoxel(z)) < 9) continue;

                float depth = math.saturate((z + 48f) / 573f);
                int maxRadius = depth < 0.35f ? 5 : 4;
                int rx = rng.NextInt(1, maxRadius + 1);
                int rz = rng.NextInt(2, maxRadius + 2);
                int ry = rng.NextFloat() < math.lerp(0.42f, 0.16f, depth) ? 2 : 1;
                int top = FinalTerrainTopVoxel(x, z);

                StampEllipsoid(ref writer, new int3(x, top + ry, z),
                    new int3(rx, ry, rz), GroundToneMaterial(x, z), SurfaceStyles.Smooth);
            }
        }

        private static void BuildReadableFlowerClumps(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0xE47Du);

            for (int clump = 0; clump < 470; clump++)
            {
                int z = rng.NextInt(-50, 430);
                float depth = math.saturate((z + 50f) / 480f);
                if (rng.NextFloat() < depth * 0.30f) continue;

                int centreX = rng.NextInt(TerrainXMin + 9, TerrainXMax - 9);
                int path = PathCenterVoxel(z);
                if (z < 235 && math.abs(centreX - path) < 14)
                    centreX += centreX < path ? -18 : 18;

                byte flower = PickFlower(ref rng);
                int stems = z < 95 ? rng.NextInt(4, 8)
                          : z < 245 ? rng.NextInt(3, 6)
                                    : rng.NextInt(1, 4);

                for (int stem = 0; stem < stems; stem++)
                {
                    int x = centreX + rng.NextInt(-4, 5);
                    int zz = z + rng.NextInt(-3, 4);
                    if (x <= TerrainXMin + 4 || x >= TerrainXMax - 4) continue;
                    if (zz < 220 && math.abs(x - PathCenterVoxel(zz)) < 8) continue;

                    int top = FinalTerrainTopVoxel(x, zz);
                    if (zz < 260)
                    {
                        writer.SetStyled(x, top + 1, zz, Mat.Moss, SurfaceStyles.Smooth);
                        if (rng.NextFloat() < 0.42f)
                        {
                            int leafX = x + (rng.NextBool() ? 1 : -1);
                            writer.SetStyled(leafX, top + 1, zz, Mat.Grass, SurfaceStyles.Smooth);
                        }
                    }

                    int headY = top + (zz < 260 ? 2 : 1);
                    writer.SetStyled(x, headY, zz, flower, SurfaceStyles.Rounded);

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
            if (colour < 0.72f) return Mat.FlowerWhite;
            if (colour < 0.90f) return Mat.FlowerYellow;
            if (colour < 0.975f) return Mat.FlowerPink;
            return Mat.FlowerBlue;
        }

        private static void BuildVisibleRockAccents(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0x9A31u);

            for (int cluster = 0; cluster < 92; cluster++)
            {
                int z = rng.NextInt(-38, 340);
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

        private static void BuildFarRockShelves(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0xC511u);

            // Dense small shelves carry the rock rhythm into the far valley. Their world size falls
            // with distance so the top third reads as many small limestone marks rather than a few
            // oversized boulders.
            for (int cluster = 0; cluster < 125; cluster++)
            {
                int z = rng.NextInt(125, 540);
                float zm = z * 0.1f;
                int valley = (int)math.round(ValleyCenterMetres(zm) * 10f);
                int side = rng.NextBool() ? -1 : 1;
                int centreX = valley + side * rng.NextInt(48, 150);
                if (centreX < TerrainXMin + 7 || centreX > TerrainXMax - 7) continue;

                int pieces = rng.NextInt(2, 5);
                for (int piece = 0; piece < pieces; piece++)
                {
                    int x = centreX + rng.NextInt(-6, 7);
                    int zz = z + rng.NextInt(-5, 6);
                    int hx = rng.NextInt(1, z > 360 ? 4 : 5);
                    int hz = rng.NextInt(1, z > 360 ? 4 : 5);
                    int hy = rng.NextInt(1, 3);
                    int y = FinalTerrainTopVoxel(x, zz) + hy - 1;
                    StampRoundedBox(ref writer, new int3(x, y, zz), new int3(hx, hy, hz),
                        1, Mat.TerrainLimestone, SurfaceStyles.Rounded,
                        rng.NextFloat() < 0.12f);
                }
            }
        }
    }
}
