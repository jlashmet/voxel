using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    internal static class SunlitWaterfallLushPass
    {
        private static bool _done;
        private static Transform _root;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            GameObject scene = GameObject.Find("Sunlit Coherent Terrain Scene");
            if (scene == null) return;
            _root = scene.transform;
            Vector3 o = camera.transform.position - new Vector3(0.3f,5.1f,-24.0f);

            Material grass = MaterialOf("Reusable storybook terrain patch",0);
            Material slope = MaterialOf("Reusable storybook terrain patch",1);
            Material stone = MaterialOf("Warm chunky masonry",0);
            Material moss = MaterialOf("Style moss clump",0);
            Material water = MaterialOf("Shared turquoise channel plane",0);
            Material fall = MaterialOf("Coherent waterfall",0);

            if (slope != null)
            {
                slope.SetColor("_BaseColor",new Color(0.36f,0.43f,0.24f));
                slope.SetColor("_EmissionColor",new Color(0.006f,0.008f,0.003f));
            }
            if (grass != null) grass.SetColor("_BaseColor",new Color(0.54f,0.71f,0.22f));
            if (stone != null) stone.SetColor("_BaseColor",new Color(0.84f,0.78f,0.65f));
            if (water != null) water.SetColor("_BaseColor",new Color(0.04f,0.67f,0.88f,0.92f));
            if (fall != null) fall.SetColor("_BaseColor",new Color(0.76f,0.96f,1f,0.92f));

            if (slope != null && grass != null)
            {
                Peninsula(o+new Vector3(-4.2f,-0.95f,-8.1f),new Vector3(6.0f,1.35f,3.6f),slope,grass);
                Peninsula(o+new Vector3(4.7f,-1.05f,-7.6f),new Vector3(3.6f,1.05f,2.7f),slope,grass);
            }

            if (stone != null)
            {
                EdgeStone(o+new Vector3(-7.2f,-0.02f,-7.4f),new Vector3(0.90f,0.62f,0.75f),stone,7f);
                EdgeStone(o+new Vector3(-6.0f,0.05f,-7.0f),new Vector3(0.72f,0.52f,0.64f),stone,-6f);
                EdgeStone(o+new Vector3(-1.0f,-0.10f,-7.9f),new Vector3(0.82f,0.54f,0.70f),stone,4f);
                EdgeStone(o+new Vector3(5.6f,-0.12f,-6.8f),new Vector3(0.70f,0.48f,0.60f),stone,-8f);
                EdgeStone(o+new Vector3(4.5f,0.78f,0.2f),new Vector3(0.56f,0.38f,0.48f),stone,10f);
                EdgeStone(o+new Vector3(5.7f,1.95f,4.1f),new Vector3(0.48f,0.34f,0.43f),stone,-7f);
            }

            if (moss != null)
            {
                Moss(o+new Vector3(-6.6f,0.42f,-7.1f),0.66f,moss);
                Moss(o+new Vector3(-2.4f,0.28f,-7.4f),0.62f,moss);
                Moss(o+new Vector3(4.8f,0.12f,-6.7f),0.58f,moss);
                Moss(o+new Vector3(4.8f,1.18f,0.4f),0.50f,moss);
            }

            Light sun = Object.FindAnyObjectByType<Light>();
            if (sun != null)
            {
                sun.color = new Color(1f,0.92f,0.73f);
                sun.intensity = 1.28f;
                sun.shadowStrength = 0.34f;
            }
        }

        private static void Peninsula(Vector3 p,Vector3 scale,Material rock,Material grass)
        {
            GameObject body=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name="Foreground garden rock";body.transform.SetParent(_root,false);body.transform.position=p;body.transform.localScale=scale;
            Object.DestroyImmediate(body.GetComponent<Collider>());body.GetComponent<Renderer>().sharedMaterial=rock;

            GameObject cap=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name="Foreground garden turf";cap.transform.SetParent(_root,false);cap.transform.position=p+new Vector3(0f,scale.y*0.72f,0f);cap.transform.localScale=new Vector3(scale.x*0.94f,scale.y*0.20f,scale.z*0.94f);
            Object.DestroyImmediate(cap.GetComponent<Collider>());cap.GetComponent<Renderer>().sharedMaterial=grass;
        }

        private static void EdgeStone(Vector3 p,Vector3 scale,Material mat,float yaw)
        {
            GameObject q=GameObject.CreatePrimitive(PrimitiveType.Cube);q.name="Foreground pale ruin block";q.transform.SetParent(_root,false);q.transform.position=p;q.transform.localScale=scale;q.transform.rotation=Quaternion.Euler(0f,yaw,0f);
            Object.DestroyImmediate(q.GetComponent<Collider>());q.GetComponent<Renderer>().sharedMaterial=mat;
        }

        private static void Moss(Vector3 p,float scale,Material mat)
        {
            for(int i=0;i<4;i++)
            {
                float a=i*1.65f;GameObject q=GameObject.CreatePrimitive(PrimitiveType.Sphere);q.name="Foreground moss cushion";q.transform.SetParent(_root,false);
                q.transform.position=p+new Vector3(Mathf.Cos(a)*scale*0.28f,(i%2)*0.05f,Mathf.Sin(a)*scale*0.22f);q.transform.localScale=new Vector3(scale*0.62f,scale*0.32f,scale*0.50f);
                Object.DestroyImmediate(q.GetComponent<Collider>());q.GetComponent<Renderer>().sharedMaterial=mat;
            }
        }

        private static Material MaterialOf(string name,int index)
        {
            Renderer[] all=_root.GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<all.Length;i++) if(all[i].gameObject.name==name)
            {
                Material[] mats=all[i].sharedMaterials;if(index>=0&&index<mats.Length)return mats[index];
            }
            return null;
        }
    }
}
