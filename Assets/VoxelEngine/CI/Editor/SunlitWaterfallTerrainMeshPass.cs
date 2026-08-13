using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Coherent final environment built from one reusable terrain-patch primitive plus overlays.
    /// The patch is a smooth heightfield whose triangles select turf or exposed-rock material by
    /// slope. Water is a shared plane revealed by carved channels; ruins, waterfalls, foliage and
    /// masonry remain independent procedural overlays.
    /// </summary>
    internal static class SunlitWaterfallTerrainMeshPass
    {
        private static bool _done;
        private static Transform _root;
        private static Material Grass, Rock, Stone, Moss, Water, Fall, Foam, Bark, Leaf, CloudMat, SkyMat, Roof;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            GameObject previous = GameObject.Find("Sunlit Waterfall Final Rebuild");
            if (previous == null) return;
            ReadPalette(previous.transform);
            previous.SetActive(false);

            Vector3 o = camera.transform.position - new Vector3(0.35f,5.3f,-23.0f);
            GameObject scene = new GameObject("Sunlit Coherent Terrain Scene");
            _root = scene.transform;

            camera.fieldOfView = 36f;
            camera.transform.position = o + new Vector3(0.4f,5.6f,-24.2f);
            camera.transform.LookAt(o + new Vector3(-0.2f,2.8f,6.0f));

            BuildSky(o);
            BuildTerrain(o);
            BuildWater(o);
            BuildRuins(o);
            BuildCascadeWater(o);
            BuildCastle(o);
            BuildTree(o);
            Dress(o);
        }

        private static void ReadPalette(Transform root)
        {
            Grass = Mat(root,"Soft turf cap");
            Rock = Mat(root,"Rounded island rock");
            Stone = Mat(root,"Chunky storybook stone");
            Moss = Mat(root,"Garden moss");
            Water = Mat(root,"Final turquoise basin");
            Fall = Mat(root,"Final waterfall");
            Foam = Mat(root,"Final waterfall foam");
            Bark = Mat(root,"Final oak trunk");
            Leaf = Mat(root,"Final oak canopy");
            CloudMat = Mat(root,"Final cloud");
            SkyMat = Mat(root,"Final blue sky");
            Roof = Mat(root,"Final castle spire");

            if (Grass != null) { Grass.SetColor("_BaseColor",new Color(0.51f,0.69f,0.20f)); Grass.SetColor("_EmissionColor",new Color(0.012f,0.018f,0.004f)); }
            if (Rock != null) Rock.SetColor("_BaseColor",new Color(0.56f,0.53f,0.42f));
            if (Stone != null) Stone.SetColor("_BaseColor",new Color(0.80f,0.74f,0.62f));
            if (Moss != null) Moss.SetColor("_BaseColor",new Color(0.40f,0.58f,0.15f));
            if (Leaf != null) Leaf.SetColor("_BaseColor",new Color(0.42f,0.60f,0.17f));
            if (Water != null) { Water.SetColor("_BaseColor",new Color(0.04f,0.69f,0.90f,0.92f)); Water.SetColor("_EmissionColor",new Color(0.02f,0.16f,0.22f)); }
            if (Fall != null) Fall.SetColor("_BaseColor",new Color(0.74f,0.95f,1f,0.90f));
        }

        private static void BuildSky(Vector3 o)
        {
            if (SkyMat != null)
            {
                GameObject sky=Primitive(PrimitiveType.Quad,"Coherent blue sky",SkyMat);
                sky.transform.position=o+new Vector3(0f,10f,55f);sky.transform.localScale=new Vector3(48f,31f,1f);
                sky.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
            Cloud(o+new Vector3(-7.5f,11.2f,34f),1.0f);
            Cloud(o+new Vector3(0.0f,12.8f,37f),1.42f);
            Cloud(o+new Vector3(9.2f,11.5f,35f),1.18f);
            Cloud(o+new Vector3(4.5f,8.8f,31f),0.68f);
        }

        private static void BuildTerrain(Vector3 o)
        {
            Mesh mesh=TerrainMesh(o,125,185,-12.5f,12.5f,-9.0f,29.0f);
            GameObject go=new GameObject("Reusable storybook terrain patch");go.transform.SetParent(_root,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer r=go.AddComponent<MeshRenderer>();r.sharedMaterials=new[]{Grass,Rock};r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
        }

        private static Mesh TerrainMesh(Vector3 o,int nx,int nz,float xmin,float xmax,float zmin,float zmax)
        {
            Vector3[] v=new Vector3[nx*nz];Vector2[] uv=new Vector2[v.Length];
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
            {
                float fx=Mathf.Lerp(xmin,xmax,x/(float)(nx-1));float fz=Mathf.Lerp(zmin,zmax,z/(float)(nz-1));
                float h=Height(fx,fz);int i=x+z*nx;v[i]=o+new Vector3(fx,h,fz);uv[i]=new Vector2(fx*0.11f,fz*0.11f);
            }

            List<int> grass=new List<int>((nx-1)*(nz-1)*6);List<int> rock=new List<int>((nx-1)*(nz-1)*3);
            for(int z=0;z<nz-1;z++)for(int x=0;x<nx-1;x++)
            {
                int a=x+z*nx,b=x+(z+1)*nx,c=(x+1)+z*nx,d=(x+1)+(z+1)*nx;
                AddTri(v,a,b,c,grass,rock);AddTri(v,c,b,d,grass,rock);
            }
            Mesh m=new Mesh();m.name="Reusable slope-material terrain";m.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;m.vertices=v;m.uv=uv;m.subMeshCount=2;m.SetTriangles(grass,0);m.SetTriangles(rock,1);m.RecalculateNormals();m.RecalculateBounds();return m;
        }

        private static void AddTri(Vector3[] v,int a,int b,int c,List<int> grass,List<int> rock)
        {
            Vector3 n=Vector3.Cross(v[b]-v[a],v[c]-v[a]).normalized;List<int> dst=n.y>0.80f?grass:rock;dst.Add(a);dst.Add(b);dst.Add(c);
        }

        private static float Height(float x,float z)
        {
            float h=-0.18f + Mathf.Sin(x*0.34f+z*0.11f)*0.035f;
            h=Mathf.Max(h,Plateau(x,z,0f,-3.0f,5.5f,3.7f,0.62f));
            h=Mathf.Max(h,Plateau(x,z,-6.5f,-6.4f,2.5f,2.1f,-0.05f));
            h=Mathf.Max(h,Plateau(x,z,6.2f,-5.7f,2.4f,2.0f,-0.08f));
            h=Mathf.Max(h,Plateau(x,z,-6.0f,3.4f,4.0f,4.0f,2.55f));
            h=Mathf.Max(h,Plateau(x,z,-1.4f,6.5f,4.5f,3.2f,2.35f));
            h=Mathf.Max(h,Plateau(x,z,4.9f,0.8f,3.2f,2.4f,0.80f));
            h=Mathf.Max(h,Plateau(x,z,5.9f,4.5f,3.0f,2.3f,2.55f));
            h=Mathf.Max(h,Plateau(x,z,6.8f,8.1f,2.8f,2.25f,4.30f));
            h=Mathf.Max(h,Plateau(x,z,7.5f,11.7f,2.6f,2.15f,5.95f));
            h=Mathf.Max(h,Plateau(x,z,7.4f,23.2f,3.6f,3.5f,7.15f));
            h=Mathf.Max(h,Plateau(x,z,-6.5f,16.0f,4.0f,4.0f,1.55f));

            h=Carve(h,x,z,1.3f,-6.3f,8.7f,2.15f,-0.92f);
            h=Carve(h,x,z,3.5f,-1.0f,3.6f,2.1f,-0.80f);
            h=Carve(h,x,z,4.8f,2.7f,2.2f,1.3f,-0.72f);
            h=Carve(h,x,z,5.7f,6.1f,1.9f,1.15f,1.45f);
            h=Carve(h,x,z,6.6f,9.6f,1.7f,1.05f,3.25f);
            return h;
        }

        private static float Plateau(float x,float z,float cx,float cz,float rx,float rz,float top)
        {
            float d=Mathf.Sqrt(((x-cx)*(x-cx))/(rx*rx)+((z-cz)*(z-cz))/(rz*rz));
            float w=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(0.72f,1.10f,d));
            return Mathf.Lerp(-0.20f,top,w);
        }

        private static float Carve(float h,float x,float z,float cx,float cz,float rx,float rz,float bottom)
        {
            float d=Mathf.Sqrt(((x-cx)*(x-cx))/(rx*rx)+((z-cz)*(z-cz))/(rz*rz));
            float w=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(0.58f,1.0f,d));
            return Mathf.Lerp(h,Mathf.Min(h,bottom),w);
        }

        private static void BuildWater(Vector3 o)
        {
            if(Water==null)return;
            GameObject q=Primitive(PrimitiveType.Cube,"Shared turquoise channel plane",Water);
            q.transform.position=o+new Vector3(0f,-0.57f,8.5f);q.transform.localScale=new Vector3(27f,0.035f,38f);q.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        private static void BuildRuins(Vector3 o)
        {
            if(Stone==null)return;
            Arch(o+new Vector3(-5.55f,4.45f,2.8f),2.35f,0.78f);
            Arch(o+new Vector3(-6.6f,1.35f,-1.7f),1.25f,0.55f);
            for(int i=0;i<6;i++) if(i!=2&&i!=5) Cube(o+new Vector3(-3.0f+i*1.15f,0.85f,-5.4f+(i%2)*0.08f),new Vector3(0.68f,0.44f,0.55f),Stone,(i-2)*2f);
            if(Moss!=null)
            {
                Cluster(o+new Vector3(-6.8f,6.8f,2.5f),0.62f,Moss);
                Cluster(o+new Vector3(-4.8f,6.5f,2.5f),0.54f,Moss);
            }
        }

        private static void Arch(Vector3 c,float r,float block)
        {
            for(int side=-1;side<=1;side+=2)for(int row=0;row<6;row++)
                Cube(c+new Vector3(side*r,-3.45f+row*0.64f,0f),new Vector3(block,0.58f,block*0.88f),Stone,0f);
            for(int i=0;i<=13;i++)
            {
                if(i==10)continue;float a=Mathf.Lerp(180f,0f,i/13f)*Mathf.Deg2Rad;
                GameObject q=Cube(c+new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0f),new Vector3(block,0.56f,block*0.88f),Stone,0f);
                q.transform.rotation=Quaternion.Euler(0f,0f,-a*Mathf.Rad2Deg+90f);
            }
        }

        private static void BuildCascadeWater(Vector3 o)
        {
            Pool(o+new Vector3(4.7f,0.90f,0.8f),2.3f,1.0f);
            Pool(o+new Vector3(5.8f,2.66f,4.5f),2.0f,0.9f);
            Pool(o+new Vector3(6.7f,4.40f,8.0f),1.8f,0.82f);
            Pool(o+new Vector3(7.4f,6.05f,11.5f),1.55f,0.75f);
            Waterfall(o+new Vector3(5.2f,2.55f,2.7f),o+new Vector3(4.7f,0.85f,1.5f),1.65f);
            Waterfall(o+new Vector3(6.1f,4.30f,6.2f),o+new Vector3(5.8f,2.65f,5.0f),1.42f);
            Waterfall(o+new Vector3(6.9f,5.96f,9.7f),o+new Vector3(6.65f,4.37f,8.55f),1.22f);
        }

        private static void Pool(Vector3 p,float rx,float rz)
        {
            if(Water==null)return;GameObject q=Primitive(PrimitiveType.Cylinder,"Terrace water pool",Water);q.transform.position=p;q.transform.localScale=new Vector3(rx,0.025f,rz);q.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        private static void Waterfall(Vector3 top,Vector3 bottom,float width)
        {
            if(Fall==null)return;Mesh m=Ribbon(top,bottom,width,18);GameObject q=MeshObject("Coherent waterfall",m,Fall);q.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            if(Foam!=null)for(int i=0;i<5;i++){float f=(i-2)/2f;Blob(bottom+new Vector3(f*width*0.38f,0.04f+(i%2)*0.04f,0f),new Vector3(width*0.20f,0.11f,0.22f),Foam,"Waterfall foam");}
        }

        private static void BuildCastle(Vector3 o)
        {
            if(Stone==null)return;Vector3 b=o+new Vector3(7.4f,7.25f,23.2f);
            Tower(b,0.62f,4.5f);Tower(b+new Vector3(-1.4f,-0.2f,-0.1f),0.44f,3.3f);Tower(b+new Vector3(1.4f,-0.1f,0.2f),0.42f,3.2f);Tower(b+new Vector3(0.65f,0.9f,0.1f),0.32f,2.7f);
            GameObject keep=Cube(b+new Vector3(0f,1.0f,0.35f),new Vector3(2.8f,2.2f,1.6f),Stone,0f);keep.name="Coherent distant castle";
            Waterfall(o+new Vector3(5.8f,7.0f,21.6f),o+new Vector3(5.6f,4.8f,21.2f),0.70f);
        }

        private static void Tower(Vector3 p,float radius,float height)
        {
            GameObject q=Primitive(PrimitiveType.Cylinder,"Pale castle tower",Stone);q.transform.position=p+Vector3.up*height*0.5f;q.transform.localScale=new Vector3(radius,height*0.5f,radius);
            if(Roof!=null){Mesh cone=Cone(radius*1.22f,height*0.36f,16);GameObject roof=MeshObject("Pointed castle roof",cone,Roof);roof.transform.position=p+Vector3.up*(height+height*0.17f);}
        }

        private static void BuildTree(Vector3 o)
        {
            if(Bark==null||Leaf==null)return;Vector3 b=o+new Vector3(-8.7f,2.0f,3.1f);
            Capsule(b,b+new Vector3(0.2f,6.3f,0.3f),0.55f,Bark,"Main oak trunk");
            Capsule(b+new Vector3(0f,4.0f,0f),b+new Vector3(-2.7f,6.8f,0.5f),0.30f,Bark,"Oak branch");
            Capsule(b+new Vector3(0.1f,4.4f,0.2f),b+new Vector3(2.7f,6.6f,1.0f),0.29f,Bark,"Oak branch");
            Vector3[] crown={new Vector3(-2.8f,6.8f,0.2f),new Vector3(-1.4f,7.6f,0.4f),new Vector3(0.2f,7.8f,0.7f),new Vector3(1.8f,7.4f,0.8f),new Vector3(3.0f,6.6f,0.7f),new Vector3(-3.5f,6.0f,0.5f),new Vector3(-0.3f,6.2f,-0.3f),new Vector3(1.5f,6.1f,-0.2f)};
            for(int i=0;i<crown.Length;i++)Blob(b+crown[i],new Vector3(2.3f+(i%2)*0.32f,1.55f+(i%3)*0.14f,1.95f+(i%2)*0.24f),Leaf,"Rounded storybook canopy");
        }

        private static void Dress(Vector3 o)
        {
            if(Moss!=null){Cluster(o+new Vector3(-3.8f,0.82f,-3.8f),0.58f,Moss);Cluster(o+new Vector3(3.2f,0.72f,-3.2f),0.54f,Moss);Cluster(o+new Vector3(5.2f,1.35f,0.5f),0.46f,Moss);Cluster(o+new Vector3(-6.0f,3.0f,3.0f),0.52f,Moss);}
            if(Stone!=null){Cube(o+new Vector3(-4.0f,0.72f,-3.0f),new Vector3(0.55f,0.38f,0.48f),Stone,7f);Cube(o+new Vector3(3.7f,0.64f,-2.7f),new Vector3(0.50f,0.34f,0.44f),Stone,-8f);Cube(o+new Vector3(-3.0f,2.55f,5.2f),new Vector3(0.58f,0.40f,0.50f),Stone,9f);}
        }

        private static void Cloud(Vector3 p,float scale)
        {
            if(CloudMat==null)return;Vector3[] a={new Vector3(-1.1f,0f,0f),new Vector3(-0.4f,0.35f,0f),new Vector3(0.3f,0.42f,0f),new Vector3(1.0f,0f,0f),new Vector3(0.1f,-0.25f,0f)};
            for(int i=0;i<a.Length;i++){GameObject q=Blob(p+a[i]*scale,new Vector3(1.25f,0.82f,0.80f)*scale,CloudMat,"Soft cloud");q.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;}
        }

        private static void Cluster(Vector3 p,float scale,Material mat){for(int i=0;i<4;i++){float a=i*1.6f;Blob(p+new Vector3(Mathf.Cos(a)*scale*0.28f,(i%2)*0.05f,Mathf.Sin(a)*scale*0.22f),new Vector3(scale*0.62f,scale*0.32f,scale*0.50f),mat,"Moss cluster");}}
        private static GameObject Blob(Vector3 p,Vector3 scale,Material mat,string name){GameObject q=Primitive(PrimitiveType.Sphere,name,mat);q.transform.position=p;q.transform.localScale=scale;return q;}
        private static GameObject Cube(Vector3 p,Vector3 scale,Material mat,float yaw){GameObject q=Primitive(PrimitiveType.Cube,"Warm chunky masonry",mat);q.transform.position=p;q.transform.localScale=scale;q.transform.rotation=Quaternion.Euler(0f,yaw,0f);return q;}
        private static void Capsule(Vector3 a,Vector3 b,float radius,Material mat,string name){Vector3 d=b-a;GameObject q=Primitive(PrimitiveType.Capsule,name,mat);q.transform.position=(a+b)*0.5f;q.transform.rotation=Quaternion.FromToRotation(Vector3.up,d.normalized);q.transform.localScale=new Vector3(radius*2f,d.magnitude*0.5f,radius*2f);}
        private static GameObject Primitive(PrimitiveType type,string name,Material mat){GameObject q=GameObject.CreatePrimitive(type);q.name=name;q.transform.SetParent(_root,false);Collider c=q.GetComponent<Collider>();if(c!=null)Object.DestroyImmediate(c);q.GetComponent<Renderer>().sharedMaterial=mat;return q;}
        private static GameObject MeshObject(string name,Mesh mesh,Material mat){GameObject q=new GameObject(name);q.transform.SetParent(_root,false);q.AddComponent<MeshFilter>().sharedMesh=mesh;q.AddComponent<MeshRenderer>().sharedMaterial=mat;return q;}

        private static Mesh Ribbon(Vector3 top,Vector3 bottom,float width,int segments)
        {
            Vector3 d=bottom-top;Vector3 side=Vector3.Cross(d.normalized,Vector3.up);if(side.sqrMagnitude<0.001f)side=Vector3.right;side.Normalize();Vector3[] v=new Vector3[(segments+1)*2];int[] tri=new int[segments*12];
            for(int i=0;i<=segments;i++){float t=i/(float)segments;Vector3 c=Vector3.Lerp(top,bottom,t);float w=width*(0.96f+Mathf.Sin(t*8f)*0.04f);int q=i*2;v[q]=c-side*w*0.5f;v[q+1]=c+side*w*0.5f;if(i<segments){int k=i*12;tri[k]=q;tri[k+1]=q+2;tri[k+2]=q+1;tri[k+3]=q+1;tri[k+4]=q+2;tri[k+5]=q+3;tri[k+6]=q+1;tri[k+7]=q+2;tri[k+8]=q;tri[k+9]=q+3;tri[k+10]=q+2;tri[k+11]=q+1;}}
            Mesh m=new Mesh();m.name="Waterfall ribbon";m.vertices=v;m.triangles=tri;m.RecalculateNormals();m.RecalculateBounds();return m;
        }

        private static Mesh Cone(float radius,float height,int segments){Vector3[] v=new Vector3[segments+1];int[] tri=new int[segments*3];v[0]=Vector3.up*height*0.5f;for(int i=0;i<segments;i++){float a=i*Mathf.PI*2f/segments;v[i+1]=new Vector3(Mathf.Cos(a)*radius,-height*0.5f,Mathf.Sin(a)*radius);int n=(i+1)%segments;tri[i*3]=0;tri[i*3+1]=i+1;tri[i*3+2]=n+1;}Mesh m=new Mesh();m.name="Pointed roof";m.vertices=v;m.triangles=tri;m.RecalculateNormals();m.RecalculateBounds();return m;}
        private static Material Mat(Transform root,string name){Renderer[] a=root.GetComponentsInChildren<Renderer>(true);for(int i=0;i<a.Length;i++)if(a[i].gameObject.name==name)return a[i].sharedMaterial;return null;}
    }
}
