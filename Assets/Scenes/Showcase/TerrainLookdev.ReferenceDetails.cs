using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using Mat = Game.Materials.Api.GameMaterialIds;   // engine-side Mat constants were removed

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private bool _referenceDetailsApplied;

        private void ApplyReferenceDetails()
        {
            if (!_built || _referenceDetailsApplied) return;

            var writer = CreateWriter(2_200_000);
            RestyleBaseStoneRounded(writer);
            BuildReferenceTufts(writer);
            BuildReferenceFlowers(writer);
            BuildReferenceRockAccents(writer);

            if (writer.BudgetExceeded)
                throw new System.InvalidOperationException("Terrain reference detail pass exceeded voxel authoring budget.");

            PublishAllResidentRegions();
            _referenceDetailsApplied = true;
        }

        private static void BuildReferenceTufts(IStructureAuthoringSession writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0x45A1u);
            for (int i = 0; i < 460; i++)
            {
                int z = rng.NextInt(-48, 500);
                float depth = math.saturate((z + 48f) / 548f);
                if (rng.NextFloat() < depth * 0.26f) continue;

                int x = rng.NextInt(TerrainXMin + 6, TerrainXMax - 6);
                if (z < 250 && math.abs(x - PathCenterVoxel(z)) < 10) continue;

                int rx = depth < 0.32f ? rng.NextInt(2, 6) : rng.NextInt(1, 4);
                int rz = depth < 0.32f ? rng.NextInt(2, 6) : rng.NextInt(1, 4);
                int ry = rng.NextFloat() < 0.18f ? 2 : 1;
                int top = FinalTerrainTopVoxel(x, z);
                StampEllipsoid(writer, new int3(x, top + ry, z), new int3(rx, ry, rz),
                    rng.NextFloat() < 0.22f ? Mat.Moss : Mat.Grass, SurfaceStyles.Smooth);
            }
        }

        private static void BuildReferenceFlowers(IStructureAuthoringSession writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0xF10Au);
            for (int clump = 0; clump < 390; clump++)
            {
                int z = rng.NextInt(-48, 430);
                float depth = math.saturate((z + 48f) / 478f);
                if (rng.NextFloat() < depth * 0.32f) continue;

                int cx = rng.NextInt(TerrainXMin + 8, TerrainXMax - 8);
                if (z < 245 && math.abs(cx - PathCenterVoxel(z)) < 12) continue;

                byte flower = ReferenceFlowerColour(ref rng);
                int stems = depth < 0.28f ? rng.NextInt(3, 7)
                          : depth < 0.60f ? rng.NextInt(2, 5)
                                         : rng.NextInt(1, 3);
                for (int s = 0; s < stems; s++)
                {
                    int x = cx + rng.NextInt(-3, 4);
                    int zz = z + rng.NextInt(-3, 4);
                    if (x <= TerrainXMin + 3 || x >= TerrainXMax - 3) continue;
                    int top = FinalTerrainTopVoxel(x, zz);

                    if (depth < 0.62f)
                    {
                        writer.SetStyled(x, top + 1, zz, Mat.Moss, SurfaceStyles.Smooth);
                        if (depth < 0.34f && rng.NextFloat() < 0.45f)
                            writer.SetStyled(x + (rng.NextBool() ? 1 : -1), top + 1, zz,
                                Mat.Grass, SurfaceStyles.Smooth);
                    }

                    int headY = top + (depth < 0.62f ? 2 : 1);
                    writer.SetStyled(x, headY, zz, flower, SurfaceStyles.Rounded);
                    if (depth < 0.24f && rng.NextFloat() < 0.55f)
                        writer.SetStyled(x + 1, headY, zz, flower, SurfaceStyles.Rounded);
                }
            }
        }

        private static byte ReferenceFlowerColour(ref Unity.Mathematics.Random rng)
        {
            float c = rng.NextFloat();
            if (c < 0.84f) return Mat.FlowerWhite;
            if (c < 0.94f) return Mat.FlowerYellow;
            if (c < 0.985f) return Mat.FlowerPink;
            return Mat.FlowerBlue;
        }

        private static void BuildReferenceRockAccents(IStructureAuthoringSession writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0x6C31u);
            for (int cluster = 0; cluster < 62; cluster++)
            {
                int z = rng.NextInt(-42, 430);
                float depth = math.saturate((z + 42f) / 472f);
                int valley = (int)math.round(ValleyCenterMetres(z * 0.1f) * 10f);
                int side = rng.NextBool() ? -1 : 1;
                int cx = valley + side * rng.NextInt(42, 142);
                if (cx <= TerrainXMin + 9 || cx >= TerrainXMax - 9) continue;

                int pieces = depth < 0.30f ? rng.NextInt(2, 5) : rng.NextInt(1, 3);
                for (int p = 0; p < pieces; p++)
                {
                    int x = cx + rng.NextInt(-6, 7);
                    int zz = z + rng.NextInt(-5, 6);
                    int maxHalf = depth < 0.28f ? 6 : 4;
                    int hx = rng.NextInt(2, maxHalf + 1);
                    int hz = rng.NextInt(2, maxHalf + 1);
                    int hy = depth < 0.34f ? rng.NextInt(1, 4) : rng.NextInt(1, 3);
                    int y = FinalTerrainTopVoxel(x, zz) + hy;
                    StampRoundedBox(writer, new int3(x, y, zz), new int3(hx, hy, hz),
                        1, TerrainLimestoneAccent, SurfaceStyles.Rounded,
                        rng.NextFloat() < math.lerp(0.56f, 0.20f, depth));
                }
            }
        }
    }
}
