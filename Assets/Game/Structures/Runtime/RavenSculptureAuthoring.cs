using System;
using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Deterministic high-resolution raven sculpt authored into canonical voxel storage. The
    /// overlapping integer primitives deliberately model anatomy and feather groups rather than
    /// importing a presentation mesh, so destruction and collision consume the same cells.
    /// </summary>
    public static class RavenSculptureAuthoring
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte CoreFeather = GameMaterialIds.DarkStone;
        private const byte FlightFeather = GameMaterialIds.Slate;
        private const byte BlueSheen = GameMaterialIds.Crystal;
        private const byte VioletSheen = GameMaterialIds.Glass;
        private const byte Beak = GameMaterialIds.Bedrock;
        private const byte Talon = GameMaterialIds.Stone;
        private const byte Eye = GameMaterialIds.Gold;

        public static readonly int3 LocalMin = new int3(-72, 0, -82);
        public static readonly int3 LocalSize = new int3(144, 168, 174);

        public static void Author(IStructureAuthoringSession authoring, int3 origin)
        {
            if (authoring == null) throw new ArgumentNullException(nameof(authoring));

            Body(authoring, origin);
            Tail(authoring, origin);
            FoldedWing(authoring, origin, -1);
            FoldedWing(authoring, origin, 1);
            NeckAndHead(authoring, origin);
            Face(authoring, origin);
            LegsAndTalons(authoring, origin, -1);
            LegsAndTalons(authoring, origin, 1);
            BreastFeathers(authoring, origin);
            IridescentAccents(authoring, origin);
        }

        private static void Body(IStructureAuthoringSession a, int3 o)
        {
            // A raven is a deep-keel, narrow-shouldered bird. Overlapping masses form a continuous
            // pear-shaped breast without reducing the result to one round primitive.
            Ellipsoid(a, o, new int3(0, 60, 5), new int3(31, 42, 32), CoreFeather);
            Ellipsoid(a, o, new int3(0, 87, 11), new int3(29, 37, 35), CoreFeather);
            Ellipsoid(a, o, new int3(0, 105, -3), new int3(25, 31, 29), CoreFeather);
            Ellipsoid(a, o, new int3(0, 49, 23), new int3(25, 31, 28), FlightFeather);

            // The forward breast and rear mantle establish the characteristic upright perch.
            Ellipsoid(a, o, new int3(0, 75, -22), new int3(25, 39, 23), CoreFeather);
            Ellipsoid(a, o, new int3(0, 91, 31), new int3(25, 28, 25), FlightFeather);
        }

        private static void NeckAndHead(IStructureAuthoringSession a, int3 o)
        {
            Ellipsoid(a, o, new int3(0, 116, -12), new int3(23, 28, 24), CoreFeather);
            Ellipsoid(a, o, new int3(0, 136, -24), new int3(23, 22, 24), CoreFeather);
            Ellipsoid(a, o, new int3(0, 143, -31), new int3(20, 16, 20), CoreFeather);

            // Shaggy hackles break the lower-neck outline and read at three-quarter distance.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 5; i++)
                {
                    int x = side * (19 + i);
                    int y = 116 - i * 5;
                    int z = -13 + i * 3;
                    Stroke(a, o, new int3(x, y + 6, z), new int3(x + side * 5, y - 5, z + 4),
                        4, 1, FlightFeather);
                }
            }
        }

        private static void Face(IStructureAuthoringSession a, int3 o)
        {
            // Heavy brow and cheek masses are essential to distinguish a raven from a songbird.
            Ellipsoid(a, o, new int3(-13, 143, -39), new int3(10, 7, 8), CoreFeather);
            Ellipsoid(a, o, new int3(13, 143, -39), new int3(10, 7, 8), CoreFeather);
            Stroke(a, o, new int3(-3, 149, -38), new int3(-18, 146, -34), 4, 1, FlightFeather);
            Stroke(a, o, new int3(3, 149, -38), new int3(18, 146, -34), 4, 1, FlightFeather);

            // Long wedge with a slightly dropped hook. The narrow lower mandible is authored
            // separately so the mouth seam survives the high-density head mass.
            BeakWedge(a, o, upper: true);
            BeakWedge(a, o, upper: false);
            Box(a, o, new int3(-12, 135, -63), new int3(25, 2, 20), Empty);
            Stroke(a, o, new int3(-1, 139, -58), new int3(0, 133, -78), 5, 1, Beak);

            // Nostril cuts and stylized amber eyes.
            Ellipsoid(a, o, new int3(-6, 143, -53), new int3(3, 2, 3), Empty);
            Ellipsoid(a, o, new int3(6, 143, -53), new int3(3, 2, 3), Empty);
            Ellipsoid(a, o, new int3(-17, 143, -43), new int3(3, 3, 2), Eye);
            Ellipsoid(a, o, new int3(17, 143, -43), new int3(3, 3, 2), Eye);
            Set(a, o, -18, 143, -44, Beak);
            Set(a, o, 18, 143, -44, Beak);

            // Small nasal and chin bristles keep the beak/head transition from looking welded.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    Stroke(a, o, new int3(side * (3 + i * 2), 140 - i, -45),
                        new int3(side * (5 + i * 3), 137 - i, -55 - i * 2), 2, 0, FlightFeather);
                }
            }
        }

        private static void BeakWedge(IStructureAuthoringSession a, int3 o, bool upper)
        {
            const int baseZ = -45;
            const int length = 36;
            for (int i = 0; i <= length; i++)
            {
                int z = baseZ - i;
                int width = math.max(1, 15 - i * 14 / length);
                int hook = i > 24 ? (i - 24) * (i - 24) / 30 : 0;
                int centreY = 139 - i / 12 - hook;
                int height = math.max(1, (upper ? 7 : 4) - i * (upper ? 5 : 3) / length);
                int minY = upper ? centreY : centreY - height - 2;
                int maxY = upper ? centreY + height : centreY - 2;
                for (int y = minY; y <= maxY; y++)
                for (int x = -width; x <= width; x++)
                {
                    // Chamfer the wedge corners to avoid a rectangular bill.
                    int vertical = upper ? maxY - y : y - minY;
                    if (math.abs(x) + vertical <= width + math.max(1, height / 2))
                        Set(a, o, x, y, z, Beak);
                }
            }
        }

        private static void FoldedWing(IStructureAuthoringSession a, int3 o, int side)
        {
            int s = side;
            Ellipsoid(a, o, new int3(24 * s, 91, 13), new int3(13, 31, 29), FlightFeather);
            Ellipsoid(a, o, new int3(27 * s, 66, 26), new int3(12, 34, 30), FlightFeather);

            // Layered coverts make the wing surface read as feathers instead of a smooth shell.
            for (int row = 0; row < 4; row++)
            {
                int y = 105 - row * 14;
                int z = 4 + row * 8;
                int count = 4 + row;
                for (int i = 0; i < count; i++)
                {
                    int lateral = (i - (count - 1) / 2) * 4;
                    int x = s * (29 + math.abs(lateral) / 5);
                    Stroke(a, o, new int3(x, y, z + lateral),
                        new int3(x + s * 3, y - 14 - row * 2, z + 17 + lateral),
                        5, 1, (i + row) % 3 == 0 ? BlueSheen : FlightFeather);
                }
            }

            // Seven distinct primaries extend beyond the body and converge at the shoulder.
            for (int i = 0; i < 7; i++)
            {
                int rootY = 84 - i * 4;
                int rootZ = 20 + i * 3;
                int tipX = s * (37 + (i % 2) * 2);
                int tipY = 27 - i * 2;
                int tipZ = 52 + i * 5;
                Stroke(a, o, new int3(25 * s, rootY, rootZ), new int3(tipX, tipY, tipZ),
                    5 - i / 3, 1, i == 1 || i == 5 ? VioletSheen : FlightFeather);
                Stroke(a, o, new int3(tipX, tipY, tipZ), new int3(tipX, tipY - 3, tipZ + 10),
                    2, 0, FlightFeather);
            }
        }

        private static void Tail(IStructureAuthoringSession a, int3 o)
        {
            // A slightly fanned, wedge-shaped tail is the raven's strongest rear silhouette cue.
            for (int i = -3; i <= 3; i++)
            {
                int rootX = i * 5;
                int tipX = i * 9;
                int tipZ = 80 - math.abs(i) * 2;
                Stroke(a, o, new int3(rootX, 52, 32), new int3(tipX, 13 + math.abs(i), tipZ),
                    7, 2, i == -2 || i == 2 ? BlueSheen : FlightFeather);
                Ellipsoid(a, o, new int3(tipX, 13 + math.abs(i), tipZ), new int3(3, 2, 8),
                    FlightFeather);
            }
        }

        private static void LegsAndTalons(IStructureAuthoringSession a, int3 o, int side)
        {
            int s = side;
            Stroke(a, o, new int3(13 * s, 45, -1), new int3(13 * s, 18, -8), 5, 4, Talon);
            Ellipsoid(a, o, new int3(13 * s, 15, -10), new int3(6, 4, 7), Talon);

            for (int toe = -1; toe <= 1; toe++)
            {
                int spreadX = 13 * s + toe * 7;
                int tipZ = -30 - (toe == 0 ? 5 : 0);
                Stroke(a, o, new int3(13 * s, 14, -12), new int3(spreadX, 7, tipZ), 3, 1, Talon);
                Stroke(a, o, new int3(spreadX, 7, tipZ), new int3(spreadX, 4, tipZ - 7), 2, 0, Beak);
            }

            // Opposing rear toe makes the foot read as a perching bird rather than three pegs.
            Stroke(a, o, new int3(13 * s, 14, -7), new int3(16 * s, 7, 9), 3, 1, Talon);
            Stroke(a, o, new int3(16 * s, 7, 9), new int3(18 * s, 4, 15), 2, 0, Beak);
        }

        private static void BreastFeathers(IStructureAuthoringSession a, int3 o)
        {
            for (int row = 0; row < 7; row++)
            {
                int y = 105 - row * 10;
                int halfCount = 2 + row / 2;
                int z = -26 - row / 2;
                for (int i = -halfCount; i <= halfCount; i++)
                {
                    int x = i * 6;
                    int materialSelector = math.abs(i * 3 + row * 5) % 7;
                    byte material = materialSelector == 0 ? BlueSheen : CoreFeather;
                    Stroke(a, o, new int3(x, y + 4, z), new int3(x, y - 7, z - 3), 4, 1, material);
                }
            }
        }

        private static void IridescentAccents(IStructureAuthoringSession a, int3 o)
        {
            // Restrained cool-color bands mimic the blue/violet structural sheen of black corvid
            // feathers. They follow existing anatomy and never change the occupied silhouette.
            Stroke(a, o, new int3(-15, 125, -8), new int3(12, 119, -4), 3, 1, BlueSheen);
            Stroke(a, o, new int3(-17, 100, 23), new int3(18, 95, 29), 3, 1, VioletSheen);
            Ellipsoid(a, o, new int3(-24, 76, 31), new int3(4, 9, 13), BlueSheen);
            Ellipsoid(a, o, new int3(24, 72, 35), new int3(4, 10, 14), VioletSheen);
        }

        private static void Ellipsoid(
            IStructureAuthoringSession a, int3 o, int3 centre, int3 radius, byte material)
        {
            long rx2 = (long)radius.x * radius.x;
            long ry2 = (long)radius.y * radius.y;
            long rz2 = (long)radius.z * radius.z;
            long limit = rx2 * ry2 * rz2;
            for (int y = centre.y - radius.y; y <= centre.y + radius.y; y++)
            for (int z = centre.z - radius.z; z <= centre.z + radius.z; z++)
            for (int x = centre.x - radius.x; x <= centre.x + radius.x; x++)
            {
                long dx = x - centre.x;
                long dy = y - centre.y;
                long dz = z - centre.z;
                long value = dx * dx * ry2 * rz2 + dy * dy * rx2 * rz2 + dz * dz * rx2 * ry2;
                if (value <= limit) Set(a, o, x, y, z, material);
            }
        }

        private static void Stroke(
            IStructureAuthoringSession a,
            int3 o,
            int3 start,
            int3 end,
            int startRadius,
            int endRadius,
            byte material)
        {
            int3 delta = end - start;
            int steps = math.max(1, math.cmax(math.abs(delta)));
            for (int i = 0; i <= steps; i++)
            {
                int3 centre = start + new int3(
                    RoundedDivide(delta.x * i, steps),
                    RoundedDivide(delta.y * i, steps),
                    RoundedDivide(delta.z * i, steps));
                int radius = math.max(0, RoundedDivide(startRadius * (steps - i) + endRadius * i, steps));
                if (radius == 0)
                    Set(a, o, centre.x, centre.y, centre.z, material);
                else
                    Ellipsoid(a, o, centre, new int3(radius), material);
            }
        }

        private static int RoundedDivide(int numerator, int denominator)
        {
            if (numerator >= 0) return (numerator + denominator / 2) / denominator;
            return -((-numerator + denominator / 2) / denominator);
        }

        private static void Box(IStructureAuthoringSession a, int3 o, int3 min, int3 size, byte material)
        {
            for (int y = 0; y < size.y; y++)
            for (int z = 0; z < size.z; z++)
            for (int x = 0; x < size.x; x++)
                Set(a, o, min.x + x, min.y + y, min.z + z, material);
        }

        private static void Set(
            IStructureAuthoringSession a, int3 o, int x, int y, int z, byte material) =>
            a.Set(o.x + x, o.y + y, o.z + z, material);
    }
}
