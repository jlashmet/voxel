using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// V12 fixes a material-language error visible in every prior production capture: Stone was
    /// being used both for body scales and for bone accents, making the sculpt read as gray skin
    /// covered in unrelated white rocks. The reference keeps body scales in the blue/slate family
    /// and reserves warm bone for horns, teeth and claws. V12 remaps the inherited scale relief to
    /// the body palette, then deliberately repaints only true bone accents.
    /// </summary>
    public static class DragonStatueConceptV12PaletteAndScaleAuthoring
    {
        private const byte Body = GameMaterialIds.Slate;
        private const byte LegacyScale = GameMaterialIds.Stone;
        private const byte Bone = GameMaterialIds.Dirt;

        public static void Author(IStructureAuthoringSession a, int3 o)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            DragonStatueConceptV11ReferenceSilhouetteAuthoring.Author(a, o);
            RemapScaleReliefToBody(a, o);
            AuthorBoneAccents(a, o);
            AuthorDenseBodyScaleRelief(a, o);
        }

        private static void RemapScaleReliefToBody(IStructureAuthoringSession a, int3 o)
        {
            // Target only regions where V10/V11 authored scale/horn/claw material. Keeping this
            // bounded avoids a 10M-voxel full-object scan while still eliminating every visible
            // white-rock patch from the production silhouette.
            RemapBox(a, o, new int3(-38, 52, -108), new int3(76, 108, 112));   // head + neck
            RemapBox(a, o, new int3(-62, 0, -78), new int3(124, 78, 138));     // body + limbs
            RemapBox(a, o, new int3(-135, 36, -36), new int3(270, 124, 88));  // wings
            RemapBox(a, o, new int3(8, 0, -70), new int3(104, 42, 126));      // tail
        }

        private static void RemapBox(IStructureAuthoringSession a, int3 o, int3 min, int3 size)
        {
            int3 max = min + size;
            for (int y = min.y; y < max.y; y++)
            for (int z = min.z; z < max.z; z++)
            for (int x = min.x; x < max.x; x++)
            {
                int wx = o.x + x, wy = o.y + y, wz = o.z + z;
                if (a.Get(wx, wy, wz) == LegacyScale)
                    a.Set(wx, wy, wz, Body);
            }
        }

        private static void AuthorBoneAccents(IStructureAuthoringSession a, int3 o)
        {
            // Main horns: warm material begins only after the broad body-colored roots.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                float3 p1 = new float3(12.5f*s, 140, -51);
                float3 p2 = new float3(16.5f*s, 145, -40);
                float3 p3 = new float3(18.0f*s, 146, -28);
                float3 p4 = new float3(16.0f*s, 152, -17);
                HardSegment(a, o, p1, p2, 2.7f, 2.1f, Bone);
                HardSegment(a, o, p2, p3, 2.1f, 1.15f, Bone);
                HardSegment(a, o, p3, p4, 1.15f, .24f, Bone);

                // Temple and cheek fins get warm tips, not full white blades.
                PaintTip(a, o, new float3(10*s,132,-61), new float3(24*s,136,-48), 1.45f);
                PaintTip(a, o, new float3(13*s,125,-65), new float3(29*s,128,-52), 1.20f);
                PaintTip(a, o, new float3(14*s,117,-66), new float3(27*s,115,-53), 1.05f);
                PaintTip(a, o, new float3(11*s,109,-68), new float3(20*s,103,-57), .85f);
            }

            // Teeth.
            int[] toothZ = { -77, -84, -91 };
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < toothZ.Length; i++)
            {
                float x = side * (3.6f + i*.9f);
                HardSegment(a, o,
                    new float3(x,113.5f,toothZ[i]),
                    new float3(x+.15f*side,109.8f,toothZ[i]-1f),
                    1.0f,.22f,Bone);
            }

            // Foreclaws.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                float[] offsets = { -5.4f, -1.8f, 1.8f, 5.4f };
                for (int i = 0; i < offsets.Length; i++)
                {
                    float extra = (i == 1 || i == 2) ? 2.3f : 0f;
                    float x = 24*s + offsets[i]*s;
                    HardSegment(a,o,
                        new float3(x+.8f*s,4.3f,-65-extra),
                        new float3(x+2.0f*s,1.5f,-72-extra),
                        1.35f,.22f,Bone);
                }

                // Rear claws.
                for (int i = 0; i < offsets.Length; i++)
                {
                    float extra = (i == 1 || i == 2) ? 1.8f : 0f;
                    float x = 31*s + offsets[i]*s;
                    HardSegment(a,o,
                        new float3(x+.6f*s,3.8f,-34-extra),
                        new float3(x+1.8f*s,1.4f,-41-extra),
                        1.25f,.21f,Bone);
                }
            }

            // Bone tips on the dorsal/tail crest echo the reference without turning every scale pale.
            float3[] crest =
            {
                new float3(0,113,-56),new float3(0,102,-47),new float3(0,91,-38),
                new float3(0,80,-29),new float3(0,69,-19),new float3(0,58,-9)
            };
            for (int i=0;i<crest.Length;i++)
            {
                float t=i/(float)(crest.Length-1);
                float3 tip=crest[i]+new float3(0,math.lerp(7.5f,5.0f,t),4.5f);
                HardSegment(a,o,math.lerp(crest[i],tip,.58f),tip,math.lerp(.95f,.65f,t),.20f,Bone);
            }
        }

        private static void PaintTip(IStructureAuthoringSession a,int3 o,float3 root,float3 tip,float radius)
        {
            HardSegment(a,o,math.lerp(root,tip,.62f),tip,radius,.20f,Bone);
        }

        private static void AuthorDenseBodyScaleRelief(IStructureAuthoringSession a, int3 o)
        {
            // Same-material relief is what the reference needs: visible overlapping geometry without
            // the polka-dot color breakup. Staggered rows follow the chest and haunch muscle flow.
            for (int side=-1; side<=1; side+=2)
            {
                float s=side;
                for (int row=0; row<5; row++)
                {
                    float y=35f+row*6.0f;
                    float z=8f-row*5.0f;
                    float lateral=18f+row*1.2f;
                    for (int col=0; col<3; col++)
                    {
                        float x=(lateral+col*5.0f)*s;
                        ShieldPlate(a,o,new float3(x,y-(col%2)*2.2f,z+col*2.6f),3.1f,3.8f,1.5f,Body);
                    }
                }

                // Upper-arm scale cadence.
                for (int row=0; row<4; row++)
                {
                    float t=row/3f;
                    float3 c=math.lerp(new float3(19*s,58,-25),new float3(27*s,43,-34),t);
                    ShieldPlate(a,o,c,3.0f,3.7f,1.5f,Body);
                }
            }

            // Tail top gets a continuous shingled ridge rather than isolated pale stones.
            float3[] tail =
            {
                new float3(31,25,44),new float3(52,18,47),new float3(72,12,42),
                new float3(89,7,28),new float3(98,4,9),new float3(96,3,-11),
                new float3(85,3,-29),new float3(68,3,-43),new float3(48,2.7f,-53)
            };
            float[] rise = {7.5f,6.7f,5.8f,4.8f,4.0f,3.4f,2.8f,2.2f,1.7f};
            for(int i=0;i<tail.Length;i++)
                ShieldPlate(a,o,tail[i]+new float3(0,rise[i],-1),math.lerp(3.7f,1.8f,i/8f),math.lerp(3.8f,2.2f,i/8f),1.5f,Body);
        }

        private static void ShieldPlate(IStructureAuthoringSession a,int3 o,float3 c,float halfWidth,float height,float depth,byte material)
        {
            int top=(int)math.ceil(c.y+height*.45f),bottom=(int)math.floor(c.y-height*.55f),front=(int)math.floor(c.z-depth),back=(int)math.ceil(c.z+depth*.25f),rows=math.max(1,top-bottom);
            for(int y=bottom;y<=top;y++)
            {
                float fromTop=(top-y)/(float)rows;
                float widthFactor=fromTop<.45f?math.lerp(.78f,1f,fromTop/.45f):math.lerp(1f,.18f,(fromTop-.45f)/.55f);
                int hw=math.max(1,(int)math.round(halfWidth*widthFactor));
                for(int z=front;z<=back;z++)
                for(int x=(int)math.round(c.x)-hw;x<=(int)math.round(c.x)+hw;x++) a.Set(o.x+x,o.y+y,o.z+z,material);
            }
        }

        private static void HardSegment(IStructureAuthoringSession a,int3 o,float3 p0,float3 p1,float r0,float r1,byte material)
        {
            float len=math.length(p1-p0); int steps=math.max(1,(int)math.ceil(len*1.7f));
            for(int i=0;i<=steps;i++)
            {
                float t=i/(float)steps; float3 p=math.lerp(p0,p1,t); float r=math.lerp(r0,r1,t);
                ChamferedBox(a,o,p,new float3(r,r*.78f,r*.78f),material);
            }
        }

        private static void ChamferedBox(IStructureAuthoringSession a,int3 o,float3 c,float3 half,byte material)
        {
            int3 min=(int3)math.floor(c-half),max=(int3)math.ceil(c+half);
            for(int y=min.y;y<=max.y;y++)
            for(int z=min.z;z<=max.z;z++)
            for(int x=min.x;x<=max.x;x++)
            {
                float ax=math.abs(x+.5f-c.x)/math.max(.5f,half.x),ay=math.abs(y+.5f-c.y)/math.max(.5f,half.y),az=math.abs(z+.5f-c.z)/math.max(.5f,half.z);
                if(ax<=1f && ay<=1f && az<=1f && ax+ay+az<=2.15f) a.Set(o.x+x,o.y+y,o.z+z,material);
            }
        }
    }
}
