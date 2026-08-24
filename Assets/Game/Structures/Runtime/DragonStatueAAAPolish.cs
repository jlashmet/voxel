using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Destructive visual pass driven by production-render review.</summary>
    public static class DragonStatueAAAPolish
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Dark = GameMaterialIds.DarkStone;
        private const byte Plate = GameMaterialIds.Stone;
        private const byte Horn = GameMaterialIds.Stone;
        private const byte Wing = GameMaterialIds.Wood;
        private const byte Eye = GameMaterialIds.Gold;

        public static void Apply(IStructureAuthoringSession a, int3 o)
        {
            RebuildHead(a, o);
            RebuildChest(a, o);
            RebuildForeleg(a, o, -1);
            RebuildForeleg(a, o, 1);
            RebuildWing(a, o, -1);
            RebuildWing(a, o, 1);
        }

        private static void RebuildHead(IStructureAuthoringSession a, int3 o)
        {
            // Remove the tall goat horns and the crocodile muzzle while preserving the rear cranium.
            Clear(a, o, new int3(-44, 152, -52), new int3(88, 26, 58));
            Clear(a, o, new int3(-18, 119, -98), new int3(36, 34, 40));

            // Low armored muzzle: broad at the eyes, narrow and pointed at the nose.
            Ellipsoid(a, o, new float3(0, 143, -65), new float3(13, 7, 12), Body);
            Ellipsoid(a, o, new float3(0, 141, -77), new float3(10, 6, 10), Body);
            Ellipsoid(a, o, new float3(0, 139, -86), new float3(7, 4.5f, 7), Body);

            // Open jaw with a pronounced hinge and tapered lower jaw.
            Capsule(a, o, new float3(-11, 133, -61), new float3(-8, 125, -83), 3.8f, 1.8f, Dark);
            Capsule(a, o, new float3(11, 133, -61), new float3(8, 125, -83), 3.8f, 1.8f, Dark);
            Capsule(a, o, new float3(-8, 125, -83), new float3(8, 125, -83), 2.2f, 2.2f, Dark);
            Ellipsoid(a, o, new float3(0, 131, -75), new float3(9, 5, 14), Empty);

            // Angular brow armor and narrow glowing eyes.
            Capsule(a, o, new float3(-4, 148, -61), new float3(-17, 150, -53), 4.3f, 1.2f, Dark);
            Capsule(a, o, new float3(4, 148, -61), new float3(17, 150, -53), 4.3f, 1.2f, Dark);
            Ellipsoid(a, o, new float3(-7, 144, -68), new float3(4, 2.8f, 3), Empty);
            Ellipsoid(a, o, new float3(7, 144, -68), new float3(4, 2.8f, 3), Empty);
            Box(a, o, new int3(-8, 144, -71), new int3(3, 2, 2), Eye);
            Box(a, o, new int3(6, 144, -71), new int3(3, 2, 2), Eye);

            // Teeth are individually readable and follow the upper/lower jaw arcs.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                for (int i = 0; i < 4; i++)
                {
                    float x = (3f + i * 2.4f) * s;
                    float z = -71 - i * 3.4f;
                    Capsule(a, o, new float3(x, 135, z), new float3(x, 129, z - 1), 1.15f, 0.15f, Horn);
                }
                Capsule(a, o, new float3(13*s, 143, -53), new float3(27*s, 146, -43), 2.7f, 0.2f, Horn);
                Capsule(a, o, new float3(12*s, 136, -50), new float3(23*s, 132, -39), 2.2f, 0.2f, Horn);
            }

            // Reference horns sweep backward and outward; they do not stand vertically like antlers.
            SweptHorn(a, o, -1);
            SweptHorn(a, o, 1);
        }

        private static void SweptHorn(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 p0 = new float3(8*s, 151, -48);
            float3 p1 = new float3(16*s, 158, -41);
            float3 p2 = new float3(27*s, 162, -31);
            float3 p3 = new float3(39*s, 161, -18);
            float3 p4 = new float3(47*s, 154, -5);
            Capsule(a, o, p0, p1, 4.0f, 3.2f, Horn);
            Capsule(a, o, p1, p2, 3.2f, 2.3f, Horn);
            Capsule(a, o, p2, p3, 2.3f, 1.2f, Horn);
            Capsule(a, o, p3, p4, 1.2f, 0.18f, Horn);
        }

        private static void RebuildChest(IStructureAuthoringSession a, int3 o)
        {
            // Remove the bead-like ellipsoids from the production render.
            Clear(a, o, new int3(-20, 40, -48), new int3(40, 88, 38));

            // Restore the neck/breast substrate first.
            Capsule(a, o, new float3(0, 119, -30), new float3(0, 52, -12), 12f, 17f, Body);

            // Flat, overlapping shield plates. Wider toward the belly, shallow in depth.
            for (int i = 0; i < 9; i++)
            {
                int y = 116 - i * 8;
                int half = 7 + i;
                int z = -39 + i * 3;
                Box(a, o, new int3(-half, y - 3, z - 3), new int3(half * 2 + 1, 6, 6), Plate);
                // V-shaped lower point.
                for (int row = 0; row < 4; row++)
                {
                    int rowHalf = math.max(1, half - row * 3);
                    Box(a, o, new int3(-rowHalf, y - 4 - row, z - 4), new int3(rowHalf * 2 + 1, 1, 5), Plate);
                }
            }
        }

        private static void RebuildForeleg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            int minX = side < 0 ? -48 : 10;
            Clear(a, o, new int3(minX, 0, -62), new int3(38, 82, 68));

            float3 shoulder = new float3(20*s, 73, -3);
            float3 elbow = new float3(35*s, 52, -21);
            float3 wrist = new float3(27*s, 25, -38);
            float3 palm = new float3(23*s, 8, -52);
            Capsule(a, o, shoulder, elbow, 11f, 8f, Body);
            Ellipsoid(a, o, elbow, new float3(9, 9, 10), Body);
            Capsule(a, o, elbow, wrist, 8f, 5.5f, Body);
            Ellipsoid(a, o, wrist, new float3(6, 6, 7), Dark);
            Capsule(a, o, wrist, palm, 5.4f, 4.2f, Body);
            Ellipsoid(a, o, new float3(23*s, 7, -55), new float3(11, 4.5f, 13), Body);

            for (int i = 0; i < 4; i++)
            {
                float lateral = (i - 1.5f) * 4.4f;
                float x = 23*s + lateral;
                float z = -61 - (i == 1 || i == 2 ? 4 : 0);
                Capsule(a, o, new float3(x, 7, -57), new float3(x + s, 5, z), 2.4f, 1.3f, Body);
                Capsule(a, o, new float3(x + s, 5, z), new float3(x + 2*s, 2.8f, z - 9), 1.3f, 0.15f, Horn);
            }
        }

        private static void RebuildWing(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            int minX = side < 0 ? -112 : 24;
            Clear(a, o, new int3(minX, 34, 3), new int3(88, 106, 39));

            float3 root = new float3(17*s, 88, 8);
            float3 elbow = new float3(47*s, 121, 16);
            float3 wrist = new float3(91*s, 132, 24);
            float3 hook = new float3(107*s, 111, 29);

            // Define the bat-like outer silhouette by the endpoints, not one giant triangular sheet.
            float3 f0 = new float3(102*s, 102, 28);
            float3 f1 = new float3(94*s, 84, 27);
            float3 f2 = new float3(82*s, 66, 24);
            float3 f3 = new float3(66*s, 52, 20);
            float3 inner = new float3(34*s, 63, 12);

            // Individual bays; each has its own lower endpoint.
            Membrane(a, o, root, elbow, inner, 1.3f, Dark);
            Membrane(a, o, elbow, wrist, inner, 1.3f, Wing);
            Membrane(a, o, wrist, f0, f1, 1.3f, Wing);
            Membrane(a, o, wrist, f1, f2, 1.3f, Wing);
            Membrane(a, o, wrist, f2, f3, 1.3f, Wing);
            Membrane(a, o, wrist, f3, inner, 1.3f, Wing);

            // Cut much larger concavities so the lower edge cannot read rectangular.
            Ellipsoid(a, o, new float3(99*s, 93, 28), new float3(13, 10, 7), Empty);
            Ellipsoid(a, o, new float3(90*s, 75, 26), new float3(14, 11, 7), Empty);
            Ellipsoid(a, o, new float3(75*s, 59, 22), new float3(14, 10, 7), Empty);

            // Restore all bones after cutting.
            Capsule(a, o, root, elbow, 7f, 5.2f, Body);
            Capsule(a, o, elbow, wrist, 5.2f, 3f, Body);
            Capsule(a, o, wrist, hook, 3f, 0.25f, Horn);
            Capsule(a, o, wrist, f0, 3f, 0.65f, Dark);
            Capsule(a, o, wrist, f1, 2.8f, 0.6f, Dark);
            Capsule(a, o, wrist, f2, 2.6f, 0.5f, Dark);
            Capsule(a, o, wrist, f3, 2.4f, 0.4f, Dark);
        }

        private static void Clear(IStructureAuthoringSession a, int3 o, int3 min, int3 size)
        {
            int3 max = min + size;
            for (int y = min.y; y < max.y; y++)
            for (int z = min.z; z < max.z; z++)
            for (int x = min.x; x < max.x; x++)
                a.Set(o.x + x, o.y + y, o.z + z, Empty);
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
            float3 safe = math.max(r, new float3(0.5f));
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 q = (new float3(x + .5f, y + .5f, z + .5f) - c) / safe;
                if (math.dot(q, q) <= 1f) a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void Capsule(IStructureAuthoringSession a, int3 o, float3 start, float3 end, float r0, float r1, byte material)
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
                if (math.dot(d, d) <= r*r) a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void Membrane(IStructureAuthoringSession a, int3 o, float3 va, float3 vb, float3 vc, float halfThickness, byte material)
        {
            float3 n = math.normalizesafe(math.cross(vb - va, vc - va), new float3(0, 0, 1));
            int3 min = (int3)math.floor(math.min(va, math.min(vb, vc)) - halfThickness - 1f);
            int3 max = (int3)math.ceil(math.max(va, math.max(vb, vc)) + halfThickness + 1f);
            float3 e0 = vb-va, e1 = vc-va;
            float d00 = math.dot(e0,e0), d01 = math.dot(e0,e1), d11 = math.dot(e1,e1);
            float denom = math.max(.0001f, d00*d11-d01*d01);
            for (int y=min.y;y<=max.y;y++) for(int z=min.z;z<=max.z;z++) for(int x=min.x;x<=max.x;x++)
            {
                float3 p=new float3(x+.5f,y+.5f,z+.5f);
                float sd=math.dot(p-va,n); if(math.abs(sd)>halfThickness) continue;
                float3 v2=p-n*sd-va;
                float d20=math.dot(v2,e0), d21=math.dot(v2,e1);
                float v=(d11*d20-d01*d21)/denom, w=(d00*d21-d01*d20)/denom, u=1f-v-w;
                if(u>=-.01f&&v>=-.01f&&w>=-.01f) a.Set(o.x+x,o.y+y,o.z+z,material);
            }
        }
    }
}
