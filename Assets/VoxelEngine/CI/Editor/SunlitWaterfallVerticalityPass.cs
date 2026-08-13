using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Final composition pass for the Sunlit Waterfall lookdev.  The earlier passes prove the
    /// reusable terrain/material vocabulary; this pass concentrates those pieces into the tall,
    /// layered silhouette of the concept: a framed foreground, hero ruin, rising cascade wall,
    /// atmospheric background shapes and denser garden dressing.
    /// </summary>
    internal static class SunlitWaterfallVerticalityPass
    {
        private static bool _done;
        private static Transform _root;
        private static Material _grass, _rock, _stone, _moss, _water, _fall, _foam, _leaf, _bark;
        private static Material _hazeNear, _hazeFar, _stoneLight, _flowerWhite, _flowerPink, _flowerBlue;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            GameObject scene = GameObject.Find("Sunlit Coherent Terrain Scene");
            if (scene == null) return;
            _root = scene.transform;

            // StylePass/LushPass leave the camera at this exact offset, so recovering the shared
            // origin here keeps the pass deterministic even when the environment moves as a unit.
            Vector3 o = camera.transform.position - new Vector3(0.3f, 5.1f, -24.0f);

            _grass = MaterialOf("Reusable storybook terrain patch", 0);
            _rock = MaterialOf("Reusable storybook terrain patch", 1);
            _stone = MaterialOf("Warm chunky masonry", 0);
            _moss = FirstMaterial("Style moss clump", "Moss cluster", "Foreground moss cushion");
            _water = FirstMaterial("Shared turquoise channel plane", "Terrace water pool");
            _fall = MaterialOf("Coherent waterfall", 0);
            _foam = FirstMaterial("Waterfall foam", "Final waterfall foam");
            _leaf = FirstMaterial("Rounded storybook canopy", "Final oak canopy");
            _bark = FirstMaterial("Main oak trunk", "Final oak trunk");

            BuildPalette();
            RemoveCompetingArch(o);
            BuildAtmosphericDepth(o);
            BuildHeroArch(o);
            BuildCascadeMass(o);
            BuildUpperRuins(o);
            BuildGardenLayers(o);
            BuildWaterAccents(o);
            Reframe(camera, o);
        }

        private static void BuildPalette()
        {
            Shader shader = Shader.Find("VoxelEngine/SunlitSmooth");
            if (shader == null) return;

            _hazeFar = Make(shader, "Distant blue-green haze", new Color(0.35f, 0.57f, 0.53f));
            _hazeNear = Make(shader, "Near blue-green haze", new Color(0.39f, 0.55f, 0.39f));
            _stoneLight = Make(shader, "Sunlit pale stone", new Color(0.91f, 0.85f, 0.72f));
            _flowerWhite = Make(shader, "Verticality flower white", new Color(1.0f, 0.98f, 0.88f));
            _flowerPink = Make(shader, "Verticality flower pink", new Color(0.98f, 0.45f, 0.60f));
            _flowerBlue = Make(shader, "Verticality flower blue", new Color(0.30f, 0.68f, 0.96f));

            if (_rock != null)
            {
                _rock.SetColor("_BaseColor", new Color(0.40f, 0.45f, 0.29f));
                _rock.SetFloat("_Smoothness", 0.02f);
            }
            if (_grass != null)
                _grass.SetColor("_BaseColor", new Color(0.50f, 0.70f, 0.19f));
            if (_stone != null)
                _stone.SetColor("_BaseColor", new Color(0.86f, 0.80f, 0.67f));
            if (_water != null)
                _water.SetColor("_BaseColor", new Color(0.03f, 0.64f, 0.85f, 0.90f));
            if (_fall != null)
                _fall.SetColor("_BaseColor", new Color(0.80f, 0.97f, 1.0f, 0.92f));
        }

        private static void RemoveCompetingArch(Vector3 o)
        {
            // The first experimental arch was deliberately oversized.  Remove only its high,
            // near-left masonry so the new hero arch can frame the valley without crowding it.
            Renderer[] all = _root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Renderer r = all[i];
                if (r == null || r.gameObject.name != "Warm chunky masonry") continue;
                Vector3 p = r.transform.position;
                if (p.x < o.x - 3.4f && p.z > o.z + 0.3f && p.z < o.z + 7.0f && p.y > o.y + 1.15f)
                    r.gameObject.SetActive(false);
            }
        }

        private static void BuildAtmosphericDepth(Vector3 o)
        {
            if (_hazeFar != null)
            {
                // Low-contrast distant masses make the castle sit inside a world rather than on a
                // blank blue card.  They are broad ellipsoids, deliberately softer than gameplay
                // geometry and pushed well behind the cascade.
                Blob(o + new Vector3(-8.8f, 5.0f, 24.5f), new Vector3(7.5f, 5.6f, 4.0f), _hazeFar, "Distant mist mountain");
                Blob(o + new Vector3(0.2f, 6.1f, 27.0f), new Vector3(8.8f, 6.8f, 4.4f), _hazeFar, "Distant mist mountain");
                Blob(o + new Vector3(10.0f, 5.3f, 25.0f), new Vector3(7.2f, 5.8f, 4.0f), _hazeFar, "Distant mist mountain");
            }
            if (_hazeNear != null)
            {
                Blob(o + new Vector3(-9.2f, 3.1f, 15.0f), new Vector3(5.0f, 4.0f, 3.6f), _hazeNear, "Midground green ridge");
                Blob(o + new Vector3(1.0f, 2.6f, 17.0f), new Vector3(6.0f, 3.5f, 3.2f), _hazeNear, "Midground green ridge");
            }
        }

        private static void BuildHeroArch(Vector3 o)
        {
            Material mat = _stoneLight != null ? _stoneLight : _stone;
            if (mat == null) return;

            Vector3 c = o + new Vector3(-5.0f, 2.35f, 5.7f);
            const float halfOpening = 1.18f;
            const float blockW = 0.54f;
            const float blockH = 0.58f;

            // Tall, narrow piers give the scene literal vertical lines while keeping a generous
            // opening through which the middle-distance valley remains visible.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 7; row++)
                {
                    Vector3 p = c + new Vector3(side * halfOpening, -1.9f + row * 0.54f, (row % 2) * 0.025f);
                    Cube(p, new Vector3(blockW, blockH, 0.72f), mat, side * (row % 2 == 0 ? 1.7f : -1.1f), "Hero ruin pier");
                }
            }

            float radius = halfOpening;
            for (int i = 0; i <= 10; i++)
            {
                if (i == 8) continue; // one missing voussoir sells the ruined silhouette
                float t = i / 10f;
                float a = Mathf.Lerp(180f, 0f, t) * Mathf.Deg2Rad;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * radius, 1.35f + Mathf.Sin(a) * radius, 0f);
                GameObject q = Cube(p, new Vector3(0.56f, 0.50f, 0.72f), mat, 0f, "Hero ruin arch stone");
                q.transform.rotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg + 90f);
            }

            // Broken vertical continuation on only one side prevents the arch reading like a tidy
            // garden gate and gives the ruin an asymmetrical skyline.
            Cube(c + new Vector3(-halfOpening, 2.35f, 0.02f), new Vector3(0.58f, 0.82f, 0.72f), mat, -4f, "Broken ruin crown");
            Cube(c + new Vector3(-halfOpening + 0.08f, 3.04f, 0.00f), new Vector3(0.50f, 0.58f, 0.68f), mat, 6f, "Broken ruin crown");

            if (_moss != null)
            {
                Moss(c + new Vector3(-1.18f, 2.82f, -0.10f), 0.50f);
                Moss(c + new Vector3(1.05f, 1.98f, -0.08f), 0.40f);
            }
        }

        private static void BuildCascadeMass(Vector3 o)
        {
            if (_rock == null || _grass == null) return;

            // One coherent vertical landform behind the existing terrace waterfalls.  The broad
            // rock bodies overlap so the silhouette feels eroded rather than like stacked boxes.
            CliffLobe(o + new Vector3(6.0f, 2.0f, 5.0f), new Vector3(4.4f, 3.4f, 3.7f));
            CliffLobe(o + new Vector3(6.5f, 4.5f, 8.1f), new Vector3(3.9f, 3.6f, 3.4f));
            CliffLobe(o + new Vector3(7.0f, 7.0f, 11.4f), new Vector3(3.4f, 3.5f, 3.0f));
            CliffLobe(o + new Vector3(7.1f, 9.2f, 14.4f), new Vector3(3.0f, 3.0f, 2.8f));

            // A second, slimmer pillar creates the stepped canyon profile visible in the concept.
            CliffLobe(o + new Vector3(2.9f, 4.2f, 10.8f), new Vector3(2.3f, 4.2f, 2.5f));
        }

        private static void CliffLobe(Vector3 p, Vector3 scale)
        {
            GameObject body = Blob(p, scale, _rock, "Rounded vertical cliff");
            body.transform.rotation = Quaternion.Euler(0f, 0f, -3f);

            GameObject cap = Blob(p + new Vector3(0f, scale.y * 0.72f, -0.05f),
                new Vector3(scale.x * 0.92f, Mathf.Max(0.22f, scale.y * 0.10f), scale.z * 0.92f),
                _grass, "Grassy cliff crown");
            cap.transform.rotation = Quaternion.Euler(-2f, 0f, 0f);

            if (_moss != null)
            {
                Moss(p + new Vector3(-scale.x * 0.44f, scale.y * 0.20f, -scale.z * 0.48f), Mathf.Min(0.55f, scale.x * 0.13f));
                Moss(p + new Vector3(scale.x * 0.30f, -scale.y * 0.05f, -scale.z * 0.50f), Mathf.Min(0.48f, scale.x * 0.12f));
            }
        }

        private static void BuildUpperRuins(Vector3 o)
        {
            Material mat = _stoneLight != null ? _stoneLight : _stone;
            if (mat == null) return;

            // Small ancient fragments on the cascade crest establish scale.  Their vertical posts
            // echo the arch without competing with the distant castle.
            Vector3 p = o + new Vector3(4.0f, 8.1f, 12.8f);
            Cube(p, new Vector3(0.48f, 2.5f, 0.55f), mat, -4f, "Upper ruin pillar");
            Cube(p + new Vector3(1.35f, 0.55f, 0.2f), new Vector3(0.42f, 1.65f, 0.50f), mat, 5f, "Upper ruin pillar");
            Cube(p + new Vector3(0.62f, 1.55f, 0.08f), new Vector3(1.75f, 0.38f, 0.55f), mat, 2f, "Upper ruin lintel");
            if (_moss != null) Moss(p + new Vector3(0.55f, 2.0f, -0.3f), 0.40f);
        }

        private static void BuildGardenLayers(Vector3 o)
        {
            if (_moss != null)
            {
                Vector3[] bushes =
                {
                    new Vector3(-7.0f,0.10f,-5.3f), new Vector3(-5.8f,0.22f,-4.7f),
                    new Vector3(-3.6f,0.36f,-3.6f), new Vector3(-1.0f,0.28f,-3.9f),
                    new Vector3(2.1f,0.32f,-3.2f), new Vector3(4.7f,0.55f,-1.8f),
                    new Vector3(-4.4f,1.18f,2.5f), new Vector3(2.2f,1.5f,4.8f)
                };
                for (int i = 0; i < bushes.Length; i++) Moss(o + bushes[i], 0.44f + (i % 3) * 0.07f);
            }

            FlowerPatch(o + new Vector3(-5.8f, 0.42f, -4.5f), _flowerWhite, 1);
            FlowerPatch(o + new Vector3(-3.2f, 0.48f, -3.4f), _flowerPink, 2);
            FlowerPatch(o + new Vector3(-0.4f, 0.36f, -3.6f), _flowerBlue, 3);
            FlowerPatch(o + new Vector3(3.5f, 0.55f, -2.2f), _flowerWhite, 4);
            FlowerPatch(o + new Vector3(4.0f, 1.35f, 1.2f), _flowerPink, 5);

            if (_bark != null && _leaf != null)
            {
                // A slim midground tree counters the huge left framing oak and repeats the scene's
                // upward rhythm without hiding the cascade.
                Vector3 b = o + new Vector3(2.7f, 1.0f, 7.1f);
                Capsule(b, b + new Vector3(0.05f, 4.2f, 0.15f), 0.19f, _bark, "Slender valley tree trunk");
                Blob(b + new Vector3(-0.6f, 4.25f, 0f), new Vector3(1.35f, 0.90f, 1.15f), _leaf, "Slender valley tree canopy");
                Blob(b + new Vector3(0.5f, 4.45f, 0.1f), new Vector3(1.45f, 1.0f, 1.18f), _leaf, "Slender valley tree canopy");
                Blob(b + new Vector3(0.0f, 5.15f, 0.2f), new Vector3(1.25f, 0.95f, 1.05f), _leaf, "Slender valley tree canopy");
            }
        }

        private static void BuildWaterAccents(Vector3 o)
        {
            Material foam = _foam != null ? _foam : _flowerWhite;
            if (foam == null) return;

            // Foam ladders visually connect the separate terrace ribbons into one continuous
            // waterfall system and add the bright white accents missing from the prior render.
            FoamShelf(o + new Vector3(4.7f, 0.82f, 1.35f), 1.55f, foam);
            FoamShelf(o + new Vector3(5.8f, 2.62f, 4.85f), 1.32f, foam);
            FoamShelf(o + new Vector3(6.65f, 4.34f, 8.42f), 1.12f, foam);
            FoamShelf(o + new Vector3(7.25f, 6.0f, 11.45f), 0.95f, foam);
        }

        private static void FoamShelf(Vector3 p, float width, Material mat)
        {
            for (int i = 0; i < 5; i++)
            {
                float t = (i - 2) / 2f;
                Blob(p + new Vector3(t * width * 0.34f, (i % 2) * 0.035f, -0.05f),
                    new Vector3(width * 0.20f, 0.075f, 0.18f + (i % 2) * 0.03f), mat, "Bright waterfall foam");
            }
        }

        private static void Reframe(Camera camera, Vector3 o)
        {
            // Fill the portrait frame with land instead of sky/water.  The right cliff is the tall
            // anchor; the arch and oak form the left frame; the castle stays a small destination.
            camera.fieldOfView = 32.5f;
            camera.transform.position = o + new Vector3(0.35f, 5.9f, -21.8f);
            camera.transform.LookAt(o + new Vector3(0.15f, 3.55f, 7.2f));

            Light sun = Object.FindAnyObjectByType<Light>();
            if (sun != null)
            {
                sun.color = new Color(1.0f, 0.93f, 0.78f);
                sun.intensity = 1.38f;
                sun.shadowStrength = 0.30f;
                sun.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            }
        }

        private static Material Make(Shader shader, string name, Color colour)
        {
            Material m = new Material(shader);
            m.name = name;
            m.SetTexture("_MainTex", Texture2D.whiteTexture);
            m.SetColor("_BaseColor", colour);
            m.SetColor("_EmissionColor", colour * 0.018f);
            m.SetFloat("_Smoothness", 0.02f);
            m.SetFloat("_Cull", 2f);
            m.SetFloat("_ZWrite", 1f);
            return m;
        }

        private static GameObject Cube(Vector3 p, Vector3 scale, Material mat, float yaw, string name)
        {
            GameObject q = Primitive(PrimitiveType.Cube, name, mat);
            q.transform.position = p;
            q.transform.localScale = scale;
            q.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return q;
        }

        private static GameObject Blob(Vector3 p, Vector3 scale, Material mat, string name)
        {
            GameObject q = Primitive(PrimitiveType.Sphere, name, mat);
            q.transform.position = p;
            q.transform.localScale = scale;
            return q;
        }

        private static void Moss(Vector3 p, float scale)
        {
            if (_moss == null) return;
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.37f;
                Blob(p + new Vector3(Mathf.Cos(a) * scale * 0.28f, (i % 2) * 0.045f, Mathf.Sin(a) * scale * 0.22f),
                    new Vector3(scale * 0.60f, scale * 0.30f, scale * 0.48f), _moss, "Verticality moss cushion");
            }
        }

        private static void FlowerPatch(Vector3 p, Material petals, int seed)
        {
            if (petals == null || _moss == null) return;
            for (int i = 0; i < 6; i++)
            {
                float ox = (Hash(seed * 31 + i) - 0.5f) * 0.78f;
                float oz = (Hash(seed * 47 + i + 5) - 0.5f) * 0.62f;
                Vector3 q = p + new Vector3(ox, 0f, oz);
                float h = 0.18f + (i % 3) * 0.035f;
                Capsule(q, q + Vector3.up * h, 0.012f, _moss, "Garden flower stem");
                for (int j = 0; j < 5; j++)
                {
                    float a = j * Mathf.PI * 2f / 5f;
                    Blob(q + Vector3.up * h + new Vector3(Mathf.Cos(a) * 0.052f, 0f, Mathf.Sin(a) * 0.052f),
                        new Vector3(0.055f, 0.020f, 0.040f), petals, "Garden flower petal");
                }
            }
        }

        private static void Capsule(Vector3 a, Vector3 b, float radius, Material mat, string name)
        {
            Vector3 d = b - a;
            GameObject q = Primitive(PrimitiveType.Capsule, name, mat);
            q.transform.position = (a + b) * 0.5f;
            q.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            q.transform.localScale = new Vector3(radius * 2f, d.magnitude * 0.5f, radius * 2f);
        }

        private static GameObject Primitive(PrimitiveType type, string name, Material mat)
        {
            GameObject q = GameObject.CreatePrimitive(type);
            q.name = name;
            q.transform.SetParent(_root, false);
            Collider c = q.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            Renderer r = q.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            return q;
        }

        private static Material MaterialOf(string name, int index)
        {
            Renderer[] all = _root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.name != name) continue;
                Material[] mats = all[i].sharedMaterials;
                if (index >= 0 && index < mats.Length) return mats[index];
            }
            return null;
        }

        private static Material FirstMaterial(params string[] names)
        {
            Renderer[] all = _root.GetComponentsInChildren<Renderer>(true);
            for (int n = 0; n < names.Length; n++)
                for (int i = 0; i < all.Length; i++)
                    if (all[i].gameObject.name == names[n] && all[i].sharedMaterial != null)
                        return all[i].sharedMaterial;
            return null;
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
