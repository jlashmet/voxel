using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// V13 removes the last obviously wrong material cue from the V10-V12 lineage: brown Wood
    /// wing membranes. The reference uses cool blue-gray membranes framed by darker/slate spars.
    /// This pass keeps the existing V11 wing geometry but remaps membrane voxels into the dragon's
    /// cool palette and reinforces the principal rib hierarchy in Slate.
    /// </summary>
    public static class DragonStatueConceptV13WingPaletteAuthoring
    {
        private const byte Wood = GameMaterialIds.Wood;
        private const byte Membrane = GameMaterialIds.DarkStone;
        private const byte Rib = GameMaterialIds.Slate;

        public static void Author(IStructureAuthoringSession a, int3 o)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            DragonStatueConceptV12PaletteAndScaleAuthoring.Author(a, o);
            RemapWingMembranes(a, o);
            ReinforceWingRibs(a, o, 1, 1.00f, 8f);
            ReinforceWingRibs(a, o, -1, .84f, -14f);
        }

        private static void RemapWingMembranes(IStructureAuthoringSession a, int3 o)
        {
            RemapWood(a, o, new int3(12, 48, -10), new int3(123, 110, 58));
            RemapWood(a, o, new int3(-135, 38, -35), new int3(123, 120, 70));
        }

        private static void RemapWood(IStructureAuthoringSession a, int3 o, int3 min, int3 size)
        {
            int3 max = min + size;
            for (int y = min.y; y < max.y; y++)
            for (int z = min.z; z < max.z; z++)
            for (int x = min.x; x < max.x; x++)
            {
                int wx=o.x+x, wy=o.y+y, wz=o.z+z;
                if (a.Get(wx,wy,wz) == Wood)
                    a.Set(wx,wy,wz,Membrane);
            }
        }

        private static void ReinforceWingRibs(IStructureAuthoringSession a,int3 o,int side,float scale,float zOffset)
        {
            float3 root = WingPoint(side,15*scale,62*scale,zOffset);
            float3 elbow = WingPoint(side,34*scale,84*scale,zOffset);
            float3 wrist = WingPoint(side,56*scale,109*scale,zOffset);
            float3 arch = WingPoint(side,82*scale,134*scale,zOffset);
            float3 crown = WingPoint(side,106*scale,150*scale,zOffset);
            float3 hook = WingPoint(side,124*scale,148*scale,zOffset);

            HardSegment(a,o,root,elbow,6.3f*scale,5.0f*scale,Rib);
            HardSegment(a,o,elbow,wrist,5.0f*scale,3.7f*scale,Rib);
            HardSegment(a,o,wrist,arch,3.7f*scale,2.6f*scale,Rib);
            HardSegment(a,o,arch,crown,2.6f*scale,1.5f*scale,Rib);
            HardSegment(a,o,crown,hook,1.5f*scale,.25f,Rib);

            float2[] fingers =
            {
                new float2(120,121), new float2(111,85), new float2(95,67), new float2(73,55)
            };
            for(int i=0;i<fingers.Length;i++)
            {
                float3 end=WingPoint(side,fingers[i].x*scale,fingers[i].y*scale,zOffset);
                float3 fingerRoot=i<2?math.lerp(wrist,arch,.18f+i*.18f):wrist;
                float3 bend=math.lerp(fingerRoot,end,.50f);
                bend+=new float3((4.0f-i*.55f)*scale*side,3.0f-i*.35f,2.6f*scale);
                float r0=math.lerp(2.45f,1.45f,i/3f)*scale;
                HardSegment(a,o,fingerRoot,bend,r0,r0*.58f,Rib);
                HardSegment(a,o,bend,end,r0*.58f,.22f,Rib);
            }
        }

        private static float3 WingPoint(int side,float u,float y,float zOffset)
        {
            float nu=math.saturate((u-14f)/112f);
            float z=zOffset+.13f*u+.045f*(y-62f)+6.0f*math.sin(math.PI*nu);
            return new float3(side*u,y,z);
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
