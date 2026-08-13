using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    internal static class SunlitWaterfallFinalShapePass
    {
        private static bool _done;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            GameObject scene = GameObject.Find("Sunlit Waterfall Target Scene");
            if (scene == null) return;
            Transform root = scene.transform;
            Transform hero = Find(root, "Hero terrace turf");
            if (hero == null) return;

            Material grass = MaterialOf(root, "Hero terrace turf");
            Material cliff = MaterialOf(root, "Hero terrace cliff");
            Material stone = MaterialOf(root, "Rounded ashlar");
            Material moss = MaterialOf(root, "Rounded shrub");
            Material bark = MaterialOf(root, "Oak trunk");

            BuildCleanArch(root, hero.position, stone, moss);
            BreakForegroundEdge(root, hero.position, grass, cliff, stone, moss);
            SoftenCascadeFaces(root, hero.position, cliff, stone, moss);
            CleanDistantArtifacts(root, hero.position);
            AddGardenDressing(root, hero.position, grass, stone, moss, bark);
        }

        private static void BuildCleanArch(Transform root, Vector3 hero, Material stone, Material moss)
        {
            if (stone == null) return;

            // Hide the earlier exploratory main arch stones, but keep the low secondary arch.
            MeshRenderer[] old = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < old.Length; i++)
            {
                Transform t = old[i].transform;
                if (t.name != "Rounded ashlar") continue;
                Vector3 p = t.position;
                if (p.x < hero.x - 2.3f && p.y > hero.y + 1.1f && p.z > hero.z + 1.4f)
                    t.gameObject.SetActive(false);
            }

            Vector3 c = hero + new Vector3(-4.75f, 4.15f, 4.35f);
            float radius = 2.28f;

            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 6; row++)
                {
                    Vector3 p = c + new Vector3(side * radius, -3.55f + row * 0.67f, 0f);
                    ArchBlock(root, p, new Vector3(0.78f, 0.61f, 0.72f), stone, row * 13 + side);
                }
            }

            for (int i = 0; i <= 13; i++)
            {
                if (i == 10) continue;
                float a = Mathf.Lerp(180f, 0f, i / 13f) * Mathf.Deg2Rad;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                GameObject block = ArchBlock(root, p, new Vector3(0.80f, 0.58f, 0.73f), stone, 100 + i);
                block.transform.rotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg + 90f);
            }

            if (moss != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    float a = Mathf.Lerp(155f, 35f, i / 5f) * Mathf.Deg2Rad;
                    Vector3 p = c + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius + 0.30f, -0.25f);
                    GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    cap.name = "Clean arch moss";
                    cap.transform.SetParent(root, false);
                    cap.transform.position = p;
                    cap.transform.localScale = new Vector3(0.62f, 0.18f, 0.36f);
                    Object.DestroyImmediate(cap.GetComponent<Collider>());
                    cap.GetComponent<Renderer>().sharedMaterial = moss;
                }
            }
        }

        private static GameObject ArchBlock(Transform root, Vector3 p, Vector3 scale, Material mat, int seed)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Cube);
            q.name = "Final ashlar block";
            q.transform.SetParent(root, false);
            q.transform.position = p;
            q.transform.localScale = scale * (0.96f + Hash(seed) * 0.06f);
            q.transform.rotation = Quaternion.Euler((Hash(seed + 7) - 0.5f) * 2.5f,
                                                    (Hash(seed + 17) - 0.5f) * 4.0f,
                                                    (Hash(seed + 29) - 0.5f) * 2.5f);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.GetComponent<Renderer>().sharedMaterial = mat;
            return q;
        }

        private static void BreakForegroundEdge(Transform root, Vector3 hero, Material grass, Material cliff, Material stone, Material moss)
        {
            Transform face = Find(root, "Hero terrace cliff");
            if (face != null)
            {
                MeshRenderer r = face.GetComponent<MeshRenderer>();
                if (r != null) r.enabled = false;
            }

            if (cliff == null) return;
            for (int i = 0; i < 9; i++)
            {
                float x = -4.35f + i * 1.08f;
                float y = -0.66f + (i % 3) * 0.08f;
                float z = -0.18f + ((i * 5) % 3 - 1) * 0.22f;
                Rock(root, hero + new Vector3(x, y, z),
                     new Vector3(1.30f, 0.72f, 1.02f) * (0.90f + (i % 2) * 0.10f), cliff);

                if (grass != null && i % 2 == 0)
                    TurfCap(root, hero + new Vector3(x, y + 0.58f, z), new Vector3(1.12f, 0.18f, 0.88f), grass);

                if (stone != null && i % 3 == 1)
                    SmallCube(root, hero + new Vector3(x + 0.18f, y + 0.58f, z - 0.28f),
                              new Vector3(0.54f, 0.38f, 0.48f), stone, i * 9f);
            }

            if (moss != null)
            {
                Cluster(root, hero + new Vector3(-3.7f, 0.05f, -0.2f), 0.62f, moss);
                Cluster(root, hero + new Vector3(2.8f, 0.02f, -0.25f), 0.58f, moss);
            }
        }

        private static void SoftenCascadeFaces(Transform root, Vector3 hero, Material cliff, Material stone, Material moss)
        {
            if (cliff == null) return;

            string[] names = { "Cascade zero cliff", "Cascade one cliff", "Cascade two cliff", "Cascade three cliff" };
            Vector3[] offsets = {
                new Vector3(4.25f,0.30f,4.0f), new Vector3(5.7f,2.0f,7.0f),
                new Vector3(6.6f,3.8f,10.1f), new Vector3(7.2f,5.6f,13.1f) };

            for (int i = 0; i < names.Length; i++)
            {
                Transform t = Find(root, names[i]);
                if (t != null)
                {
                    Vector3 s = t.localScale;
                    t.localScale = new Vector3(s.x, s.y * 0.55f, s.z);
                    Renderer rr = t.GetComponent<Renderer>();
                    if (rr != null && rr.sharedMaterial != null)
                        rr.sharedMaterial.SetColor("_BaseColor", new Color(0.50f, 0.49f, 0.39f));
                }

                Rock(root, hero + offsets[i], new Vector3(1.28f - i*0.08f, 0.82f, 1.0f), cliff);
                Rock(root, hero + offsets[i] + new Vector3(1.0f, -0.15f, 0.35f), new Vector3(0.90f,0.64f,0.80f), cliff);
                if (moss != null) Cluster(root, hero + offsets[i] + new Vector3(0.25f,0.65f,-0.08f),0.46f,moss);
                if (stone != null && i < 3) SmallCube(root, hero + offsets[i] + new Vector3(-0.65f,0.52f,-0.30f),new Vector3(0.46f,0.34f,0.42f),stone,-7f+i*6f);
            }
        }

        private static void CleanDistantArtifacts(Transform root, Vector3 hero)
        {
            Renderer[] all = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i].transform;
                if (t.position.z < hero.z + 24f) continue;
                if (t.name == "Rounded shrub" || t.name == "Organic moss cluster" || t.name == "Boulder turf cap")
                    t.gameObject.SetActive(false);
            }
        }

        private static void AddGardenDressing(Transform root, Vector3 hero, Material grass, Material stone, Material moss, Material bark)
        {
            if (stone != null)
            {
                SmallCube(root, hero + new Vector3(-4.0f,0.55f,2.1f),new Vector3(0.72f,0.48f,0.62f),stone,7f);
                SmallCube(root, hero + new Vector3(-3.4f,0.45f,2.5f),new Vector3(0.54f,0.38f,0.48f),stone,-9f);
                SmallCube(root, hero + new Vector3(3.3f,0.42f,2.0f),new Vector3(0.60f,0.42f,0.52f),stone,11f);
            }
            if (moss != null)
            {
                Cluster(root, hero + new Vector3(-4.4f,0.70f,2.0f),0.58f,moss);
                Cluster(root, hero + new Vector3(3.7f,0.62f,2.0f),0.54f,moss);
            }
            if (bark != null)
            {
                // Small fallen branch adds an organic diagonal like the illustrated foreground.
                GameObject branch = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                branch.name = "Fallen garden branch";
                branch.transform.SetParent(root,false);
                branch.transform.position = hero + new Vector3(-3.7f,0.52f,3.1f);
                branch.transform.rotation = Quaternion.Euler(0f,0f,68f);
                branch.transform.localScale = new Vector3(0.13f,0.85f,0.13f);
                Object.DestroyImmediate(branch.GetComponent<Collider>());
                branch.GetComponent<Renderer>().sharedMaterial = bark;
            }
        }

        private static void Rock(Transform root, Vector3 p, Vector3 scale, Material mat)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            q.name = "Broken rounded cliff rock";
            q.transform.SetParent(root,false);
            q.transform.position = p;
            q.transform.localScale = scale;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void TurfCap(Transform root, Vector3 p, Vector3 scale, Material mat)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            q.name = "Broken edge turf cap";
            q.transform.SetParent(root,false);
            q.transform.position = p;
            q.transform.localScale = scale;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void SmallCube(Transform root, Vector3 p, Vector3 scale, Material mat, float yaw)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Cube);
            q.name = "Chunky exposed stone";
            q.transform.SetParent(root,false);
            q.transform.position = p;
            q.transform.localScale = scale;
            q.transform.rotation = Quaternion.Euler(0f,yaw,0f);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void Cluster(Transform root, Vector3 p, float scale, Material mat)
        {
            for (int i=0;i<4;i++)
            {
                float a=i*1.7f;
                GameObject q=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                q.name="Soft moss cluster";
                q.transform.SetParent(root,false);
                q.transform.position=p+new Vector3(Mathf.Cos(a)*scale*0.28f,(i%2)*0.06f,Mathf.Sin(a)*scale*0.22f);
                q.transform.localScale=new Vector3(scale*0.62f,scale*0.32f,scale*0.50f);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.GetComponent<Renderer>().sharedMaterial=mat;
            }
        }

        private static Transform Find(Transform root,string name)
        {
            Transform[] all=root.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++) if(all[i].name==name) return all[i];
            return null;
        }

        private static Material MaterialOf(Transform root,string name)
        {
            Renderer[] all=root.GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<all.Length;i++) if(all[i].gameObject.name==name) return all[i].sharedMaterial;
            return null;
        }

        private static float Hash(int n)
        {
            unchecked { uint x=(uint)n; x^=x>>16; x*=0x7feb352d; x^=x>>15; x*=0x846ca68b; x^=x>>16; return (x&0x00ffffff)/16777215f; }
        }
    }
}
