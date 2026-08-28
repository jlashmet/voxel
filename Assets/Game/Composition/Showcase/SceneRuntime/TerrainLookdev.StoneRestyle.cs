using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using Mat = Game.Materials.Api.GameMaterialIds;   // engine-side Mat constants were removed

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        /// <summary>
        /// Replays the deterministic base stone placement with rounded surface reconstruction.
        /// Geometry and material stay on the normal voxel path; only the per-voxel surface style is
        /// changed so individual limestone blocks read as soft cuboids instead of zebra-like stacks
        /// of planar horizontal faces.
        /// </summary>
        private static void RestyleBaseStoneRounded(IStructureAuthoringSession writer)
        {
            var rng = new Unity.Mathematics.Random(Seed);

            for (int cluster = 0; cluster < 78; cluster++)
            {
                int z = rng.NextInt(-48, 548);
                float zm = z * 0.1f;
                int centre = (int)math.round(ValleyCenterMetres(zm) * 10f);
                int side = rng.NextBool() ? -1 : 1;
                int distance = rng.NextInt(38, 150);
                int centreX = centre + side * distance;
                if (centreX < TerrainXMin + 10 || centreX > TerrainXMax - 10) continue;

                int count = rng.NextInt(2, 6);
                int stride = rng.NextInt(5, 10);
                for (int i = 0; i < count; i++)
                {
                    int x = centreX + (i - count / 2) * stride + rng.NextInt(-2, 3);
                    int zz = z + rng.NextInt(-5, 6) + (i - count / 2) / 2;
                    if (x <= TerrainXMin + 5 || x >= TerrainXMax - 5) continue;

                    int maxHalf = z < 150 ? 6 : (z < 330 ? 5 : 4);
                    int hx = rng.NextInt(2, maxHalf + 1);
                    int hz = rng.NextInt(2, maxHalf + 1);
                    int hy = rng.NextInt(1, z < 170 ? 4 : 3);
                    int y = HeightVoxel(x, zz) + hy;
                    bool moss = rng.NextFloat() < 0.58f;
                    StampRoundedBox(writer, new int3(x, y, zz), new int3(hx, hy, hz),
                        1, Mat.TerrainLimestone, SurfaceStyles.Rounded, moss);

                    if (z < 230 && rng.NextFloat() < 0.16f)
                    {
                        int upperHx = math.max(2, hx - 1);
                        int upperHz = math.max(2, hz - 1);
                        int ux = x + rng.NextInt(-2, 3);
                        int uz = zz + rng.NextInt(-2, 3);
                        bool upperMoss = rng.NextFloat() < 0.62f;
                        StampRoundedBox(writer, new int3(ux, y + hy + 1, uz),
                            new int3(upperHx, 1, upperHz), 1,
                            Mat.TerrainLimestone, SurfaceStyles.Rounded, upperMoss);
                    }
                }
            }

            for (int i = 0; i < 240; i++)
            {
                int z = rng.NextInt(-58, 545);
                int x = rng.NextInt(TerrainXMin + 7, TerrainXMax - 7);
                int path = PathCenterVoxel(z);
                if (z < 255 && math.abs(x - path) < 11)
                    x += x < path ? -14 : 14;

                int maxHalf = z > 360 ? 4 : (z > 180 ? 5 : 6);
                int hx = rng.NextInt(2, maxHalf + 1);
                int hz = rng.NextInt(2, maxHalf + 1);
                int hy = rng.NextInt(1, z > 300 ? 3 : 4);
                int y = HeightVoxel(x, z) + hy;
                bool moss = rng.NextFloat() < 0.42f;
                StampRoundedBox(writer, new int3(x, y, z), new int3(hx, hy, hz),
                    1, Mat.TerrainLimestone, SurfaceStyles.Rounded, moss);
            }

            RestyleForegroundOutcrop(writer, new int3(-106, 0, -46), 15, ref rng);
            RestyleForegroundOutcrop(writer, new int3(104, 0, -38), 14, ref rng);
            RestyleForegroundOutcrop(writer, new int3(-130, 0, 32), 11, ref rng);
            RestyleForegroundOutcrop(writer, new int3(125, 0, 66), 10, ref rng);

            // Replay the path positions too, but use rounded surface extraction. This does not add
            // a second path; it overwrites the style of the same deterministic paver voxels.
            var pathRng = new Unity.Mathematics.Random(Seed ^ 0x2231u);
            int pzCursor = -60;
            while (pzCursor < 285)
            {
                float progress = math.saturate((pzCursor + 60f) / 345f);
                int centreX = PathCenterVoxel(pzCursor);
                int halfWidth = math.max(4, (int)math.round(math.lerp(15f, 5f, progress)));
                for (int lateral = -halfWidth; lateral <= halfWidth; lateral += 3)
                {
                    if (pathRng.NextFloat() < math.lerp(0.07f, 0.32f, progress)) continue;
                    int px = centreX + lateral + pathRng.NextInt(-2, 3);
                    int pz = pzCursor + pathRng.NextInt(-2, 3);
                    int hx = pathRng.NextInt(1, progress < 0.30f ? 4 : 3);
                    int hz = pathRng.NextInt(1, progress < 0.30f ? 4 : 3);
                    int py = HeightVoxel(px, pz);
                    StampRoundedBox(writer, new int3(px, py + 1, pz),
                        new int3(hx, 1, hz), 1, Mat.TerrainPathStone,
                        SurfaceStyles.Rounded, false);
                }
                pzCursor += pathRng.NextInt(4, 8) + (int)math.round(progress * 3f);
            }
        }

        private static void RestyleForegroundOutcrop(IStructureAuthoringSession writer, int3 centre, int scale,
            ref Unity.Mathematics.Random rng)
        {
            for (int layer = 0; layer < 3; layer++)
            {
                int count = 7 - layer;
                for (int i = 0; i < count; i++)
                {
                    int x = centre.x + (i - count / 2) * (scale - 4) + rng.NextInt(-2, 3);
                    int z = centre.z + layer * 5 + rng.NextInt(-2, 3);
                    int hx = rng.NextInt(3, math.max(5, scale / 2 + 1));
                    int hy = rng.NextInt(2, 5);
                    int hz = rng.NextInt(3, math.max(5, scale / 2 + 1));
                    int y = HeightVoxel(x, z) + layer * 2 + hy;
                    bool moss = rng.NextFloat() < 0.68f;
                    StampRoundedBox(writer, new int3(x, y, z), new int3(hx, hy, hz),
                        1, Mat.TerrainLimestone, SurfaceStyles.Rounded, moss);
                }
            }
        }
    }
}
