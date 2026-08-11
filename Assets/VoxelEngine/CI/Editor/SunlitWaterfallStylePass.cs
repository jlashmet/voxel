using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine.CI
{
    internal static class SunlitWaterfallStylePass
    {
        private static bool _done;
        private static Transform _root;
        private static Material _grass, _rock, _stone, _moss, _leaf;
        private static Material _white, _pink, _blue, _yellow;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            GameObject scene = GameObject.Find("Sunlit Coherent Terrain Scene");
            if (scene == null) return;
            _root = scene.transform;
            Vector3 o = camera.transform.position - new Vector3(0.4f,5.6f,-24.2f);

            _grass = MaterialOf("Reusable storybook terrain patch",0);
            _rock = MaterialOf("Reusable storybook terrain patch",1);
            _stone = MaterialOf("Warm chunky masonry",0);
            _moss = MaterialOf("Moss cluster",0);
            _leaf = MaterialOf("Rounded storybook canopy",0);

            TuneMaterials();
            CompressTerrain(o);
            ReframeArch(o);
            ShrinkCastle(o);
            AddSoftSlopeDressing(o);
            AddFlowers(o);
            TuneCamera(camera,o);
        }

        private static void TuneMaterials()
        {
            if (_grass != null)
            {
                _grass.SetColor("_BaseColor",new Color(0.53f,0.70f,0.22f));
                _grass.SetColor("_EmissionColor",new Color(0.018f,0.025f,0.006f));
            }
            if (_rock != null)
                _rock.SetColor("_BaseColor",new Color(0.52f,0.51f,0.39f));
            if (_stone != null)
                _stone.SetColor("_BaseColor",new Color(0.82f,0.76f,0.64f));
            if (_moss != null)
                _moss.SetColor("_BaseColor",new Color(0.43f,0.60f,0.16f));
            if (_leaf != null)
            {
                _leaf.SetColor("_BaseColor",new Color(0.45f,0.63f,0.19f));
                _leaf.SetColor("_EmissionColor",new Color(0.012f,0.018f,0.004f));
            }

            Shader shader=Shader.Find("VoxelEngine/SunlitSmooth");
            _white=Make(shader,"Flower white",new Color(0.99f,0.98f,0.90f));
            _pink=Make(shader,"Flower pink",new Color(0.96f,0.50f,0.59f));
            _blue=Make(shader,"Flower blue",new Color(0.36f,0.68f,0.94f));
            _yellow=Make(shader,"Flower yellow",new Color(1.0f,0.73f,0.14f));
        }

        private static void CompressTerrain(Vector3 o)
        {
            MeshFilter terrain=FindFilter("Reusable storybook terrain patch");
            if(terrain!=null && terrain.sharedMesh!=null)
            {
                Mesh mesh=terrain.sharedMesh;Vector3[] v=mesh.vertices;
                for(int i=0;i<v.Length;i++)
                {
                    float dy=v[i].y-o.y;
                    if(dy>0f) v[i].y=o.y+dy*0.73f;
                }
                mesh.vertices=v;mesh.RecalculateNormals();mesh.RecalculateBounds();
            }

            // Bring overlays down with the compressed terrain while keeping clouds/tree height.
            Transform[] all=_root.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)
            {
                Transform t=all[i];string n=t.name;
                if(n.Contains("cloud")||n.Contains("sky")||n.Contains("oak")||n.Contains("canopy")) continue;
                if(n=="Reusable storybook terrain patch") continue;
                float dy=t.position.y-o.y;
                if(dy>0.2f) t.position=new Vector3(t.position.x,o.y+dy*0.73f,t.position.z);
            }

            MeshFilter[] filters=_root.GetComponentsInChildren<MeshFilter>(true);
            for(int i=0;i<filters.Length;i++)
            {
                MeshFilter mf=filters[i];
                if(!mf.gameObject.name.Contains("waterfall") || mf.sharedMesh==null) continue;
                Vector3[] v=mf.sharedMesh.vertices;
                for(int j=0;j<v.Length;j++){float dy=v[j].y-o.y;if(dy>0.2f)v[j].y=o.y+dy*0.73f;}
                mf.sharedMesh.vertices=v;mf.sharedMesh.RecalculateNormals();mf.sharedMesh.RecalculateBounds();
            }
        }

        private static void ReframeArch(Vector3 o)
        {
            Transform[] all=_root.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)
            {
                Transform t=all[i];
                if(t.name!="Warm chunky masonry") continue;
                if(t.position.x<o.x-2.5f && t.position.y>o.y+0.8f && t.position.z<o.z+6.0f)
                {
                    t.position+=new Vector3(0.95f,0.45f,1.35f);
                    t.localScale*=0.88f;
                }
            }
        }

        private static void ShrinkCastle(Vector3 o)
        {
            List<Transform> castle=new List<Transform>();
            Transform[] all=_root.GetComponentsInChildren<Transform>(true);
            Vector3 centre=Vector3.zero;
            for(int i=0;i<all.Length;i++)
            {
                Transform t=all[i];string n=t.name;
                if(t.position.z<o.z+18f) continue;
                if(n.Contains("castle")||n.Contains("Castle")||n.Contains("tower")||n.Contains("Tower")||n.Contains("roof")||n.Contains("Roof"))
                {castle.Add(t);centre+=t.position;}
            }
            if(castle.Count==0)return;centre/=castle.Count;
            for(int i=0;i<castle.Count;i++)
            {
                Transform t=castle[i];Vector3 offset=t.position-centre;
                t.position=centre+offset*0.72f+new Vector3(0f,0f,4.0f);
                t.localScale*=0.72f;
            }
        }

        private static void AddSoftSlopeDressing(Vector3 o)
        {
            if(_moss==null)return;
            Vector3[] moss={
                new Vector3(-4.5f,0.70f,-2.8f),new Vector3(3.9f,0.65f,-2.2f),
                new Vector3(4.8f,0.95f,0.6f),new Vector3(5.7f,2.0f,4.3f),
                new Vector3(6.5f,3.2f,7.8f),new Vector3(-5.5f,2.0f,3.5f),
                new Vector3(-2.8f,1.8f,5.8f)};
            for(int i=0;i<moss.Length;i++) Cluster(o+moss[i],0.48f+(i%3)*0.07f,_moss);

            if(_stone!=null)
            {
                Cube(o+new Vector3(4.3f,0.82f,0.0f),new Vector3(0.52f,0.35f,0.46f),_stone,-7f);
                Cube(o+new Vector3(5.4f,1.85f,3.9f),new Vector3(0.46f,0.32f,0.42f),_stone,8f);
                Cube(o+new Vector3(-4.8f,1.95f,3.2f),new Vector3(0.55f,0.38f,0.48f),_stone,10f);
            }
        }

        private static void AddFlowers(Vector3 o)
        {
            FlowerPatch(o+new Vector3(-3.4f,0.72f,-3.6f),_white,1);
            FlowerPatch(o+new Vector3(-2.2f,0.70f,-4.1f),_pink,2);
            FlowerPatch(o+new Vector3(2.5f,0.66f,-3.4f),_blue,3);
            FlowerPatch(o+new Vector3(-5.7f,2.05f,2.6f),_yellow,4);
            FlowerPatch(o+new Vector3(-4.6f,2.15f,3.8f),_white,5);
            FlowerPatch(o+new Vector3(4.4f,0.95f,0.8f),_pink,6);
        }

        private static void FlowerPatch(Vector3 p,Material petals,int seed)
        {
            if(petals==null||_moss==null)return;
            for(int i=0;i<4;i++)
            {
                float ox=(Hash(seed*17+i)-0.5f)*0.65f;float oz=(Hash(seed*29+i+3)-0.5f)*0.55f;
                Vector3 q=p+new Vector3(ox,0f,oz);float h=0.18f+(i%2)*0.04f;
                GameObject stem=GameObject.CreatePrimitive(PrimitiveType.Capsule);stem.name="Tiny flower stem";stem.transform.SetParent(_root,false);stem.transform.position=q+Vector3.up*h*0.5f;stem.transform.localScale=new Vector3(0.018f,h*0.5f,0.018f);Object.DestroyImmediate(stem.GetComponent<Collider>());stem.GetComponent<Renderer>().sharedMaterial=_moss;
                for(int j=0;j<5;j++){float a=j*Mathf.PI*2f/5f;GameObject petal=GameObject.CreatePrimitive(PrimitiveType.Sphere);petal.name="Tiny flower petal";petal.transform.SetParent(_root,false);petal.transform.position=q+Vector3.up*h+new Vector3(Mathf.Cos(a)*0.055f,0f,Mathf.Sin(a)*0.055f);petal.transform.localScale=new Vector3(0.065f,0.022f,0.045f);Object.DestroyImmediate(petal.GetComponent<Collider>());petal.GetComponent<Renderer>().sharedMaterial=petals;}
            }
        }

        private static void TuneCamera(Camera camera,Vector3 o)
        {
            camera.fieldOfView=37f;
            camera.transform.position=o+new Vector3(0.3f,5.1f,-24.0f);
            camera.transform.LookAt(o+new Vector3(-0.2f,2.15f,5.8f));
        }

        private static Material Make(Shader shader,string name,Color c)
        {
            if(shader==null)return null;Material m=new Material(shader);m.name=name;m.SetTexture("_MainTex",Texture2D.whiteTexture);m.SetColor("_BaseColor",c);m.SetColor("_EmissionColor",Color.black);m.SetFloat("_Smoothness",0.02f);m.SetFloat("_Cull",2f);m.SetFloat("_ZWrite",1f);return m;
        }

        private static void Cluster(Vector3 p,float scale,Material mat)
        {
            for(int i=0;i<4;i++){float a=i*1.6f;GameObject q=GameObject.CreatePrimitive(PrimitiveType.Sphere);q.name="Style moss clump";q.transform.SetParent(_root,false);q.transform.position=p+new Vector3(Mathf.Cos(a)*scale*0.28f,(i%2)*0.05f,Mathf.Sin(a)*scale*0.22f);q.transform.localScale=new Vector3(scale*0.62f,scale*0.32f,scale*0.50f);Object.DestroyImmediate(q.GetComponent<Collider>());q.GetComponent<Renderer>().sharedMaterial=mat;}
        }

        private static void Cube(Vector3 p,Vector3 s,Material mat,float yaw){GameObject q=GameObject.CreatePrimitive(PrimitiveType.Cube);q.name="Style exposed stone";q.transform.SetParent(_root,false);q.transform.position=p;q.transform.localScale=s;q.transform.rotation=Quaternion.Euler(0f,yaw,0f);Object.DestroyImmediate(q.GetComponent<Collider>());q.GetComponent<Renderer>().sharedMaterial=mat;}
        private static MeshFilter FindFilter(string name){MeshFilter[] a=_root.GetComponentsInChildren<MeshFilter>(true);for(int i=0;i<a.Length;i++)if(a[i].gameObject.name==name)return a[i];return null;}
        private static Material MaterialOf(string name,int index){Renderer[] a=_root.GetComponentsInChildren<Renderer>(true);for(int i=0;i<a.Length;i++)if(a[i].gameObject.name==name){Material[] m=a[i].sharedMaterials;if(index>=0&&index<m.Length)return m[index];}return null;}
        private static float Hash(int n){unchecked{uint x=(uint)n;x^=x>>16;x*=0x7feb352d;x^=x>>15;x*=0x846ca68b;x^=x>>16;return(x&0x00ffffff)/16777215f;}}
    }
}
