using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Final macro-composition correction for the reference shot. The waterfall becomes a broad
    /// diagonal valley rather than a vertical rock tower, and the castle moves onto a separate,
    /// distant hill so it reads as background architecture like the concept art.
    /// </summary>
    internal static class SunlitWaterfallCompositionPass
    {
        private static bool _done;
        private static Transform _root;
        private static Material _rock, _turf, _water, _fall, _foam, _stone, _roof, _moss;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            GameObject scene = GameObject.Find("Sunlit Coherent Terrain Scene");
            if (scene == null) return;
            _root = scene.transform;

            Vector3 o = camera.transform.position - new Vector3(0.10f, 5.05f, -22.6f);
            BuildMaterials();
            HideOldRightComposition();
            BuildWaterfallValley(o);
            BuildDistantCastle(o);
            BuildDistantFloatingGarden(o);
        }

        private static void BuildMaterials()
        {
            Shader shader = Shader.Find("VoxelEngine/SunlitSmooth");
            if (shader == null) return;
            _rock = Mat(shader, "Reference4 cliff rock", new Color(0.47f, 0.48f, 0.31f));
            _turf = Mat(shader, "Reference4 grass turf", new Color(0.55f, 0.72f, 0.23f));
            _moss = Mat(shader, "Reference4 moss", new Color(0.36f, 0.58f, 0.14f));
            _stone = Mat(shader, "Reference4 castle stone", new Color(0.86f, 0.84f, 0.74f));
            _roof = Mat(shader, "Reference4 roof spire", new Color(0.43f, 0.47f, 0.58f));
            _water = Mat(shader, "Reference4 turquoise water", new Color(0.04f, 0.52f, 0.72f, 0.90f), true, 4f);
            _fall = Mat(shader, "Reference4 waterfall cascade", new Color(0.60f, 0.88f, 0.97f, 0.88f), true, 5f);
            _foam = Mat(shader, "Reference4 waterfall foam", new Color(0.98f, 0.995f, 1f, 0.82f), true, 6f);
        }

        private static void HideOldRightComposition()
        {
            Renderer[] renderers = _root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                string n = r.gameObject.name.ToLowerInvariant();
                if (n.StartsWith("reference4 ")) continue;

                if (n.Contains("reference3 irregular cliff ledge") ||
                    n.Contains("reference3 grass turf cap") ||
                    n.Contains("reference2 castle tower stone") ||
                    n.Contains("reference2 castle hall stone") ||
                    n.Contains("reference2 roof spire") ||
                    n.Contains("reference waterfall cascade") ||
                    n.Contains("reference waterfall foam") ||
                    n.Contains("reference turquoise water pool") ||
                    n.Contains("reference turquoise water shelf"))
                {
                    r.gameObject.SetActive(false);
                }
            }
        }

        private static void BuildWaterfallValley(Vector3 o)
        {
            // Broad, overlapping ledges climb diagonally into the distance. Elevation increases
            // gently so the valley reads as landscape rather than a stack of separate islands.
            Ledge(o + new Vector3(4.15f, 0.55f, 6.40f), new Vector3(5.20f, 2.10f, 3.90f), 13);
            Ledge(o + new Vector3(4.90f, 1.75f, 9.70f), new Vector3(4.85f, 2.00f, 3.70f), 29);
            Ledge(o + new Vector3(5.55f, 2.95f, 13.10f), new Vector3(4.45f, 1.95f, 3.45f), 47);
            Ledge(o + new Vector3(6.15f, 4.08f, 16.55f), new Vector3(4.05f, 1.85f, 3.20f), 67);

            // Water shelves lie slightly inside the grassy caps so a ring of turf remains visible.
            Pool(o + new Vector3(3.95f, 1.64f, 6.55f), 2.05f, 1.18f);
            Pool(o + new Vector3(4.72f, 2.80f, 9.86f), 1.88f, 1.04f);
            Pool(o + new Vector3(5.40f, 3.96f, 13.25f), 1.70f, 0.95f);
            Pool(o + new Vector3(6.02f, 5.06f, 16.70f), 1.48f, 0.84f);

            Fall(o + new Vector3(6.02f, 5.08f, 16.05f), o + new Vector3(5.48f, 4.02f, 13.95f), 1.12f);
            Fall(o + new Vector3(5.42f, 3.98f, 12.62f), o + new Vector3(4.82f, 2.86f, 10.55f), 1.34f);
            Fall(o + new Vector3(4.78f, 2.82f, 9.18f), o + new Vector3(4.12f, 1.70f, 7.12f), 1.52f);
            Fall(o + new Vector3(4.08f, 1.66f, 5.92f), o + new Vector3(3.35f, 0.48f, 4.25f), 1.70f);

            MossPatch(o + new Vector3(3.05f, 1.55f, 6.15f), 0.54f, 9);
            MossPatch(o + new Vector3(5.92f, 2.73f, 9.55f), 0.45f, 19);
            MossPatch(o + new Vector3(4.30f, 3.89f, 13.02f), 0.40f, 31);
            MossPatch(o + new Vector3(6.86f, 4.98f, 16.38f), 0.35f, 43);
        }

        private static void BuildDistantCastle(Vector3 o)
        {
            // Separate hill, farther back and higher in frame. The architecture is deliberately
            // narrow: one dominant tower with two secondary spires, matching the reference's
            // distant fairytale silhouette without competing with the ruin arch.
            Vector3 hill = o + new Vector3(8.25f, 5.65f, 25.2f);
            Ledge(hill, new Vector3(4.20f, 2.30f, 3.35f), 103);

            Vector3 b = o + new Vector3(8.15f, 6.90f, 24.55f);
            CastleBlock(b + new Vector3(0f, 0.65f, 0f), new Vector3(1.35f, 1.30f, 0.82f));
            Tower(b + new Vector3(0.10f, 0f, 0f), 0.52f, 3.85f);
            Tower(b + new Vector3(-0.90f, -0.10f, 0.30f), 0.34f, 2.55f);
            Tower(b + new Vector3(0.88f, -0.28f, 0.38f), 0.31f, 2.15f);

            // Tiny lower structures soften the transition from hill to tower.
            CastleBlock(b + new Vector3(-1.10f, 0.20f, 0.16f), new Vector3(0.62f, 0.72f, 0.60f));
            CastleBlock(b + new Vector3(1.02f, 0.08f, 0.22f), new Vector3(0.55f, 0.60f, 0.55f));
        }

        private static void BuildDistantFloatingGarden(Vector3 o)
        {
            // Small floating garden between the arch and castle gives the upper middle distance a
            // fantasy landmark present in the target without filling the central character space.
            Vector3 p = o + new Vector3(2.15f, 7.45f, 22.4f);
            Mesh island = CreateIrregularLedgeMesh(new Vector3(1.55f, 1.65f, 1.30f), 151);
            GameObject body = MeshObject("Reference4 floating garden rock", island, _rock);
            body.transform.position = p;

            Mesh capMesh = CreateTurfCapMesh(new Vector3(1.55f, 1.65f, 1.30f), 151);
            GameObject cap = MeshObject("Reference4 floating garden grass", capMesh, _turf);
            cap.transform.position = p + Vector3.up * 0.86f;
            cap.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Reference4 floating garden trunk";
            trunk.transform.SetParent(_root, false);
            trunk.transform.position = p + new Vector3(0f, 1.40f, 0f);
            trunk.transform.localScale = new Vector3(0.11f, 0.56f, 0.11f);
            RemoveCollider(trunk);
            trunk.GetComponent<Renderer>().sharedMaterial = _stone;

            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Blob(p + new Vector3(Mathf.Cos(a) * 0.38f, 2.00f + (i % 2) * 0.18f, Mathf.Sin(a) * 0.25f),
                    new Vector3(0.62f, 0.45f, 0.52f), _moss, "Reference4 floating garden foliage");
            }
        }

        private static void Ledge(Vector3 center, Vector3 scale, int seed)
        {
            Mesh bodyMesh = CreateIrregularLedgeMesh(scale, seed);
            GameObject body = MeshObject("Reference4 irregular cliff ledge", bodyMesh, _rock);
            body.transform.position = center;

            Mesh capMesh = CreateTurfCapMesh(scale, seed);
            GameObject cap = MeshObject("Reference4 grass turf cap", capMesh, _turf);
            cap.transform.position = center + Vector3.up * (scale.y * 0.5f + 0.035f);
            cap.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            if (cap.GetComponent<Renderer>().sharedMaterial.HasProperty("_Cull"))
                cap.GetComponent<Renderer>().sharedMaterial.SetFloat("_Cull", 0f);
        }

        private static Mesh CreateIrregularLedgeMesh(Vector3 scale, int seed)
        {
            const int sides = 14;
            var vertices = new List<Vector3>(sides * 3 + 1);
            var triangles = new List<int>(sides * 15);

            for (int ring = 0; ring < 3; ring++)
            {
                float y = ring == 0 ? -scale.y * 0.50f : ring == 1 ? -scale.y * 0.05f : scale.y * 0.50f;
                float ringScale = ring == 0 ? 0.76f : ring == 1 ? 1.00f : 0.90f;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    float radial = 0.88f + Hash(seed + i * 17) * 0.20f;
                    float depth = 0.92f + Hash(seed * 5 + i * 23) * 0.13f;
                    vertices.Add(new Vector3(
                        Mathf.Cos(a) * scale.x * 0.5f * ringScale * radial,
                        y,
                        Mathf.Sin(a) * scale.z * 0.5f * ringScale * depth));
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

            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, -scale.y * 0.50f, 0f));
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                triangles.Add(bottomCenter); triangles.Add(j); triangles.Add(i);
            }

            Mesh mesh = new Mesh { name = "Reference4 irregular ledge rock" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateTurfCapMesh(Vector3 scale, int seed)
        {
            const int sides = 14;
            var vertices = new List<Vector3>(sides + 1) { Vector3.zero };
            var triangles = new List<int>(sides * 3);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float radial = 0.88f + Hash(seed + i * 17) * 0.20f;
                float depth = 0.92f + Hash(seed * 5 + i * 23) * 0.13f;
                vertices.Add(new Vector3(
                    Mathf.Cos(a) * scale.x * 0.5f * 0.92f * radial,
                    0f,
                    Mathf.Sin(a) * scale.z * 0.5f * 0.92f * depth));
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                // Reversed relative to the first version so normals point upward.
                triangles.Add(0); triangles.Add(next + 1); triangles.Add(i + 1);
            }

            Mesh mesh = new Mesh { name = "Reference4 turf cap" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Pool(Vector3 pos, float rx, float rz)
        {
            const int segments = 56;
            var vertices = new List<Vector3>(segments + 1) { Vector3.zero };
            var triangles = new List<int>(segments * 3);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(Mathf.Cos(a) * rx, 0f, Mathf.Sin(a) * rz));
            }
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                triangles.Add(0); triangles.Add(next + 1); triangles.Add(i + 1);
            }
            Mesh mesh = new Mesh { name = "Reference4 water pool" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            GameObject go = MeshObject("Reference4 turquoise water pool", mesh, _water);
            go.transform.position = pos;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Fall(Vector3 top, Vector3 bottom, float width)
        {
            Vector3 d = bottom - top;
            Vector3 right = Vector3.Cross(d.normalized, Vector3.forward);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right.Normalize();

            const int steps = 14;
            var vertices = new List<Vector3>((steps + 1) * 2);
            var triangles = new List<int>(steps * 6);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 c = Vector3.Lerp(top, bottom, t);
                float wave = Mathf.Sin(t * Mathf.PI * 3f) * width * 0.035f;
                c += Vector3.forward * wave;
                float w = width * (0.96f - 0.08f * t);
                vertices.Add(c - right * w * 0.5f);
                vertices.Add(c + right * w * 0.5f);
            }
            for (int i = 0; i < steps; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 2;
                int d0 = a + 3;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d0);
            }
            Mesh mesh = new Mesh { name = "Reference4 waterfall ribbon" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            GameObject ribbon = MeshObject("Reference4 waterfall cascade", mesh, _fall);
            ribbon.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            for (int i = 0; i < 8; i++)
            {
                float f = (i - 3.5f) / 3.5f;
                Blob(bottom + new Vector3(f * width * 0.42f, 0.05f + (i % 2) * 0.035f, 0f),
                    new Vector3(width * 0.15f, 0.09f, 0.18f), _foam, "Reference4 waterfall foam");
            }
        }

        private static void Tower(Vector3 basePos, float width, float height)
        {
            CastleBlock(basePos + Vector3.up * height * 0.5f, new Vector3(width, height, width));
            GameObject roof = MeshObject("Reference4 roof spire", CreatePyramidMesh(), _roof);
            roof.transform.position = basePos + Vector3.up * (height + 0.42f);
            roof.transform.localScale = new Vector3(width * 1.48f, 0.86f, width * 1.48f);
        }

        private static void CastleBlock(Vector3 pos, Vector3 scale)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Cube);
            q.name = "Reference4 castle stone block";
            q.transform.SetParent(_root, false);
            q.transform.position = pos;
            q.transform.localScale = scale;
            RemoveCollider(q);
            q.GetComponent<Renderer>().sharedMaterial = _stone;
        }

        private static Mesh CreatePyramidMesh()
        {
            Vector3[] vertices =
            {
                new Vector3(-0.5f,-0.5f,-0.5f), new Vector3(0.5f,-0.5f,-0.5f),
                new Vector3(0.5f,-0.5f,0.5f), new Vector3(-0.5f,-0.5f,0.5f),
                new Vector3(0f,0.5f,0f)
            };
            int[] triangles = { 0,2,1, 0,3,2, 0,1,4, 1,2,4, 2,3,4, 3,0,4 };
            Mesh mesh = new Mesh { name = "Reference4 pointed roof" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void MossPatch(Vector3 p, float size, int seed)
        {
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.27f + seed * 0.09f;
                Blob(p + new Vector3(Mathf.Cos(a) * size * 0.30f, (i % 2) * 0.025f, Mathf.Sin(a) * size * 0.22f),
                    new Vector3(size * 0.62f, size * 0.18f, size * 0.46f), _moss, "Reference4 moss patch");
            }
        }

        private static GameObject Blob(Vector3 p, Vector3 scale, Material material, string name)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            q.name = name;
            q.transform.SetParent(_root, false);
            q.transform.position = p;
            q.transform.localScale = scale;
            RemoveCollider(q);
            q.GetComponent<Renderer>().sharedMaterial = material;
            return q;
        }

        private static GameObject MeshObject(string name, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        private static Material Mat(Shader shader, string name, Color colour, bool transparent = false, float kind = 0f)
        {
            Material m = new Material(shader) { name = name };
            m.SetTexture("_MainTex", Texture2D.whiteTexture);
            m.SetColor("_BaseColor", colour);
            m.SetColor("_SecondaryColor", colour);
            m.SetColor("_TopColor", colour);
            m.SetColor("_EmissionColor", Color.black);
            m.SetFloat("_TextureScale", 0.28f);
            m.SetFloat("_TextureStrength", 0.25f);
            m.SetFloat("_DetailScale", 0.08f);
            m.SetFloat("_DetailStrength", 0.05f);
            m.SetFloat("_TopStrength", 0f);
            m.SetFloat("_RimStrength", 0.045f);
            m.SetFloat("_SurfaceKind", kind);
            m.SetFloat("_Cull", transparent ? 0f : 2f);
            m.SetFloat("_ZWrite", transparent ? 0f : 1f);
            m.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
            return m;
        }

        private static void RemoveCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
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
