using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// V4 silhouette surgery for Dragon A. This pass deliberately replaces the V3 forms that read
    /// poorly in the production capture: blunt skull, paddle hands, sheet-like wings, and the front
    /// tail ring. Everything is authored directly against the 10 cm canonical voxel grid.
    /// </summary>
    public static class DragonStatueConceptV4SilhouettePass
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Shadow = GameMaterialIds.DarkStone;
        private const byte Scale = GameMaterialIds.Stone;
        private const byte Warm = GameMaterialIds.Dirt;
        private const byte Eye = GameMaterialIds.Gold;

        public static void Apply(IStructureAuthoringSession a, int3 o)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            ReplaceHead(a, o);
            StrengthenChest(a, o);
            ReplaceHands(a, o);
            ReplaceWings(a, o);
            OpenTailSilhouette(a, o);
        }

        private static void ReplaceHead(IStructureAuthoringSession a, int3 o)
        {
            // Remove the whole visible V3 skull so the new wedge is not inflated by hidden ellipsoids.
            ClearBox(a, o, new int3(-34, 96, -92), new int3(64, 62, 55));

            // Rear cranium: compact, angular, and much smaller than V3.
            for (int z = -62; z <= -46; z++)
            {
                float t = (z + 62) / 16f;
                int halfX = (int)math.round(math.lerp(13f, 16f, t));
                int halfY = (int)math.round(math.lerp(9f, 12f, t));
                FillOvalSliceXY(a, o, -5, 121, z, halfX, halfY, Body);
            }

            // Long wedge muzzle. Each 10 cm slice is intentionally authored, giving us a readable
            // taper instead of a rounded capsule nose.
            for (int z = -91; z <= -62; z++)
            {
                float t = (z + 91) / 29f;
                int halfX = (int)math.round(math.lerp(5f, 12f, t));
                int halfY = (int)math.round(math.lerp(3f, 6f, t));
                int cy = (int)math.round(math.lerp(116f, 120f, t));
                FillOvalSliceXY(a, o, -5, cy, z, halfX, halfY, Body);
            }

            // Sharp nose cap rather than a bulb.
            FillRun(a, o, -10, 5, 114, -92, Body);
            FillRun(a, o, -9, 4, 115, -91, Body);
            FillRun(a, o, -8, 3, 116, -90, Body);

            // Deep mouth cut, then an independently modeled lower jaw.
            for (int z = -88; z <= -61; z++)
            {
                float t = (z + 88) / 27f;
                int half = (int)math.round(math.lerp(4f, 10f, t));
                for (int y = 105; y <= 111; y++) FillRun(a, o, -5 - half, -5 + half, y, z, Empty);
            }
            for (int z = -87; z <= -61; z++)
            {
                float t = (z + 87) / 26f;
                int half = (int)math.round(math.lerp(5f, 11f, t));
                int cy = (int)math.round(math.lerp(103f, 106f, t));
                FillOvalSliceXY(a, o, -5, cy, z, half, 3, Shadow);
            }
            // Chin planes make the jaw look armored rather than rubbery.
            for (int z = -82; z <= -62; z += 5)
                FillRun(a, o, -14, 4, 101, z, Scale);

            // Eye sockets and aggressive brows.
            CarveOval(a, o, new int3(-16, 120, -66), new int3(4, 4, 4));
            CarveOval(a, o, new int3(6, 120, -66), new int3(4, 4, 4));
            Box(a, o, new int3(-17, 120, -69), new int3(3, 2, 2), Eye);
            Box(a, o, new int3(5, 120, -69), new int3(3, 2, 2), Eye);
            TaperedVoxelLine(a, o, new float3(-7, 127, -63), new float3(-22, 126, -58), 4.0f, 1.4f, Shadow);
            TaperedVoxelLine(a, o, new float3(-3, 127, -63), new float3(12, 126, -58), 4.0f, 1.4f, Shadow);

            // Nostrils are true negative space.
            Box(a, o, new int3(-10, 116, -91), new int3(3, 2, 3), Empty);
            Box(a, o, new int3(-3, 116, -91), new int3(3, 2, 3), Empty);

            // Long swept crown horns. Strong side silhouette is more important than symmetry detail.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                TaperedVoxelLine(a, o, new float3(-5 + 9*s, 129, -50), new float3(-5 + 22*s, 143, -37), 4.2f, 3.0f, Warm);
                TaperedVoxelLine(a, o, new float3(-5 + 22*s, 143, -37), new float3(-5 + 38*s, 151, -18), 3.0f, 1.5f, Warm);
                TaperedVoxelLine(a, o, new float3(-5 + 38*s, 151, -18), new float3(-5 + 49*s, 149, -2), 1.5f, .25f, Warm);

                TaperedVoxelLine(a, o, new float3(-5 + 10*s, 124, -51), new float3(-5 + 25*s, 134, -35), 3.0f, 1.4f, Warm);
                TaperedVoxelLine(a, o, new float3(-5 + 25*s, 134, -35), new float3(-5 + 40*s, 136, -18), 1.4f, .2f, Warm);

                // Cheek fins/spines establish a dragon profile at thumbnail size.
                TaperedVoxelLine(a, o, new float3(-5 + 14*s, 119, -56), new float3(-5 + 31*s, 122, -43), 2.4f, .2f, Warm);
                TaperedVoxelLine(a, o, new float3(-5 + 14*s, 112, -55), new float3(-5 + 28*s, 110, -42), 2.0f, .2f, Warm);
            }

            // Teeth: separated, uneven lengths, and kept inside the jaw silhouette.
            int[] toothZ = { -68, -73, -78, -83, -87 };
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < toothZ.Length; i++)
                {
                    float x = -5 + side * (5 + i * .8f);
                    float len = 5 + (i % 2) * 2;
                    TaperedVoxelLine(a, o, new float3(x, 112, toothZ[i]), new float3(x, 112 - len, toothZ[i] - 1), 1.2f, .15f, Warm);
                }
            }
        }

        private static void StrengthenChest(IStructureAuthoringSession a, int3 o)
        {
            // Widen the shoulder/chest wedge without making the belly fatter.
            FillEllipsoid(a, o, new float3(0, 72, -5), new float3(32, 20, 20), Body);
            FillEllipsoid(a, o, new float3(0, 61, -10), new float3(30, 21, 20), Body);

            // Re-establish neck/chest armor over the broadened body as tapered, overlapping shields.
            for (int i = 0; i < 10; i++)
            {
                int y = 105 - i * 6;
                int z = -34 + i * 2;
                int half = 7 + i / 2;
                for (int row = 0; row < 4; row++)
                {
                    int h = math.max(2, half - row * 2);
                    FillRun(a, o, -h, h, y - row, z - row, Warm);
                    FillRun(a, o, -math.max(1, h - 2), math.max(1, h - 2), y - row - 1, z - row - 1, Warm);
                }
            }
        }

        private static void ReplaceHands(IStructureAuthoringSession a, int3 o)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                int minX = side < 0 ? -54 : 12;
                ClearBox(a, o, new int3(minX, 0, -91), new int3(42, 15, 48));

                float s = side;
                float3 palm = new float3(30*s, 7, -58);
                FillEllipsoid(a, o, palm, new float3(10, 5, 10), Body);

                // Four distinct fingers. Gaps are intentionally wider than one voxel so smoothing
                // cannot merge them back into a paddle.
                float[] offsets = { -9f, -3f, 3f, 9f };
                for (int i = 0; i < offsets.Length; i++)
                {
                    float x = 30*s + offsets[i];
                    float z0 = -61 - (i == 1 || i == 2 ? 2 : 0);
                    float3 knuckle = new float3(x, 6, z0);
                    float3 tip = new float3(x + 1.5f*s, 4, z0 - 12 - (i == 1 || i == 2 ? 3 : 0));
                    float3 claw = new float3(x + 3.0f*s, 1.8f, tip.z - 10);
                    TaperedVoxelLine(a, o, knuckle, tip, 2.4f, 1.6f, Body);
                    TaperedVoxelLine(a, o, tip, claw, 1.7f, .15f, Warm);
                }
            }
        }

        private static void ReplaceWings(IStructureAuthoringSession a, int3 o)
        {
            // Remove only the exposed V3 wing sheets; roots inside the shoulder volume stay intact.
            ClearBox(a, o, new int3(29, 37, 8), new int3(77, 113, 42));
            ClearBox(a, o, new int3(-106, 37, 8), new int3(77, 113, 42));

            AuthorWing(a, o, -1);
            AuthorWing(a, o, 1);
        }

        private static void AuthorWing(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 root = new float3(18*s, 76, 12);
            float3 elbow = new float3(50*s, 112, 19);
            float3 wrist = new float3(95*s, 145, 27);
            float3[] edge =
            {
                new float3(105*s, 132, 34),
                new float3(105*s, 111, 36),
                new float3(100*s, 89, 35),
                new float3(90*s, 68, 31),
                new float3(75*s, 51, 26),
                new float3(56*s, 41, 20),
                new float3(34*s, 48, 14),
            };

            // Dark membrane keeps the wing visually part of the dragon instead of a brown tarp.
            FillTriangleMembrane(a, o, root, elbow, edge[6], 1.0f, Shadow);
            FillTriangleMembrane(a, o, elbow, wrist, edge[6], 1.0f, Shadow);
            for (int i = 0; i < edge.Length - 1; i++)
                FillTriangleMembrane(a, o, wrist, edge[i], edge[i + 1], .9f, Shadow);

            // Carve large U-shaped bites between the finger endpoints before restoring spars.
            for (int i = 0; i < edge.Length - 2; i++)
            {
                float3 mid = (edge[i] + edge[i + 1]) * .5f;
                float radius = math.lerp(8.5f, 6.0f, i / 5f);
                CarveOval(a, o, (int3)math.round(mid + new float3(-3*s, -2, 0)), new int3((int)radius, (int)(radius * .8f), 4));
            }

            // Bone arm and six visibly separate finger spars.
            TaperedVoxelLine(a, o, root, elbow, 7.0f, 5.0f, Body);
            TaperedVoxelLine(a, o, elbow, wrist, 5.0f, 3.0f, Body);
            TaperedVoxelLine(a, o, wrist, edge[0] + new float3(5*s, 3, 0), 3.0f, .2f, Warm);
            for (int i = 0; i < edge.Length - 1; i++)
                TaperedVoxelLine(a, o, wrist, edge[i], math.lerp(2.7f, 1.7f, i / 5f), .45f, Scale);

            // Short trailing claws/hooks at outer finger tips add a serrated silhouette.
            for (int i = 1; i < edge.Length - 1; i += 2)
                TaperedVoxelLine(a, o, edge[i], edge[i] + new float3(4*s, -6, -1), 1.2f, .15f, Warm);
        }

        private static void OpenTailSilhouette(IStructureAuthoringSession a, int3 o)
        {
            // Delete the front crossbar that turned the tail into a closed ring around the statue.
            ClearBox(a, o, new int3(-58, 0, -92), new int3(92, 17, 40));

            // Rebuild a tapering tip that stays on the dragon's right/front quarter instead of
            // crossing the entire frame.
            float3[] p =
            {
                new float3(77, 8, -43),
                new float3(67, 7, -50),
                new float3(56, 6, -57),
                new float3(46, 5, -62),
                new float3(38, 4, -65),
            };
            float[] r = { 6.8f, 5.4f, 4.0f, 2.5f, .3f };
            for (int i = 0; i < p.Length - 1; i++) TaperedVoxelLine(a, o, p[i], p[i + 1], r[i], r[i + 1], Body);
            for (int i = 0; i < p.Length - 2; i++)
                TaperedVoxelLine(a, o, p[i] + new float3(0, 1, 0), p[i] + new float3(0, 7 - i, -1), 1.7f, .15f, Warm);
        }

        private static void FillRun(IStructureAuthoringSession a, int3 o, int x0, int x1, int y, int z, byte m)
        {
            if (x0 > x1) (x0, x1) = (x1, x0);
            for (int x = x0; x <= x1; x++) a.Set(o.x + x, o.y + y, o.z + z, m);
        }

        private static void FillOvalSliceXY(IStructureAuthoringSession a, int3 o, int cx, int cy, int z, int rx, int ry, byte m)
        {
            float sx = math.max(1, rx);
            float sy = math.max(1, ry);
            for (int y = cy - ry; y <= cy + ry; y++)
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = (x + .5f - cx) / sx;
                float dy = (y + .5f - cy) / sy;
                if (dx*dx + dy*dy <= 1f) a.Set(o.x + x, o.y + y, o.z + z, m);
            }
        }

        private static void FillEllipsoid(IStructureAuthoringSession a, int3 o, float3 c, float3 r, byte m)
        {
            int3 min = (int3)math.floor(c-r-1);
            int3 max = (int3)math.ceil(c+r+1);
            float3 safe = math.max(r, new float3(.5f));
            for (int y=min.y; y<=max.y; y++)
            for (int z=min.z; z<=max.z; z++)
            for (int x=min.x; x<=max.x; x++)
            {
                float3 q=(new float3(x+.5f,y+.5f,z+.5f)-c)/safe;
                if (math.dot(q,q)<=1f) a.Set(o.x+x,o.y+y,o.z+z,m);
            }
        }

        private static void CarveOval(IStructureAuthoringSession a, int3 o, int3 c, int3 r)
        {
            for (int y=c.y-r.y; y<=c.y+r.y; y++)
            for (int z=c.z-r.z; z<=c.z+r.z; z++)
            for (int x=c.x-r.x; x<=c.x+r.x; x++)
            {
                float dx=(x+.5f-c.x)/math.max(1f,r.x);
                float dy=(y+.5f-c.y)/math.max(1f,r.y);
                float dz=(z+.5f-c.z)/math.max(1f,r.z);
                if (dx*dx+dy*dy+dz*dz<=1f) a.Set(o.x+x,o.y+y,o.z+z,Empty);
            }
        }

        private static void TaperedVoxelLine(IStructureAuthoringSession a, int3 o, float3 p0, float3 p1, float r0, float r1, byte m)
        {
            float3 axis = p1-p0;
            float length = math.length(axis);
            int steps = math.max(1, (int)math.ceil(length * 1.5f));
            for (int i=0; i<=steps; i++)
            {
                float t=i/(float)steps;
                float3 p=math.lerp(p0,p1,t);
                float r=math.lerp(r0,r1,t);
                int ir=math.max(1,(int)math.ceil(r));
                for (int y=(int)math.floor(p.y-r); y<=(int)math.ceil(p.y+r); y++)
                for (int z=(int)math.floor(p.z-r); z<=(int)math.ceil(p.z+r); z++)
                for (int x=(int)math.floor(p.x-r); x<=(int)math.ceil(p.x+r); x++)
                {
                    float3 d=new float3(x+.5f,y+.5f,z+.5f)-p;
                    if (math.dot(d,d)<=r*r) a.Set(o.x+x,o.y+y,o.z+z,m);
                }
            }
        }

        private static void FillTriangleMembrane(IStructureAuthoringSession a, int3 o, float3 va, float3 vb, float3 vc, float thick, byte m)
        {
            float3 n=math.normalizesafe(math.cross(vb-va,vc-va),new float3(0,0,1));
            int3 min=(int3)math.floor(math.min(va,math.min(vb,vc))-thick-1);
            int3 max=(int3)math.ceil(math.max(va,math.max(vb,vc))+thick+1);
            float3 v0=vb-va,v1=vc-va;
            float d00=math.dot(v0,v0),d01=math.dot(v0,v1),d11=math.dot(v1,v1);
            float den=d00*d11-d01*d01;
            if(math.abs(den)<.0001f)return;
            for(int y=min.y;y<=max.y;y++)
            for(int z=min.z;z<=max.z;z++)
            for(int x=min.x;x<=max.x;x++)
            {
                float3 p=new float3(x+.5f,y+.5f,z+.5f);
                float dist=math.dot(p-va,n);
                if(math.abs(dist)>thick)continue;
                float3 v2=(p-n*dist)-va;
                float d20=math.dot(v2,v0),d21=math.dot(v2,v1);
                float v=(d11*d20-d01*d21)/den,w=(d00*d21-d01*d20)/den,u=1-v-w;
                if(u>=0&&v>=0&&w>=0)a.Set(o.x+x,o.y+y,o.z+z,m);
            }
        }

        private static void Box(IStructureAuthoringSession a, int3 o, int3 min, int3 size, byte m)
        {
            int3 max=min+size;
            for(int y=min.y;y<max.y;y++)
            for(int z=min.z;z<max.z;z++)
            for(int x=min.x;x<max.x;x++)a.Set(o.x+x,o.y+y,o.z+z,m);
        }

        private static void ClearBox(IStructureAuthoringSession a, int3 o, int3 min, int3 size)
        {
            Box(a, o, min, size, Empty);
        }
    }
}
