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
        private static void BuildWestKeep(NativeList<Primitive> output, ProfileBlockStore profiles,
                                          ref int order)
        {
            byte stone = 1;
            byte trim = 2;
            byte roof = 3;

            // Keep body: broad, asymmetric, stepped back from the cliff.
            output.Add(RoundedBox(new int3(-120, 22, 18), new int3(88, 92, 86), 5,
                                  stone, SurfaceStyles.MasonryJoint, ref order));
            output.Add(RoundedBox(new int3(-111, 31, 8), new int3(70, 70, 14), 4,
                                  trim, SurfaceStyles.Rounded, ref order));

            // Three facade buttresses create deep vertical shadow.
            for (int x = -111; x <= -49; x += 31)
                output.Add(RoundedBox(new int3(x, 20, 3), new int3(8, 72, 16), 2,
                                      trim, SurfaceStyles.Rounded, ref order));

            // Tall recessed windows, two uneven rows.
            for (int row = 0; row < 2; row++)
            {
                int y = row == 0 ? 46 : 78;
                for (int i = 0; i < 3; i++)
                {
                    int x = -101 + i * 25 + (row == 1 ? 7 : 0);
                    output.Add(RoundedBox(new int3(x, y, 5), new int3(8, 22, 20), 3,
                                          stone, SurfaceStyles.Rounded, ref order,
                                          PrimitiveMode.Carve));
                    output.Add(RoundedBox(new int3(x + 1, y + 2, 4), new int3(6, 18, 3), 2,
                                          6, SurfaceStyles.Sharp, ref order));
                }
            }

            // Roof mass and crenellations.
            output.Add(RoundedBox(new int3(-116, 112, 23), new int3(80, 13, 77), 4,
                                  roof, SurfaceStyles.Planar, ref order));
            AddBattlements(output, new int3(-123, 113, 15), new int3(94, 10, 91),
                           stone, ref order);

            // A narrow proud arch on the lower western wall.
            var arch = new ArchFeatureDefinition
            {
                ClearSpan = 24,
                PierHeight = 30,
                RingThickness = 6,
                Depth = 9,
                VoussoirCount = 11,
                JointRecessDepth = 1,
                StoneMaterial = stone,
                PierStyle = SurfaceStyles.MasonryJoint,
                RingStyle = SurfaceStyles.MasonryJoint,
            };
            arch.Emit(new int3(-91, 20, -1), output, profiles);
            output.Add(Box(new int3(-86, 20, 1), new int3(24, 31, 26), stone,
                           SurfaceStyles.MasonryJoint, ref order, PrimitiveMode.Carve));
        }

        private static void BuildGatehouse(NativeList<Primitive> output,
                                           ProfileBlockStore profiles, ref int order)
        {
            byte stone = 1;
            byte trim = 2;
            byte roof = 3;
            byte wood = 4;

            // Compact gatehouse deliberately offset right of centre.
            output.Add(RoundedBox(new int3(-22, 16, 28), new int3(78, 76, 65), 4,
                                  stone, SurfaceStyles.MasonryJoint, ref order));

            // Paired gate towers are unequal so the scene avoids bilateral symmetry.
            AddGateTower(output, new int3(-31, 13, 20), new int3(31, 91, 56),
                         stone, trim, roof, ref order);
            AddGateTower(output, new int3(42, 13, 25), new int3(27, 82, 52),
                         stone, trim, roof, ref order);

            // Main arch on the front face.
            var arch = new ArchFeatureDefinition
            {
                ClearSpan = 30,
                PierHeight = 34,
                RingThickness = 7,
                Depth = 10,
                VoussoirCount = 13,
                JointRecessDepth = 1,
                StoneMaterial = stone,
                PierStyle = SurfaceStyles.MasonryJoint,
                RingStyle = SurfaceStyles.MasonryJoint,
            };
            arch.Emit(new int3(0, 16, 11), output, profiles);
            output.Add(Box(new int3(7, 16, 17), new int3(30, 35, 30), stone,
                           SurfaceStyles.MasonryJoint, ref order, PrimitiveMode.Carve));

            // Heavy door slab set deeper than the stone ring.
            output.Add(RoundedBox(new int3(10, 17, 35), new int3(24, 31, 5), 2,
                                  wood, SurfaceStyles.Planar, ref order));

            // Machicolation / balcony band with deep shadow underneath.
            output.Add(RoundedBox(new int3(-26, 64, 14), new int3(84, 8, 18), 2,
                                  trim, SurfaceStyles.Rounded, ref order));
            for (int x = -21; x <= 52; x += 12)
                output.Add(Box(new int3(x, 57, 19), new int3(5, 8, 9), trim,
                               SurfaceStyles.Planar, ref order));

            AddBattlements(output, new int3(-26, 91, 22), new int3(86, 9, 75),
                           stone, ref order);
        }

        private static void AddGateTower(NativeList<Primitive> output, int3 min, int3 size,
                                         byte stone, byte trim, byte roof, ref int order)
        {
            output.Add(RoundedBox(min, size, 5, stone, SurfaceStyles.MasonryJoint, ref order));
            output.Add(RoundedBox(min + new int3(4, size.y - 22, -4),
                                  new int3(size.x - 8, 20, 11), 3,
                                  trim, SurfaceStyles.Rounded, ref order));
            output.Add(RoundedBox(min + new int3(3, size.y, 5),
                                  new int3(size.x - 6, 9, size.z - 10), 3,
                                  roof, SurfaceStyles.Planar, ref order));
        }

        private static void BuildEastTower(NativeList<Primitive> output, ref int order)
        {
            byte stone = 1;
            byte trim = 2;
            byte roof = 3;
            int3 min = new(84, 14, 45);

            output.Add(RoundedBox(min, new int3(47, 118, 49), 7,
                                  stone, SurfaceStyles.MasonryJoint, ref order));
            output.Add(RoundedBox(new int3(78, 104, 38), new int3(59, 20, 63), 5,
                                  trim, SurfaceStyles.Rounded, ref order));
            output.Add(RoundedBox(new int3(85, 132, 46), new int3(45, 12, 48), 4,
                                  roof, SurfaceStyles.Planar, ref order));
            AddBattlements(output, new int3(77, 125, 37), new int3(62, 10, 66),
                           stone, ref order);

            // Offset slit windows wrap the tower.
            for (int y = 40; y <= 104; y += 24)
            {
                int x = y % 48 == 0 ? 96 : 110;
                output.Add(RoundedBox(new int3(x, y, 41), new int3(5, 15, 12), 2,
                                      stone, SurfaceStyles.Rounded, ref order,
                                      PrimitiveMode.Carve));
                output.Add(RoundedBox(new int3(x + 1, y + 1, 40), new int3(3, 13, 3), 1,
                                      6, SurfaceStyles.Sharp, ref order));
            }
        }

        private static void BuildWallsAndCourtyard(NativeList<Primitive> output, ref int order)
        {
            byte stone = 1;
            byte trim = 2;
            byte wood = 4;

            // Rear and side walls, deliberately broken into separate runs.
            output.Add(Box(new int3(-120, 18, 101), new int3(184, 48, 12), stone,
                           SurfaceStyles.MasonryJoint, ref order));
            output.Add(Box(new int3(64, 16, 83), new int3(14, 52, 71), stone,
                           SurfaceStyles.MasonryJoint, ref order));
            output.Add(Box(new int3(78, 17, 142), new int3(60, 42, 12), stone,
                           SurfaceStyles.MasonryJoint, ref order));

            // Broken/open wall section exposes the courtyard.
            output.Add(RoundedBox(new int3(-9, 12, 105), new int3(55, 9, 10), 3,
                                  trim, SurfaceStyles.Rounded, ref order));

            // Courtyard arcade: small unequal bays produce the repeated architectural rhythm.
            for (int i = 0; i < 5; i++)
            {
                int x = -53 + i * 23;
                output.Add(Box(new int3(x, 16, 79), new int3(6, 42, 8), trim,
                               SurfaceStyles.MasonryJoint, ref order));
                output.Add(RoundedBox(new int3(x + 6, 17, 78), new int3(17, 34, 9), 3,
                                      stone, SurfaceStyles.Rounded, ref order,
                                      PrimitiveMode.Carve));
            }

            // Covered timber walk behind the arcade.
            output.Add(Box(new int3(-50, 53, 80), new int3(122, 5, 26), wood,
                           SurfaceStyles.Planar, ref order));
            for (int x = -46; x <= 66; x += 16)
                output.Add(Box(new int3(x, 17, 83), new int3(4, 38, 4), wood,
                               SurfaceStyles.Planar, ref order));
        }

        private static void AddBattlements(NativeList<Primitive> output, int3 min,
                                           int3 size, byte material, ref int order)
        {
            output.Add(Box(min, new int3(size.x, 3, size.z), material,
                           SurfaceStyles.MasonryJoint, ref order));
            const int merlon = 7;
            const int gap = 5;
            int period = merlon + gap;
            for (int x = 0; x < size.x; x += period)
            {
                int width = math.min(merlon, size.x - x);
                output.Add(Box(min + new int3(x, 3, 0), new int3(width, 6, 5), material,
                               SurfaceStyles.MasonryJoint, ref order));
                output.Add(Box(min + new int3(x, 3, size.z - 5),
                               new int3(width, 6, 5), material,
                               SurfaceStyles.MasonryJoint, ref order));
            }
            for (int z = period; z < size.z - period; z += period)
            {
                int depth = math.min(merlon, size.z - z);
                output.Add(Box(min + new int3(0, 3, z), new int3(5, 6, depth), material,
                               SurfaceStyles.MasonryJoint, ref order));
                output.Add(Box(min + new int3(size.x - 5, 3, z),
                               new int3(5, 6, depth), material,
                               SurfaceStyles.MasonryJoint, ref order));
            }
        }
    }
}