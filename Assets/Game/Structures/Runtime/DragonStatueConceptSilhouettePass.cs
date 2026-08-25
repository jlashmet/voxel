using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Production-render correction pass for the literal concept match. This pass owns only the
    /// silhouette landmarks that were still wrong after the first reference-voxel rebuild.
    /// </summary>
    public static class DragonStatueConceptSilhouettePass
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Dark = GameMaterialIds.DarkStone;
        private const byte Plate = GameMaterialIds.Stone;
        private const byte Horn = GameMaterialIds.Stone;
        private const byte Eye = GameMaterialIds.Gold;

        public static void Apply(IStructureAuthoringSession a, int3 o)
        {
            RebuildHeadAndCrown(a, o);
            RebuildForeleg(a, o, -1);
            RebuildForeleg(a, o, 1);
            ReinforceHaunches(a, o);
            ReinforceTail(a, o);
            RefineChestArmor(a, o);
        }

        private static void RebuildHeadAndCrown(IStructureAuthoringSession a, int3 o)
        {
            // Remove the antler-like crown and upper skull while leaving the neck root intact.
            Clear(a, o, new int3(-60, 118, -100), new int3(120, 56, 108));

            // Target skull: broad rear cranium, long low wedge muzzle, aggressive downturned nose.
            Ellipsoid(a, o, new float3(0, 139, -51), new float3(19, 12, 16), Body);
            Ellipsoid(a, o, new float3(0, 137, -66), new float3(15, 9, 15), Body);
            for (int i = 0; i < 9; i++)
            {
                int half = math.max(5, 13 - i);
                int height = math.max(5, 9 - i / 2);
                Box(a, o, new int3(-half, 134 - height / 2 - i / 4, -70 - i * 2),
                    new int3(half * 2 + 1, height, 3), Body);
            }

            // Angular cheek plates make the head wide behind the muzzle like the reference.
            Ellipsoid(a, o, new float3(-14, 134, -57), new float3(8, 10, 10), Body);
            Ellipsoid(a, o, new float3(14, 134, -57), new float3(8, 10, 10), Body);
            Capsule(a, o, new float3(-8, 142, -56), new float3(-18, 145, -48), 5.2f, 2f, Dark);
            Capsule(a, o, new float3(8, 142, -56), new float3(18, 145, -48), 5.2f, 2f, Dark);

            // Deep open mouth with a shorter, heavier lower jaw.
            Ellipsoid(a, o, new float3(0, 128, -75), new float3(11, 6, 18), Empty);
            Box(a, o, new int3(-10, 127, -91), new int3(21, 6, 17), Empty);
            Capsule(a, o, new float3(-11, 126, -61), new float3(-9, 119, -83), 4.2f, 2.2f, Dark);
            Capsule(a, o, new float3(11, 126, -61), new float3(9, 119, -83), 4.2f, 2.2f, Dark);
            Capsule(a, o, new float3(-9, 119, -83), new float3(9, 119, -83), 2.8f, 2.8f, Dark);
            Ellipsoid(a, o, new float3(0, 118, -75), new float3(10, 4, 13), Body);

            // Brow and inset eyes.
            Ellipsoid(a, o, new float3(-7, 139, -67), new float3(4, 3.5f, 4), Empty);
            Ellipsoid(a, o, new float3(7, 139, -67), new float3(4, 3.5f, 4), Empty);
            Box(a, o, new int3(-8, 139, -71), new int3(3, 2, 2), Eye);
            Box(a, o, new int3(6, 139, -71), new int3(3, 2, 2), Eye);
            Capsule(a, o, new float3(0, 141, -65), new float3(0, 136, -87), 5.5f, 3.2f, Body);
            Box(a, o, new int3(-6, 135, -91), new int3(3, 2, 3), Empty);
            Box(a, o, new int3(4, 135, -91), new int3(3, 2, 3), Empty);

            // Teeth: front canines plus an irregular jaw line.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                Capsule(a, o, new float3(8*s, 133, -70), new float3(8*s, 123, -72), 1.6f, .12f, Horn);
                for (int i = 0; i < 4; i++)
                {
                    float x = (2.5f + i * 2.8f) * s;
                    float z = -74 - i * 3.3f;
                    Capsule(a, o, new float3(x, 132, z), new float3(x, 125, z - 1), 1.1f, .12f, Horn);
                }
            }

            // Compact layered crown. The concept horns grow back from the skull in overlapping tiers;
            // none should dominate the full silhouette like antlers.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                HornPath(a, o, s,
                    new float3(8,146,-49), new float3(16,153,-41), new float3(26,157,-31),
                    new float3(35,156,-20), new float3(40,151,-11), 4.3f);
                HornPath(a, o, s,
                    new float3(12,142,-48), new float3(21,147,-38), new float3(31,148,-27),
                    new float3(39,144,-17), new float3(43,139,-10), 3.2f);
                HornPath(a, o, s,
                    new float3(15,137,-46), new float3(24,139,-36), new float3(33,137,-27),
                    new float3(39,132,-19), new float3(42,127,-13), 2.4f);

                // Four short cheek/jaw spines create the dense radial silhouette in the concept.
                Capsule(a, o, new float3(16*s,139,-57), new float3(30*s,143,-47), 2.8f,.12f,Horn);
                Capsule(a, o, new float3(17*s,133,-54), new float3(31*s,133,-43), 2.5f,.12f,Horn);
                Capsule(a, o, new float3(15*s,127,-50), new float3(28*s,123,-39), 2.2f,.12f,Horn);
                Capsule(a, o, new float3(12*s,121,-46), new float3(23*s,114,-36), 1.8f,.12f,Horn);
            }

            // Layered forehead plates bridge from crown into muzzle.
            for (int row = 0; row < 5; row++)
            {
                int half = 10 - row;
                Box(a, o, new int3(-half, 147 - row*4, -57 - row*5),
                    new int3(half*2+1, 2, 4), Plate);
            }
        }

        private static void RebuildForeleg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            int minX = side < 0 ? -50 : 10;
            Clear(a, o, new int3(minX, 0, -78), new int3(40, 82, 80));

            // Shorter, more powerful front limbs: shoulder and elbow are lower, palm is huge.
            float3 shoulder = new float3(21*s, 70, -4);
            float3 elbow = new float3(35*s, 46, -21);
            float3 wrist = new float3(30*s, 23, -39);
            float3 palm = new float3(29*s, 9, -55);
            Ellipsoid(a, o, shoulder, new float3(14, 15, 14), Body);
            Capsule(a, o, shoulder, elbow, 12f, 8.5f, Body);
            Ellipsoid(a, o, elbow, new float3(10, 10, 11), Body);
            Capsule(a, o, elbow, wrist, 8.5f, 6f, Body);
            Ellipsoid(a, o, wrist, new float3(6.5f, 6.5f, 8), Dark);
            Capsule(a, o, wrist, palm, 6f, 5f, Body);
            Ellipsoid(a, o, new float3(29*s, 7, -60), new float3(14, 5.5f, 16), Body);

            // Four broad toes and long hooked claws with visible spacing.
            for (int i = 0; i < 4; i++)
            {
                float spread = (i - 1.5f) * 6f;
                float x = 29*s + spread;
                float z = -64 - (i == 1 || i == 2 ? 4 : 0);
                float3 knuckle = new float3(x, 7, -62);
                float3 toe = new float3(x + 1.2f*s, 5, z - 8);
                float3 claw = new float3(x + 2.2f*s, 2, z - 20);
                Capsule(a, o, knuckle, toe, 3.2f, 2f, Body);
                Capsule(a, o, toe, claw, 2f, .12f, Horn);
            }
        }

        private static void ReinforceHaunches(IStructureAuthoringSession a, int3 o)
        {
            Ellipsoid(a, o, new float3(-28, 37, 29), new float3(25, 22, 27), Body);
            Ellipsoid(a, o, new float3(28, 37, 29), new float3(25, 22, 27), Body);
            Ellipsoid(a, o, new float3(-43, 24, 8), new float3(15, 13, 17), Body);
            Ellipsoid(a, o, new float3(43, 24, 8), new float3(15, 13, 17), Body);
        }

        private static void ReinforceTail(IStructureAuthoringSession a, int3 o)
        {
            // Overlay the old path with a much thicker tail all the way through the foreground.
            float3[] p =
            {
                new float3(18,42,39), new float3(45,37,55), new float3(71,30,57),
                new float3(93,21,44), new float3(103,14,20), new float3(100,10,-8),
                new float3(85,8,-35), new float3(61,7,-56), new float3(32,6,-70),
                new float3(2,5,-80), new float3(-28,5,-82), new float3(-51,5,-76)
            };
            float[] r = {17,15.5f,14,12,10,8.5f,7.5f,6.5f,5.5f,4.5f,3.2f,.35f};
            for (int i=0;i<p.Length-1;i++) Capsule(a,o,p[i],p[i+1],r[i],r[i+1],Body);

            // Dense dorsal/lateral spines are a major concept cue.
            for (int i=2;i<p.Length-2;i++)
            {
                float t=(i-2)/(float)(p.Length-4);
                float h=math.lerp(9f,3.5f,t);
                Capsule(a,o,p[i]+new float3(0,1,0),p[i]+new float3(0,h+2,-1),2.5f*(1-t)+.8f,.12f,Horn);
                if ((i&1)==0)
                    Capsule(a,o,p[i]+new float3(0,1,0),p[i]+new float3(0,2,-h-2),1.8f*(1-t)+.6f,.12f,Horn);
            }
        }

        private static void RefineChestArmor(IStructureAuthoringSession a, int3 o)
        {
            // Replace the oversized horizontal 'rib' slabs with narrower overlapping pointed shields.
            Clear(a,o,new int3(-18,42,-46),new int3(36,86,38));
            Capsule(a,o,new float3(0,119,-30),new float3(0,52,-12),12f,18f,Body);
            for(int i=0;i<11;i++)
            {
                int y=119-i*7, z=-38+i*2, half=7+i/2;
                Box(a,o,new int3(-half,y-2,z-3),new int3(half*2+1,4,4),Plate);
                for(int row=0;row<3;row++)
                {
                    int rh=math.max(1,half-row*3);
                    Box(a,o,new int3(-rh,y-3-row,z-4),new int3(rh*2+1,1,4),Plate);
                }
            }
        }

        private static void HornPath(IStructureAuthoringSession a,int3 o,float s,float3 p0,float3 p1,float3 p2,float3 p3,float3 p4,float r)
        {
            p0.x*=s;p1.x*=s;p2.x*=s;p3.x*=s;p4.x*=s;
            Capsule(a,o,p0,p1,r,r*.76f,Horn); Capsule(a,o,p1,p2,r*.76f,r*.5f,Horn);
            Capsule(a,o,p2,p3,r*.5f,r*.24f,Horn); Capsule(a,o,p3,p4,r*.24f,.12f,Horn);
        }

        private static void Clear(IStructureAuthoringSession a,int3 o,int3 min,int3 size)
        { int3 max=min+size; for(int y=min.y;y<max.y;y++)for(int z=min.z;z<max.z;z++)for(int x=min.x;x<max.x;x++)a.Set(o.x+x,o.y+y,o.z+z,Empty); }
        private static void Box(IStructureAuthoringSession a,int3 o,int3 min,int3 size,byte m)
        { int3 max=min+size; for(int y=min.y;y<max.y;y++)for(int z=min.z;z<max.z;z++)for(int x=min.x;x<max.x;x++)a.Set(o.x+x,o.y+y,o.z+z,m); }
        private static void Ellipsoid(IStructureAuthoringSession a,int3 o,float3 c,float3 r,byte m)
        { int3 min=(int3)math.floor(c-r-1),max=(int3)math.ceil(c+r+1);float3 safe=math.max(r,new float3(.5f));for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)for(int x=min.x;x<=max.x;x++){float3 q=(new float3(x+.5f,y+.5f,z+.5f)-c)/safe;if(math.dot(q,q)<=1)a.Set(o.x+x,o.y+y,o.z+z,m);} }
        private static void Capsule(IStructureAuthoringSession a,int3 o,float3 p0,float3 p1,float r0,float r1,byte m)
        { float mr=math.max(r0,r1);int3 min=(int3)math.floor(math.min(p0,p1)-mr-1),max=(int3)math.ceil(math.max(p0,p1)+mr+1);float3 axis=p1-p0;float l2=math.max(.0001f,math.dot(axis,axis));for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)for(int x=min.x;x<=max.x;x++){float3 p=new float3(x+.5f,y+.5f,z+.5f);float t=math.saturate(math.dot(p-p0,axis)/l2);float3 d=p-(p0+axis*t);float r=math.lerp(r0,r1,t);if(math.dot(d,d)<=r*r)a.Set(o.x+x,o.y+y,o.z+z,m);} }
    }
}
