using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Replaces accumulated lookdev clutter with a deliberately staged environment that mirrors
    /// the reference painting: overgrown ruin arches on the left, an open central garden island,
    /// a visible stepped waterfall valley on the right, a small pale castle in the distance, a
    /// leafy upper-left frame, bright clouds, and a luminous turquoise foreground channel.
    /// </summary>
    internal static class SunlitWaterfallReferencePass
    {
        private static bool _done;
        private static Transform _root;
        private static Material _stone, _rock, _turf, _moss, _water, _fall, _foam, _leaf, _bark, _roof, _cloud, _haze;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            GameObject scene = GameObject.Find("Sunlit Coherent Terrain Scene");
            if (scene == null) return;
            _root = scene.transform;

            Vector3 o = camera.transform.position - new Vector3(0.35f, 5.9f, -21.8f);
            BuildMaterials();
            HideAccumulatedPrototypeGeometry();
            BuildSky(o);
            BuildClouds(o);
            BuildLeftRuinGarden(o);
            BuildWaterfallValley(o);
            BuildCastleHill(o);
            BuildUpperLeftTree(o);
            BuildForegroundGarden(o);
            Reframe(camera, o);
        }

        private static void BuildMaterials()
        {
            Shader smooth = Shader.Find("VoxelEngine/SunlitSmooth");
            if (smooth == null) return;
            _stone = Mat(smooth, "Reference ruin stone", new Color(0.88f, 0.82f, 0.69f));
            _rock = Mat(smooth, "Reference cliff rock", new Color(0.46f, 0.48f, 0.31f));
            _turf = Mat(smooth, "Reference turf ledge", new Color(0.54f, 0.71f, 0.22f));
            _moss = Mat(smooth, "Reference moss cushion", new Color(0.37f, 0.58f, 0.15f));
            _water = Mat(smooth, "Reference turquoise water pool", new Color(0.03f, 0.62f, 0.84f, 0.90f), true);
            _fall = Mat(smooth, "Reference waterfall cascade", new Color(0.58f, 0.89f, 0.98f, 0.88f), true);
            _foam = Mat(smooth, "Reference waterfall foam", new Color(0.97f, 0.995f, 1.0f, 0.82f), true);
            _leaf = Mat(smooth, "Reference leaf canopy", new Color(0.34f, 0.57f, 0.14f));
            _bark = Mat(smooth, "Reference bark trunk", new Color(0.34f, 0.22f, 0.11f));
            _roof = Mat(smooth, "Reference roof spire", new Color(0.44f, 0.47f, 0.56f));
            _cloud = Mat(smooth, "Reference cloud", new Color(0.99f, 0.99f, 0.97f));
            _haze = Mat(smooth, "Reference haze ridge", new Color(0.42f, 0.61f, 0.53f));
        }

        private static void HideAccumulatedPrototypeGeometry()
        {
            Renderer[] renderers = _root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                string n = renderer.gameObject.name.ToLowerInvariant();
                if (n.StartsWith("reference ")) continue;

                if (ContainsAny(n,
                    "distant mist mountain", "midground green ridge",
                    "hero ruin", "broken ruin crown", "upper ruin",
                    "rounded vertical cliff", "grassy cliff crown",
                    "slender valley tree", "bright waterfall foam",
                    "warm chunky masonry", "style arch", "final arch", "rebuilt arch",
                    "distant pale tower", "distant spire", "final castle", "rebuilt castle",
                    "story book castle", "storybook castle",
                    "coherent waterfall", "waterfall ribbon", "waterfall foam",
                    "terrace water", "shared turquoise channel", "water pool",
                    "rounded storybook canopy", "final oak", "main oak trunk",
                    "storybook cloud"))
                {
                    renderer.gameObject.SetActive(false);
                }
            }
        }

        private static void BuildSky(Vector3 o)
        {
            Shader skyShader = Shader.Find("VoxelEngine/SunlitSkyGradient");
            if (skyShader == null) return;
            Material sky = new Material(skyShader) { name = "Reference gradient sky" };
            sky.SetColor("_BottomColor", new Color(0.54f, 0.82f, 0.97f));
            sky.SetColor("_HorizonColor", new Color(0.31f, 0.68f, 0.94f));
            sky.SetColor("_TopColor", new Color(0.12f, 0.46f, 0.85f));

            GameObject q = Primitive(PrimitiveType.Quad, "Reference gradient sky", sky, false);
            q.transform.position = o + new Vector3(0f, 15.0f, 58f);
            q.transform.localScale = new Vector3(58f, 47f, 1f);
        }

        private static void BuildClouds(Vector3 o)
        {
            if (_cloud == null) return;
            Cloud(o + new Vector3(-1.2f, 11.0f, 31f), new Vector3(4.4f, 2.4f, 1.6f));
            Cloud(o + new Vector3(9.4f, 11.5f, 33f), new Vector3(3.9f, 2.2f, 1.4f));
            Cloud(o + new Vector3(-8.0f, 7.7f, 28f), new Vector3(2.8f, 1.5f, 1.2f));

            if (_haze != null)
            {
                Blob(o + new Vector3(-1.8f, 3.3f, 24f), new Vector3(5.0f, 3.0f, 2.4f), _haze, "Reference haze ridge");
                Blob(o + new Vector3(9.6f, 3.8f, 25f), new Vector3(4.5f, 3.4f, 2.3f), _haze, "Reference haze ridge");
            }
        }

        private static void BuildLeftRuinGarden(Vector3 o)
        {
            if (_stone == null) return;
            BuildArch(o + new Vector3(-6.3f, 2.1f, 5.1f), 1.55f, 7, 0.58f, 0.52f, 0.82f, true);
            BuildArch(o + new Vector3(-6.9f, 0.55f, 8.3f), 0.83f, 4, 0.40f, 0.38f, 0.58f, false);

            // Broken garden wall stepping toward the viewer.
            for (int i = 0; i < 7; i++)
            {
                float x = -8.2f + i * 0.78f;
                float y = -0.10f + (i % 3) * 0.10f;
                Cube(o + new Vector3(x, y, -1.2f + (i % 2) * 0.18f),
                    new Vector3(0.72f, 0.48f + (i % 2) * 0.12f, 0.72f), _stone,
                    (i % 2 == 0 ? -4f : 4f), "Reference ruin stone block");
            }

            MossCluster(o + new Vector3(-7.3f, 5.5f, 4.8f), 0.78f);
            MossCluster(o + new Vector3(-5.0f, 4.7f, 4.9f), 0.62f);
            MossCluster(o + new Vector3(-7.4f, 1.7f, 8.0f), 0.48f);
            FlowerPatch(o + new Vector3(-7.6f, 6.0f, 4.6f), 0);
            FlowerPatch(o + new Vector3(-5.2f, 5.2f, 4.8f), 1);
        }

        private static void BuildArch(Vector3 c, float halfOpening, int rows, float blockW, float blockH, float depth, bool broken)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < rows; row++)
                {
                    if (broken && side > 0 && row == rows - 1) continue;
                    Vector3 p = c + new Vector3(side * halfOpening, row * blockH, (row % 2) * 0.035f);
                    Cube(p, new Vector3(blockW, blockH * 0.94f, depth), _stone,
                        side * (row % 2 == 0 ? 1.5f : -1.0f), "Reference ruin stone pier");
                }
            }

            float archY = rows * blockH - blockH * 0.22f;
            const int segments = 11;
            for (int i = 0; i <= segments; i++)
            {
                if (broken && (i == 1 || i == 9)) continue;
                float a = Mathf.Lerp(Mathf.PI, 0f, i / (float)segments);
                Vector3 p = c + new Vector3(Mathf.Cos(a) * halfOpening, archY + Mathf.Sin(a) * halfOpening, 0f);
                GameObject q = Cube(p, new Vector3(blockW * 1.02f, blockH * 0.92f, depth), _stone, 0f, "Reference ruin stone arch");
                q.transform.rotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg + 90f);
            }

            if (broken)
            {
                Cube(c + new Vector3(-halfOpening, rows * blockH + halfOpening * 0.95f, 0f),
                    new Vector3(blockW * 1.05f, 0.72f, depth), _stone, -4f, "Reference broken ruin crown");
                Cube(c + new Vector3(-halfOpening + 0.10f, rows * blockH + halfOpening * 1.35f, 0f),
                    new Vector3(blockW * 0.92f, 0.58f, depth * 0.92f), _stone, 6f, "Reference broken ruin crown");
            }
        }

        private static void BuildWaterfallValley(Vector3 o)
        {
            if (_rock == null || _turf == null || _water == null || _fall == null) return;

            // Four overlapping garden cliffs make one readable diagonal valley rather than four
            // disconnected pancakes. Each turf skin intersects its rock body instead of hovering.
            Cliff(o + new Vector3(4.2f, 0.75f, 6.1f), new Vector3(4.1f, 2.6f, 3.0f));
            Cliff(o + new Vector3(4.9f, 2.9f, 9.2f), new Vector3(3.8f, 2.8f, 2.8f));
            Cliff(o + new Vector3(5.6f, 5.1f, 12.2f), new Vector3(3.45f, 2.8f, 2.65f));
            Cliff(o + new Vector3(6.1f, 7.15f, 15.0f), new Vector3(3.05f, 2.65f, 2.45f));

            Pool(o + new Vector3(3.7f, 0.40f, 3.7f), 3.3f, 1.65f, "Reference turquoise water pool");
            Pool(o + new Vector3(4.4f, 2.17f, 6.75f), 2.25f, 1.05f, "Reference turquoise water shelf");
            Pool(o + new Vector3(5.0f, 4.30f, 9.78f), 2.05f, 0.95f, "Reference turquoise water shelf");
            Pool(o + new Vector3(5.6f, 6.42f, 12.75f), 1.82f, 0.86f, "Reference turquoise water shelf");
            Pool(o + new Vector3(6.1f, 8.42f, 15.42f), 1.62f, 0.78f, "Reference turquoise water shelf");

            Fall(o + new Vector3(4.45f, 2.24f, 6.20f), o + new Vector3(4.00f, 0.52f, 4.48f), 1.85f);
            Fall(o + new Vector3(5.05f, 4.38f, 9.28f), o + new Vector3(4.55f, 2.28f, 7.05f), 1.62f);
            Fall(o + new Vector3(5.65f, 6.50f, 12.22f), o + new Vector3(5.10f, 4.42f, 10.05f), 1.42f);
            Fall(o + new Vector3(6.14f, 8.48f, 14.92f), o + new Vector3(5.70f, 6.54f, 13.02f), 1.22f);

            MossCluster(o + new Vector3(2.8f, 1.5f, 5.0f), 0.85f);
            MossCluster(o + new Vector3(6.2f, 3.7f, 8.0f), 0.68f);
            MossCluster(o + new Vector3(4.2f, 5.9f, 11.1f), 0.58f);
            MossCluster(o + new Vector3(7.0f, 7.7f, 14.0f), 0.52f);
        }

        private static void Cliff(Vector3 p, Vector3 scale)
        {
            Blob(p, scale, _rock, "Reference cliff rock");
            Blob(p + new Vector3(-0.08f, scale.y * 0.43f, -0.05f),
                new Vector3(scale.x * 0.92f, scale.y * 0.42f, scale.z * 0.91f), _turf, "Reference turf ledge");
        }

        private static void BuildCastleHill(Vector3 o)
        {
            Cliff(o + new Vector3(7.5f, 8.8f, 18.5f), new Vector3(3.5f, 3.1f, 2.7f));
            Vector3 b = o + new Vector3(7.35f, 10.4f, 17.7f);
            Tower(b, 0.52f, 3.9f);
            Tower(b + new Vector3(-1.02f, -0.20f, 0.35f), 0.36f, 2.6f);
            Tower(b + new Vector3(0.92f, -0.42f, 0.52f), 0.34f, 2.3f);
            Cube(b + new Vector3(0f, 0.85f, 0.20f), new Vector3(1.55f, 1.65f, 1.0f), _stone, 0f, "Reference castle hall stone");

            // Small castle-side fall mirrors the reference landmark without dominating the valley.
            Fall(o + new Vector3(7.5f, 9.4f, 16.6f), o + new Vector3(7.0f, 7.5f, 15.0f), 0.92f);
        }

        private static void Tower(Vector3 basePos, float radius, float height)
        {
            GameObject tower = Primitive(PrimitiveType.Cylinder, "Reference castle tower stone", _stone, true);
            tower.transform.position = basePos + Vector3.up * height * 0.5f;
            tower.transform.localScale = new Vector3(radius, height * 0.5f, radius);

            GameObject roof = Primitive(PrimitiveType.Cylinder, "Reference roof spire", _roof, true);
            roof.transform.position = basePos + Vector3.up * (height + 0.46f);
            roof.transform.localScale = new Vector3(radius * 1.20f, 0.55f, radius * 1.20f);
            // Taper by stacking two smaller roof drums; at this distance it reads as a pointed spire.
            GameObject tip = Primitive(PrimitiveType.Cylinder, "Reference roof spire", _roof, true);
            tip.transform.position = basePos + Vector3.up * (height + 1.02f);
            tip.transform.localScale = new Vector3(radius * 0.52f, 0.24f, radius * 0.52f);
        }

        private static void BuildUpperLeftTree(Vector3 o)
        {
            if (_bark == null || _leaf == null) return;
            Vector3 trunkA = o + new Vector3(-9.4f, 2.2f, 6.5f);
            Vector3 trunkB = o + new Vector3(-9.0f, 9.2f, 6.7f);
            Capsule(trunkA, trunkB, 0.50f, _bark, "Reference bark trunk");
            Capsule(o + new Vector3(-9.0f, 7.5f, 6.7f), o + new Vector3(-6.6f, 9.0f, 6.9f), 0.28f, _bark, "Reference bark branch");
            Capsule(o + new Vector3(-9.0f, 8.1f, 6.7f), o + new Vector3(-10.7f, 10.1f, 6.7f), 0.25f, _bark, "Reference bark branch");

            Blob(o + new Vector3(-9.4f, 10.2f, 6.8f), new Vector3(3.0f, 2.0f, 2.0f), _leaf, "Reference leaf canopy");
            Blob(o + new Vector3(-7.3f, 9.9f, 6.8f), new Vector3(2.6f, 1.8f, 1.8f), _leaf, "Reference leaf canopy");
            Blob(o + new Vector3(-10.7f, 11.1f, 6.9f), new Vector3(2.4f, 1.7f, 1.7f), _leaf, "Reference leaf canopy");
            Blob(o + new Vector3(-8.4f, 11.7f, 6.9f), new Vector3(2.7f, 1.7f, 1.9f), _leaf, "Reference leaf canopy");
        }

        private static void BuildForegroundGarden(Vector3 o)
        {
            // Chunky pale blocks and flowers establish the close-up ruin language around the open
            // future character zone without placing any character proxy.
            for (int i = 0; i < 8; i++)
            {
                float x = -4.8f + i * 1.25f;
                float z = -3.0f + (i % 3) * 0.45f;
                Cube(o + new Vector3(x, -0.18f + (i % 2) * 0.06f, z),
                    new Vector3(0.72f + (i % 2) * 0.18f, 0.44f, 0.68f), _stone,
                    (i % 2 == 0 ? 3f : -4f), "Reference foreground ruin stone");
            }

            for (int i = 0; i < 10; i++)
            {
                float x = -5.6f + (i % 5) * 2.2f;
                float z = -1.0f + (i / 5) * 1.7f + (i % 2) * 0.25f;
                FlowerPatch(o + new Vector3(x, 0.08f, z), i + 4);
            }

            MossCluster(o + new Vector3(-5.0f, 0.10f, -1.6f), 0.62f);
            MossCluster(o + new Vector3(1.8f, 0.16f, -0.8f), 0.56f);
            MossCluster(o + new Vector3(5.5f, 0.25f, 2.6f), 0.68f);
        }

        private static void Reframe(Camera camera, Vector3 o)
        {
            camera.fieldOfView = 34.0f;
            camera.transform.position = o + new Vector3(0.15f, 5.1f, -22.4f);
            camera.transform.LookAt(o + new Vector3(-0.10f, 3.7f, 7.4f));
            camera.backgroundColor = new Color(0.29f, 0.68f, 0.94f, 1f);
        }

        private static void Pool(Vector3 pos, float rx, float rz, string name)
        {
            Mesh mesh = Ellipse(rx, rz, 56);
            GameObject go = MeshObject(name, mesh, _water);
            go.transform.position = pos;
        }

        private static void Fall(Vector3 top, Vector3 bottom, float width)
        {
            Mesh mesh = Ribbon(top, bottom, width, 14);
            MeshObject("Reference waterfall cascade", mesh, _fall);
            for (int i = 0; i < 7; i++)
            {
                float f = (i - 3) / 3f;
                Blob(bottom + new Vector3(f * width * 0.45f, 0.06f + (i % 2) * 0.04f, -0.05f),
                    new Vector3(width * 0.18f, 0.10f, 0.22f), _foam, "Reference waterfall foam");
            }
        }

        private static void Cloud(Vector3 centre, Vector3 scale)
        {
            Vector3[] offsets =
            {
                new Vector3(-0.95f,0f,0f), new Vector3(-0.45f,0.35f,0f), new Vector3(0.05f,0.52f,0f),
                new Vector3(0.58f,0.30f,0f), new Vector3(1.02f,-0.02f,0f), new Vector3(0.05f,-0.24f,0f)
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject puff = Blob(centre + Vector3.Scale(offsets[i], scale),
                    new Vector3(scale.x * 0.50f, scale.y * 0.56f, scale.z), _cloud, "Reference cloud puff");
                Renderer r = puff.GetComponent<Renderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void MossCluster(Vector3 p, float size)
        {
            if (_moss == null) return;
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.17f;
                Blob(p + new Vector3(Mathf.Cos(a) * size * 0.35f, (i % 2) * size * 0.05f, Mathf.Sin(a) * size * 0.28f),
                    new Vector3(size * 0.70f, size * 0.34f, size * 0.58f), _moss, "Reference moss cushion");
            }
        }

        private static void FlowerPatch(Vector3 p, int seed)
        {
            if (_moss == null) return;
            Material petal = seed % 3 == 0 ? _foam : (seed % 3 == 1 ? _stone : _fall);
            for (int i = 0; i < 4; i++)
            {
                float ox = (Hash(seed * 31 + i) - 0.5f) * 0.70f;
                float oz = (Hash(seed * 43 + i + 9) - 0.5f) * 0.58f;
                Vector3 q = p + new Vector3(ox, 0f, oz);
                float h = 0.22f + (i % 3) * 0.04f;
                Capsule(q, q + Vector3.up * h, 0.012f, _moss, "Reference flower stem");
                for (int j = 0; j < 5; j++)
                {
                    float a = j * Mathf.PI * 2f / 5f;
                    Blob(q + Vector3.up * h + new Vector3(Mathf.Cos(a) * 0.052f, 0f, Mathf.Sin(a) * 0.052f),
                        new Vector3(0.060f, 0.020f, 0.040f), petal, "Reference flower petal");
                }
            }
        }

        private static Material Mat(Shader shader, string name, Color colour, bool transparent = false)
        {
            Material m = new Material(shader) { name = name };
            m.SetTexture("_MainTex", Texture2D.whiteTexture);
            m.SetColor("_BaseColor", colour);
            m.SetColor("_SecondaryColor", colour);
            m.SetColor("_TopColor", colour);
            m.SetColor("_EmissionColor", Color.black);
            m.SetFloat("_TextureScale", 0.3f);
            m.SetFloat("_TextureStrength", 0.4f);
            m.SetFloat("_DetailScale", 0.09f);
            m.SetFloat("_DetailStrength", 0.05f);
            m.SetFloat("_TopStrength", 0f);
            m.SetFloat("_RimStrength", 0.04f);
            m.SetFloat("_SurfaceKind", 0f);
            m.SetFloat("_Cull", transparent ? 0f : 2f);
            m.SetFloat("_ZWrite", transparent ? 0f : 1f);
            m.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
            return m;
        }

        private static GameObject Cube(Vector3 p, Vector3 scale, Material mat, float yaw, string name)
        {
            GameObject q = Primitive(PrimitiveType.Cube, name, mat, true);
            q.transform.position = p;
            q.transform.localScale = scale;
            q.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return q;
        }

        private static GameObject Blob(Vector3 p, Vector3 scale, Material mat, string name)
        {
            GameObject q = Primitive(PrimitiveType.Sphere, name, mat, true);
            q.transform.position = p;
            q.transform.localScale = scale;
            return q;
        }

        private static void Capsule(Vector3 a, Vector3 b, float radius, Material mat, string name)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 0.0001f) return;
            GameObject q = Primitive(PrimitiveType.Capsule, name, mat, true);
            q.transform.position = (a + b) * 0.5f;
            q.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            q.transform.localScale = new Vector3(radius * 2f, d.magnitude * 0.5f, radius * 2f);
        }

        private static GameObject Primitive(PrimitiveType type, string name, Material mat, bool shadows)
        {
            GameObject q = GameObject.CreatePrimitive(type);
            q.name = name;
            q.transform.SetParent(_root, false);
            Collider c = q.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            Renderer r = q.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = shadows;
            return q;
        }

        private static GameObject MeshObject(string name, Mesh mesh, Material mat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Renderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = mat.renderQueue >= (int)RenderQueue.Transparent ? ShadowCastingMode.Off : ShadowCastingMode.On;
            r.receiveShadows = true;
            return go;
        }

        private static Mesh Ellipse(float rx, float rz, int segments)
        {
            Vector3[] v = new Vector3[segments + 1];
            Vector3[] n = new Vector3[segments + 1];
            Vector2[] uv = new Vector2[segments + 1];
            int[] t = new int[segments * 3];
            v[0] = Vector3.zero; n[0] = Vector3.up; uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(a), z = Mathf.Sin(a);
                v[i + 1] = new Vector3(x * rx, 0f, z * rz);
                n[i + 1] = Vector3.up;
                uv[i + 1] = new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f);
                int next = (i + 1) % segments;
                t[i * 3] = 0; t[i * 3 + 1] = i + 1; t[i * 3 + 2] = next + 1;
            }
            Mesh mesh = new Mesh { name = "Reference pool ellipse" };
            mesh.vertices = v; mesh.normals = n; mesh.uv = uv; mesh.triangles = t; mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Ribbon(Vector3 top, Vector3 bottom, float width, int segments)
        {
            Vector3[] v = new Vector3[(segments + 1) * 2];
            Vector3[] n = new Vector3[v.Length];
            Vector2[] uv = new Vector2[v.Length];
            int[] tri = new int[segments * 6];
            Vector3 d = bottom - top;
            Vector3 side = Vector3.Cross(d.normalized, Vector3.up);
            if (side.sqrMagnitude < 0.001f) side = Vector3.right;
            side.Normalize();
            for (int i = 0; i <= segments; i++)
            {
                float f = i / (float)segments;
                Vector3 c = Vector3.Lerp(top, bottom, f);
                c += side * (Mathf.Sin(f * 8f) * width * 0.035f);
                float w = width * (0.96f + Mathf.Sin(f * 5.5f) * 0.04f);
                int q = i * 2;
                v[q] = c - side * w * 0.5f;
                v[q + 1] = c + side * w * 0.5f;
                n[q] = Vector3.back; n[q + 1] = Vector3.back;
                uv[q] = new Vector2(0f, f * 3f); uv[q + 1] = new Vector2(1f, f * 3f);
                if (i < segments)
                {
                    int k = i * 6;
                    tri[k] = q; tri[k + 1] = q + 2; tri[k + 2] = q + 1;
                    tri[k + 3] = q + 1; tri[k + 4] = q + 2; tri[k + 5] = q + 3;
                }
            }
            Mesh mesh = new Mesh { name = "Reference waterfall ribbon" };
            mesh.vertices = v; mesh.normals = n; mesh.uv = uv; mesh.triangles = tri; mesh.RecalculateBounds();
            return mesh;
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            for (int i = 0; i < terms.Length; i++) if (value.Contains(terms[i])) return true;
            return false;
        }

        private static float Hash(int n)
        {
            unchecked
            {
                uint x = (uint)n;
                x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16;
                return (x & 0x00ffffff) / 16777215f;
            }
        }
    }
}
