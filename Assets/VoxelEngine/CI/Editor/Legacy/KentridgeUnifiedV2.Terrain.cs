using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Features.Emitters;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.CI
{
    internal static partial class KentridgeUnifiedV2
    {
        private static void BuildTerrain(NativeList<Primitive> output, ref int order)
        {
            // Tiered stepped limestone plinth. Three different reconstruction styles keep the
            // terraces from reading like a single extruded block.
            output.Add(RoundedBox(new int3(-150, 0, -55), new int3(310, 8, 225), 6,
                                  2, SurfaceStyles.Rounded, ref order));
            output.Add(RoundedBox(new int3(-140, 8, -37), new int3(292, 7, 195), 5,
                                  1, SurfaceStyles.MasonryJoint, ref order));
            output.Add(RoundedBox(new int3(-128, 15, -20), new int3(268, 6, 168), 4,
                                  2, SurfaceStyles.Rounded, ref order));

            // Broken cliff shelves on the western edge.
            for (int i = 0; i < 7; i++)
            {
                int z = -35 + i * 22;
                int x = -152 + (i % 3) * 5;
                int width = 34 + (i % 2) * 12;
                output.Add(RoundedBox(new int3(x, 4 + i * 2, z),
                                      new int3(width, 9, 20), 4,
                                      2, SurfaceStyles.Rounded, ref order));
            }

            // East hillside rises behind the tower, giving the scene an asymmetrical backdrop.
            for (int i = 0; i < 5; i++)
            {
                output.Add(RoundedBox(new int3(112 + i * 9, 6 + i * 8, 47 + i * 12),
                                      new int3(52, 18, 58), 7,
                                      2, SurfaceStyles.Rounded, ref order));
            }

            // A winding stone path / causeway into the courtyard.
            for (int i = 0; i < 11; i++)
            {
                int x = -52 + i * 10 + (i % 3) * 4;
                int z = -63 + i * 11;
                output.Add(RoundedBox(new int3(x, 15, z), new int3(28, 4, 18), 3,
                                      1, SurfaceStyles.MasonryJoint, ref order));
            }
        }

        private static void BuildGarden(NativeList<Primitive> output, ref int order)
        {
            // Asymmetric garden terraces and planted blocks to keep architecture in a landscape.
            output.Add(RoundedBox(new int3(54, 13, -22), new int3(72, 7, 42), 5,
                                  5, SurfaceStyles.Smooth, ref order, coating: Coatings.Moss));
            output.Add(RoundedBox(new int3(65, 20, -14), new int3(52, 4, 28), 4,
                                  5, SurfaceStyles.Smooth, ref order, coating: Coatings.Moss));

            // Reflecting pool with a sharply cut stone rim.
            output.Add(RoundedBox(new int3(71, 21, -9), new int3(39, 3, 18), 2,
                                  1, SurfaceStyles.MasonryJoint, ref order));
            output.Add(RoundedBox(new int3(76, 23, -6), new int3(29, 2, 12), 2,
                                  6, SurfaceStyles.Sharp, ref order));

            // Low planted islands inside the courtyard.
            output.Add(RoundedBox(new int3(-16, 19, 61), new int3(24, 5, 15), 4,
                                  5, SurfaceStyles.Smooth, ref order, coating: Coatings.Moss));
            output.Add(RoundedBox(new int3(18, 19, 69), new int3(20, 4, 13), 4,
                                  5, SurfaceStyles.Smooth, ref order, coating: Coatings.Moss));
        }
    }
}