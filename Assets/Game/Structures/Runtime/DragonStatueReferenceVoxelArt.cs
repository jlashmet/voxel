using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Reference-driven 10 cm voxel sculpture used by Model Viewer Dragon A.
    /// The major masses are sampled volumes, but the reference-defining features are authored
    /// explicitly at voxel scale: jaw, teeth, horn tiers, throat shields, digits, wing fingers,
    /// scallops, tail fins and scale bands.
    /// </summary>
    public static class DragonStatueReferenceVoxelArt
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Dark = GameMaterialIds.DarkStone;
        private const byte Plate = GameMaterialIds.Stone;
        private const byte Horn = GameMaterialIds.Stone;
        private const byte Wing = GameMaterialIds.Wood;
        private const byte Moss = GameMaterialIds.Moss;
        private const byte Eye = GameMaterialIds.Gold;

        public static readonly int3 LocalMin = new int3(-112, 0, -102);
        public static readonly int3 LocalSize = new int3(224, 178, 216);

        public static void Author(IStructureAuthoringSession a, int3 o)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            BodyMass(a, o);
            RearLeg(a, o, -1);
            RearLeg(a, o, 1);
            Neck(a, o);
            Head(a, o);
            Foreleg(a, o, -1);
            Foreleg(a, o, 1);
            Wing(a, o, -1);
            Wing(a, o, 1);
            Tail(a, o);
            ThroatArmor(a, o);
            DorsalCrest(a, o);
            SurfaceScaleBands(a, o);
            Patina(a, o);
        }

        private static void BodyMass(IStructureAuthoringSession a, int3 o)
        {
            // Seated reference proportions: compact chest over huge haunches, not a horse torso.
            Ellipsoid(a, o, new float3(0, 38, 28), new float3(30, 24, 34), Body);
            Ellipsoid(a, o, new float3(0, 63, 9), new float3(25, 31, 27), Body);
            Ellipsoid(a, o, new float3(0, 82, -3), new float3(22, 24, 22), Body);
            Ellipsoid(a, o, new float3(-24, 37, 31), new float3(22, 20, 25), Body);
            Ellipsoid(a, o, new float3(24, 37, 31), new float3(22, 20, 25), Body);

            // Underside/chest shadow creates one readable torso instead of stacked spheres.
            Ellipsoid(a, o, new float3(0, 49, -16), new float3(17, 22, 10), Dark);
            Capsule(a, o, new float3(0, 75, -7), new float3(0, 54, -13), 17f, 19f, Body);
        }

        private static void Neck(IStructureAuthoringSession a, int3 o)
        {
            // Long S-neck with deliberate forward lean at the skull.
            Capsule(a, o, new float3(0, 79, -4), new float3(-2, 100, -19), 16f, 13.5f, Body);
            Capsule(a, o, new float3(-2, 99, -19), new float3(1, 121, -33), 13.5f, 10.5f, Body);
            Capsule(a, o, new float3(1, 120, -33), new float3(-1, 139, -47), 10.5f, 8.5f, Body);

            // Side scale shelves are small enough to read as scales, not masonry blocks.
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f;
                float y = math.lerp(88f, 132f, t);
                float z = math.lerp(-12f, -42f, t);
                float x = math.lerp(13f, 8f, t);
                int sy = (int)math.round(y);
                int sz = (int)math.round(z);
                int sx = (int)math.round(x);
                Box(a, o, new int3(-sx - 2, sy - 2, sz - 2), new int3(5, 4, 4), Dark);
                Box(a, o, new int3(sx - 2, sy - 2, sz - 2), new int3(5, 4, 4), Dark);
            }
        }

        private static void Head(IStructureAuthoringSession a, int3 o)
        {
            // Low, long skull. The muzzle points forward and slightly down like the concept.
            Ellipsoid(a, o, new float3(0, 146, -56), new float3(18, 11, 15), Body);
            Ellipsoid(a, o, new float3(0, 143, -69), new float3(14, 8, 14), Body);
            Ellipsoid(a, o, new float3(0, 140, -81), new float3(10, 6, 11), Body);
            WedgeSnout(a, o);

            // Cheek/jaw hinges create the dragon's angular side silhouette.
            Ellipsoid(a, o, new float3(-13, 137, -60), new float3(7, 9, 9), Body);
            Ellipsoid(a, o, new float3(13, 137, -60), new float3(7, 9, 9), Body);

            // Mouth cavity, then a separate lower jaw/chin.
            Ellipsoid(a, o, new float3(0, 134, -75), new float3(10, 5.5f, 17), Empty);
            Box(a, o, new int3(-9, 132, -93), new int3(19, 5, 18), Empty);
            Capsule(a, o, new float3(-10, 130, -62), new float3(-8, 124, -86), 3.2f, 1.8f, Dark);
            Capsule(a, o, new float3(10, 130, -62), new float3(8, 124, -86), 3.2f, 1.8f, Dark);
            Capsule(a, o, new float3(-8, 124, -86), new float3(8, 124, -86), 2.4f, 2.4f, Dark);
            Ellipsoid(a, o, new float3(0, 123, -78), new float3(9, 3.5f, 12), Body);

            // Strong layered brows with recessed eyes.
            Capsule(a, o, new float3(-2, 151, -65), new float3(-17, 153, -55), 5f, 1.5f, Dark);
            Capsule(a, o, new float3(2, 151, -65), new float3(17, 153, -55), 5f, 1.5f, Dark);
            Ellipsoid(a, o, new float3(-7, 146, -69), new float3(4, 3, 3.5f), Empty);
            Ellipsoid(a, o, new float3(7, 146, -69), new float3(4, 3, 3.5f), Empty);
            Box(a, o, new int3(-8, 146, -72), new int3(3, 2, 2), Eye);
            Box(a, o, new int3(6, 146, -72), new int3(3, 2, 2), Eye);

            // Nose bridge + nostrils.
            Capsule(a, o, new float3(0, 147, -66), new float3(0, 142, -88), 5.5f, 3.5f, Body);
            Box(a, o, new int3(-6, 141, -91), new int3(3, 2, 3), Empty);
            Box(a, o, new int3(4, 141, -91), new int3(3, 2, 3), Empty);

            Teeth(a, o);
            Crown(a, o);
            CheekSpines(a, o, -1);
            CheekSpines(a, o, 1);

            // Small forehead plates produce the layered armored concept read.
            for (int row = 0; row < 4; row++)
            {
                int y = 154 - row * 4;
                int z = -58 - row * 5;
                int half = 8 - row;
                Box(a, o, new int3(-half, y, z), new int3(half * 2 + 1, 2, 4), Plate);
            }
        }

        private static void WedgeSnout(IStructureAuthoringSession a, int3 o)
        {
            // Stair-step wedge gives a crisp voxel silhouette with a narrow nose.
            for (int i = 0; i < 10; i++)
            {
                int half = math.max(4, 11 - i / 2);
                int height = math.max(4, 8 - i / 3);
                Box(a, o,
                    new int3(-half, 137 - height / 2, -78 - i * 2),
                    new int3(half * 2 + 1, height, 3), Body);
            }
        }

        private static void Teeth(IStructureAuthoringSession a, int3 o)
        {
            // Long front canines + smaller alternating teeth; each tooth spans several 10 cm voxels.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                Capsule(a, o, new float3(7.5f * s, 138, -71), new float3(7.5f * s, 129, -73), 1.4f, 0.15f, Horn);
                for (int i = 0; i < 4; i++)
                {
                    float x = (2.5f + i * 2.8f) * s;
                    float z = -76 - i * 3.4f;
                    Capsule(a, o, new float3(x, 137, z), new float3(x, 131, z - 1), 1.0f, 0.12f, Horn);
                }
                for (int i = 0; i < 3; i++)
                {
                    float x = (3.5f + i * 3.2f) * s;
                    float z = -72 - i * 4f;
                    Capsule(a, o, new float3(x, 125, z), new float3(x, 131, z - 1), 0.9f, 0.12f, Horn);
                }
            }
        }

        private static void Crown(IStructureAuthoringSession a, int3 o)
        {
            // Concept has several swept-back horns rather than one goat horn.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                SweptHorn(a, o,
                    new float3(8*s, 154, -52), new float3(18*s, 164, -43),
                    new float3(31*s, 171, -29), new float3(42*s, 173, -12),
                    new float3(49*s, 168, 2), 4.2f);
                SweptHorn(a, o,
                    new float3(12*s, 150, -49), new float3(24*s, 157, -37),
                    new float3(38*s, 160, -23), new float3(49*s, 156, -9),
                    new float3(55*s, 149, 1), 3.2f);
                SweptHorn(a, o,
                    new float3(14*s, 145, -47), new float3(27*s, 148, -35),
                    new float3(39*s, 146, -22), new float3(48*s, 140, -11),
                    new float3(52*s, 134, -3), 2.4f);
            }
        }

        private static void SweptHorn(IStructureAuthoringSession a, int3 o,
            float3 p0, float3 p1, float3 p2, float3 p3, float3 p4, float radius)
        {
            Capsule(a, o, p0, p1, radius, radius * .78f, Horn);
            Capsule(a, o, p1, p2, radius * .78f, radius * .53f, Horn);
            Capsule(a, o, p2, p3, radius * .53f, radius * .27f, Horn);
            Capsule(a, o, p3, p4, radius * .27f, .12f, Horn);
        }

        private static void CheekSpines(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            Capsule(a, o, new float3(16*s, 145, -57), new float3(31*s, 149, -44), 2.8f, .15f, Horn);
            Capsule(a, o, new float3(16*s, 139, -54), new float3(30*s, 138, -40), 2.5f, .15f, Horn);
            Capsule(a, o, new float3(14*s, 133, -51), new float3(26*s, 128, -38), 2.2f, .15f, Horn);
            Capsule(a, o, new float3(11*s, 127, -47), new float3(21*s, 120, -35), 1.8f, .12f, Horn);
        }

        private static void Foreleg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 shoulder = new float3(21*s, 78, -5);
            float3 elbow = new float3(34*s, 55, -23);
            float3 wrist = new float3(30*s, 27, -39);
            float3 palm = new float3(29*s, 10, -55);

            Ellipsoid(a, o, shoulder, new float3(12, 14, 13), Body);
            Capsule(a, o, shoulder, elbow, 10.5f, 7.5f, Body);
            Ellipsoid(a, o, elbow, new float3(8.5f, 9, 10), Body);
            Capsule(a, o, elbow, wrist, 7.5f, 5.3f, Body);
            Ellipsoid(a, o, wrist, new float3(5.5f, 6, 7), Dark);
            Capsule(a, o, wrist, palm, 5.2f, 4.2f, Body);
            Ellipsoid(a, o, new float3(29*s, 8, -59), new float3(11, 5, 13), Body);

            // Four fingers with two phalanges, not spikes glued to a paddle.
            for (int i = 0; i < 4; i++)
            {
                float spread = (i - 1.5f) * 4.7f;
                float x0 = 29*s + spread;
                float z0 = -63 - (i == 1 || i == 2 ? 3 : 0);
                float3 knuckle = new float3(x0, 8, -61);
                float3 digit = new float3(x0 + 1.3f*s, 5.5f, z0 - 6);
                float3 claw = new float3(x0 + 2.4f*s, 2.5f, z0 - 15);
                Capsule(a, o, knuckle, digit, 2.5f, 1.5f, Body);
                Capsule(a, o, digit, claw, 1.5f, .12f, Horn);
            }

            // Knuckle armor ridges.
            for (int i = 0; i < 3; i++)
                Box(a, o, new int3((int)(s * 26) - 3, 18 + i * 7, -47 + i * 3), new int3(7, 3, 5), Plate);
        }

        private static void RearLeg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 hip = new float3(25*s, 39, 30);
            float3 knee = new float3(42*s, 27, 11);
            float3 hock = new float3(38*s, 12, -13);

            Ellipsoid(a, o, hip, new float3(21, 19, 24), Body);
            Capsule(a, o, hip, knee, 14f, 9.5f, Body);
            Ellipsoid(a, o, knee, new float3(12, 11, 13), Body);
            Capsule(a, o, knee, hock, 8.5f, 5.5f, Body);
            Ellipsoid(a, o, new float3(38*s, 8, -23), new float3(15, 6, 19), Body);

            for (int i = 0; i < 4; i++)
            {
                float spread = (i - 1.5f) * 5.4f;
                float x0 = 38*s + spread;
                float z0 = -30 - (i == 1 || i == 2 ? 4 : 0);
                float3 digit = new float3(x0 + s, 5.5f, z0 - 8);
                float3 claw = new float3(x0 + 2.2f*s, 2.5f, z0 - 19);
                Capsule(a, o, new float3(x0, 8, -27), digit, 2.8f, 1.6f, Body);
                Capsule(a, o, digit, claw, 1.6f, .12f, Horn);
            }
        }

        private static void Wing(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 root = new float3(15*s, 88, 8);
            float3 elbow = new float3(52*s, 126, 18);
            float3 wrist = new float3(96*s, 144, 28);
            float3 hook = new float3(109*s, 127, 36);

            // Finger endpoints deliberately define a deep scalloped bat-wing silhouette.
            float3 f0 = new float3(106*s, 111, 35);
            float3 f1 = new float3(100*s, 91, 34);
            float3 f2 = new float3(91*s, 72, 31);
            float3 f3 = new float3(77*s, 55, 27);
            float3 f4 = new float3(59*s, 43, 22);
            float3 inner = new float3(30*s, 54, 13);

            // Separate membrane bays prevent the tarp/rectangle read.
            Membrane(a, o, root, elbow, inner, 1.35f, Dark);
            Membrane(a, o, elbow, wrist, inner, 1.35f, Wing);
            Membrane(a, o, wrist, f0, f1, 1.25f, Wing);
            Membrane(a, o, wrist, f1, f2, 1.25f, Wing);
            Membrane(a, o, wrist, f2, f3, 1.25f, Wing);
            Membrane(a, o, wrist, f3, f4, 1.25f, Wing);
            Membrane(a, o, wrist, f4, inner, 1.25f, Wing);

            // Deep concave bites between finger tips.
            Ellipsoid(a, o, new float3(104*s, 101, 34), new float3(11, 9, 5), Empty);
            Ellipsoid(a, o, new float3(97*s, 82, 32), new float3(12, 10, 5), Empty);
            Ellipsoid(a, o, new float3(84*s, 63, 28), new float3(13, 10, 5), Empty);
            Ellipsoid(a, o, new float3(68*s, 49, 23), new float3(12, 8, 5), Empty);

            // Restore heavy leading bones and all distinct fingers after carving.
            Capsule(a, o, root, elbow, 7.5f, 5.5f, Body);
            Capsule(a, o, elbow, wrist, 5.5f, 3.3f, Body);
            Capsule(a, o, wrist, hook, 3.3f, .15f, Horn);
            Capsule(a, o, wrist, f0, 3.2f, .65f, Dark);
            Capsule(a, o, wrist, f1, 3.0f, .58f, Dark);
            Capsule(a, o, wrist, f2, 2.8f, .52f, Dark);
            Capsule(a, o, wrist, f3, 2.5f, .46f, Dark);
            Capsule(a, o, wrist, f4, 2.2f, .35f, Dark);

            // Small armor plates ride the leading edge; each is only a handful of voxels.
            for (int i = 0; i < 11; i++)
            {
                float t = i / 10f;
                float3 p = t < .46f
                    ? math.lerp(root, elbow, t / .46f)
                    : math.lerp(elbow, wrist, (t - .46f) / .54f);
                int3 q = (int3)math.round(p + new float3(-2*s, 2, -2));
                Box(a, o, q, new int3(5, 3, 5), Plate);
            }
        }

        private static void Tail(IStructureAuthoringSession a, int3 o)
        {
            // Thick base exits right haunch, sweeps around foreground, then points left like reference.
            float3[] p =
            {
                new float3(17, 40, 39), new float3(43, 35, 54), new float3(69, 28, 56),
                new float3(91, 20, 43), new float3(101, 13, 20), new float3(98, 9, -8),
                new float3(83, 7, -34), new float3(59, 6, -55), new float3(30, 5, -70),
                new float3(0, 4, -79), new float3(-29, 4, -82), new float3(-50, 4, -76)
            };
            float[] r = { 15f, 13.5f, 12f, 10f, 8f, 6.3f, 5f, 3.9f, 3f, 2.1f, 1.2f, .18f };
            for (int i = 0; i < p.Length - 1; i++)
                Capsule(a, o, p[i], p[i + 1], r[i], r[i + 1], Body);

            // Paired lateral fins/spines along the visible foreground tail.
            for (int i = 3; i < p.Length - 2; i++)
            {
                float3 baseP = p[i];
                float size = math.lerp(5.5f, 2.0f, (i - 3) / 7f);
                Capsule(a, o, baseP + new float3(0, size * .2f, -1),
                    baseP + new float3(0, size + 4f, 0), size * .45f, .12f, Horn);
                if ((i & 1) == 0)
                    Capsule(a, o, baseP + new float3(0, 1, 0),
                        baseP + new float3(0, 2, -size - 3f), size * .35f, .12f, Horn);
            }
        }

        private static void ThroatArmor(IStructureAuthoringSession a, int3 o)
        {
            // Overlapping ventral shields run from jaw to belly. Each plate is a tapered voxel shield.
            for (int i = 0; i < 12; i++)
            {
                int y = 128 - i * 7;
                int z = -39 + i * 2;
                int half = math.min(14, 7 + i / 2);
                int h = 5;
                Box(a, o, new int3(-half, y - 2, z - 4), new int3(half * 2 + 1, h, 5), Plate);
                for (int row = 0; row < 4; row++)
                {
                    int rh = math.max(1, half - row * 3);
                    Box(a, o, new int3(-rh, y - 3 - row, z - 5), new int3(rh * 2 + 1, 1, 5), Plate);
                }
            }
        }

        private static void DorsalCrest(IStructureAuthoringSession a, int3 o)
        {
            float3[] basePoints =
            {
                new float3(0, 133, -35), new float3(0, 119, -25), new float3(0, 104, -15),
                new float3(0, 89, -3), new float3(0, 76, 10), new float3(0, 63, 23),
                new float3(0, 51, 35)
            };
            for (int i = 0; i < basePoints.Length; i++)
            {
                float h = math.lerp(10f, 6f, i / (float)(basePoints.Length - 1));
                Capsule(a, o, basePoints[i], basePoints[i] + new float3(0, h, 4), 2.3f, .12f, Horn);
            }
        }

        private static void SurfaceScaleBands(IStructureAuthoringSession a, int3 o)
        {
            // Dense but shallow 10 cm-scale rows create the concept's reptilian surface without stone blocks.
            for (int side = -1; side <= 1; side += 2)
            {
                int s = side;
                for (int row = 0; row < 10; row++)
                {
                    int y = 91 - row * 5;
                    int z = 2 + row * 3;
                    int x = 18 + row / 3;
                    for (int j = 0; j < 4; j++)
                    {
                        int dz = (j - 1) * 4;
                        Box(a, o, new int3(s * x - (s > 0 ? 1 : 2), y, z + dz), new int3(3, 2, 3), Dark);
                    }
                }

                // Haunch scale arcs.
                for (int i = 0; i < 9; i++)
                {
                    float ang = math.radians(-65 + i * 16);
                    int x = (int)math.round(25*s + math.sin(ang) * 18*s);
                    int y = (int)math.round(39 + math.cos(ang) * 13);
                    int z = 30 - i / 2;
                    Box(a, o, new int3(x - 1, y - 1, z), new int3(3, 2, 4), Dark);
                }
            }
        }

        private static void Patina(IStructureAuthoringSession a, int3 o)
        {
            // Sparse deterministic moss accents only on upper-facing architectural-like ridges.
            int3[] patches =
            {
                new int3(-10, 151, -53), new int3(12, 155, -44), new int3(-5, 112, -22),
                new int3(17, 91, 3), new int3(-21, 70, 12), new int3(30, 45, 35),
                new int3(67, 30, 52), new int3(90, 16, 26), new int3(-55, 127, 18)
            };
            foreach (int3 p in patches)
                Box(a, o, p, new int3(3, 2, 3), Moss);
        }

        private static void Box(IStructureAuthoringSession a, int3 o, int3 min, int3 size, byte material)
        {
            int3 max = min + size;
            for (int y = min.y; y < max.y; y++)
            for (int z = min.z; z < max.z; z++)
            for (int x = min.x; x < max.x; x++)
                a.Set(o.x + x, o.y + y, o.z + z, material);
        }

        private static void Ellipsoid(IStructureAuthoringSession a, int3 o, float3 c, float3 r, byte material)
        {
            int3 min = (int3)math.floor(c - r - 1f);
            int3 max = (int3)math.ceil(c + r + 1f);
            float3 safe = math.max(r, new float3(.5f));
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 q = (new float3(x + .5f, y + .5f, z + .5f) - c) / safe;
                if (math.dot(q, q) <= 1f)
                    a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void Capsule(IStructureAuthoringSession a, int3 o, float3 start, float3 end,
            float r0, float r1, byte material)
        {
            float mr = math.max(r0, r1);
            int3 min = (int3)math.floor(math.min(start, end) - mr - 1f);
            int3 max = (int3)math.ceil(math.max(start, end) + mr + 1f);
            float3 axis = end - start;
            float len2 = math.max(.0001f, math.dot(axis, axis));
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 p = new float3(x + .5f, y + .5f, z + .5f);
                float t = math.saturate(math.dot(p - start, axis) / len2);
                float3 d = p - (start + axis * t);
                float r = math.lerp(r0, r1, t);
                if (math.dot(d, d) <= r * r)
                    a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void Membrane(IStructureAuthoringSession a, int3 o, float3 va, float3 vb, float3 vc,
            float halfThickness, byte material)
        {
            float3 n = math.normalizesafe(math.cross(vb - va, vc - va), new float3(0, 0, 1));
            int3 min = (int3)math.floor(math.min(va, math.min(vb, vc)) - halfThickness - 1f);
            int3 max = (int3)math.ceil(math.max(va, math.max(vb, vc)) + halfThickness + 1f);
            float3 v0 = vb - va;
            float3 v1 = vc - va;
            float d00 = math.dot(v0, v0);
            float d01 = math.dot(v0, v1);
            float d11 = math.dot(v1, v1);
            float denom = d00 * d11 - d01 * d01;
            if (math.abs(denom) < .0001f) return;

            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 p = new float3(x + .5f, y + .5f, z + .5f);
                float dist = math.dot(p - va, n);
                if (math.abs(dist) > halfThickness) continue;
                float3 projected = p - n * dist;
                float3 v2 = projected - va;
                float d20 = math.dot(v2, v0);
                float d21 = math.dot(v2, v1);
                float v = (d11 * d20 - d01 * d21) / denom;
                float w = (d00 * d21 - d01 * d20) / denom;
                float u = 1f - v - w;
                if (u >= 0f && v >= 0f && w >= 0f)
                    a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }
    }
}
