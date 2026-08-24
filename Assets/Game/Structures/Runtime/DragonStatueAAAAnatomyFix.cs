using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Restores primary anatomy after destructive render-driven carving, then layers readable detail.</summary>
    public static class DragonStatueAAAAnatomyFix
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Dark = GameMaterialIds.DarkStone;
        private const byte Plate = GameMaterialIds.Stone;
        private const byte Horn = GameMaterialIds.Stone;
        private const byte Wing = GameMaterialIds.Wood;

        public static void Apply(IStructureAuthoringSession a, int3 o)
        {
            RestoreTorso(a, o);
            RestoreChest(a, o);
            RestoreForeleg(a, o, -1);
            RestoreForeleg(a, o, 1);
            DeepenWing(a, o, -1);
            DeepenWing(a, o, 1);
            TailArmor(a, o);
            NeckArmor(a, o);
        }

        private static void RestoreTorso(IStructureAuthoringSession a, int3 o)
        {
            Ellipsoid(a, o, new float3(0, 43, 22), new float3(30, 24, 33), Body);
            Ellipsoid(a, o, new float3(0, 66, 6), new float3(24, 30, 26), Body);
            Ellipsoid(a, o, new float3(0, 82, -5), new float3(20, 23, 21), Body);
            Ellipsoid(a, o, new float3(-21, 72, 0), new float3(14, 16, 15), Body);
            Ellipsoid(a, o, new float3(21, 72, 0), new float3(14, 16, 15), Body);
            Ellipsoid(a, o, new float3(-24, 39, 25), new float3(20, 18, 22), Body);
            Ellipsoid(a, o, new float3(24, 39, 25), new float3(20, 18, 22), Body);
            Ellipsoid(a, o, new float3(0, 43, -11), new float3(17, 19, 11), Dark);
        }

        private static void RestoreChest(IStructureAuthoringSession a, int3 o)
        {
            Capsule(a, o, new float3(0, 120, -30), new float3(0, 51, -13), 11f, 16f, Body);
            for (int i = 0; i < 10; i++)
            {
                int y = 118 - i * 8;
                int half = 7 + i;
                int z = -40 + i * 3;
                // Shallow slab, not a bead.
                Box(a, o, new int3(-half, y - 2, z - 3), new int3(half * 2 + 1, 5, 6), Plate);
                int pointHalf = math.max(2, half - 4);
                Box(a, o, new int3(-pointHalf, y - 5, z - 4), new int3(pointHalf * 2 + 1, 3, 5), Plate);
            }
        }

        private static void RestoreForeleg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 shoulder = new float3(20*s, 72, -3);
            float3 elbow = new float3(34*s, 52, -20);
            float3 wrist = new float3(27*s, 25, -39);
            float3 palm = new float3(22*s, 8, -54);
            Capsule(a, o, shoulder, elbow, 10.5f, 7.8f, Body);
            Ellipsoid(a, o, elbow, new float3(9, 9, 10), Body);
            Capsule(a, o, elbow, wrist, 7.8f, 5.2f, Body);
            Ellipsoid(a, o, wrist, new float3(6, 6, 7), Dark);
            Capsule(a, o, wrist, palm, 5.2f, 4.0f, Body);
            Ellipsoid(a, o, new float3(22*s, 7, -57), new float3(11, 4.5f, 13), Body);

            // Make each toe a visible finger segment before the claw.
            for (int i = 0; i < 4; i++)
            {
                float lateral = (i - 1.5f) * 4.5f;
                float x = 22*s + lateral;
                float z0 = -61 - (i == 1 || i == 2 ? 4 : 0);
                Capsule(a, o, new float3(x, 7, -58), new float3(x + 1.5f*s, 5, z0), 2.5f, 1.6f, Body);
                Capsule(a, o, new float3(x + 1.5f*s, 5, z0), new float3(x + 3*s, 2.5f, z0 - 10), 1.6f, 0.15f, Horn);
            }
        }

        private static void DeepenWing(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            // Carve the membrane from its lower edge upward so the silhouette has three unmistakable bays.
            Ellipsoid(a, o, new float3(99*s, 91, 27), new float3(15, 15, 8), Empty);
            Ellipsoid(a, o, new float3(89*s, 72, 25), new float3(16, 15, 8), Empty);
            Ellipsoid(a, o, new float3(74*s, 56, 21), new float3(16, 14, 8), Empty);

            float3 wrist = new float3(91*s, 132, 24);
            float3 f0 = new float3(104*s, 103, 28);
            float3 f1 = new float3(95*s, 84, 27);
            float3 f2 = new float3(82*s, 66, 24);
            float3 f3 = new float3(65*s, 52, 20);
            Capsule(a, o, wrist, f0, 3.1f, 0.6f, Dark);
            Capsule(a, o, wrist, f1, 2.9f, 0.55f, Dark);
            Capsule(a, o, wrist, f2, 2.7f, 0.5f, Dark);
            Capsule(a, o, wrist, f3, 2.5f, 0.45f, Dark);

            // Small warm panels near the root keep the wing from becoming visually black after cuts.
            Membrane(a, o, new float3(47*s,121,16), wrist, new float3(39*s,67,13), 1.2f, Wing);
        }

        private static void TailArmor(IStructureAuthoringSession a, int3 o)
        {
            float3[] p =
            {
                new float3(64, 31, 53), new float3(83, 24, 46), new float3(96, 16, 28),
                new float3(96, 10, 4), new float3(84, 8, -24), new float3(65, 7, -43),
                new float3(44, 6, -59), new float3(22, 5, -71)
            };
            for (int i = 0; i < p.Length; i++)
            {
                float scale = math.lerp(6f, 3f, i / (float)(p.Length - 1));
                Ellipsoid(a, o, p[i] + new float3(0, scale * 0.7f, -1), new float3(scale, 2.3f, scale * 0.8f), Plate);
                if ((i & 1) == 0)
                    Capsule(a, o, p[i] + new float3(0, scale, 0), p[i] + new float3(1, scale + 7, 3), 2f, 0.15f, Horn);
            }
        }

        private static void NeckArmor(IStructureAuthoringSession a, int3 o)
        {
            // Side armor plates add the layered dragon texture missing from the smooth neck cylinder.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                for (int i = 0; i < 7; i++)
                {
                    float t = i / 6f;
                    float y = math.lerp(91f, 137f, t);
                    float x = math.lerp(12f, 8f, t) * s;
                    float z = math.lerp(-17f, -47f, t);
                    Ellipsoid(a, o, new float3(x, y, z), new float3(5, 3, 4), Plate);
                }
            }
        }

        private static void Box(IStructureAuthoringSession a,int3 o,int3 min,int3 size,byte m){int3 max=min+size;for(int y=min.y;y<max.y;y++)for(int z=min.z;z<max.z;z++)for(int x=min.x;x<max.x;x++)a.Set(o.x+x,o.y+y,o.z+z,m);}
        private static void Ellipsoid(IStructureAuthoringSession a,int3 o,float3 c,float3 r,byte m){int3 min=(int3)math.floor(c-r-1f),max=(int3)math.ceil(c+r+1f);float3 s=math.max(r,new float3(.5f));for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)for(int x=min.x;x<=max.x;x++){float3 q=(new float3(x+.5f,y+.5f,z+.5f)-c)/s;if(math.dot(q,q)<=1f)a.Set(o.x+x,o.y+y,o.z+z,m);}}
        private static void Capsule(IStructureAuthoringSession a,int3 o,float3 p0,float3 p1,float r0,float r1,byte m){float mr=math.max(r0,r1);int3 min=(int3)math.floor(math.min(p0,p1)-mr-1f),max=(int3)math.ceil(math.max(p0,p1)+mr+1f);float3 ax=p1-p0;float l2=math.max(.0001f,math.dot(ax,ax));for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)for(int x=min.x;x<=max.x;x++){float3 p=new float3(x+.5f,y+.5f,z+.5f);float t=math.saturate(math.dot(p-p0,ax)/l2);float3 d=p-(p0+ax*t);float r=math.lerp(r0,r1,t);if(math.dot(d,d)<=r*r)a.Set(o.x+x,o.y+y,o.z+z,m);}}
        private static void Membrane(IStructureAuthoringSession a,int3 o,float3 va,float3 vb,float3 vc,float ht,byte m){float3 n=math.normalizesafe(math.cross(vb-va,vc-va),new float3(0,0,1));int3 min=(int3)math.floor(math.min(va,math.min(vb,vc))-ht-1f),max=(int3)math.ceil(math.max(va,math.max(vb,vc))+ht+1f);float3 e0=vb-va,e1=vc-va;float d00=math.dot(e0,e0),d01=math.dot(e0,e1),d11=math.dot(e1,e1),den=math.max(.0001f,d00*d11-d01*d01);for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)for(int x=min.x;x<=max.x;x++){float3 p=new float3(x+.5f,y+.5f,z+.5f);float sd=math.dot(p-va,n);if(math.abs(sd)>ht)continue;float3 v2=p-n*sd-va;float d20=math.dot(v2,e0),d21=math.dot(v2,e1);float v=(d11*d20-d01*d21)/den,w=(d00*d21-d01*d20)/den,u=1f-v-w;if(u>=-.01f&&v>=-.01f&&w>=-.01f)a.Set(o.x+x,o.y+y,o.z+z,m);}}
    }
}
