using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Clean single-owner Dragon A sculpture, authored directly on the 10 cm canonical voxel grid.
    /// Proportions and secondary forms are matched to the established studio concept rather than
    /// inherited from previous procedural dragon passes.
    /// </summary>
    public static class DragonStatueConceptV3Authoring
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Shadow = GameMaterialIds.DarkStone;
        private const byte Scale = GameMaterialIds.Stone;
        private const byte Warm = GameMaterialIds.Dirt;
        private const byte Membrane = GameMaterialIds.Wood;
        private const byte Moss = GameMaterialIds.Moss;
        private const byte Eye = GameMaterialIds.Gold;

        public static readonly int3 LocalMin = new int3(-106, 0, -92);
        public static readonly int3 LocalSize = new int3(212, 174, 198);

        public static void Author(IStructureAuthoringSession a, int3 o)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            AuthorTorso(a, o);
            AuthorRearLeg(a, o, -1);
            AuthorRearLeg(a, o, 1);
            AuthorNeck(a, o);
            AuthorHead(a, o);
            AuthorForeleg(a, o, -1);
            AuthorForeleg(a, o, 1);
            AuthorWing(a, o, -1);
            AuthorWing(a, o, 1);
            AuthorTail(a, o);
            AuthorVentralArmor(a, o);
            AuthorDorsalArmor(a, o);
            AuthorScales(a, o);
            AuthorMoss(a, o);
        }

        private static void AuthorTorso(IStructureAuthoringSession a, int3 o)
        {
            // Low, dense seated mass. The reference is powerful rather than vertically stretched.
            Ellipsoid(a, o, new float3(0, 35, 26), new float3(32, 24, 33), Body);
            Ellipsoid(a, o, new float3(0, 55, 7), new float3(28, 29, 27), Body);
            Ellipsoid(a, o, new float3(0, 73, -7), new float3(24, 23, 22), Body);
            Ellipsoid(a, o, new float3(-25, 35, 29), new float3(23, 21, 25), Body);
            Ellipsoid(a, o, new float3(25, 35, 29), new float3(23, 21, 25), Body);
            Ellipsoid(a, o, new float3(0, 42, -18), new float3(18, 18, 10), Shadow);
            Capsule(a, o, new float3(0, 70, -6), new float3(0, 49, -12), 18, 20, Body);
        }

        private static void AuthorNeck(IStructureAuthoringSession a, int3 o)
        {
            // Forward S-curve: thick at shoulder, narrowing continuously into the skull.
            Capsule(a, o, new float3(0, 72, -5), new float3(-1, 88, -17), 18, 15.5f, Body);
            Capsule(a, o, new float3(-1, 87, -17), new float3(-3, 104, -31), 15.5f, 12.5f, Body);
            Capsule(a, o, new float3(-3, 103, -31), new float3(-5, 117, -44), 12.5f, 10.2f, Body);

            // Layered lateral neck scales follow the curve and break the smooth tube silhouette.
            for (int i = 0; i < 10; i++)
            {
                float t = i / 9f;
                float y = math.lerp(78f, 112f, t);
                float z = math.lerp(-8f, -39f, t);
                float x = math.lerp(15f, 9f, t);
                ScaleLozenge(a, o, new float3(-x, y, z), 4.0f - t, 2.2f, Shadow);
                ScaleLozenge(a, o, new float3(x, y, z), 4.0f - t, 2.2f, Shadow);
            }
        }

        private static void AuthorHead(IStructureAuthoringSession a, int3 o)
        {
            // Broad armored rear skull flowing into a long, low wedge muzzle.
            Ellipsoid(a, o, new float3(-5, 121, -53), new float3(20, 13, 17), Body);
            Ellipsoid(a, o, new float3(-7, 119, -67), new float3(16, 9, 15), Body);
            for (int i = 0; i < 9; i++)
            {
                int half = math.max(5, 13 - i);
                int height = math.max(5, 9 - i / 2);
                int y = 117 - height / 2 - i / 4;
                Box(a, o, new int3(-7 - half, y, -70 - i * 2), new int3(half * 2 + 1, height, 3), Body);
            }

            // Rear cheeks/jaw muscles are huge in the concept.
            Ellipsoid(a, o, new float3(-19, 115, -57), new float3(9, 11, 11), Body);
            Ellipsoid(a, o, new float3(9, 115, -57), new float3(9, 11, 11), Body);

            // Clearly separated upper and lower jaws with a dark mouth cavity.
            Ellipsoid(a, o, new float3(-7, 108, -76), new float3(11, 6.5f, 18), Empty);
            Box(a, o, new int3(-17, 107, -92), new int3(21, 6, 17), Empty);
            Capsule(a, o, new float3(-18, 106, -62), new float3(-16, 100, -84), 4.3f, 2.2f, Shadow);
            Capsule(a, o, new float3(4, 106, -62), new float3(2, 100, -84), 4.3f, 2.2f, Shadow);
            Capsule(a, o, new float3(-16, 100, -84), new float3(2, 100, -84), 2.8f, 2.8f, Shadow);
            Ellipsoid(a, o, new float3(-7, 99, -76), new float3(10, 4, 13), Body);

            // Heavy brows and inset glowing eyes.
            Capsule(a, o, new float3(-10, 125, -64), new float3(-22, 127, -55), 5.5f, 2, Shadow);
            Capsule(a, o, new float3(-2, 125, -64), new float3(10, 127, -55), 5.5f, 2, Shadow);
            Ellipsoid(a, o, new float3(-13, 120, -69), new float3(4, 3.5f, 4), Empty);
            Ellipsoid(a, o, new float3(-1, 120, -69), new float3(4, 3.5f, 4), Empty);
            Box(a, o, new int3(-14, 120, -72), new int3(3, 2, 2), Eye);
            Box(a, o, new int3(-2, 120, -72), new int3(3, 2, 2), Eye);

            // Nose ridge and nostrils.
            Capsule(a, o, new float3(-7, 123, -66), new float3(-7, 117, -89), 5.5f, 3.2f, Body);
            Box(a, o, new int3(-13, 116, -92), new int3(3, 2, 3), Empty);
            Box(a, o, new int3(-3, 116, -92), new int3(3, 2, 3), Empty);

            AuthorTeeth(a, o);
            AuthorCrown(a, o);
            AuthorCheekSpines(a, o);
            AuthorHeadPlates(a, o);
        }

        private static void AuthorTeeth(IStructureAuthoringSession a, int3 o)
        {
            // Visible side gets stronger teeth, but both jaw rails are populated.
            for (int side = -1; side <= 1; side += 2)
            {
                float sx = side;
                Capsule(a, o, new float3(-7 + 7*sx, 113, -70), new float3(-7 + 7*sx, 103, -73), 1.7f, .12f, Warm);
                for (int i=0;i<4;i++)
                {
                    float x=-7+(2.5f+i*2.7f)*sx;
                    float z=-74-i*3.2f;
                    Capsule(a,o,new float3(x,112,z),new float3(x,105,z-1),1.1f,.12f,Warm);
                }
            }
        }

        private static void AuthorCrown(IStructureAuthoringSession a, int3 o)
        {
            // Two dominant layered horns on each side, swept upward/back with compact proportions.
            for (int side=-1; side<=1; side+=2)
            {
                float s=side;
                HornPath(a,o,s,
                    new float3(2,129,-49), new float3(11,139,-42), new float3(21,146,-32),
                    new float3(31,149,-20), new float3(38,145,-7), 4.5f);
                HornPath(a,o,s,
                    new float3(7,124,-48), new float3(17,132,-39), new float3(28,136,-28),
                    new float3(37,134,-17), new float3(43,128,-8), 3.3f);
            }
        }

        private static void AuthorCheekSpines(IStructureAuthoringSession a, int3 o)
        {
            for (int side=-1; side<=1; side+=2)
            {
                float s=side;
                Capsule(a,o,new float3((-5+17*s),122,-56),new float3((-5+32*s),127,-44),3,.12f,Warm);
                Capsule(a,o,new float3((-5+18*s),116,-53),new float3((-5+33*s),116,-41),2.6f,.12f,Warm);
                Capsule(a,o,new float3((-5+16*s),110,-49),new float3((-5+29*s),106,-37),2.2f,.12f,Warm);
                Capsule(a,o,new float3((-5+13*s),104,-46),new float3((-5+24*s),97,-35),1.8f,.12f,Warm);
            }
        }

        private static void AuthorHeadPlates(IStructureAuthoringSession a, int3 o)
        {
            for(int row=0;row<5;row++)
            {
                int half=10-row;
                Box(a,o,new int3(-7-half,130-row*4,-58-row*5),new int3(half*2+1,2,4),Scale);
            }
        }

        private static void AuthorForeleg(IStructureAuthoringSession a,int3 o,int side)
        {
            float s=side;
            float3 shoulder=new float3(22*s,64,-3), elbow=new float3(35*s,39,-24), wrist=new float3(31*s,18,-43);
            Ellipsoid(a,o,shoulder,new float3(14,15,14),Body);
            Capsule(a,o,shoulder,elbow,12,8.5f,Body);
            Ellipsoid(a,o,elbow,new float3(10,10,11),Body);
            Capsule(a,o,elbow,wrist,8.5f,6,Body);
            Ellipsoid(a,o,wrist,new float3(6.5f,6.5f,8),Shadow);
            Capsule(a,o,wrist,new float3(30*s,8,-57),6,5,Body);
            Ellipsoid(a,o,new float3(30*s,6,-60),new float3(15,5.5f,16),Body);

            for(int i=0;i<4;i++)
            {
                float spread=(i-1.5f)*6.2f;
                float x=30*s+spread;
                float z=-65-(i==1||i==2?4:0);
                float3 toe=new float3(x+1.2f*s,4.5f,z-8);
                float3 claw=new float3(x+2.4f*s,1.5f,z-20);
                Capsule(a,o,new float3(x,6,-62),toe,3.2f,2,Body);
                Capsule(a,o,toe,claw,2,.12f,Warm);
            }
        }

        private static void AuthorRearLeg(IStructureAuthoringSession a,int3 o,int side)
        {
            float s=side;
            float3 hip=new float3(28*s,34,24), knee=new float3(46*s,23,2), ankle=new float3(43*s,10,-20);
            Ellipsoid(a,o,hip,new float3(25,22,27),Body);
            Capsule(a,o,hip,knee,15,10,Body);
            Ellipsoid(a,o,knee,new float3(13,12,14),Body);
            Capsule(a,o,knee,ankle,9.5f,6.5f,Body);
            Ellipsoid(a,o,new float3(43*s,6,-32),new float3(17,6,21),Body);

            for(int i=0;i<4;i++)
            {
                float spread=(i-1.5f)*6.5f;
                float x=43*s+spread;
                float z=-38-(i==1||i==2?5:0);
                float3 toe=new float3(x+s,4.5f,z-9);
                float3 claw=new float3(x+2.2f*s,1.5f,z-22);
                Capsule(a,o,new float3(x,6,-35),toe,3.6f,2.2f,Body);
                Capsule(a,o,toe,claw,2.2f,.12f,Warm);
            }
        }

        private static void AuthorWing(IStructureAuthoringSession a,int3 o,int side)
        {
            float s=side;
            float3 root=new float3(17*s,74,13), elbow=new float3(51*s,108,22), wrist=new float3(95*s,134,31);
            float3 hook=new float3(104*s,126,38);
            float3 f0=new float3(103*s,111,37), f1=new float3(98*s,91,36), f2=new float3(90*s,72,33);
            float3 f3=new float3(77*s,55,29), f4=new float3(60*s,43,24), inner=new float3(32*s,49,15);

            MembraneTri(a,o,root,elbow,inner,1.4f,Shadow);
            MembraneTri(a,o,elbow,wrist,inner,1.4f,Membrane);
            MembraneTri(a,o,wrist,f0,f1,1.25f,Membrane);
            MembraneTri(a,o,wrist,f1,f2,1.25f,Membrane);
            MembraneTri(a,o,wrist,f2,f3,1.25f,Membrane);
            MembraneTri(a,o,wrist,f3,f4,1.25f,Membrane);
            MembraneTri(a,o,wrist,f4,inner,1.25f,Membrane);

            // Deep scallops; structural fingers are restored afterwards.
            Ellipsoid(a,o,new float3(101*s,101,36),new float3(10,9,5),Empty);
            Ellipsoid(a,o,new float3(95*s,81,34),new float3(11,10,5),Empty);
            Ellipsoid(a,o,new float3(83*s,62,30),new float3(12,10,5),Empty);
            Ellipsoid(a,o,new float3(67*s,49,25),new float3(11,8,5),Empty);

            Capsule(a,o,root,elbow,8,5.8f,Body);
            Capsule(a,o,elbow,wrist,5.8f,3.5f,Body);
            Capsule(a,o,wrist,hook,3.5f,.12f,Warm);
            Capsule(a,o,wrist,f0,3.2f,.7f,Shadow);
            Capsule(a,o,wrist,f1,3,.62f,Shadow);
            Capsule(a,o,wrist,f2,2.8f,.55f,Shadow);
            Capsule(a,o,wrist,f3,2.5f,.48f,Shadow);
            Capsule(a,o,wrist,f4,2.2f,.38f,Shadow);

            // Small overlapping scales on the wing arm.
            for(int i=0;i<11;i++)
            {
                float t=i/10f;
                float3 p=t<.45f?math.lerp(root,elbow,t/.45f):math.lerp(elbow,wrist,(t-.45f)/.55f);
                ScaleLozenge(a,o,p+new float3(-2*s,2,-2),4.2f,2.2f,Scale);
            }
        }

        private static void AuthorTail(IStructureAuthoringSession a,int3 o)
        {
            float3[] p={
                new float3(20,38,39),new float3(49,32,53),new float3(76,24,53),new float3(98,15,36),
                new float3(102,9,12),new float3(94,7,-18),new float3(73,6,-45),new float3(43,5,-65),
                new float3(8,4,-77),new float3(-25,4,-79),new float3(-47,4,-71)};
            float[] r={18,16,14,12,10,8.5f,7.2f,6,5,3.5f,.35f};
            for(int i=0;i<p.Length-1;i++) Capsule(a,o,p[i],p[i+1],r[i],r[i+1],Body);

            for(int i=1;i<p.Length-2;i++)
            {
                float t=(i-1)/(float)(p.Length-4);
                float h=math.lerp(10,4,t);
                Capsule(a,o,p[i]+new float3(0,1,0),p[i]+new float3(0,h+2,-1),math.lerp(3,1,t),.12f,Warm);
                if((i&1)==0) Capsule(a,o,p[i]+new float3(0,1,0),p[i]+new float3(0,2,-h-3),math.lerp(2.2f,.8f,t),.12f,Warm);
            }
        }

        private static void AuthorVentralArmor(IStructureAuthoringSession a,int3 o)
        {
            // Pointed, overlapping warm shields from jaw to belly.
            for(int i=0;i<12;i++)
            {
                int y=111-i*6;
                int z=-38+i*2;
                int half=7+i/2;
                Box(a,o,new int3(-half,y-2,z-4),new int3(half*2+1,4,5),Warm);
                for(int row=0;row<3;row++)
                {
                    int rh=math.max(1,half-row*3);
                    Box(a,o,new int3(-rh,y-3-row,z-5),new int3(rh*2+1,1,5),Warm);
                }
            }
        }

        private static void AuthorDorsalArmor(IStructureAuthoringSession a,int3 o)
        {
            float3[] p={new float3(0,111,-35),new float3(0,98,-24),new float3(0,85,-12),new float3(0,72,2),new float3(0,59,17),new float3(0,47,32)};
            for(int i=0;i<p.Length;i++)
            {
                float h=math.lerp(10,6,i/(float)(p.Length-1));
                Capsule(a,o,p[i],p[i]+new float3(0,h,4),2.5f,.12f,Warm);
            }
        }

        private static void AuthorScales(IStructureAuthoringSession a,int3 o)
        {
            // Dense small scale lozenges along neck, shoulder and visible haunches.
            for(int side=-1;side<=1;side+=2)
            {
                float s=side;
                for(int row=0;row<9;row++)
                {
                    int y=76-row*5;
                    int z=4+row*3;
                    int x=19+row/3;
                    for(int j=0;j<4;j++) ScaleLozenge(a,o,new float3(s*x,y,z+(j-1.5f)*4),3,1.5f,Shadow);
                }
                for(int i=0;i<10;i++)
                {
                    float angle=math.radians(-70+i*15);
                    float3 c=new float3(28*s+math.sin(angle)*19*s,35+math.cos(angle)*14,28-i*.4f);
                    ScaleLozenge(a,o,c,3.6f,1.6f,Scale);
                }
            }
        }

        private static void AuthorMoss(IStructureAuthoringSession a,int3 o)
        {
            int3[] patches={new int3(-13,126,-52),new int3(4,128,-46),new int3(-8,98,-23),new int3(18,77,3),new int3(-23,62,16),new int3(32,39,35),new int3(69,25,50),new int3(91,13,25)};
            foreach(int3 p in patches) Box(a,o,p,new int3(3,2,3),Moss);
        }

        private static void HornPath(IStructureAuthoringSession a,int3 o,float side,float3 p0,float3 p1,float3 p2,float3 p3,float3 p4,float r)
        {
            p0.x=-5+p0.x*side;p1.x=-5+p1.x*side;p2.x=-5+p2.x*side;p3.x=-5+p3.x*side;p4.x=-5+p4.x*side;
            Capsule(a,o,p0,p1,r,r*.76f,Warm);Capsule(a,o,p1,p2,r*.76f,r*.5f,Warm);
            Capsule(a,o,p2,p3,r*.5f,r*.24f,Warm);Capsule(a,o,p3,p4,r*.24f,.12f,Warm);
        }

        private static void ScaleLozenge(IStructureAuthoringSession a,int3 o,float3 c,float radius,float depth,byte material)
        {
            Ellipsoid(a,o,c,new float3(radius,radius*.65f,depth),material);
        }

        private static void Box(IStructureAuthoringSession a,int3 o,int3 min,int3 size,byte m)
        {int3 max=min+size;for(int y=min.y;y<max.y;y++)for(int z=min.z;z<max.z;z++)for(int x=min.x;x<max.x;x++)a.Set(o.x+x,o.y+y,o.z+z,m);}

        private static void Ellipsoid(IStructureAuthoringSession a,int3 o,float3 c,float3 r,byte m)
        {int3 min=(int3)math.floor(c-r-1),max=(int3)math.ceil(c+r+1);float3 safe=math.max(r,new float3(.5f));for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)for(int x=min.x;x<=max.x;x++){float3 q=(new float3(x+.5f,y+.5f,z+.5f)-c)/safe;if(math.dot(q,q)<=1)a.Set(o.x+x,o.y+y,o.z+z,m);}}

        private static void Capsule(IStructureAuthoringSession a,int3 o,float3 p0,float3 p1,float r0,float r1,byte m)
        {float mr=math.max(r0,r1);int3 min=(int3)math.floor(math.min(p0,p1)-mr-1),max=(int3)math.ceil(math.max(p0,p1)+mr+1);float3 axis=p1-p0;float l2=math.max(.0001f,math.dot(axis,axis));for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)for(int x=min.x;x<=max.x;x++){float3 p=new float3(x+.5f,y+.5f,z+.5f);float t=math.saturate(math.dot(p-p0,axis)/l2);float3 d=p-(p0+axis*t);float r=math.lerp(r0,r1,t);if(math.dot(d,d)<=r*r)a.Set(o.x+x,o.y+y,o.z+z,m);}}

        private static void MembraneTri(IStructureAuthoringSession a,int3 o,float3 va,float3 vb,float3 vc,float thick,byte m)
        {float3 n=math.normalizesafe(math.cross(vb-va,vc-va),new float3(0,0,1));int3 min=(int3)math.floor(math.min(va,math.min(vb,vc))-thick-1),max=(int3)math.ceil(math.max(va,math.max(vb,vc))+thick+1);float3 v0=vb-va,v1=vc-va;float d00=math.dot(v0,v0),d01=math.dot(v0,v1),d11=math.dot(v1,v1),den=d00*d11-d01*d01;if(math.abs(den)<.0001f)return;for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)for(int x=min.x;x<=max.x;x++){float3 p=new float3(x+.5f,y+.5f,z+.5f);float dist=math.dot(p-va,n);if(math.abs(dist)>thick)continue;float3 v2=(p-n*dist)-va;float d20=math.dot(v2,v0),d21=math.dot(v2,v1);float v=(d11*d20-d01*d21)/den,w=(d00*d21-d01*d20)/den,u=1-v-w;if(u>=0&&v>=0&&w>=0)a.Set(o.x+x,o.y+y,o.z+z,m);}}
    }
}
