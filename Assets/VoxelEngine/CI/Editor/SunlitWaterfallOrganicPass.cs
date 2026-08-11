using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    internal static class SunlitWaterfallOrganicPass
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
            Material water = MaterialOf(root, "Front channel");
            Material moss = MaterialOf(root, "Rounded shrub");
            Material stone = MaterialOf(root, "Rounded ashlar");

            ReframeMasonry(root, hero.position);
            FillValley(root, hero.position, grass, cliff, water);
            AddBoulderBreakup(root, hero.position, grass, cliff, moss, stone);
            BrightenWater(root);
        }

        private static void ReframeMasonry(Transform root, Vector3 hero)
        {
            MeshRenderer[] all = root.GetComponentsInChildren<MeshRenderer>(true);
            int frontStone = 0;
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i].transform;
                if (t.name != "Rounded ashlar") continue;
                Vector3 p = t.position;

                // High left masonry is the large framing arch: move it farther left/back so the
                // opening frames the garden instead of putting a pier through the image centre.
                if (p.x < hero.x - 2.0f && p.y > hero.y + 1.0f && p.z > hero.z + 0.5f)
                {
                    t.position += new Vector3(-1.25f, 0.35f, 1.10f);
                    t.localScale *= 0.88f;
                    continue;
                }

                // Foreground edging should read as scattered ruined masonry, not a fence.
                if (Mathf.Abs(p.x - hero.x) < 6.0f && p.y < hero.y + 1.0f && p.z < hero.z + 0.2f)
                {
                    t.localScale *= 0.58f;
                    t.position += new Vector3(0f, -0.12f, 0.18f);
                    if ((frontStone++ % 3) == 1) t.gameObject.SetActive(false);
                }
            }
        }

        private static void FillValley(Transform root, Vector3 hero, Material grass, Material cliff, Material water)
        {
            if (grass == null || cliff == null) return;

            // A low continuous garden floor removes the empty lavender void while leaving the
            // authored terraces visibly layered above it.
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = "Low valley substrate";
            rock.transform.SetParent(root, false);
            rock.transform.position = hero + new Vector3(0f, -2.15f, 8.0f);
            rock.transform.localScale = new Vector3(24f, 1.2f, 22f);
            Object.DestroyImmediate(rock.GetComponent<Collider>());
            rock.GetComponent<Renderer>().sharedMaterial = cliff;

            GameObject turf = GameObject.CreatePrimitive(PrimitiveType.Plane);
            turf.name = "Low valley turf";
            turf.transform.SetParent(root, false);
            turf.transform.position = hero + new Vector3(0f, -1.52f, 8.0f);
            turf.transform.localScale = new Vector3(2.45f, 1f, 2.25f);
            Object.DestroyImmediate(turf.GetComponent<Collider>());
            turf.GetComponent<Renderer>().sharedMaterial = grass;

            if (water != null)
            {
                AddWaterDisc(root, hero + new Vector3(1.2f, -1.43f, 4.0f), new Vector3(4.6f, 0.035f, 1.55f), water);
                AddWaterDisc(root, hero + new Vector3(4.7f, -1.40f, 7.2f), new Vector3(3.7f, 0.035f, 1.25f), water);
            }
        }

        private static void AddBoulderBreakup(Transform root, Vector3 hero, Material grass, Material cliff, Material moss, Material stone)
        {
            if (cliff == null) return;

            Boulder(root, hero + new Vector3(4.2f, -0.15f, 3.2f), new Vector3(1.45f, 1.05f, 1.10f), cliff, grass);
            Boulder(root, hero + new Vector3(6.2f, 1.65f, 6.4f), new Vector3(1.20f, 0.90f, 1.00f), cliff, grass);
            Boulder(root, hero + new Vector3(7.0f, 3.55f, 9.5f), new Vector3(1.05f, 0.82f, 0.92f), cliff, grass);
            Boulder(root, hero + new Vector3(-4.8f, 1.45f, 7.1f), new Vector3(1.45f, 1.00f, 1.18f), cliff, grass);
            Boulder(root, hero + new Vector3(-6.4f, 0.65f, 2.0f), new Vector3(1.15f, 0.86f, 0.96f), cliff, grass);

            if (moss != null)
            {
                Shrub(root, hero + new Vector3(4.1f, 0.78f, 3.0f), 0.72f, moss);
                Shrub(root, hero + new Vector3(6.1f, 2.45f, 6.3f), 0.60f, moss);
                Shrub(root, hero + new Vector3(-4.9f, 2.30f, 7.0f), 0.68f, moss);
            }

            if (stone != null)
            {
                SmallStone(root, hero + new Vector3(3.5f, 0.55f, 2.7f), new Vector3(0.60f, 0.42f, 0.52f), stone, -9f);
                SmallStone(root, hero + new Vector3(4.3f, 0.52f, 2.9f), new Vector3(0.52f, 0.36f, 0.47f), stone, 7f);
                SmallStone(root, hero + new Vector3(-4.6f, 2.42f, 6.6f), new Vector3(0.65f, 0.44f, 0.55f), stone, 8f);
            }
        }

        private static void BrightenWater(Transform root)
        {
            Renderer[] all = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Material m = all[i].sharedMaterial;
                if (m == null) continue;
                if (m.name == "Turquoise water")
                {
                    m.SetColor("_BaseColor", new Color(0.04f, 0.71f, 0.91f, 0.92f));
                    m.SetColor("_EmissionColor", new Color(0.02f, 0.17f, 0.22f));
                }
                else if (m.name == "Waterfall")
                {
                    m.SetColor("_BaseColor", new Color(0.74f, 0.95f, 1f, 0.90f));
                }
            }
        }

        private static void Boulder(Transform root, Vector3 p, Vector3 scale, Material cliff, Material grass)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Organic cliff boulder";
            rock.transform.SetParent(root, false);
            rock.transform.position = p;
            rock.transform.localScale = scale;
            Object.DestroyImmediate(rock.GetComponent<Collider>());
            rock.GetComponent<Renderer>().sharedMaterial = cliff;

            if (grass == null) return;
            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Boulder turf cap";
            cap.transform.SetParent(root, false);
            cap.transform.position = p + new Vector3(0f, scale.y * 0.65f, 0f);
            cap.transform.localScale = new Vector3(scale.x * 0.88f, scale.y * 0.20f, scale.z * 0.88f);
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            cap.GetComponent<Renderer>().sharedMaterial = grass;
        }

        private static void AddWaterDisc(Transform root, Vector3 p, Vector3 scale, Material water)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            q.name = "Turquoise valley pool";
            q.transform.SetParent(root, false);
            q.transform.position = p;
            q.transform.localScale = scale;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            Renderer r = q.GetComponent<Renderer>();
            r.sharedMaterial = water;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Shrub(Transform root, Vector3 p, float scale, Material mat)
        {
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.62f;
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                q.name = "Organic moss cluster";
                q.transform.SetParent(root, false);
                q.transform.position = p + new Vector3(Mathf.Cos(a) * scale * 0.28f, (i % 2) * 0.06f, Mathf.Sin(a) * scale * 0.24f);
                q.transform.localScale = new Vector3(scale * 0.62f, scale * 0.36f, scale * 0.52f);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        private static void SmallStone(Transform root, Vector3 p, Vector3 scale, Material mat, float yaw)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Cube);
            q.name = "Scattered ruin stone";
            q.transform.SetParent(root, false);
            q.transform.position = p;
            q.transform.localScale = scale;
            q.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.GetComponent<Renderer>().sharedMaterial = mat;
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
