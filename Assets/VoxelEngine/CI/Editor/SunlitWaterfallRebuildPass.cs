using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    internal static class SunlitWaterfallRebuildPass
    {
        private static bool _done;
        private static Transform _root;
        private static Material _grass, _grass2, _rock, _rock2, _stone, _stone2, _moss;
        private static Material _water, _fall, _foam, _bark, _leaf, _leaf2, _cloud, _sky, _roof;
        private static Material _white, _pink, _blue, _yellow;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            GameObject old = GameObject.Find("Sunlit Waterfall Target Scene");
            if (old == null) return;
            Transform oldRoot = old.transform;
            Transform hero = Find(oldRoot, "Hero terrace turf");
            if (hero == null) return;
            Vector3 o = hero.position + new Vector3(0f, 0.06f, 4.35f);

            ReadPalette(oldRoot);
            old.SetActive(false);

            GameObject fresh = new GameObject("Sunlit Waterfall Final Rebuild");
            _root = fresh.transform;

            ConfigureCamera(camera, o);
            BuildSky(o);
            BuildWaterPlane(o);
            BuildForeground(o);
            BuildLeftGardenAndArch(o);
            BuildCascade(o);
            BuildCastle(o);
            BuildTree(o);
            DressScene(o);
        }

        private static void ReadPalette(Transform root)
        {
            _grass = MaterialOf(root, "Hero terrace turf");
            _grass2 = MaterialOf(root, "Middle garden turf");
            _rock = MaterialOf(root, "Hero terrace cliff");
            _rock2 = MaterialOf(root, "Left ruin garden cliff");
            _stone = MaterialOf(root, "Final ashlar block");
            if (_stone == null) _stone = MaterialOf(root, "Rounded ashlar");
            _stone2 = _stone;
            _moss = MaterialOf(root, "Rounded shrub");
            _water = MaterialOf(root, "Front channel");
            _fall = MaterialOf(root, "Waterfall sheet");
            _foam = MaterialOf(root, "Waterfall highlight");
            _bark = MaterialOf(root, "Oak trunk");
            _leaf = MaterialOf(root, "Rounded oak canopy");
            _leaf2 = _leaf;
            _cloud = MaterialOf(root, "Puffy cloud");
            _sky = MaterialOf(root, "Physical blue sky");
            _roof = MaterialOf(root, "Castle spire");
            _white = MaterialOf(root, "Flower petal");
            _pink = _white; _blue = _white; _yellow = _white;

            if (_grass != null) _grass.SetColor("_BaseColor", new Color(0.48f,0.67f,0.19f));
            if (_grass2 != null) _grass2.SetColor("_BaseColor", new Color(0.57f,0.72f,0.24f));
            if (_rock != null) _rock.SetColor("_BaseColor", new Color(0.50f,0.49f,0.40f));
            if (_rock2 != null) _rock2.SetColor("_BaseColor", new Color(0.58f,0.55f,0.44f));
            if (_stone != null) _stone.SetColor("_BaseColor", new Color(0.78f,0.72f,0.60f));
            if (_moss != null) _moss.SetColor("_BaseColor", new Color(0.38f,0.56f,0.14f));
            if (_water != null) { _water.SetColor("_BaseColor", new Color(0.04f,0.70f,0.91f,0.90f)); _water.SetColor("_EmissionColor",new Color(0.02f,0.16f,0.22f)); }
            if (_fall != null) _fall.SetColor("_BaseColor", new Color(0.74f,0.95f,1f,0.90f));
            if (_leaf != null) _leaf.SetColor("_BaseColor", new Color(0.38f,0.57f,0.16f));
        }

        private static void ConfigureCamera(Camera camera, Vector3 o)
        {
            camera.fieldOfView = 35f;
            camera.transform.position = o + new Vector3(0.35f,5.3f,-23.0f);
            camera.transform.LookAt(o + new Vector3(-0.15f,2.85f,5.0f));
            camera.backgroundColor = new Color(0.08f,0.48f,0.88f,1f);
            RenderSettings.fog = false;
            RenderSettings.ambientIntensity = 0.82f;
        }

        private static void BuildSky(Vector3 o)
        {
            if (_sky != null)
            {
                GameObject q = Primitive(PrimitiveType.Quad,"Final blue sky",_sky);
                q.transform.position = o + new Vector3(0f,10f,52f);
                q.transform.localScale = new Vector3(45f,30f,1f);
                q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            Cloud(o + new Vector3(-7.0f,11.0f,32f),1.0f);
            Cloud(o + new Vector3(0f,12.6f,35f),1.4f);
            Cloud(o + new Vector3(9.0f,11.4f,34f),1.18f);
            Cloud(o + new Vector3(4.5f,8.8f,30f),0.68f);
        }

        private static void BuildWaterPlane(Vector3 o)
        {
            if (_water == null) return;
            GameObject water = Primitive(PrimitiveType.Cube,"Final turquoise basin",_water);
            water.transform.position = o + new Vector3(1.5f,-1.55f,-0.8f);
            water.transform.localScale = new Vector3(24f,0.08f,18f);
            water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void BuildForeground(Vector3 o)
        {
            Island(o + new Vector3(0f,-0.15f,-3.4f),10.7f,6.1f,1.65f,_grass,_rock);
            Island(o + new Vector3(-6.6f,-0.75f,-6.7f),4.4f,3.2f,1.25f,_grass2,_rock2);
            Island(o + new Vector3(6.3f,-0.80f,-6.0f),4.2f,3.0f,1.15f,_grass,_rock);

            if (_stone != null)
            {
                for (int i=0;i<7;i++)
                {
                    float x=-3.3f+i*1.08f;
                    if(i==2||i==5) continue;
                    Cube(o+new Vector3(x,0.72f,-6.05f+((i%2)*0.12f)),new Vector3(0.70f,0.46f,0.58f),_stone,(i-3)*2.5f);
                }
                for(int i=0;i<6;i++)
                    Cube(o+new Vector3(-1.7f+i*0.62f,0.73f,-3.55f+(i%2)*0.08f),new Vector3(0.42f,0.10f,0.36f),_stone,(i-2)*1.5f);
            }
        }

        private static void BuildLeftGardenAndArch(Vector3 o)
        {
            Island(o + new Vector3(-6.0f,2.15f,3.4f),7.0f,6.4f,2.1f,_grass2,_rock2);
            Island(o + new Vector3(-1.6f,2.15f,6.5f),7.2f,5.0f,1.75f,_grass,_rock);

            if (_stone == null) return;
            Vector3 c=o+new Vector3(-5.25f,4.5f,2.2f);
            float r=2.35f;
            for(int side=-1;side<=1;side+=2)
                for(int row=0;row<6;row++)
                    Cube(c+new Vector3(side*r,-3.65f+row*0.67f,0f),new Vector3(0.78f,0.62f,0.72f),_stone,row*3f+side);
            for(int i=0;i<=13;i++)
            {
                if(i==10) continue;
                float a=Mathf.Lerp(180f,0f,i/13f)*Mathf.Deg2Rad;
                GameObject b=Cube(c+new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0f),new Vector3(0.80f,0.58f,0.72f),_stone,0f);
                b.transform.rotation=Quaternion.Euler(0f,0f,-a*Mathf.Rad2Deg+90f);
            }
            if(_moss!=null)
            {
                for(int i=0;i<7;i++)
                {
                    float a=Mathf.Lerp(155f,25f,i/6f)*Mathf.Deg2Rad;
                    Blob(c+new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r+0.32f,-0.26f),new Vector3(0.62f,0.20f,0.38f),_moss,"Arch moss");
                }
                for(int strand=0;strand<3;strand++)
                    for(int i=0;i<5+strand;i++)
                        Blob(c+new Vector3(-1.8f+strand*1.35f,2.15f-i*0.34f,-0.32f),new Vector3(0.26f,0.18f,0.10f),_moss,"Ivy leaf");
            }
        }

        private static void BuildCascade(Vector3 o)
        {
            Vector3[] centres={
                o+new Vector3(5.0f,0.55f,0.3f), o+new Vector3(5.9f,2.45f,3.7f),
                o+new Vector3(6.8f,4.30f,7.0f), o+new Vector3(7.5f,6.05f,10.2f)};
            float[] widths={6.1f,5.5f,4.9f,4.4f};
            for(int i=0;i<centres.Length;i++)
                Island(centres[i],widths[i],3.7f-i*0.12f,1.45f,_grass2,_rock2);

            Pool(o+new Vector3(4.7f,1.33f,0.6f),4.0f,1.6f);
            Pool(o+new Vector3(5.7f,3.25f,4.0f),3.5f,1.45f);
            Pool(o+new Vector3(6.7f,5.10f,7.3f),3.0f,1.30f);
            Pool(o+new Vector3(7.4f,6.85f,10.5f),2.6f,1.15f);

            Waterfall(o+new Vector3(5.3f,2.45f,2.25f),o+new Vector3(4.8f,0.68f,1.0f),1.85f);
            Waterfall(o+new Vector3(6.2f,4.30f,5.55f),o+new Vector3(5.8f,2.55f,4.35f),1.55f);
            Waterfall(o+new Vector3(7.0f,6.08f,8.75f),o+new Vector3(6.7f,4.38f,7.65f),1.35f);
        }

        private static void BuildCastle(Vector3 o)
        {
            Vector3 hill=o+new Vector3(7.4f,7.7f,25.5f);
            Island(hill,6.8f,4.8f,2.8f,_grass2,_rock2);
            Vector3 b=hill+new Vector3(0f,1.45f,0f);
            Tower(b,0.68f,4.8f);
            Tower(b+new Vector3(-1.55f,-0.2f,-0.1f),0.48f,3.5f);
            Tower(b+new Vector3(1.50f,-0.1f,0.25f),0.46f,3.4f);
            Tower(b+new Vector3(0.7f,1.0f,0.15f),0.36f,2.8f);
            if(_stone!=null)
            {
                GameObject keep=Cube(b+new Vector3(0f,1.15f,0.40f),new Vector3(3.0f,2.4f,1.8f),_stone,0f);
                keep.name="Final distant castle keep";
            }
            Waterfall(hill+new Vector3(-1.8f,0.5f,-1.0f),hill+new Vector3(-2.0f,-2.2f,-1.3f),0.8f);
        }

        private static void BuildTree(Vector3 o)
        {
            if(_bark==null||_leaf==null)return;
            Vector3 b=o+new Vector3(-8.2f,1.2f,2.8f);
            Capsule(b,b+new Vector3(0.2f,6.7f,0.4f),0.58f,_bark,"Final oak trunk");
            Capsule(b+new Vector3(0f,4.2f,0f),b+new Vector3(-2.8f,7.0f,0.4f),0.31f,_bark,"Oak bough");
            Capsule(b+new Vector3(0.1f,4.6f,0.2f),b+new Vector3(2.8f,6.9f,1.0f),0.30f,_bark,"Oak bough");
            Vector3[] crown={new Vector3(-2.8f,7.0f,0.2f),new Vector3(-1.4f,7.8f,0.4f),new Vector3(0.2f,8.0f,0.7f),new Vector3(1.8f,7.6f,0.8f),new Vector3(3.0f,6.8f,0.6f),new Vector3(-3.4f,6.2f,0.4f),new Vector3(-0.3f,6.5f,-0.3f),new Vector3(1.5f,6.3f,-0.2f)};
            for(int i=0;i<crown.Length;i++) Blob(b+crown[i],new Vector3(2.4f+(i%2)*0.35f,1.65f+(i%3)*0.16f,2.0f+(i%2)*0.25f),_leaf,"Final oak canopy");
        }

        private static void DressScene(Vector3 o)
        {
            if(_moss!=null)
            {
                Cluster(o+new Vector3(-3.9f,0.95f,-4.9f),0.68f,_moss);
                Cluster(o+new Vector3(3.3f,0.88f,-4.5f),0.62f,_moss);
                Cluster(o+new Vector3(5.2f,1.45f,0.2f),0.54f,_moss);
                Cluster(o+new Vector3(6.0f,3.3f,3.6f),0.46f,_moss);
            }
            if(_stone!=null)
            {
                Cube(o+new Vector3(-4.0f,0.88f,-4.3f),new Vector3(0.58f,0.40f,0.50f),_stone,8f);
                Cube(o+new Vector3(3.8f,0.75f,-3.7f),new Vector3(0.52f,0.36f,0.46f),_stone,-7f);
                Cube(o+new Vector3(-3.2f,2.9f,4.5f),new Vector3(0.62f,0.42f,0.52f),_stone,10f);
            }
        }

        private static void Island(Vector3 c,float width,float depth,float height,Material grass,Material rock)
        {
            if(rock==null)return;
            float spread=width*0.23f;
            Blob(c+new Vector3(-spread,-height*0.38f,0f),new Vector3(width*0.38f,height,depth*0.58f),rock,"Rounded island rock");
            Blob(c+new Vector3(0f,-height*0.46f,0.15f),new Vector3(width*0.46f,height*1.10f,depth*0.64f),rock,"Rounded island rock");
            Blob(c+new Vector3(spread,-height*0.36f,-0.05f),new Vector3(width*0.37f,height*0.96f,depth*0.56f),rock,"Rounded island rock");
            if(grass!=null)
            {
                Blob(c+new Vector3(-width*0.16f,height*0.28f,0f),new Vector3(width*0.50f,height*0.22f,depth*0.48f),grass,"Soft turf cap");
                Blob(c+new Vector3(width*0.18f,height*0.26f,0.05f),new Vector3(width*0.46f,height*0.20f,depth*0.46f),grass,"Soft turf cap");
            }
        }

        private static void Pool(Vector3 p,float width,float depth)
        {
            if(_water==null)return;
            GameObject q=Primitive(PrimitiveType.Cylinder,"Cascade turquoise pool",_water);
            q.transform.position=p;q.transform.localScale=new Vector3(width,0.025f,depth);q.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        private static void Waterfall(Vector3 top,Vector3 bottom,float width)
        {
            if(_fall==null)return;
            Mesh m=Ribbon(top,bottom,width,18);GameObject q=MeshObject("Final waterfall",m,_fall);q.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            if(_foam!=null)
            {
                for(int i=0;i<6;i++){float f=(i-2.5f)/2.5f;Blob(bottom+new Vector3(f*width*0.42f,0.04f+(i%2)*0.05f,0f),new Vector3(width*0.20f,0.12f,0.24f),_foam,"Final waterfall foam");}
            }
        }

        private static void Tower(Vector3 p,float radius,float height)
        {
            if(_stone==null)return;
            GameObject t=Primitive(PrimitiveType.Cylinder,"Final distant tower",_stone);t.transform.position=p+Vector3.up*height*0.5f;t.transform.localScale=new Vector3(radius,height*0.5f,radius);
            if(_roof!=null){Mesh cone=Cone(radius*1.2f,height*0.38f,16);GameObject r=MeshObject("Final castle spire",cone,_roof);r.transform.position=p+Vector3.up*(height+height*0.18f);}
        }

        private static void Cloud(Vector3 p,float scale)
        {
            if(_cloud==null)return;
            Vector3[] o={new Vector3(-1.1f,0f,0f),new Vector3(-0.4f,0.35f,0f),new Vector3(0.3f,0.42f,0f),new Vector3(1.0f,0f,0f),new Vector3(0.1f,-0.25f,0f)};
            for(int i=0;i<o.Length;i++){GameObject q=Blob(p+o[i]*scale,new Vector3(1.25f,0.82f,0.8f)*scale,_cloud,"Final cloud");q.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;}
        }

        private static void Cluster(Vector3 p,float scale,Material mat)
        {
            for(int i=0;i<4;i++){float a=i*1.6f;Blob(p+new Vector3(Mathf.Cos(a)*scale*0.28f,(i%2)*0.05f,Mathf.Sin(a)*scale*0.22f),new Vector3(scale*0.62f,scale*0.32f,scale*0.50f),mat,"Garden moss");}
        }

        private static GameObject Blob(Vector3 p,Vector3 scale,Material mat,string name)
        {
            GameObject q=Primitive(PrimitiveType.Sphere,name,mat);q.transform.position=p;q.transform.localScale=scale;return q;
        }

        private static GameObject Cube(Vector3 p,Vector3 scale,Material mat,float yaw)
        {
            GameObject q=Primitive(PrimitiveType.Cube,"Chunky storybook stone",mat);q.transform.position=p;q.transform.localScale=scale;q.transform.rotation=Quaternion.Euler(0f,yaw,0f);return q;
        }

        private static void Capsule(Vector3 a,Vector3 b,float radius,Material mat,string name)
        {
            Vector3 d=b-a;GameObject q=Primitive(PrimitiveType.Capsule,name,mat);q.transform.position=(a+b)*0.5f;q.transform.rotation=Quaternion.FromToRotation(Vector3.up,d.normalized);q.transform.localScale=new Vector3(radius*2f,d.magnitude*0.5f,radius*2f);
        }

        private static GameObject Primitive(PrimitiveType type,string name,Material mat)
        {
            GameObject q=GameObject.CreatePrimitive(type);q.name=name;q.transform.SetParent(_root,false);Collider c=q.GetComponent<Collider>();if(c!=null)Object.DestroyImmediate(c);q.GetComponent<Renderer>().sharedMaterial=mat;return q;
        }

        private static GameObject MeshObject(string name,Mesh mesh,Material mat)
        {
            GameObject q=new GameObject(name);q.transform.SetParent(_root,false);q.AddComponent<MeshFilter>().sharedMesh=mesh;q.AddComponent<MeshRenderer>().sharedMaterial=mat;return q;
        }

        private static Mesh Ribbon(Vector3 top,Vector3 bottom,float width,int segments)
        {
            Vector3 d=bottom-top;Vector3 side=Vector3.Cross(d.normalized,Vector3.up);if(side.sqrMagnitude<0.001f)side=Vector3.right;side.Normalize();
            Vector3[] v=new Vector3[(segments+1)*2];int[] tri=new int[segments*12];
            for(int i=0;i<=segments;i++){float t=i/(float)segments;Vector3 c=Vector3.Lerp(top,bottom,t);float w=width*(0.96f+Mathf.Sin(t*8f)*0.04f);int q=i*2;v[q]=c-side*w*0.5f;v[q+1]=c+side*w*0.5f;if(i<segments){int k=i*12;tri[k]=q;tri[k+1]=q+2;tri[k+2]=q+1;tri[k+3]=q+1;tri[k+4]=q+2;tri[k+5]=q+3;tri[k+6]=q+1;tri[k+7]=q+2;tri[k+8]=q;tri[k+9]=q+3;tri[k+10]=q+2;tri[k+11]=q+1;}}
            Mesh m=new Mesh();m.name="Reusable waterfall ribbon";m.vertices=v;m.triangles=tri;m.RecalculateNormals();m.RecalculateBounds();return m;
        }

        private static Mesh Cone(float radius,float height,int segments)
        {
            Vector3[] v=new Vector3[segments+1];int[] tri=new int[segments*3];v[0]=Vector3.up*height*0.5f;
            for(int i=0;i<segments;i++){float a=i*Mathf.PI*2f/segments;v[i+1]=new Vector3(Mathf.Cos(a)*radius,-height*0.5f,Mathf.Sin(a)*radius);int n=(i+1)%segments;tri[i*3]=0;tri[i*3+1]=i+1;tri[i*3+2]=n+1;}
            Mesh m=new Mesh();m.name="Reusable pointed roof";m.vertices=v;m.triangles=tri;m.RecalculateNormals();m.RecalculateBounds();return m;
        }

        private static Transform Find(Transform root,string name){Transform[] a=root.GetComponentsInChildren<Transform>(true);for(int i=0;i<a.Length;i++)if(a[i].name==name)return a[i];return null;}
        private static Material MaterialOf(Transform root,string name){Renderer[] a=root.GetComponentsInChildren<Renderer>(true);for(int i=0;i<a.Length;i++)if(a[i].gameObject.name==name)return a[i].sharedMaterial;return null;}
    }
}
