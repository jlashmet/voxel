using UnityEngine;

namespace VoxelEngine.CI
{
    internal static class SunlitWaterfallMatchPass
    {
        private static bool _applied;

        public static void Apply(Camera camera)
        {
            if (_applied || camera == null) return;
            _applied = true;

            GameObject root = GameObject.Find("Sunlit Waterfall Target Scene");
            if (root == null) return;
            Vector3 origin = Origin(root.transform);

            TuneTerraces(root.transform, origin);
            TuneArchAndMasonry(root.transform, origin);
            TuneCastle(root.transform, origin);
            TuneMaterials(root.transform);
            TuneCamera(camera, origin);
        }

        private static void TuneTerraces(Transform root, Vector3 o)
        {
            ScaleMove(root,"Hero terrace",new Vector3(0.78f,0.76f,0.78f),new Vector3(0f,-0.28f,-0.65f));
            ScaleMove(root,"Left ruin garden",new Vector3(0.82f,0.72f,0.84f),new Vector3(-0.25f,-0.10f,0.85f));
            ScaleMove(root,"Middle garden",new Vector3(0.76f,0.72f,0.80f),new Vector3(-0.15f,0.15f,1.25f));

            ScaleMove(root,"Cascade zero",new Vector3(0.78f,0.76f,0.82f),new Vector3(0.65f,-0.10f,0.80f));
            ScaleMove(root,"Cascade one",new Vector3(0.78f,0.76f,0.82f),new Vector3(0.70f,-0.10f,1.00f));
            ScaleMove(root,"Cascade two",new Vector3(0.77f,0.76f,0.82f),new Vector3(0.75f,-0.05f,1.15f));
            ScaleMove(root,"Cascade three",new Vector3(0.76f,0.76f,0.82f),new Vector3(0.80f,0f,1.30f));

            ScaleMove(root,"Front left island",new Vector3(0.86f,0.80f,0.86f),new Vector3(-0.20f,-0.12f,-0.20f));
            ScaleMove(root,"Front right island",new Vector3(0.86f,0.80f,0.86f),new Vector3(0.15f,-0.10f,-0.15f));
        }

        private static void ScaleMove(Transform root, string prefix, Vector3 scale, Vector3 delta)
        {
            Transform[] all=root.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)
            {
                Transform t=all[i];
                if(!t.name.StartsWith(prefix)) continue;
                t.localScale=Vector3.Scale(t.localScale,scale);
                t.position+=delta;
            }
        }

        private static void TuneArchAndMasonry(Transform root, Vector3 o)
        {
            GameObject temp=GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cube=temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);

            MeshFilter[] filters=root.GetComponentsInChildren<MeshFilter>(true);
            for(int i=0;i<filters.Length;i++)
            {
                MeshFilter mf=filters[i];
                if(mf.gameObject.name!="Rounded ashlar") continue;
                mf.sharedMesh=cube;
                Vector3 s=mf.transform.localScale;
                mf.transform.localScale=new Vector3(s.x,s.y*1.65f,s.z);

                Vector3 p=mf.transform.position;
                bool arch=p.x<o.x-2.6f && p.z>o.z-4.4f && p.z<o.z+1.0f && p.y>o.y-1.0f;
                if(arch)
                    mf.transform.position=p+new Vector3(0.75f,0.25f,-1.55f);
            }
        }

        private static void TuneCastle(Transform root, Vector3 o)
        {
            Vector3 oldAnchor=o+new Vector3(8.3f,7.2f,29.0f);
            Vector3 newAnchor=o+new Vector3(8.0f,6.9f,34.0f);
            Transform[] all=root.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)
            {
                Transform t=all[i];
                string n=t.name;
                if(!(n.Contains("Distant castle hill")||n.Contains("Distant tower")||n.Contains("Castle spire")||n.Contains("Distant castle keep"))) continue;
                Vector3 offset=t.position-oldAnchor;
                t.position=newAnchor+offset*0.73f;
                t.localScale*=0.73f;
            }
        }

        private static void TuneMaterials(Transform root)
        {
            Texture2D softNoise=NoiseTexture(96,0.16f,17);
            Texture2D stoneNoise=NoiseTexture(96,0.12f,41);

            Renderer[] renderers=root.GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<renderers.Length;i++)
            {
                Renderer r=renderers[i];
                Material m=r.sharedMaterial;
                if(m==null) continue;
                string n=m.name;

                if(n.Contains("grass")||n.Contains("Moss")||n.Contains("moss"))
                {
                    m.SetTexture("_MainTex",softNoise);
                    m.SetTextureScale("_MainTex",new Vector2(1.5f,1.5f));
                }
                else if(n.Contains("stone")||n.Contains("Ashlar")||n.Contains("cliff")||n.Contains("Cliff"))
                {
                    m.SetTexture("_MainTex",stoneNoise);
                    m.SetTextureScale("_MainTex",new Vector2(1.25f,1.25f));
                }

                if(n=="Sunlit grass") m.SetColor("_BaseColor",new Color(0.48f,0.65f,0.19f));
                if(n=="Sunlit grass light") m.SetColor("_BaseColor",new Color(0.57f,0.72f,0.24f));
                if(n=="Garden cliff") m.SetColor("_BaseColor",new Color(0.47f,0.47f,0.37f));
                if(n=="Warm garden cliff") m.SetColor("_BaseColor",new Color(0.56f,0.53f,0.42f));
                if(n=="Warm ruin stone") m.SetColor("_BaseColor",new Color(0.72f,0.66f,0.54f));
                if(n=="Sunlit ruin stone") m.SetColor("_BaseColor",new Color(0.84f,0.78f,0.65f));

                MeshFilter mf=r.GetComponent<MeshFilter>();
                if(mf!=null && mf.sharedMesh!=null && (r.gameObject.name.Contains(" turf")||r.gameObject.name.Contains(" cliff")))
                    EnsureUv(mf.sharedMesh,r.gameObject.name.Contains(" turf"));
            }
        }

        private static void EnsureUv(Mesh mesh,bool top)
        {
            Vector3[] v=mesh.vertices;
            if(v==null||v.Length==0) return;
            Vector2[] uv=new Vector2[v.Length];
            for(int i=0;i<v.Length;i++)
            {
                if(top) uv[i]=new Vector2(v[i].x*0.16f,v[i].z*0.16f);
                else uv[i]=new Vector2((v[i].x+v[i].z)*0.10f,v[i].y*0.22f);
            }
            mesh.uv=uv;
        }

        private static Texture2D NoiseTexture(int size,float variation,int seed)
        {
            Texture2D tex=new Texture2D(size,size,TextureFormat.RGBA32,false,false);
            tex.name="Broad painterly material variation";
            tex.wrapMode=TextureWrapMode.Repeat;
            tex.filterMode=FilterMode.Bilinear;
            Color32[] pixels=new Color32[size*size];
            float ox=seed*0.23f,oy=seed*0.41f;
            for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                float a=Mathf.PerlinNoise(ox+x/34f,oy+y/34f)-0.5f;
                float b=Mathf.PerlinNoise(oy+x/15f,ox+y/15f)-0.5f;
                float value=Mathf.Clamp01(0.92f+a*variation+b*variation*0.32f);
                Color c=new Color(value,value,value,1f);
                pixels[x+y*size]=c;
            }
            tex.SetPixels32(pixels);tex.Apply(false,false);return tex;
        }

        private static void TuneCamera(Camera camera, Vector3 o)
        {
            camera.fieldOfView=38f;
            camera.transform.position=o+new Vector3(0.35f,5.75f,-24.7f);
            camera.transform.LookAt(o+new Vector3(-0.15f,2.55f,4.7f));
        }

        private static Vector3 Origin(Transform root)
        {
            Transform[] all=root.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)
                if(all[i].name=="Hero terrace turf") return all[i].position-new Vector3(0f,0.215f,-3.7f);
            return Vector3.zero;
        }
    }
}
