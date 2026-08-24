using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Reference-driven voxel statue. This version prioritizes silhouette, anatomy and readable
    /// secondary forms over smooth primitive convenience. It is intentionally authored as dense
    /// voxels through the canonical structure-authoring session.
    /// </summary>
    public static class DragonStatueAAAAuthoring
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
        public static readonly int3 LocalSize = new int3(224, 178, 214);

        public static void Author(IStructureAuthoringSession a, int3 o)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            BodyMass(a, o);
            Neck(a, o);
            Head(a, o);
            Foreleg(a, o, -1);
            Foreleg(a, o, 1);
            RearLeg(a, o, -1);
            RearLeg(a, o, 1);
            WingAssembly(a, o, -1);
            WingAssembly(a, o, 1);
            Tail(a, o);
            ChestPlates(a, o);
            SpineCrest(a, o);
            SurfaceAccents(a, o);
        }

        private static void BodyMass(IStructureAuthoringSession a, int3 o)
        {
            Ellipsoid(a, o, new float3(0, 44, 23), new float3(30, 25, 34), Body);
            Ellipsoid(a, o, new float3(0, 67, 8), new float3(25, 30, 27), Body);
            Ellipsoid(a, o, new float3(0, 84, -4), new float3(20, 23, 21), Body);

            // Shoulder caps and low haunches keep the torso from reading as stacked balls.
            Ellipsoid(a, o, new float3(-21, 72, 0), new float3(14, 17, 16), Body);
            Ellipsoid(a, o, new float3(21, 72, 0), new float3(14, 17, 16), Body);
            Ellipsoid(a, o, new float3(-24, 39, 24), new float3(20, 18, 22), Body);
            Ellipsoid(a, o, new float3(24, 39, 24), new float3(20, 18, 22), Body);

            // Underside shadow gives a ribcage/abdomen transition.
            Ellipsoid(a, o, new float3(0, 48, -11), new float3(18, 22, 12), Dark);
        }

        private static void Neck(IStructureAuthoringSession a, int3 o)
        {
            // Backward S curve with changing cross-section. The reference neck is broad at the chest,
            // narrow under the jaw and not a straight cylinder.
            Capsule(a, o, new float3(0, 80, -5), new float3(-2, 99, -18), 17f, 14f, Body);
            Capsule(a, o, new float3(-2, 98, -18), new float3(1, 119, -31), 14f, 11f, Body);
            Capsule(a, o, new float3(1, 118, -31), new float3(-1, 136, -45), 11f, 8.5f, Body);

            // Cheek-side scale masses break the tube silhouette.
            for (int y = 91; y <= 128; y += 9)
            {
                float t = (y - 91) / 37f;
                float x = math.lerp(11f, 7f, t);
                float z = math.lerp(-16f, -39f, t);
                Ellipsoid(a, o, new float3(-x, y, z), new float3(5.5f, 6f, 4f), Body);
                Ellipsoid(a, o, new float3(x, y, z), new float3(5.5f, 6f, 4f), Body);
            }
        }

        private static void Head(IStructureAuthoringSession a, int3 o)
        {
            // Long low dragon skull. The old model was too tall and goat-like.
            Ellipsoid(a, o, new float3(0, 143, -52), new float3(17, 11, 15), Body);
            Ellipsoid(a, o, new float3(0, 140, -66), new float3(13, 8, 16), Body);
            TaperedSnout(a, o);

            // Deep jaw hinge and separate lower jaw.
            Ellipsoid(a, o, new float3(-11, 134, -55), new float3(7, 8, 8), Body);
            Ellipsoid(a, o, new float3(11, 134, -55), new float3(7, 8, 8), Body);
            Capsule(a, o, new float3(-10, 130, -59), new float3(-8, 124, -78), 3.2f, 2.0f, Dark);
            Capsule(a, o, new float3(10, 130, -59), new float3(8, 124, -78), 3.2f, 2.0f, Dark);
            Capsule(a, o, new float3(-8, 124, -78), new float3(8, 124, -78), 2.4f, 2.4f, Dark);

            // Carve the mouth last enough to remain clearly open.
            Ellipsoid(a, o, new float3(0, 130, -70), new float3(9.5f, 5.2f, 13), Empty);
            Box(a, o, new int3(-8, 128, -85), new int3(17, 4, 16), Empty);

            // Brow wedge and eye sockets.
            Capsule(a, o, new float3(-4, 148, -61), new float3(-16, 150, -53), 4.8f, 1.4f, Dark);
            Capsule(a, o, new float3(4, 148, -61), new float3(16, 150, -53), 4.8f, 1.4f, Dark);
            Ellipsoid(a, o, new float3(-7, 144, -66), new float3(4, 3, 3), Empty);
            Ellipsoid(a, o, new float3(7, 144, -66), new float3(4, 3, 3), Empty);
            Box(a, o, new int3(-8, 144, -69), new int3(3, 2, 2), Eye);
            Box(a, o, new int3(6, 144, -69), new int3(3, 2, 2), Eye);

            // Nose ridge and nostrils.
            Capsule(a, o, new float3(0, 145, -63), new float3(0, 141, -84), 6f, 3.5f, Body);
            Ellipsoid(a, o, new float3(-4.5f, 141, -84), new float3(2, 1.5f, 2.3f), Empty);
            Ellipsoid(a, o, new float3(4.5f, 141, -84), new float3(2, 1.5f, 2.3f), Empty);

            // Teeth follow the jaw edges instead of forming a fence.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                for (int i = 0; i < 4; i++)
                {
                    float x = (3.2f + i * 2.3f) * s;
                    float z = -70 - i * 3.2f;
                    Capsule(a, o, new float3(x, 135, z), new float3(x, 129, z - 1), 1.2f, 0.2f, Horn);
                }
                Capsule(a, o, new float3(8 * s, 126, -73), new float3(8 * s, 131, -75), 1.1f, 0.2f, Horn);
            }

            CrownHorn(a, o, -1);
            CrownHorn(a, o, 1);

            // Facial spikes cascade backward.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                Capsule(a, o, new float3(15 * s, 143, -49), new float3(27 * s, 146, -40), 3.1f, 0.3f, Horn);
                Capsule(a, o, new float3(15 * s, 137, -48), new float3(25 * s, 135, -38), 2.6f, 0.25f, Horn);
                Capsule(a, o, new float3(12 * s, 131, -45), new float3(20 * s, 127, -35), 2.1f, 0.2f, Horn);
            }
        }

        private static void TaperedSnout(IStructureAuthoringSession a, int3 o)
        {
            // Overlapping smaller ellipsoids create a wedge rather than a cuboid muzzle.
            Ellipsoid(a, o, new float3(0, 140, -75), new float3(12, 7, 10), Body);
            Ellipsoid(a, o, new float3(0, 139, -83), new float3(9, 6, 8), Body);
            Ellipsoid(a, o, new float3(0, 138, -89), new float3(7, 5, 5), Body);
        }

        private static void CrownHorn(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3[] p =
            {
                new float3(8*s, 151, -47),
                new float3(13*s, 160, -40),
                new float3(20*s, 168, -30),
                new float3(27*s, 172, -17),
                new float3(31*s, 169, -5),
            };
            float[] r = { 4.2f, 3.4f, 2.6f, 1.5f, 0.25f };
            for (int i = 0; i < p.Length - 1; i++)
                Capsule(a, o, p[i], p[i + 1], r[i], r[i + 1], Horn);
        }

        private static void Foreleg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 shoulder = new float3(20*s, 75, -2);
            float3 elbow = new float3(31*s, 55, -14);
            float3 wrist = new float3(30*s, 27, -28);
            float3 palm = new float3(29*s, 10, -43);

            Capsule(a, o, shoulder, elbow, 10f, 7f, Body);
            Ellipsoid(a, o, elbow, new float3(8, 9, 9), Body);
            Capsule(a, o, elbow, wrist, 7f, 4.8f, Body);
            Ellipsoid(a, o, wrist, new float3(5.5f, 6, 6), Dark);
            Capsule(a, o, wrist, palm, 4.8f, 3.8f, Body);
            Ellipsoid(a, o, new float3(29*s, 7, -48), new float3(10, 4.5f, 13), Body);

            // Four spread digits and long claws.
            for (int i = 0; i < 4; i++)
            {
                float lateral = (i - 1.5f) * 4.2f;
                float x = 29*s + lateral;
                float toeZ = -54 - (i == 1 || i == 2 ? 4 : 0);
                Capsule(a, o, new float3(x, 7, -50), new float3(x + 0.8f*s, 5.5f, toeZ), 2.3f, 1.4f, Body);
                Capsule(a, o, new float3(x + 0.8f*s, 5.5f, toeZ), new float3(x + 1.8f*s, 3.4f, toeZ - 9), 1.4f, 0.18f, Horn);
            }
        }

        private static void RearLeg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 hip = new float3(24*s, 40, 26);
            float3 knee = new float3(40*s, 28, 8);
            float3 hock = new float3(35*s, 12, -12);

            Ellipsoid(a, o, hip, new float3(19, 18, 22), Body);
            Capsule(a, o, hip, knee, 13f, 9f, Body);
            Ellipsoid(a, o, knee, new float3(11, 10, 12), Body);
            Capsule(a, o, knee, hock, 8f, 5f, Body);
            Ellipsoid(a, o, new float3(35*s, 7, -20), new float3(13, 5, 17), Body);

            for (int i = 0; i < 4; i++)
            {
                float lateral = (i - 1.5f) * 4.7f;
                float x = 35*s + lateral;
                Capsule(a, o, new float3(x, 7, -24), new float3(x + s, 5, -32), 2.5f, 1.5f, Body);
                Capsule(a, o, new float3(x + s, 5, -32), new float3(x + 2*s, 3, -42 - (i == 1 || i == 2 ? 3 : 0)), 1.5f, 0.18f, Horn);
            }
        }

        private static void WingAssembly(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 root = new float3(15*s, 88, 9);
            float3 elbow = new float3(48*s, 121, 19);
            float3 wrist = new float3(91*s, 130, 27);
            float3 tip = new float3(107*s, 111, 31);

            // Heavy leading spar with a hooked outer tip.
            Capsule(a, o, root, elbow, 7f, 5.2f, Body);
            Ellipsoid(a, o, elbow, new float3(6.5f, 6.5f, 7), Body);
            Capsule(a, o, elbow, wrist, 5.2f, 3f, Body);
            Capsule(a, o, wrist, tip, 3f, 0.7f, Body);

            float3 r0 = new float3(100*s, 91, 31);
            float3 r1 = new float3(94*s, 70, 30);
            float3 r2 = new float3(82*s, 52, 27);
            float3 r3 = new float3(65*s, 40, 22);
            float3 inner = new float3(29*s, 52, 12);

            // Membranes first.
            Membrane(a, o, root, elbow, inner, 1.5f, Dark);
            Membrane(a, o, elbow, wrist, inner, 1.5f, Wing);
            Membrane(a, o, wrist, r0, r1, 1.4f, Wing);
            Membrane(a, o, wrist, r1, r2, 1.4f, Wing);
            Membrane(a, o, wrist, r2, r3, 1.4f, Wing);
            Membrane(a, o, wrist, r3, inner, 1.4f, Wing);

            // Deep round bites create the three large scallops from the reference.
            Ellipsoid(a, o, new float3(96*s, 79, 31), new float3(13, 9, 7), Empty);
            Ellipsoid(a, o, new float3(86*s, 60, 29), new float3(14, 10, 7), Empty);
            Ellipsoid(a, o, new float3(71*s, 46, 24), new float3(13, 9, 7), Empty);

            // Restore structural rays after cuts so each membrane bay reads distinctly.
            Capsule(a, o, wrist, r0, 3.0f, 0.9f, Dark);
            Capsule(a, o, wrist, r1, 2.8f, 0.8f, Dark);
            Capsule(a, o, wrist, r2, 2.6f, 0.7f, Dark);
            Capsule(a, o, wrist, r3, 2.4f, 0.6f, Dark);

            // Leading-edge armor cadence.
            for (int i = 1; i < 8; i++)
            {
                float t = i / 8f;
                float3 p = math.lerp(root, wrist, t);
                Ellipsoid(a, o, p + new float3(-1.5f*s, 2.5f, -1), new float3(4.5f, 2.5f, 4), Plate);
            }
        }

        private static void Tail(IStructureAuthoringSession a, int3 o)
        {
            float3[] p =
            {
                new float3(11, 43, 34),
                new float3(35, 38, 49),
                new float3(62, 30, 54),
                new float3(86, 22, 44),
                new float3(99, 14, 23),
                new float3(96, 9, -5),
                new float3(79, 7, -32),
                new float3(52, 6, -55),
                new float3(22, 5, -72),
                new float3(-8, 5, -80),
            };
            float[] r = { 15f, 13f, 10.5f, 8f, 6.2f, 4.8f, 3.6f, 2.5f, 1.5f, 0.35f };
            for (int i = 0; i < p.Length - 1; i++)
                Capsule(a, o, p[i], p[i + 1], r[i], r[i + 1], Body);

            // Alternating dorsal tail blades.
            for (int i = 1; i < p.Length - 2; i++)
            {
                float3 baseP = p[i];
                float h = math.max(3f, 8f - i * 0.65f);
                Capsule(a, o, baseP + new float3(0, r[i] * 0.65f, 0),
                    baseP + new float3(0, r[i] + h, 2), 2.2f, 0.2f, Horn);
            }
            Capsule(a, o, p[p.Length - 2], p[p.Length - 1] + new float3(-10, 2, -5), 1.5f, 0.15f, Horn);
        }

        private static void ChestPlates(IStructureAuthoringSession a, int3 o)
        {
            // Overlapping tapered breast plates; each is broad, shallow and angled by placement.
            for (int i = 0; i < 9; i++)
            {
                float t = i / 8f;
                float y = math.lerp(121f, 47f, t);
                float half = math.lerp(8f, 15f, t);
                float z = math.lerp(-42f, -21f, t);
                Ellipsoid(a, o, new float3(0, y, z), new float3(half, 5.2f, 3.1f), Plate);
                // Small bottom point keeps the plate shield-like instead of a floating bar.
                Capsule(a, o, new float3(0, y - 2, z - 1), new float3(0, y - 7, z - 3), 2.3f, 0.3f, Plate);
            }
        }

        private static void SpineCrest(IStructureAuthoringSession a, int3 o)
        {
            float3[] roots =
            {
                new float3(0, 136, -42), new float3(0, 124, -31), new float3(0, 111, -22),
                new float3(0, 98, -13), new float3(0, 84, -2), new float3(0, 70, 10),
                new float3(0, 58, 22), new float3(4, 48, 32)
            };
            for (int i = 0; i < roots.Length; i++)
            {
                float h = math.lerp(13f, 7f, i / (float)(roots.Length - 1));
                Capsule(a, o, roots[i], roots[i] + new float3(0, h, 5), 3.2f, 0.2f, Horn);
            }
        }

        private static void SurfaceAccents(IStructureAuthoringSession a, int3 o)
        {
            // Shoulder and haunch scale clusters, enough to imply layered armor without noisy bumps.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                for (int i = 0; i < 5; i++)
                {
                    float y = 77 - i * 5;
                    float x = (23 + i * 1.2f) * s;
                    float z = -5 + i * 4;
                    Ellipsoid(a, o, new float3(x, y, z), new float3(5, 3.5f, 5), Plate);
                }
            }

            // Restrained moss streaks only on upward-facing body regions.
            Ellipsoid(a, o, new float3(-10, 83, 4), new float3(8, 2.2f, 7), Moss);
            Ellipsoid(a, o, new float3(18, 49, 27), new float3(8, 2f, 8), Moss);
            Ellipsoid(a, o, new float3(53, 31, 51), new float3(7, 1.8f, 7), Moss);
        }

        private static void Box(IStructureAuthoringSession a, int3 o, int3 min, int3 size, byte material)
        {
            for (int y = 0; y < size.y; y++)
            for (int z = 0; z < size.z; z++)
            for (int x = 0; x < size.x; x++)
                a.Set(o.x + min.x + x, o.y + min.y + y, o.z + min.z + z, material);
        }

        private static void Ellipsoid(IStructureAuthoringSession a, int3 o, float3 c, float3 r, byte material)
        {
            int3 min = (int3)math.floor(c - r - 1f);
            int3 max = (int3)math.ceil(c + r + 1f);
            float3 sr = math.max(r, new float3(0.5f));
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 q = (new float3(x + 0.5f, y + 0.5f, z + 0.5f) - c) / sr;
                if (math.dot(q, q) <= 1f) a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void Capsule(IStructureAuthoringSession a, int3 o, float3 start, float3 end,
            float r0, float r1, byte material)
        {
            float mr = math.max(r0, r1);
            int3 min = (int3)math.floor(math.min(start, end) - mr - 1f);
            int3 max = (int3)math.ceil(math.max(start, end) + mr + 1f);
            float3 axis = end - start;
            float len2 = math.max(0.0001f, math.dot(axis, axis));
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 p = new float3(x + 0.5f, y + 0.5f, z + 0.5f);
                float t = math.saturate(math.dot(p - start, axis) / len2);
                float3 d = p - (start + axis * t);
                float r = math.lerp(r0, r1, t);
                if (math.dot(d, d) <= r * r) a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void Membrane(IStructureAuthoringSession a, int3 o, float3 va, float3 vb, float3 vc,
            float halfThickness, byte material)
        {
            float3 n = math.normalizesafe(math.cross(vb - va, vc - va), new float3(0, 0, 1));
            int3 min = (int3)math.floor(math.min(va, math.min(vb, vc)) - halfThickness - 1f);
            int3 max = (int3)math.ceil(math.max(va, math.max(vb, vc)) + halfThickness + 1f);
            float3 e0 = vb - va;
            float3 e1 = vc - va;
            float d00 = math.dot(e0, e0), d01 = math.dot(e0, e1), d11 = math.dot(e1, e1);
            float denom = math.max(0.0001f, d00 * d11 - d01 * d01);
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 p = new float3(x + 0.5f, y + 0.5f, z + 0.5f);
                float signed = math.dot(p - va, n);
                if (math.abs(signed) > halfThickness) continue;
                float3 projected = p - n * signed;
                float3 v2 = projected - va;
                float d20 = math.dot(v2, e0), d21 = math.dot(v2, e1);
                float v = (d11 * d20 - d01 * d21) / denom;
                float w = (d00 * d21 - d01 * d20) / denom;
                float u = 1f - v - w;
                if (u >= -0.01f && v >= -0.01f && w >= -0.01f)
                    a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }
    }
}
