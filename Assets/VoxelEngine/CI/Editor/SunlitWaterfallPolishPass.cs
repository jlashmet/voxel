using UnityEngine;

namespace VoxelEngine.CI
{
    internal static class SunlitWaterfallPolishPass
    {
        private static bool _done;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            GameObject scene = GameObject.Find("Sunlit Waterfall Target Scene");
            if (scene == null) return;
            Transform root = scene.transform;

            Material cliff = MaterialOf(root, "Hero terrace cliff");
            Material grass = MaterialOf(root, "Hero terrace turf");
            Material moss = MaterialOf(root, "Rounded shrub");
            Material leaf = MaterialOf(root, "Rounded oak canopy");

            LiftAndSetBackArch(root);
            ReplaceCascadeWalls(root, cliff, grass, moss);
            ReplaceCastleShelf(root, cliff, grass, moss);
            TuneFoliage(root, leaf, moss);
        }

        private static void LiftAndSetBackArch(Transform root)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            Vector3 centre = Vector3.zero;
            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name != "Final ashlar block") continue;
                centre += all[i].position;
                count++;
            }
            if (count == 0) return;
            centre /= count;

            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t.name != "Final ashlar block" && t.name != "Clean arch moss") continue;
                Vector3 offset = t.position - centre;
                t.position = centre + offset * 0.88f + new Vector3(-0.20f, 0.85f, 1.15f);
                t.localScale *= 0.88f;
            }
        }

        private static void ReplaceCascadeWalls(Transform root, Material cliff, Material grass, Material moss)
        {
            if (cliff == null) return;
            string[] ids = { "Cascade zero", "Cascade one", "Cascade two", "Cascade three" };
            for (int i = 0; i < ids.Length; i++)
            {
                Transform wall = Find(root, ids[i] + " cliff");
                Transform top = Find(root, ids[i] + " turf");
                if (wall == null || top == null) continue;
                MeshRenderer renderer = wall.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = false;

                float width = 2.1f - i * 0.12f;
                Vector3 basePos = top.position + new Vector3(0f, -0.72f, -0.08f);
                Rock(root, basePos + new Vector3(-width * 0.55f, 0f, 0f), new Vector3(width, 0.88f, 1.35f), cliff);
                Rock(root, basePos + new Vector3(0f, -0.10f, 0.15f), new Vector3(width * 1.15f, 0.96f, 1.45f), cliff);
                Rock(root, basePos + new Vector3(width * 0.58f, 0.02f, -0.05f), new Vector3(width * 0.92f, 0.82f, 1.25f), cliff);

                if (grass != null)
                {
                    Cap(root, basePos + new Vector3(-width * 0.50f, 0.70f, 0f), new Vector3(width * 0.88f, 0.16f, 1.10f), grass);
                    Cap(root, basePos + new Vector3(width * 0.48f, 0.69f, 0f), new Vector3(width * 0.82f, 0.15f, 1.03f), grass);
                }
                if (moss != null) Cluster(root, basePos + new Vector3(width * 0.12f, 0.76f, -0.42f), 0.50f, moss);
            }
        }

        private static void ReplaceCastleShelf(Transform root, Material cliff, Material grass, Material moss)
        {
            Transform wall = Find(root, "Distant castle hill cliff");
            Transform turf = Find(root, "Distant castle hill turf");
            if (wall == null || turf == null || cliff == null) return;
            MeshRenderer wr = wall.GetComponent<MeshRenderer>();
            MeshRenderer tr = turf.GetComponent<MeshRenderer>();
            if (wr != null) wr.enabled = false;
            if (tr != null) tr.enabled = false;

            Vector3 p = turf.position + new Vector3(0f, -1.55f, 0f);
            Rock(root, p + new Vector3(-1.8f, 0f, 0f), new Vector3(2.6f, 1.85f, 2.1f), cliff);
            Rock(root, p + new Vector3(0f, 0.25f, 0.15f), new Vector3(3.0f, 2.15f, 2.35f), cliff);
            Rock(root, p + new Vector3(1.8f, -0.05f, 0.1f), new Vector3(2.4f, 1.75f, 2.0f), cliff);
            if (grass != null)
            {
                Cap(root, p + new Vector3(-1.0f, 1.65f, 0f), new Vector3(2.5f, 0.25f, 1.85f), grass);
                Cap(root, p + new Vector3(1.15f, 1.58f, 0.1f), new Vector3(2.2f, 0.22f, 1.70f), grass);
            }
            if (moss != null)
            {
                Cluster(root, p + new Vector3(-2.0f, 1.1f, -0.5f), 0.55f, moss);
                Cluster(root, p + new Vector3(2.0f, 0.95f, -0.4f), 0.48f, moss);
            }
        }

        private static void TuneFoliage(Transform root, Material leaf, Material moss)
        {
            if (leaf != null)
            {
                leaf.SetColor("_BaseColor", new Color(0.36f, 0.56f, 0.15f));
                leaf.SetColor("_EmissionColor", new Color(0.015f, 0.025f, 0.006f));
            }
            if (moss != null)
                moss.SetColor("_BaseColor", new Color(0.37f, 0.55f, 0.14f));
        }

        private static void Rock(Transform root, Vector3 p, Vector3 scale, Material mat)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            q.name = "Polished rounded cliff mass";
            q.transform.SetParent(root, false);
            q.transform.position = p;
            q.transform.localScale = scale;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void Cap(Transform root, Vector3 p, Vector3 scale, Material mat)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            q.name = "Polished turf cap";
            q.transform.SetParent(root, false);
            q.transform.position = p;
            q.transform.localScale = scale;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void Cluster(Transform root, Vector3 p, float scale, Material mat)
        {
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.65f;
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                q.name = "Polished moss cluster";
                q.transform.SetParent(root, false);
                q.transform.position = p + new Vector3(Mathf.Cos(a) * scale * 0.30f, (i % 2) * 0.05f, Mathf.Sin(a) * scale * 0.22f);
                q.transform.localScale = new Vector3(scale * 0.62f, scale * 0.33f, scale * 0.50f);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        private static Transform Find(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++) if (all[i].name == name) return all[i];
            return null;
        }

        private static Material MaterialOf(Transform root, string name)
        {
            Renderer[] all = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++) if (all[i].gameObject.name == name) return all[i].sharedMaterial;
            return null;
        }
    }
}
