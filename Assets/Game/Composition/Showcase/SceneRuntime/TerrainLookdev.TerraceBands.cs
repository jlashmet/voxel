using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using Mat = Game.Materials.Api.GameMaterialIds;   // engine-side Mat constants were removed

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private static void AddTerraceBands(IStructureAuthoringSession writer)
        {
            int[] centres = { 258, 224, 194, 164, 134, 104, 76, 50 };
            for (int band = 0; band < centres.Length; band++)
            {
                int step = band < 3 ? 12 : 10;
                for (int x = -150; x <= 150; x += step)
                {
                    int z = centres[band] - x / 9
                          + Mathf.RoundToInt(5f * Mathf.Sin(x * 0.055f + band * 0.9f));
                    if (z <= TerrainZMin + 4 || z >= TerrainZMax - 4) continue;
                    if (math.abs(x - PathCenterVoxel(z)) < (band < 4 ? 18 : 28)) continue;

                    int y = HeightVoxel(x, z);
                    if (((x / step) + band) % 3 == 0)
                    {
                        StampEllipsoid(writer, new int3(x, y + 2, z),
                            new int3(band < 3 ? 8 : 10, 2, band < 3 ? 4 : 5),
                            band < 6 ? TerrainTurfMid : TerrainTurfNear,
                            SurfaceStyles.Smooth);
                    }
                    else
                    {
                        StampRoundedBox(writer, new int3(x, y + 2, z),
                            new int3(band < 3 ? 4 : 5, 2, band < 3 ? 3 : 4),
                            2, Mat.TerrainLimestone, SurfaceStyles.Rounded, true);
                    }
                }
            }

            AddTerraceBank(writer, -105, 12, 27, 18);
            AddTerraceBank(writer, 104, 16, 27, 18);
            AddTerraceBank(writer, -125, 42, 22, 15);
            AddTerraceBank(writer, 122, 48, 22, 15);
        }

        private static void AddTerraceBank(IStructureAuthoringSession writer, int x, int z, int rx, int rz)
        {
            StampEllipsoid(writer, new int3(x, HeightVoxel(x, z) + 3, z),
                new int3(rx, 3, rz), TerrainTurfNear, SurfaceStyles.Smooth);
        }
    }
}
