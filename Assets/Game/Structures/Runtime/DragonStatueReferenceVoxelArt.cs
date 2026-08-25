using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Reference-driven 10 cm voxel sculpture for Model Viewer Dragon A.</summary>
    public static class DragonStatueReferenceVoxelArt
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Dark = GameMaterialIds.DarkStone;
        private const byte Plate = GameMaterialIds.Stone;
        private const byte Horn = GameMaterialIds.Stone;
        private const byte MembraneMat = GameMaterialIds.Wood;
        private const byte Moss = GameMaterialIds.Moss;
        private const byte Eye = GameMaterialIds.Gold;

        public static readonly int3 LocalMin = new int3(-106, 0, -92);
        public static readonly int3 LocalSize = new int3(212, 174, 198);

        public static void Author(IStructureAuthoringSession a, int3 o)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));
            BodyMass(a, o);
            RearLeg(a, o, -1); RearLeg(a, o, 1);
            Neck(a, o); Head(a, o);
            Foreleg(a, o, -1); Foreleg(a, o, 1);
            AuthorWing(a, o, -1); AuthorWing(a, o, 1);
            Tail(a, o); ThroatArmor(a, o); DorsalCrest(a, o);
            ScaleBands(a, o); Patina(a, o);
        }

        private static void BodyMass(IStructureAuthoringSession a, int3 o)
        {
            Ellipsoid(a,o,new float3(0,38,27),new float3(30,24,34),Body);
            Ellipsoid(a,o,new float3(0,63,8),new float3(25,31,27),Body);
            Ellipsoid(a,o,new float3(0,82,-3),new float3(22,24,22),Body);
            Ellipsoid(a,o,new float3(-24,38,30),new float3(22,20,25),Body);
            Ellipsoid(a,o,new float3(24,38,30),new float3(22,20,25),Body);
            Ellipsoid(a,o,new float3(0,49,-16),new float3(17,22,10),Dark);
            Capsule(a,o,new float3(0,76,-7),new float3(0,54,-12),17,19,Body);
        }

        private static void Neck(IStructureAuthoringSession a, int3 o)
        {
            Capsule(a,o,new float3(0,79,-4),new float3(-2,100,-19),16,13.5f,Body);
            Capsule(a,o,new float3(-2,99,-19),new float3(1,121,-33),13.5f,10.5f,Body);
            Capsule(a,o,new float3(1,120,-33),new float3(0,139,-47),10.5f,8.5f,Body);
            for(int i=0;i<8;i++)
            {
                float t=i/7f; int y=(int)math.round(math.lerp(88,132,t));
                int z=(int)math.round(math.lerp(-12,-42,t)); int x=(int)math.round(math.lerp(13,8,t));
                Box(a,o,new int3(-x-2,y-2,z-2),new int3(5,4,4),Dark);
                Box(a,o,new int3(x-2,y-2,z-2),new int3(5,4,4),Dark);
            }
        }

        private static void Head(IStructureAuthoringSession a, int3 o)
        {
            Ellipsoid(a,o,new float3(0,146,-55),new float3(18,11,15),Body);
            Ellipsoid(a,o,new float3(0,143,-69),new float3(14,8,14),Body);
            Ellipsoid(a,o,new float3(0,140,-81),new float3(10,6,10),Body);
            for(int i=0;i<7;i++)
            {
                int half=math.max(4,10-i); int h=math.max(4,7-i/2);
                Box(a,o,new int3(-half,137-h/2,-80-i*2),new int3(half*2+1,h,3),Body);
            }
            Ellipsoid(a,o,new float3(-13,137,-60),new float3(7,9,9),Body);
            Ellipsoid(a,o,new float3(13,137,-60),new float3(7,9,9),Body);

            // Open mouth and separate lower jaw.
            Ellipsoid(a,o,new float3(0,134,-75),new float3(10,5.5f,16),Empty);
            Box(a,o,new int3(-9,132,-91),new int3(19,5,16),Empty);
            Capsule(a,o,new float3(-10,130,-62),new float3(-8,124,-85),3.2f,1.8f,Dark);
            Capsule(a,o,new float3(10,130,-62),new float3(8,124,-85),3.2f,1.8f,Dark);
            Capsule(a,o,new float3(-8,124,-85),new float3(8,124,-85),2.4f,2.4f,Dark);
            Ellipsoid(a,o,new float3(0,123,-77),new float3(9,3.5f,12),Body);

            // Brow, eyes, nose.
            Capsule(a,o,new float3(-2,151,-65),new float3(-17,153,-55),5,1.5f,Dark);
            Capsule(a,o,new float3(2,151,-65),new float3(17,153,-55),5,1.5f,Dark);
            Ellipsoid(a,o,new float3(-7,146,-69),new float3(4,3,3.5f),Empty);
            Ellipsoid(a,o,new float3(7,146,-69),new float3(4,3,3.5f),Empty);
            Box(a,o,new int3(-8,146,-72),new int3(3,2,2),Eye);
            Box(a,o,new int3(6,146,-72),new int3(3,2,2),Eye);
            Capsule(a,o,new float3(0,147,-66),new float3(0,142,-88),5.5f,3.5f,Body);
            Box(a,o,new int3(-6,141,-90),new int3(3,2,3),Empty);
            Box(a,o,new int3(4,141,-90),new int3(3,2,3),Empty);

            Teeth(a,o); Crown(a,o); CheekSpines(a,o,-1); CheekSpines(a,o,1);
            for(int row=0;row<4;row++)
            {
                int half=8-row;
                Box(a,o,new int3(-half,154-row*4,-58-row*5),new int3(half*2+1,2,4),Plate);
            }
        }

        private static void Teeth(IStructureAuthoringSession a,int3 o)
        {
            for(int side=-1;side<=1;side+=2)
            {
                float s=side;
                Capsule(a,o,new float3(7.5f*s,138,-71),new float3(7.5f*s,129,-73),1.4f,.15f,Horn);
                for(int i=0;i<4;i++)
                {
                    float x=(2.5f+i*2.8f)*s, z=-76-i*3.2f;
                    Capsule(a,o,new float3(x,137,z),new float3(x,131,z-1),1,.12f,Horn);
                }
                for(int i=0;i<3;i++)
                {
                    float x=(3.5f+i*3.2f)*s, z=-72-i*4;
                    Capsule(a,o,new float3(x,125,z),new float3(x,131,z-1),.9f,.12f,Horn);
                }
            }
        }

        private static void Crown(IStructureAuthoringSession a,int3 o)
        {
            for(int side=-1;side<=1;side+=2)
            {
                float s=side;
                HornPath(a,o,s, new float3(8,154,-52),new float3(18,163,-43),new float3(31,169,-29),new float3(42,170,-13),new float3(49,165,1),4.2f);
                HornPath(a,o,s, new float3(12,150,-49),new float3(24,157,-37),new float3(38,159,-23),new float3(49,155,-9),new float3(55,148,1),3.2f);
                HornPath(a,o,s, new float3(14,145,-47),new float3(27,148,-35),new float3(39,146,-22),new float3(48,140,-11),new float3(52,134,-3),2.4f);
            }
        }

        private static void HornPath(IStructureAuthoringSession a,int3 o,float s,float3 p0,float3 p1,float3 p2,float3 p3,float3 p4,float r)
        {
            p0.x*=s;p1.x*=s;p2.x*=s;p3.x*=s;p4.x*=s;
            Capsule(a,o,p0,p1,r,r*.78f,Horn); Capsule(a,o,p1,p2,r*.78f,r*.53f,Horn);
            Capsule(a,o,p2,p3,r*.53f,r*.27f,Horn); Capsule(a,o,p3,p4,r*.27f,.12f,Horn);
        }

        private static void CheekSpines(IStructureAuthoringSession a,int3 o,int side)
        {
            float s=side;
            Capsule(a,o,new float3(16*s,145,-57),new float3(31*s,149,-44),2.8f,.15f,Horn);
            Capsule(a,o,new float3(16*s,139,-54),new float3(30*s,138,-40),2.5f,.15f,Horn);
            Capsule(a,o,new float3(14*s,133,-51),new float3(26*s,128,-38),2.2f,.15f,Horn);
            Capsule(a,o,new float3(11*s,127,-47),new float3(21*s,120,-35),1.8f,.12f,Horn);
        }

        private static void Foreleg(IStructureAuthoringSession a,int3 o,int side)
        {
            float s=side; float3 shoulder=new float3(21*s,78,-5), elbow=new float3(34*s,55,-23);
            float3 wrist=new float3(30*s,27,-39), palm=new float3(29*s,10,-55);
            Ellipsoid(a,o,shoulder,new float3(12,14,13),Body); Capsule(a,o,shoulder,elbow,10.5f,7.5f,Body);
            Ellipsoid(a,o,elbow,new float3(8.5f,9,10),Body); Capsule(a,o,elbow,wrist,7.5f,5.3f,Body);
            Ellipsoid(a,o,wrist,new float3(5.5f,6,7),Dark); Capsule(a,o,wrist,palm,5.2f,4.2f,Body);
            Ellipsoid(a,o,new float3(29*s,8,-59),new float3(11,5,13),Body);
            for(int i=0;i<4;i++)
            {
                float spread=(i-1.5f)*4.7f, x=29*s+spread, z=-63-(i==1||i==2?3:0);
                float3 knuckle=new float3(x,8,-61), digit=new float3(x+1.3f*s,5.5f,z-6), claw=new float3(x+2.4f*s,2.5f,z-15);
                Capsule(a,o,knuckle,digit,2.5f,1.5f,Body); Capsule(a,o,digit,claw,1.5f,.12f,Horn);
            }
        }

        private static void RearLeg(IStructureAuthoringSession a,int3 o,int side)
        {
            float s=side; float3 hip=new float3(25*s,39,30), knee=new float3(42*s,27,11), hock=new float3(38*s,12,-13);
            Ellipsoid(a,o,hip,new float3(21,19,24),Body); Capsule(a,o,hip,knee,14,9.5f,Body);
            Ellipsoid(a,o,knee,new float3(12,11,13),Body); Capsule(a,o,knee,hock,8.5f,5.5f,Body);
            Ellipsoid(a,o,new float3(38*s,8,-23),new float3(15,6,19),Body);
            for(int i=0;i<4;i++)
            {
                float spread=(i-1.5f)*5.4f,x=38*s+spread,z=-30-(i==1||i==2?4:0);
                float3 digit=new float3(x+s,5.5f,z-8), claw=new float3(x+2.2f*s,2.5f,z-19);
                Capsule(a,o,new float3(x,8,-27),digit,2.8f,1.6f,Body); Capsule(a,o,digit,claw,1.6f,.12f,Horn);
            }
        }

        private static void AuthorWing(IStructureAuthoringSession a,int3 o,int side)
        {
            float s=side; float3 root=new float3(15*s,88,8), elbow=new float3(50*s,125,18), wrist=new float3(94*s,143,28);
            float3 hook=new float3(104*s,126,35), f0=new float3(102*s,110,34), f1=new float3(97*s,90,33);
            float3 f2=new float3(88*s,71,30), f3=new float3(74*s,54,26), f4=new float3(57*s,43,21), inner=new float3(30*s,54,13);
            Membrane(a,o,root,elbow,inner,1.35f,Dark); Membrane(a,o,elbow,wrist,inner,1.35f,MembraneMat);
            Membrane(a,o,wrist,f0,f1,1.2f,MembraneMat); Membrane(a,o,wrist,f1,f2,1.2f,MembraneMat);
            Membrane(a,o,wrist,f2,f3,1.2f,MembraneMat); Membrane(a,o,wrist,f3,f4,1.2f,MembraneMat); Membrane(a,o,wrist,f4,inner,1.2f,MembraneMat);
            Ellipsoid(a,o,new float3(100*s,100,33),new float3(10,9,5),Empty);
            Ellipsoid(a,o,new float3(94*s,81,31),new float3(11,10,5),Empty);
            Ellipsoid(a,o,new float3(81*s,62,27),new float3(12,10,5),Empty);
            Ellipsoid(a,o,new float3(65*s,49,22),new float3(11,8,5),Empty);
            Capsule(a,o,root,elbow,7.5f,5.5f,Body); Capsule(a,o,elbow,wrist,5.5f,3.3f,Body); Capsule(a,o,wrist,hook,3.3f,.15f,Horn);
            Capsule(a,o,wrist,f0,3.2f,.65f,Dark); Capsule(a,o,wrist,f1,3,.58f,Dark); Capsule(a,o,wrist,f2,2.8f,.52f,Dark);
            Capsule(a,o,wrist,f3,2.5f,.46f,Dark); Capsule(a,o,wrist,f4,2.2f,.35f,Dark);
            for(int i=0;i<10;i++)
            {
                float t=i/9f; float3 p=t<.45f?math.lerp(root,elbow,t/.45f):math.lerp(elbow,wrist,(t-.45f)/.55f);
                Box(a,o,(int3)math.round(p+new float3(-2*s,2,-2)),new int3(5,3,5),Plate);
            }
        }

        private static void Tail(IStructureAuthoringSession a,int3 o)
        {
            float3[] p={new float3(17,40,39),new float3(43,35,54),new float3(69,28,56),new float3(91,20,43),new float3(101,13,20),new float3(98,9,-8),new float3(83,7,-34),new float3(59,6,-55),new float3(30,5,-70),new float3(0,4,-79),new float3(-29,4,-82),new float3(-50,4,-76)};
            float[] r={15,13.5f,12,10,8,6.3f,5,3.9f,3,2.1f,1.2f,.18f};
            for(int i=0;i<p.Length-1;i++) Capsule(a,o,p[i],p[i+1],r[i],r[i+1],Body);
            for(int i=3;i<p.Length-2;i++)
            {
                float size=math.lerp(5.5f,2,(i-3)/7f); float3 b=p[i];
                Capsule(a,o,b+new float3(0,size*.2f,-1),b+new float3(0,size+4,0),size*.45f,.12f,Horn);
            }
        }

        private static void ThroatArmor(IStructureAuthoringSession a,int3 o)
        {
            for(int i=0;i<12;i++)
            {
                int y=128-i*7,z=-39+i*2,half=math.min(14,7+i/2);
                Box(a,o,new int3(-half,y-2,z-4),new int3(half*2+1,5,5),Plate);
                for(int row=0;row<3;row++){int h=math.max(1,half-row*3);Box(a,o,new int3(-h,y-3-row,z-5),new int3(h*2+1,1,5),Plate);}
            }
        }

        private static void DorsalCrest(IStructureAuthoringSession a,int3 o)
        {
            float3[] p={new float3(0,133,-35),new float3(0,119,-25),new float3(0,104,-15),new float3(0,89,-3),new float3(0,76,10),new float3(0,63,23),new float3(0,51,35)};
            for(int i=0;i<p.Length;i++){float h=math.lerp(10,6,i/(float)(p.Length-1));Capsule(a,o,p[i],p[i]+new float3(0,h,4),2.3f,.12f,Horn);}
        }

        private static void ScaleBands(IStructureAuthoringSession a,int3 o)
        {
            for(int side=-1;side<=1;side+=2)
            for(int row=0;row<9;row++)
            {
                int y=91-row*5,z=3+row*3,x=18+row/3;
                for(int j=0;j<4;j++) Box(a,o,new int3(side*x-1,y,z+(j-1)*4),new int3(3,2,3),Dark);
            }
        }

        private static void Patina(IStructureAuthoringSession a,int3 o)
        {
            int3[] p={new int3(-10,151,-53),new int3(12,155,-44),new int3(-5,112,-22),new int3(17,91,3),new int3(-21,70,12),new int3(30,45,35),new int3(67,30,52),new int3(90,16,26)};
            foreach(int3 q in p) Box(a,o,q,new int3(3,2,3),Moss);
        }

        private static void Box(IStructureAuthoringSession a,int3 o,int3 min,int3 size,byte m){int3 max=min+size;for(int y=min.y;y<max.y;y++)for(int z=min.z;z<max.z;z++)for(int x=min.x;x<max.x;x++)a.Set(o.x+x,o.y+y,o.z+z,m);}
        private static void Ellipsoid(IStructureAuthoringSession a,int3 o,float3 c,float3 r,byte m){int3 min=(int3)math.floor(c-r-1),max=(int3)math.ceil(c+r+1);float3 s=math.max(r,new float3(.5f));for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)for(int x=min.x;x<=max.x;x++){float3 q=(new float3(x+.5f,y+.5f,z+.5f)-c)/s;if(math.dot(q,q)<=1)a.Set(o.x+x,o.y+y,o.z+z,m);}}
        private static void Capsule(IStructureAuthoringSession a,int3 o,float3 p0,float3 p1,float r0,float r1,byte m){float mr=math.max(r0,r1);int3 min=(int3)math.floor(math.min(p0,p1)-mr-1),max=(int3)math.ceil(math.max(p0,p1)+mr+1);float3 axis=p1-p0;float l2=math.max(.0001f,math.dot(axis,axis));for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)for(int x=min.x;x<=max.x;x++){float3 p=new float3(x+.5f,y+.5f,z+.5f);float t=math.saturate(math.dot(p-p0,axis)/l2);float3 d=p-(p0+axis*t);float r=math.lerp(r0,r1,t);if(math.dot(d,d)<=r*r)a.Set(o.x+x,o.y+y,o.z+z,m);}}
        private static void Membrane(IStructureAuthoringSession a,int3 o,float3 va,float3 vb,float3 vc,float thick,byte m){float3 n=math.normalizesafe(math.cross(vb-va,vc-va),new float3(0,0,1));int3 min=(int3)math.floor(math.min(va,math.min(vb,vc))-thick-1),max=(int3)math.ceil(math.max(va,math.max(vb,vc))+thick+1);float3 v0=vb-va,v1=vc-va;float d00=math.dot(v0,v0),d01=math.dot(v0,v1),d11=math.dot(v1,v1),den=d00*d11-d01*d01;if(math.abs(den)<.0001f)return;for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)for(int x=min.x;x<=max.x;x++){float3 p=new float3(x+.5f,y+.5f,z+.5f);float dist=math.dot(p-va,n);if(math.abs(dist)>thick)continue;float3 v2=(p-n*dist)-va;float d20=math.dot(v2,v0),d21=math.dot(v2,v1);float v=(d11*d20-d01*d21)/den,w=(d00*d21-d01*d20)/den,u=1-v-w;if(u>=0&&v>=0&&w>=0)a.Set(o.x+x,o.y+y,o.z+z,m);}}
    }
}
