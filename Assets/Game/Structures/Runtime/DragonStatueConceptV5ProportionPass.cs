using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Production-review pass after V4. Replaces the remaining silhouette failures instead of
    /// decorating them: skeletal head, oversized wing fan, closed tail arc, and damaged hands.
    /// </summary>
    public static class DragonStatueConceptV5ProportionPass
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
            ReplaceWings(a, o);
            ReplaceTail(a, o);
            ReplaceHands(a, o);
        }

        private static void ReplaceHead(IStructureAuthoringSession a, int3 o)
        {
            // Clear skull, jaw, old V3/V4 horns, and cheek spines. The previous head was visibly
            // assembled from multiple generations because the horn envelope extended outside the skull clear.
            ClearBox(a, o, new int3(-40, 96, -92), new int3(70, 63, 60));
            ClearBox(a, o, new int3(-62, 120, -56), new int3(124, 44, 58));

            // Compact rear skull with real cheek volume.
            for (int z = -64; z <= -46; z++)
            {
                float t = (z + 64) / 18f;
                int rx = (int)math.round(math.lerp(12f, 16f, t));
                int ry = (int)math.round(math.lerp(8f, 12f, t));
                int cy = (int)math.round(math.lerp(119f, 122f, t));
                FillOvalSliceXY(a, o, -5, cy, z, rx, ry, Body);
            }
            FillEllipsoid(a, o, new float3(-16, 116, -57), new float3(8, 9, 9), Body);
            FillEllipsoid(a, o, new float3(6, 116, -57), new float3(8, 9, 9), Body);

            // Upper muzzle: low wedge, not a flat plank.
            for (int z = -91; z <= -63; z++)
            {
                float t = (z + 91) / 28f;
                int rx = (int)math.round(math.lerp(5f, 11f, t));
                int ry = (int)math.round(math.lerp(3f, 6f, t));
                int cy = (int)math.round(math.lerp(116f, 119f, t));
                FillOvalSliceXY(a, o, -5, cy, z, rx, ry, Body);
            }

            // Mouth gap is only 3–4 voxels high. V4's large void made the jaw look detached.
            for (int z = -87; z <= -62; z++)
            {
                float t = (z + 87) / 25f;
                int half = (int)math.round(math.lerp(4f, 9f, t));
                for (int y = 108; y <= 111; y++) FillRun(a, o, -5-half, -5+half, y, z, Empty);
            }

            // Lower jaw rises into the cheek at the rear and has a visible chin taper.
            for (int z = -87; z <= -60; z++)
            {
                float t = (z + 87) / 27f;
                int rx = (int)math.round(math.lerp(5f, 11f, t));
                int ry = (int)math.round(math.lerp(3f, 5f, t));
                int cy = (int)math.round(math.lerp(105f, 108f, t));
                FillOvalSliceXY(a, o, -5, cy, z, rx, ry, Shadow);
            }
            FillEllipsoid(a, o, new float3(-5, 108, -59), new float3(12, 6, 8), Body);

            // Brows, eye sockets and bright recessed eyes.
            CarveOval(a, o, new int3(-15, 120, -67), new int3(4, 4, 4));
            CarveOval(a, o, new int3(5, 120, -67), new int3(4, 4, 4));
            Box(a, o, new int3(-16, 120, -69), new int3(3, 2, 2), Eye);
            Box(a, o, new int3(4, 120, -69), new int3(3, 2, 2), Eye);
            VoxelLine(a, o, new float3(-6, 127, -64), new float3(-21, 126, -58), 3.6f, 1.2f, Shadow);
            VoxelLine(a, o, new float3(-4, 127, -64), new float3(11, 126, -58), 3.6f, 1.2f, Shadow);

            // Nostrils and nose plane.
            Box(a, o, new int3(-10, 116, -91), new int3(3, 2, 3), Empty);
            Box(a, o, new int3(-3, 116, -91), new int3(3, 2, 3), Empty);
            FillRun(a, o, -10, 0, 119, -87, Scale);
            FillRun(a, o, -9, -1, 119, -88, Scale);

            // Three crown directions per side: compact roots, long tapered tips. These read as
            // deliberate dragon horns instead of the antler forest visible in V4.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                VoxelLine(a, o, new float3(-5+8*s, 130, -51), new float3(-5+20*s, 142, -38), 4.0f, 2.5f, Warm);
                VoxelLine(a, o, new float3(-5+20*s, 142, -38), new float3(-5+35*s, 150, -20), 2.5f, .3f, Warm);
                VoxelLine(a, o, new float3(-5+10*s, 126, -49), new float3(-5+26*s, 136, -31), 2.8f, .25f, Warm);
                VoxelLine(a, o, new float3(-5+13*s, 118, -54), new float3(-5+29*s, 121, -41), 2.0f, .2f, Warm);
            }

            // Sparse uneven teeth. Fewer, larger silhouettes look intentional at 10 cm resolution.
            int[] zs = { -69, -77, -85 };
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < zs.Length; i++)
            {
                float x = -5 + side * (5 + i * 1.2f);
                float len = i == 1 ? 6f : 4.5f;
                VoxelLine(a, o, new float3(x, 113, zs[i]), new float3(x, 113-len, zs[i]-1), 1.15f, .15f, Warm);
            }
        }

        private static void ReplaceWings(IStructureAuthoringSession a, int3 o)
        {
            // Remove the V4 exposed fan completely. V5 is intentionally ~20% smaller so wings frame
            // the body rather than becoming the subject.
            ClearBox(a, o, new int3(28, 36, 7), new int3(78, 116, 45));
            ClearBox(a, o, new int3(-106, 36, 7), new int3(78, 116, 45));
            AuthorWing(a, o, -1);
            AuthorWing(a, o, 1);
        }

        private static void AuthorWing(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 root = new float3(18*s, 76, 12);
            float3 elbow = new float3(43*s, 104, 18);
            float3 wrist = new float3(76*s, 132, 24);
            float3 tip = new float3(86*s, 138, 28);
            float3[] finger =
            {
                new float3(86*s, 118, 30),
                new float3(82*s, 98, 29),
                new float3(74*s, 79, 27),
                new float3(63*s, 62, 23),
                new float3(49*s, 50, 18),
                new float3(32*s, 49, 14),
            };

            FillTriangle(a, o, root, elbow, finger[5], .9f, Shadow);
            FillTriangle(a, o, elbow, wrist, finger[5], .9f, Shadow);
            FillTriangle(a, o, wrist, tip, finger[0], .85f, Shadow);
            for (int i = 0; i < finger.Length-1; i++) FillTriangle(a, o, wrist, finger[i], finger[i+1], .85f, Shadow);

            // Fewer, much deeper scallops. The prior six straight ribs read like blinds.
            for (int i = 0; i < finger.Length-1; i++)
            {
                float3 mid = (finger[i] + finger[i+1]) * .5f;
                int rx = 7 + (i < 2 ? 1 : 0);
                CarveOval(a, o, (int3)math.round(mid + new float3(-2*s, -2, 0)), new int3(rx, 6, 4));
            }

            VoxelLine(a, o, root, elbow, 6.5f, 4.7f, Body);
            VoxelLine(a, o, elbow, wrist, 4.7f, 2.8f, Body);
            VoxelLine(a, o, wrist, tip, 2.8f, .2f, Warm);

            // Three structural fingers only; enough to articulate the membrane without making a cage.
            VoxelLine(a, o, wrist, finger[0], 2.3f, .35f, Scale);
            VoxelLine(a, o, wrist, finger[2], 2.1f, .35f, Scale);
            VoxelLine(a, o, wrist, finger[4], 1.8f, .3f, Scale);
            VoxelLine(a, o, finger[0], finger[0] + new float3(4*s, -5, -1), 1.1f, .15f, Warm);
            VoxelLine(a, o, finger[2], finger[2] + new float3(3*s, -5, -1), 1.0f, .15f, Warm);
        }

        private static void ReplaceTail(IStructureAuthoringSession a, int3 o)
        {
            // Erase the V3 tail using its actual centerline instead of rectangular clears that can
            // damage feet. The root segment stays because it is integrated into the haunch.
            float3[] old =
            {
                new float3(20,38,39),new float3(49,32,53),new float3(76,24,53),new float3(98,15,36),
                new float3(102,9,12),new float3(94,7,-18),new float3(73,6,-45),new float3(43,5,-65),
                new float3(8,4,-77),new float3(-25,4,-79),new float3(-47,4,-71)
            };
            float[] oldR={18,16,14,12,10,8.5f,7.2f,6,5,3.5f,.35f};
            for (int i=1; i<old.Length-1; i++) ClearLine(a, o, old[i], old[i+1], oldR[i]+1.5f, oldR[i+1]+1.5f);

            // Clear V4's short replacement tip too.
            ClearLine(a, o, new float3(77,8,-43), new float3(38,4,-65), 8.0f, 2.0f);

            // Compact sweep stays behind/right of the body and terminates before crossing the feet.
            float3[] p=
            {
                new float3(20,38,39), new float3(45,31,50), new float3(65,22,45),
                new float3(77,13,28), new float3(75,8,8), new float3(65,6,-9),
                new float3(54,5,-21), new float3(45,4,-29)
            };
            float[] r={17,14,11,8,6,4,2,.25f};
            for(int i=0;i<p.Length-1;i++) VoxelLine(a,o,p[i],p[i+1],r[i],r[i+1],Body);
            for(int i=2;i<p.Length-2;i++)
                VoxelLine(a,o,p[i]+new float3(0,1,0),p[i]+new float3(0,6-(i-2),-1),1.4f,.15f,Warm);
        }

        private static void ReplaceHands(IStructureAuthoringSession a, int3 o)
        {
            // Tail removal intersects the V4 hand envelope, so hands are always authored last.
            for(int side=-1;side<=1;side+=2)
            {
                int minX=side<0?-54:12;
                ClearBox(a,o,new int3(minX,0,-91),new int3(42,15,48));
                float s=side;
                FillEllipsoid(a,o,new float3(30*s,7,-58),new float3(9,5,9),Body);

                float[] dx={-7f,0f,7f};
                for(int i=0;i<3;i++)
                {
                    float x=30*s+dx[i];
                    float z=-62-(i==1?3:0);
                    float3 mid=new float3(x+1.2f*s,4.5f,z-11-(i==1?2:0));
                    float3 claw=new float3(x+2.8f*s,1.5f,mid.z-11);
                    VoxelLine(a,o,new float3(x,6,z),mid,2.5f,1.5f,Body);
                    VoxelLine(a,o,mid,claw,1.6f,.15f,Warm);
                }
            }
        }

        private static void FillRun(IStructureAuthoringSession a,int3 o,int x0,int x1,int y,int z,byte m)
        {if(x0>x1)(x0,x1)=(x1,x0);for(int x=x0;x<=x1;x++)a.Set(o.x+x,o.y+y,o.z+z,m);}

        private static void FillOvalSliceXY(IStructureAuthoringSession a,int3 o,int cx,int cy,int z,int rx,int ry,byte m)
        {float sx=math.max(1,rx),sy=math.max(1,ry);for(int y=cy-ry;y<=cy+ry;y++)for(int x=cx-rx;x<=cx+rx;x++){float dx=(x+.5f-cx)/sx,dy=(y+.5f-cy)/sy;if(dx*dx+dy*dy<=1f)a.Set(o.x+x,o.y+y,o.z+z,m);}}

        private static void FillEllipsoid(IStructureAuthoringSession a,int3 o,float3 c,float3 r,byte m)
        {int3 min=(int3)math.floor(c-r-1),max=(int3)math.ceil(c+r+1);float3 safe=math.max(r,new float3(.5f));for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)for(int x=min.x;x<=max.x;x++){float3 q=(new float3(x+.5f,y+.5f,z+.5f)-c)/safe;if(math.dot(q,q)<=1)a.Set(o.x+x,o.y+y,o.z+z,m);}}

        private static void CarveOval(IStructureAuthoringSession a,int3 o,int3 c,int3 r)
        {for(int y=c.y-r.y;y<=c.y+r.y;y++)for(int z=c.z-r.z;z<=c.z+r.z;z++)for(int x=c.x-r.x;x<=c.x+r.x;x++){float dx=(x+.5f-c.x)/math.max(1f,r.x),dy=(y+.5f-c.y)/math.max(1f,r.y),dz=(z+.5f-c.z)/math.max(1f,r.z);if(dx*dx+dy*dy+dz*dz<=1f)a.Set(o.x+x,o.y+y,o.z+z,Empty);}}

        private static void VoxelLine(IStructureAuthoringSession a,int3 o,float3 p0,float3 p1,float r0,float r1,byte m)
        {float3 axis=p1-p0;float len=math.length(axis);int steps=math.max(1,(int)math.ceil(len*1.5f));for(int i=0;i<=steps;i++){float t=i/(float)steps;float3 p=math.lerp(p0,p1,t);float r=math.lerp(r0,r1,t);for(int y=(int)math.floor(p.y-r);y<=(int)math.ceil(p.y+r);y++)for(int z=(int)math.floor(p.z-r);z<=(int)math.ceil(p.z+r);z++)for(int x=(int)math.floor(p.x-r);x<=(int)math.ceil(p.x+r);x++){float3 d=new float3(x+.5f,y+.5f,z+.5f)-p;if(math.dot(d,d)<=r*r)a.Set(o.x+x,o.y+y,o.z+z,m);}}}

        private static void ClearLine(IStructureAuthoringSession a,int3 o,float3 p0,float3 p1,float r0,float r1)
        {VoxelLine(a,o,p0,p1,r0,r1,Empty);}

        private static void FillTriangle(IStructureAuthoringSession a,int3 o,float3 va,float3 vb,float3 vc,float thick,byte m)
        {float3 n=math.normalizesafe(math.cross(vb-va,vc-va),new float3(0,0,1));int3 min=(int3)math.floor(math.min(va,math.min(vb,vc))-thick-1),max=(int3)math.ceil(math.max(va,math.max(vb,vc))+thick+1);float3 v0=vb-va,v1=vc-va;float d00=math.dot(v0,v0),d01=math.dot(v0,v1),d11=math.dot(v1,v1),den=d00*d11-d01*d01;if(math.abs(den)<.0001f)return;for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)for(int x=min.x;x<=max.x;x++){float3 p=new float3(x+.5f,y+.5f,z+.5f);float dist=math.dot(p-va,n);if(math.abs(dist)>thick)continue;float3 v2=(p-n*dist)-va;float d20=math.dot(v2,v0),d21=math.dot(v2,v1);float v=(d11*d20-d01*d21)/den,w=(d00*d21-d01*d20)/den,u=1-v-w;if(u>=0&&v>=0&&w>=0)a.Set(o.x+x,o.y+y,o.z+z,m);}}

        private static void Box(IStructureAuthoringSession a,int3 o,int3 min,int3 size,byte m)
        {int3 max=min+size;for(int y=min.y;y<max.y;y++)for(int z=min.z;z<max.z;z++)for(int x=min.x;x<max.x;x++)a.Set(o.x+x,o.y+y,o.z+z,m);}

        private static void ClearBox(IStructureAuthoringSession a,int3 o,int3 min,int3 size)
        {Box(a,o,min,size,Empty);}
    }
}
