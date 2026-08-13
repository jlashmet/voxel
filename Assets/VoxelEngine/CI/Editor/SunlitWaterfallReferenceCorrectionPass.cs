using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Second-stage reference correction. The first reference pass establishes the broad layout;
    /// this pass replaces the placeholder silhouettes that still read as primitives with reusable
    /// storybook forms: beveled ashlar, thin turf lips over irregular rock, overlapping cloud
    /// masses, and narrow pointed castle modules.
    /// </summary>
    internal static class SunlitWaterfallReferenceCorrectionPass
    {
        private static bool _done;
        private static Transform _root;
        private static Material _stone, _rock, _turf, _cloud, _roof;
        private static Mesh _beveledBox;
        private static Mesh _pyramid;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            GameObject scene = GameObject.Find("Sunlit Coherent Terrain Scene");
            if (scene == null) return;
            _root = scene.transform;

            // ReferencePass leaves the camera at this offset from its stable scene origin.
            Vector3 o = camera.transform.position - new Vector3(0.15f, 5.1f, -22.4f);
            BuildMaterials();
            HidePlaceholderReferenceForms();
            BuildBlueSky(camera, o);
            BuildSoftClouds(o);
            BuildBeveledRuins(o);
            BuildNaturalCascadeCliffs(o);
            BuildDistantCastle(o);
            Reframe(camera, o);
        }

        private static void BuildMaterials()
        {
            Shader smooth = Shader.Find("VoxelEngine/SunlitSmooth");
            if (smooth == null) return;
            _stone = Mat(smooth, "Reference2 beveled ruin stone", new Color(0.88f, 0.82f, 0.69f));
            _rock = Mat(smooth, "Reference2 cliff rock", new Color(0.45f, 0.47f, 0.31f));
            _turf = Mat(smooth, "Reference2 turf cap", new Color(0.54f, 0.71f, 0.22f));
            _cloud = Mat(smooth, "Reference2 cloud", new Color(0.99f, 0.995f, 1f));
            _roof = Mat(smooth, "Reference2 roof spire", new Color(0.43f, 0.47f, 0.58f));
            _beveledBox = CreateBeveledBoxMesh(0.105f);
            _pyramid = CreatePyramidMesh();
        }

        private static void HidePlaceholderReferenceForms()
        {
            // Remove both old lookdev skies and the first reference pass' primitive forms. The
            // water ribbons/pools, flowers, moss and framing tree are intentionally preserved.
            Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r.gameObject == null || r.gameObject.name.StartsWith("Reference2 ")) continue;
                string n = r.gameObject.name.ToLowerInvariant();
                if (n.Contains("sky") || n.Contains("reference cloud puff") || n.Contains("reference haze ridge") ||
                    n.Contains("reference ruin stone pier") || n.Contains("reference ruin stone arch") ||
                    n.Contains("reference broken ruin crown") || n.Contains("reference cliff rock") ||
                    n.Contains("reference turf ledge") || n.Contains("reference castle tower") ||
                    n.Contains("reference castle hall") || n.Contains("reference roof spire"))
                {
                    r.gameObject.SetActive(false);
                }
            }
        }

        private static void BuildBlueSky(Camera camera, Vector3 o)
        {
            Shader shader = Shader.Find("VoxelEngine/SunlitSkyGradient");
            if (shader == null) return;
            Material sky = new Material(shader) { name = "Reference2 blue sky" };
            sky.SetColor("_BottomColor", new Color(0.64f, 0.88f, 1.00f));
            sky.SetColor("_HorizonColor", new Color(0.34f, 0.73f, 0.98f));
            sky.SetColor("_TopColor", new Color(0.10f, 0.48f, 0.91f));

            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Reference2 blue sky";
            q.transform.SetParent(_root, false);
            q.transform.position = o + new Vector3(0f, 13.0f, 43f);
            q.transform.localScale = new Vector3(55f, 42f, 1f);
            q.transform.LookAt(camera.transform.position, Vector3.up);
            Collider c = q.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            Renderer renderer = q.GetComponent<Renderer>();
            renderer.sharedMaterial = sky;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void BuildSoftClouds(Vector3 o)
        {
            if (_cloud == null) return;
            Cloud(o + new Vector3(-1.8f, 10.4f, 22.0f), 2.4f, 1.20f);
            Cloud(o + new Vector3(7.5f, 11.1f, 24.0f), 2.15f, 1.05f);
            Cloud(o + new Vector3(-8.1f, 8.3f, 20.5f), 1.55f, 0.82f);
        }

        private static void Cloud(Vector3 c, float width, float height)
        {
            Vector3[] offsets =
            {
                new(-0.72f, -0.05f, 0f), new(-0.38f, 0.18f, 0f), new(0f, 0.28f, 0f),
                new(0.38f, 0.18f, 0f), new(0.72f, -0.04f, 0f), new(-0.12f, -0.18f, 0f),
                new(0.26f, -0.15f, 0f)
            };
            float[] size = { 0.82f, 1.00f, 1.10f, 0.98f, 0.80f, 0.92f, 0.86f };
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 p = c + new Vector3(offsets[i].x * width, offsets[i].y * height, 0f);
                GameObject puff = Blob(p,
                    new Vector3(width * 0.62f * size[i], height * 0.82f * size[i], 0.78f),
                    _cloud, "Reference2 cloud puff");
                Renderer r = puff.GetComponent<Renderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        private static void BuildBeveledRuins(Vector3 o)
        {
            if (_stone == null || _beveledBox == null) return;
            BuildArch(o + new Vector3(-5.65f, 1.45f, 5.25f), 1.48f, 6, 0.60f, 0.50f, 0.80f, true);
            BuildArch(o + new Vector3(-7.15f, 0.42f, 8.25f), 0.80f, 4, 0.41f, 0.37f, 0.58f, false);
        }

        private static void BuildArch(Vector3 c, float halfOpening, int rows,
                                      float blockW, float blockH, float depth, bool weathered)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < rows; row++)
                {
                    Vector3 p = c + new Vector3(side * halfOpening, row * blockH, (row % 2) * 0.025f);
                    BeveledBlock(p, new Vector3(blockW, blockH * 0.96f, depth),
                        side * (row % 2 == 0 ? 1.2f : -0.8f), "Reference2 beveled ruin pier");
                }
            }

            float archY = rows * blockH - blockH * 0.24f;
            const int segments = 13;
            for (int i = 0; i <= segments; i++)
            {
                // A single missing upper-right voussoir suggests age without destroying the arch.
                if (weathered && i == 11) continue;
                float a = Mathf.Lerp(Mathf.PI, 0f, i / (float)segments);
                Vector3 p = c + new Vector3(Mathf.Cos(a) * halfOpening,
                    archY + Mathf.Sin(a) * halfOpening, 0f);
                GameObject q = BeveledBlock(p,
                    new Vector3(blockW * 1.00f, blockH * 0.90f, depth), 0f,
                    "Reference2 beveled ruin arch");
                q.transform.rotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg + 90f);
            }

            if (weathered)
            {
                BeveledBlock(c + new Vector3(-halfOpening * 0.88f, archY + halfOpening * 1.02f, 0f),
                    new Vector3(blockW * 1.12f, 0.52f, depth), -4f, "Reference2 broken ruin crown");
                BeveledBlock(c + new Vector3(-halfOpening * 0.62f, archY + halfOpening * 1.27f, 0.02f),
                    new Vector3(blockW * 0.92f, 0.43f, depth * 0.93f), 7f, "Reference2 broken ruin crown");
            }
        }

        private static void BuildNaturalCascadeCliffs(Vector3 o)
        {
            if (_rock == null || _turf == null) return;
            RockTier(o + new Vector3(4.2f, 0.75f, 6.1f), new Vector3(4.05f, 2.60f, 3.00f), 0);
            RockTier(o + new Vector3(4.9f, 2.90f, 9.2f), new Vector3(3.72f, 2.80f, 2.78f), 1);
            RockTier(o + new Vector3(5.6f, 5.10f, 12.2f), new Vector3(3.35f, 2.80f, 2.62f), 2);
            RockTier(o + new Vector3(6.1f, 7.15f, 15.0f), new Vector3(2.95f, 2.65f, 2.42f), 3);
        }

        private static void RockTier(Vector3 p, Vector3 scale, int seed)
        {
            // Main body plus two lower side lobes gives a broken cliff silhouette instead of one
            // perfect ellipsoid. Grass is a thin lip at the actual top, not half the cliff volume.
            Blob(p, scale, _rock, "Reference2 cliff rock");
            float sign = seed % 2 == 0 ? 1f : -1f;
            Blob(p + new Vector3(sign * scale.x * 0.31f, -scale.y * 0.20f, -scale.z * 0.16f),
                new Vector3(scale.x * 0.52f, scale.y * 0.62f, scale.z * 0.58f),
                _rock, "Reference2 cliff rock shoulder");
            Blob(p + new Vector3(-sign * scale.x * 0.27f, -scale.y * 0.28f, scale.z * 0.13f),
                new Vector3(scale.x * 0.44f, scale.y * 0.52f, scale.z * 0.48f),
                _rock, "Reference2 cliff rock shoulder");

            float top = p.y + scale.y * 0.50f;
            Blob(new Vector3(p.x - 0.08f, top - 0.04f, p.z - 0.04f),
                new Vector3(scale.x * 0.86f, 0.34f, scale.z * 0.84f),
                _turf, "Reference2 thin turf cap");

            // Small broken grass shelves create the target's stepped, overgrown edge without a
            // repeated full-width pancake silhouette.
            Blob(new Vector3(p.x + sign * scale.x * 0.31f, top - 0.32f, p.z - scale.z * 0.12f),
                new Vector3(scale.x * 0.30f, 0.22f, scale.z * 0.34f),
                _turf, "Reference2 small turf shelf");
        }

        private static void BuildDistantCastle(Vector3 o)
        {
            if (_stone == null || _roof == null) return;
            Vector3 hill = o + new Vector3(7.45f, 8.75f, 18.6f);
            Blob(hill, new Vector3(2.55f, 2.35f, 2.10f), _rock, "Reference2 castle rock hill");
            Blob(hill + new Vector3(-0.08f, 1.14f, -0.02f),
                new Vector3(2.10f, 0.30f, 1.85f), _turf, "Reference2 castle turf cap");

            Vector3 b = o + new Vector3(7.30f, 9.95f, 17.85f);
            BeveledBlock(b + new Vector3(0f, 0.72f, 0.18f),
                new Vector3(1.50f, 1.45f, 0.90f), 0f, "Reference2 castle hall stone");
            Tower(b + new Vector3(0f, 0f, 0f), 0.56f, 3.55f);
            Tower(b + new Vector3(-0.92f, -0.12f, 0.32f), 0.39f, 2.38f);
            Tower(b + new Vector3(0.88f, -0.28f, 0.45f), 0.36f, 2.15f);
        }

        private static void Tower(Vector3 basePos, float width, float height)
        {
            BeveledBlock(basePos + Vector3.up * (height * 0.5f),
                new Vector3(width, height, width), 0f, "Reference2 castle tower stone");
            GameObject roof = MeshObject("Reference2 roof spire", _pyramid, _roof);
            roof.transform.position = basePos + Vector3.up * (height + 0.48f);
            roof.transform.localScale = new Vector3(width * 1.45f, 0.96f, width * 1.45f);
        }

        private static void Reframe(Camera camera, Vector3 o)
        {
            camera.fieldOfView = 36.5f;
            camera.transform.position = o + new Vector3(0.10f, 5.05f, -22.6f);
            camera.transform.LookAt(o + new Vector3(-0.52f, 3.55f, 7.15f));
            camera.backgroundColor = new Color(0.27f, 0.70f, 0.96f, 1f);
        }

        private static GameObject BeveledBlock(Vector3 p, Vector3 scale, float yaw, string name)
        {
            GameObject q = MeshObject(name, _beveledBox, _stone);
            q.transform.position = p;
            q.transform.localScale = scale;
            q.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return q;
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

        private static Material Mat(Shader shader, string name, Color colour)
        {
            Material m = new Material(shader) { name = name };
            m.SetTexture("_MainTex", Texture2D.whiteTexture);
            m.SetColor("_BaseColor", colour);
            m.SetColor("_SecondaryColor", colour);
            m.SetColor("_TopColor", colour);
            m.SetColor("_EmissionColor", Color.black);
            m.SetFloat("_TextureScale", 0.30f);
            m.SetFloat("_TextureStrength", 0.35f);
            m.SetFloat("_DetailScale", 0.08f);
            m.SetFloat("_DetailStrength", 0.06f);
            m.SetFloat("_TopStrength", 0f);
            m.SetFloat("_RimStrength", 0.05f);
            m.SetFloat("_SurfaceKind", 0f);
            m.SetFloat("_Cull", 2f);
            m.SetFloat("_ZWrite", 1f);
            return m;
        }

        private static Mesh CreatePyramidMesh()
        {
            Vector3[] v =
            {
                new(-0.5f,-0.5f,-0.5f), new(0.5f,-0.5f,-0.5f),
                new(0.5f,-0.5f,0.5f), new(-0.5f,-0.5f,0.5f), new(0f,0.5f,0f)
            };
            int[] t =
            {
                0,2,1, 0,3,2,
                0,1,4, 1,2,4, 2,3,4, 3,0,4
            };
            Mesh m = new Mesh { name = "Reference2 pointed roof" };
            m.vertices = v;
            m.triangles = t;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh CreateBeveledBoxMesh(float bevel)
        {
            bevel = Mathf.Clamp(bevel, 0.01f, 0.24f);
            float h = 0.5f;
            float inner = h - bevel;
            float[] coords = { -h, -inner, inner, h };
            var vertices = new List<Vector3>(96);
            var normals = new List<Vector3>(96);
            var uvs = new List<Vector2>(96);
            var triangles = new List<int>(324);

            AddFace(Vector3.right, Vector3.forward, Vector3.up, h, coords, inner, bevel, vertices, normals, uvs, triangles);
            AddFace(Vector3.left, Vector3.back, Vector3.up, h, coords, inner, bevel, vertices, normals, uvs, triangles);
            AddFace(Vector3.up, Vector3.right, Vector3.forward, h, coords, inner, bevel, vertices, normals, uvs, triangles);
            AddFace(Vector3.down, Vector3.right, Vector3.back, h, coords, inner, bevel, vertices, normals, uvs, triangles);
            AddFace(Vector3.forward, Vector3.left, Vector3.up, h, coords, inner, bevel, vertices, normals, uvs, triangles);
            AddFace(Vector3.back, Vector3.right, Vector3.up, h, coords, inner, bevel, vertices, normals, uvs, triangles);

            Mesh mesh = new Mesh { name = "Reference2 beveled ashlar block" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddFace(Vector3 normal, Vector3 axisU, Vector3 axisV, float h,
                                    float[] coords, float inner, float bevel,
                                    List<Vector3> vertices, List<Vector3> normals,
                                    List<Vector2> uvs, List<int> triangles)
        {
            int start = vertices.Count;
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    Vector3 p = normal * h + axisU * coords[x] + axisV * coords[y];
                    Vector3 q = new(
                        Mathf.Clamp(p.x, -inner, inner),
                        Mathf.Clamp(p.y, -inner, inner),
                        Mathf.Clamp(p.z, -inner, inner));
                    Vector3 d = p - q;
                    Vector3 n = d.sqrMagnitude > 0.000001f ? d.normalized : normal;
                    vertices.Add(q + n * bevel);
                    normals.Add(n);
                    uvs.Add(new Vector2(x / 3f, y / 3f));
                }
            }

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    int a = start + y * 4 + x;
                    int b = a + 1;
                    int c = a + 4;
                    int d = c + 1;
                    Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    if (Vector3.Dot(cross, normal) >= 0f)
                    {
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(b); triangles.Add(d); triangles.Add(c);
                    }
                    else
                    {
                        triangles.Add(a); triangles.Add(c); triangles.Add(b);
                        triangles.Add(b); triangles.Add(c); triangles.Add(d);
                    }
                }
            }
        }
    }
}
