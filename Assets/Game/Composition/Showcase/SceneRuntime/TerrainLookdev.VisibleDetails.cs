using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using Mat = Game.Materials.Api.GameMaterialIds;   // engine-side Mat constants were removed

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private bool _visibleDetailsApplied;

        private void ApplyVisibleDetails()
        {
            if (!_built || _visibleDetailsApplied) return;

            var writer = CreateWriter(2_400_000);
            BuildTurfTufts(writer);
            BuildTurfShelfBanks(writer);
            BuildReadableFlowerClumps(writer);
            BuildVisibleRockAccents(writer);
            BuildFarRockShelves(writer);

            if (writer.BudgetExceeded)
                throw new System.InvalidOperationException("Terrain visible detail pass exceeded voxel authoring budget.");

            PublishAllResidentRegions();
            _visibleDetailsApplied = true;
        }

        private static void BuildTurfTufts(IStructureAuthoringSession writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0xA77Du);
            for (int i = 0; i < 1220; i++)
            {
                int z = rng.NextInt(-48, 535);
                int x = rng.NextInt(TerrainXMin + 5, TerrainXMax - 5);
                if (z < 250 && math.abs(x - PathCenterVoxel(z)) < 8) continue;

                float depth = math.saturate((z + 48f) / 583f);
                int maxRadius = depth < 0.35f ? 5 : 4;
                int rx = rng.NextInt(1, maxRadius + 1);
                int rz = rng.NextInt(2, maxRadius + 2);
                int ry = rng.NextFloat() < math.lerp(0.38f, 0.12f, depth) ? 2 : 1;
                int top = FinalTerrainTopVoxel(x, z);

                StampEllipsoid(writer, new int3(x, top + ry, z),
                    new int3(rx, ry, rz), GroundToneMaterial(x, z), SurfaceStyles.Smooth);
            }
        }

        private static void BuildTurfShelfBanks(IStructureAuthoringSession writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0x17B5u);

            // The reference is full of short moss-covered ledges rather than uninterrupted hills.
            // Each bank is a coherent turf lip with a few exposed limestone blocks underneath.
            for (int bank = 0; bank < 190; bank++)
            {
                int z = rng.NextInt(-28, 480);
                float zm = z * 0.1f;
                int valley = (int)math.round(ValleyCenterMetres(zm) * 10f);
                int side = rng.NextBool() ? -1 : 1;
                int centreX = valley + side * rng.NextInt(28, 138);
                if (centreX < TerrainXMin + 12 || centreX > TerrainXMax - 12) continue;

                int run = rng.NextInt(10, z < 180 ? 27 : 20);
                int depth = rng.NextInt(4, 9);
                int lipHeight = rng.NextFloat() < 0.24f ? 2 : 1;
                int top = FinalTerrainTopVoxel(centreX, z);

                // Overlapping pads avoid a mathematically straight shelf while preserving a clear
                // lateral ledge silhouette.
                int segments = math.max(2, run / 6);
                for (int segment = 0; segment < segments; segment++)
                {
                    float t = segments == 1 ? 0f : segment / (float)(segments - 1);
                    int x = centreX + (int)math.round(math.lerp(-run / 2f, run / 2f, t))
                          + rng.NextInt(-2, 3);
                    int zz = z + rng.NextInt(-2, 3);
                    int localTop = FinalTerrainTopVoxel(x, zz);
                    StampEllipsoid(writer, new int3(x, localTop + lipHeight, zz),
                        new int3(rng.NextInt(4, 8), lipHeight, depth),
                        GroundToneMaterial(x, zz), SurfaceStyles.Smooth);
                }

                int exposed = rng.NextInt(2, 6);
                for (int piece = 0; piece < exposed; piece++)
                {
                    int x = centreX + rng.NextInt(-run / 2, run / 2 + 1);
                    int zz = z + side * rng.NextInt(0, depth + 2);
                    int hx = rng.NextInt(2, 5);
                    int hz = rng.NextInt(2, 5);
                    int hy = rng.NextInt(1, 3);
                    int y = FinalTerrainTopVoxel(x, zz) + hy - 1;
                    StampRoundedBox(writer, new int3(x, y, zz), new int3(hx, hy, hz),
                        1, Mat.TerrainLimestone, SurfaceStyles.Planar,
                        rng.NextFloat() < 0.62f);
                }
            }
        }

        private static void BuildReadableFlowerClumps(IStructureAuthoringSession writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0xE47Du);

            for (int clump = 0; clump < 720; clump++)
            {
                int z = rng.NextInt(-50, 465);
                float depth = math.saturate((z + 50f) / 515f);
                if (rng.NextFloat() < depth * 0.25f) continue;

                int centreX = rng.NextInt(TerrainXMin + 9, TerrainXMax - 9);
                int path = PathCenterVoxel(z);
                if (z < 250 && math.abs(centreX - path) < 13)
                    centreX += centreX < path ? -17 : 17;

                byte flower = PickFlower(ref rng);
                int stems = z < 105 ? rng.NextInt(4, 9)
                          : z < 270 ? rng.NextInt(3, 7)
                                    : rng.NextInt(1, 4);

                for (int stem = 0; stem < stems; stem++)
                {
                    int x = centreX + rng.NextInt(-4, 5);
                    int zz = z + rng.NextInt(-3, 4);
                    if (x <= TerrainXMin + 4 || x >= TerrainXMax - 4) continue;
                    if (zz < 235 && math.abs(x - PathCenterVoxel(zz)) < 7) continue;

                    int top = FinalTerrainTopVoxel(x, zz);
                    if (zz < 290)
                    {
                        writer.SetStyled(x, top + 1, zz, Mat.Moss, SurfaceStyles.Smooth);
                        if (rng.NextFloat() < 0.52f)
                        {
                            int leafX = x + (rng.NextBool() ? 1 : -1);
                            writer.SetStyled(leafX, top + 1, zz, Mat.Grass, SurfaceStyles.Smooth);
                        }
                        if (zz < 140 && rng.NextFloat() < 0.24f)
                        {
                            int leafZ = zz + (rng.NextBool() ? 1 : -1);
                            writer.SetStyled(x, top + 1, leafZ, Mat.Moss, SurfaceStyles.Smooth);
                        }
                    }

                    int headY = top + (zz < 290 ? 2 : 1);
                    writer.SetStyled(x, headY, zz, flower, SurfaceStyles.Rounded);

                    if (zz < 110)
                    {
                        if (rng.NextFloat() < 0.78f)
                            writer.SetStyled(x + 1, headY, zz, flower, SurfaceStyles.Rounded);
                        if (rng.NextFloat() < 0.62f)
                            writer.SetStyled(x, headY, zz + 1, flower, SurfaceStyles.Rounded);
                    }
                }
            }
        }

        private static byte PickFlower(ref Unity.Mathematics.Random rng)
        {
            float colour = rng.NextFloat();
            if (colour < 0.80f) return Mat.FlowerWhite;
            if (colour < 0.93f) return Mat.FlowerYellow;
            if (colour < 0.985f) return Mat.FlowerPink;
            return Mat.FlowerBlue;
        }

        private static void BuildVisibleRockAccents(IStructureAuthoringSession writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0x9A31u);

            for (int cluster = 0; cluster < 112; cluster++)
            {
                int z = rng.NextInt(-38, 360);
                float zm = z * 0.1f;
                int valley = (int)math.round(ValleyCenterMetres(zm) * 10f);
                int side = rng.NextBool() ? -1 : 1;
                int distance = rng.NextInt(38, z < 130 ? 145 : 128);
                int centreX = valley + side * distance;
                if (centreX < TerrainXMin + 10 || centreX > TerrainXMax - 10) continue;

                int pieces = z < 120 ? rng.NextInt(3, 8) : rng.NextInt(2, 6);
                for (int piece = 0; piece < pieces; piece++)
                {
                    int x = centreX + rng.NextInt(-9, 10);
                    int zz = z + rng.NextInt(-6, 7);
                    if (x < TerrainXMin + 6 || x > TerrainXMax - 6) continue;

                    int maxHalf = z < 100 ? 7 : (z < 240 ? 6 : 5);
                    int hx = rng.NextInt(2, maxHalf + 1);
                    int hz = rng.NextInt(2, maxHalf + 1);
                    int hy = rng.NextInt(1, z < 130 ? 4 : 3);
                    int y = FinalTerrainTopVoxel(x, zz) + hy - 1;
                    bool moss = rng.NextFloat() < (z < 180 ? 0.56f : 0.32f);

                    StampRoundedBox(writer, new int3(x, y, zz), new int3(hx, hy, hz),
                        1, Mat.TerrainLimestone, SurfaceStyles.Planar, moss);
                }
            }
        }

        private static void BuildFarRockShelves(IStructureAuthoringSession writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0xC511u);

            for (int cluster = 0; cluster < 190; cluster++)
            {
                int z = rng.NextInt(125, 548);
                float zm = z * 0.1f;
                int valley = (int)math.round(ValleyCenterMetres(zm) * 10f);
                int side = rng.NextBool() ? -1 : 1;
                int centreX = valley + side * rng.NextInt(42, 152);
                if (centreX < TerrainXMin + 7 || centreX > TerrainXMax - 7) continue;

                int pieces = rng.NextInt(2, 6);
                for (int piece = 0; piece < pieces; piece++)
                {
                    int x = centreX + rng.NextInt(-7, 8);
                    int zz = z + rng.NextInt(-5, 6);
                    int maxHalf = z > 380 ? 4 : 5;
                    int hx = rng.NextInt(1, maxHalf + 1);
                    int hz = rng.NextInt(1, maxHalf + 1);
                    int hy = rng.NextInt(1, 3);
                    int y = FinalTerrainTopVoxel(x, zz) + hy - 1;
                    StampRoundedBox(writer, new int3(x, y, zz), new int3(hx, hy, hz),
                        1, Mat.TerrainLimestone, SurfaceStyles.Planar,
                        rng.NextFloat() < 0.25f);
                }
            }
        }
    }
}
