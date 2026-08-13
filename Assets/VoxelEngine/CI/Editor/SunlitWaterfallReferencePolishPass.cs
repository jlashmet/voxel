using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Replaces the last visibly primitive forms in the corrected reference shot. Waterfall tiers
    /// become irregular tapered ledge meshes with real grassy top surfaces, while old floating
    /// lookdev dressing is replaced by deliberately attached moss, ivy and flowers.
    /// </summary>
    internal static class SunlitWaterfallReferencePolishPass
    {
        private static bool _done;
        private static Transform _root;
        private static Material _rock, _grass, _moss, _white, _pink, _yellow, _blue;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            GameObject scene = GameObject.Find("Sunlit Coherent Terrain Scene");
            if (scene == null) return;
            _root = scene.transform;

            Vector3 o = camera.transform.position - new Vector3(0.10f, 5.05f, -22.6f);
            BuildMaterials();
            HideBlobCliffsAndLooseDressing();
            ForceBlueAtmosphere(camera);
            BuildCascadeLedges(o);
            BuildCastleLedge(o);
            BuildAttachedMossAndIvy(o);
            BuildGardenFlowers(o);
        }

        private static void BuildMaterials()
        {
            Shader shader = Shader.Find("VoxelEngine/SunlitSmooth");
            if (shader == null) return;
            _rock = Mat(shader, "Reference3 cliff rock", new Color(0.46f, 0.48f, 0.31f));
            _grass = Mat(shader, "Reference3 grass turf", new Color(0.54f, 0.71f, 0.22f));
            _moss = Mat(shader, "Reference3 moss ivy", new Color(0.36f, 0.58f, 0.14f));
            _white = Mat(shader, "Reference3 flower white", new Color(0.98f, 0.98f, 0.93f));
            _pink = Mat(shader, "Reference3 flower pink", new Color(0.96f, 0.50f, 0.61f));
            _yellow = Mat(shader, "Reference3 flower yellow", new Color(1.00f, 0.76f, 0.16f));
            _blue = Mat(shader, "Reference3 flower blue", new Color(0.40f, 0.72f, 0.96f));
        }

        private static void HideBlobCliffsAndLooseDressing()
        {
            Renderer[] renderers = _root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                string n = r.gameObject.name.ToLowerInvariant();
                if (n.StartsWith("reference3 ")) continue;

                if (n.Contains("reference2 cliff rock") || n.Contains("reference2 thin turf cap") ||
                    n.Contains("reference2 small turf shelf") || n.Contains("reference2 castle rock hill") ||
                    n.Contains("reference2 castle turf cap") || n.Contains("reference moss cushion") ||
                    n.Contains("reference flower stem") || n.Contains("reference flower petal"))
                {
                    r.gameObject.SetActive(false);
                }
            }
        }

        private static void ForceBlueAtmosphere(Camera camera)
        {
            // The project skybox was still tinting the lookdev shot lavender. Keep the authored
            // gradient quad, but clear behind it with a known blue and remove the scene skybox.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.20f, 0.68f, 0.97f, 1f);
            RenderSettings.skybox = null;
        }

        private static void BuildCascadeLedges(Vector3 o)
        {
            // Geometry dimensions preserve the water elevations from ReferencePass, so the pools
            // and ribbons remain connected while the geology changes from ellipsoids to ledges.
            Ledge(o + new Vector3(4.20f, 0.75f, 6.10f), new Vector3(4.10f, 2.60f, 3.00f), 11);
            Ledge(o + new Vector3(4.90f, 2.90f, 9.20f), new Vector3(3.80f, 2.80f, 2.80f), 23);
            Ledge(o + new Vector3(5.60f, 5.10f, 12.20f), new Vector3(3.45f, 2.80f, 2.65f), 37);
            Ledge(o + new Vector3(6.10f, 7.15f, 15.00f), new Vector3(3.05f, 2.65f, 2.45f), 51);

            // Small broken side ledges stop the cascade from reading as four identical plates.
            Ledge(o + new Vector3(2.82f, 1.10f, 5.25f), new Vector3(1.55f, 1.20f, 1.30f), 64);
            Ledge(o + new Vector3(6.35f, 3.46f, 8.10f), new Vector3(1.40f, 1.10f, 1.22f), 79);
            Ledge(o + new Vector3(4.25f, 5.86f, 11.25f), new Vector3(1.20f, 0.92f, 1.08f), 91);
        }

        private static void BuildCastleLedge(Vector3 o)
        {
            Ledge(o + new Vector3(7.45f, 8.75f, 18.60f), new Vector3(3.30f, 2.75f, 2.55f), 111);
        }

        private static void Ledge(Vector3 center, Vector3 scale, int seed)
        {
            Mesh rock = CreateIrregularLedgeMesh(scale, seed);
            GameObject body = MeshObject("Reference3 irregular cliff ledge", rock, _rock);
            body.transform.position = center;

            Mesh turf = CreateTurfCapMesh(scale, seed);
            GameObject cap = MeshObject("Reference3 grass turf cap", turf, _grass);
            cap.transform.position = center + Vector3.up * (scale.y * 0.5f + 0.035f);
            cap.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Mesh CreateIrregularLedgeMesh(Vector3 scale, int seed)
        {
            const int sides = 12;
            var vertices = new List<Vector3>(sides * 3);
            var triangles = new List<int>(sides * 12);

            for (int ring = 0; ring < 3; ring++)
            {
                float y = ring == 0 ? -scale.y * 0.50f : ring == 1 ? -scale.y * 0.05f : scale.y * 0.50f;
                float ringScale = ring == 0 ? 0.78f : ring == 1 ? 1.00f : 0.88f;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    float n = 0.88f + Hash(seed + i * 17) * 0.22f;
                    float squash = 0.92f + Hash(seed * 3 + i * 29) * 0.14f;
                    vertices.Add(new Vector3(
                        Mathf.Cos(a) * scale.x * 0.5f * ringScale * n,
                        y,
                        Mathf.Sin(a) * scale.z * 0.5f * ringScale * squash));
                }
            }

            for (int ring = 0; ring < 2; ring++)
            {
                int lower = ring * sides;
                int upper = (ring + 1) * sides;
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    triangles.Add(lower + i); triangles.Add(upper + i); triangles.Add(lower + j);
                    triangles.Add(lower + j); triangles.Add(upper + i); triangles.Add(upper + j);
                }
            }

            // Close the bottom so the silhouette remains solid when seen from the low camera.
            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, -scale.y * 0.50f, 0f));
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                triangles.Add(bottomCenter); triangles.Add(j); triangles.Add(i);
            }

            Mesh mesh = new Mesh { name = "Reference3 irregular ledge rock" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateTurfCapMesh(Vector3 scale, int seed)
        {
            const int sides = 12;
            var vertices = new List<Vector3>(sides + 1) { Vector3.zero };
            var triangles = new List<int>(sides * 3);

            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float n = 0.88f + Hash(seed + i * 17) * 0.22f;
                float squash = 0.92f + Hash(seed * 3 + i * 29) * 0.14f;
                vertices.Add(new Vector3(
                    Mathf.Cos(a) * scale.x * 0.5f * 0.90f * n,
                    0f,
                    Mathf.Sin(a) * scale.z * 0.5f * 0.90f * squash));
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                triangles.Add(0); triangles.Add(i + 1); triangles.Add(next + 1);
            }

            Mesh mesh = new Mesh { name = "Reference3 irregular turf cap" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BuildAttachedMossAndIvy(Vector3 o)
        {
            if (_moss == null) return;

            // Moss cushions attached to the left ruin shoulders.
            MossPatch(o + new Vector3(-7.08f, 4.92f, 5.22f), 0.56f, 7);
            MossPatch(o + new Vector3(-4.18f, 4.54f, 5.25f), 0.48f, 17);
            MossPatch(o + new Vector3(-7.42f, 1.47f, 8.24f), 0.34f, 27);

            // Two short ivy trails hang directly from the masonry rather than floating in space.
            IvyTrail(o + new Vector3(-7.10f, 4.70f, 5.28f), 1.55f, 1);
            IvyTrail(o + new Vector3(-4.18f, 4.30f, 5.30f), 1.08f, 2);

            // Wet moss around waterfall lips.
            MossPatch(o + new Vector3(3.45f, 2.08f, 6.12f), 0.42f, 41);
            MossPatch(o + new Vector3(5.95f, 4.30f, 9.16f), 0.36f, 51);
            MossPatch(o + new Vector3(4.82f, 6.48f, 12.12f), 0.31f, 61);
        }

        private static void MossPatch(Vector3 p, float size, int seed)
        {
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.31f + seed * 0.07f;
                Blob(p + new Vector3(Mathf.Cos(a) * size * 0.32f,
                                     (i % 2) * size * 0.035f,
                                     Mathf.Sin(a) * size * 0.18f),
                    new Vector3(size * (0.54f + 0.10f * (i % 2)), size * 0.18f, size * 0.38f),
                    _moss, "Reference3 moss patch");
            }
        }

        private static void IvyTrail(Vector3 start, float length, int seed)
        {
            Vector3 previous = start;
            const int links = 6;
            for (int i = 1; i <= links; i++)
            {
                float t = i / (float)links;
                float sway = Mathf.Sin((seed + i) * 1.7f) * 0.13f;
                Vector3 next = start + new Vector3(sway, -length * t, 0.02f * i);
                Capsule(previous, next, 0.022f, _moss, "Reference3 ivy vine");
                Blob(next + new Vector3((i % 2 == 0 ? 1f : -1f) * 0.055f, 0f, 0f),
                    new Vector3(0.12f, 0.045f, 0.075f), _moss, "Reference3 ivy leaf");
                previous = next;
            }
        }

        private static void BuildGardenFlowers(Vector3 o)
        {
            FlowerPatch(o + new Vector3(-5.50f, 0.10f, -0.25f), 1);
            FlowerPatch(o + new Vector3(-2.65f, 0.14f, 0.35f), 2);
            FlowerPatch(o + new Vector3(0.20f, 0.16f, 0.18f), 3);
            FlowerPatch(o + new Vector3(2.35f, 0.18f, 1.20f), 4);

            // Small flowers on two waterfall lawns echo the target's bright edge dressing.
            FlowerPatch(o + new Vector3(3.02f, 2.12f, 6.62f), 5);
            FlowerPatch(o + new Vector3(5.92f, 4.34f, 9.72f), 6);
        }

        private static void FlowerPatch(Vector3 p, int seed)
        {
            Material[] petals = { _white, _pink, _yellow, _blue };
            for (int i = 0; i < 5; i++)
            {
                float ox = (Hash(seed * 41 + i * 13) - 0.5f) * 0.72f;
                float oz = (Hash(seed * 67 + i * 19) - 0.5f) * 0.54f;
                Vector3 q = p + new Vector3(ox, 0f, oz);
                float h = 0.22f + 0.05f * (i % 3);
                Capsule(q, q + Vector3.up * h, 0.011f, _moss, "Reference3 flower stem");

                Material petal = petals[(seed + i) % petals.Length];
                for (int j = 0; j < 5; j++)
                {
                    float a = j * Mathf.PI * 2f / 5f;
                    Blob(q + Vector3.up * h + new Vector3(Mathf.Cos(a) * 0.050f, 0f, Mathf.Sin(a) * 0.050f),
                        new Vector3(0.056f, 0.018f, 0.038f), petal, "Reference3 flower petal");
                }
                Blob(q + Vector3.up * (h + 0.008f), new Vector3(0.035f, 0.022f, 0.035f),
                    _yellow, "Reference3 flower center");
            }
        }

        private static GameObject MeshObject(string name, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            return go;
        }

        private static GameObject Blob(Vector3 p, Vector3 scale, Material mat, string name)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            q.name = name;
            q.transform.SetParent(_root, false);
            q.transform.position = p;
            q.transform.localScale = scale;
            Collider c = q.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            q.GetComponent<Renderer>().sharedMaterial = mat;
            return q;
        }

        private static void Capsule(Vector3 a, Vector3 b, float radius, Material mat, string name)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 0.000001f) return;
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            q.name = name;
            q.transform.SetParent(_root, false);
            q.transform.position = (a + b) * 0.5f;
            q.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            q.transform.localScale = new Vector3(radius * 2f, d.magnitude * 0.5f, radius * 2f);
            Collider c = q.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            q.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Material Mat(Shader shader, string name, Color colour)
        {
            Material m = new Material(shader) { name = name };
            m.SetTexture("_MainTex", Texture2D.whiteTexture);
            m.SetColor("_BaseColor", colour);
            m.SetColor("_SecondaryColor", colour);
            m.SetColor("_TopColor", colour);
            m.SetColor("_EmissionColor", Color.black);
            m.SetFloat("_TextureScale", 0.28f);
            m.SetFloat("_TextureStrength", 0.22f);
            m.SetFloat("_DetailScale", 0.08f);
            m.SetFloat("_DetailStrength", 0.05f);
            m.SetFloat("_TopStrength", 0f);
            m.SetFloat("_RimStrength", 0.045f);
            m.SetFloat("_SurfaceKind", 0f);
            m.SetFloat("_Cull", 2f);
            m.SetFloat("_ZWrite", 1f);
            return m;
        }

        private static float Hash(int n)
        {
            uint x = (uint)n;
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return (x & 0x00ffffffu) / 16777215f;
        }
    }
}
