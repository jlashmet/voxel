using UnityEngine;
using VoxelEngine.Structures;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Stone-only quality pass. Replaces the generic and one-off ruin vocabularies with proper
    /// coursed ashlar and radial voussoir masonry. No terrain, water, vegetation or camera changes
    /// live here: this pass exists so stone quality can be iterated independently until the ruin
    /// vocabulary is production-worthy.
    /// </summary>
    internal static class SunlitWaterfallStoneQualityPass
    {
        private static bool _done;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            GameObject scene = GameObject.Find("Sunlit Coherent Terrain Scene");
            if (scene == null) return;

            Disable("WorldArtKit hero arch");
            Disable("WorldArtKit lower arch");
            DisableLegacyRuinGeometry();

            Transform kitRoot = scene.transform.Find("World Art Kit Reference Scene");
            if (kitRoot == null) kitRoot = scene.transform;

            Shader shader = Shader.Find("VoxelEngine/SunlitSmooth");
            if (shader == null) return;
            WorldArtPalette palette = BuildPalette(shader);

            GameObject rootObject = new GameObject("AAA Ruin Stone Study");
            rootObject.transform.SetParent(kitRoot, false);
            Transform root = rootObject.transform;

            Vector3 o = camera.transform.position - new Vector3(0.10f, 5.05f, -22.6f);

            // Treat the hero ruin as a small vertical complex rather than a single isolated arch.
            // The upper broken bay overlaps the lower mass enough to read as a surviving second
            // storey, giving the eye a climbable foreground-to-sky line instead of another tower.
            WorldArtPiece hero = WorldArtStoneKit.RuinArch(root, "AAA hero ruin arch",
                o + new Vector3(-5.55f, 1.55f, 5.35f),
                1.48f, 8, new Vector3(0.60f, 0.52f, 0.82f), 0.84f, true, 101, palette);

            WorldArtPiece upper = WorldArtStoneKit.RuinArch(root, "AAA upper ruin arch",
                o + new Vector3(-5.18f, 5.35f, 5.48f),
                0.88f, 4, new Vector3(0.44f, 0.40f, 0.66f), 0.69f, true, 173, palette);

            WorldArtPiece lower = WorldArtStoneKit.RuinArch(root, "AAA lower ruin arch",
                o + new Vector3(-5.95f, 0.45f, 8.30f),
                0.82f, 4, new Vector3(0.42f, 0.40f, 0.58f), 0.60f, false, 131, palette);

            // Re-attach overgrowth to semantic stone sockets so dressing remains subordinate to
            // masonry and demonstrates that the shared socket contract survives visual iteration.
            WorldArtKit.MossCluster(root, "AAA keystone moss",
                hero.Socket("keystone").position + new Vector3(-0.34f, 0.03f, 0f),
                0.38f, 503, palette.Get(WorldArtSurfaceRole.Moss));
            WorldArtKit.MossCluster(root, "AAA upper arch moss",
                upper.Socket("crown").position + new Vector3(0.16f, 0.01f, -0.18f),
                0.22f, 527, palette.Get(WorldArtSurfaceRole.Moss));
            WorldArtKit.MossCluster(root, "AAA lower arch moss",
                lower.Socket("crown").position + new Vector3(0.22f, 0.02f, -0.22f),
                0.24f, 541, palette.Get(WorldArtSurfaceRole.Moss));
        }

        private static void Disable(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) go.SetActive(false);
        }

        private static void DisableLegacyRuinGeometry()
        {
            // Earlier lookdev stages contain two different obsolete ruin implementations:
            // capsule-based "Rounded ashlar" and VerticalityPass's rotated cube arch. Disable only
            // those exact masonry objects. Vegetation, cliffs, upper-ruin scale cues and all other
            // vertical composition remain untouched; the shared WorldArtStoneKit becomes the sole
            // source of the foreground/hero arches.
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || !t.gameObject.scene.IsValid()) continue;

                string n = t.name;
                if (n == "Rounded ashlar" ||
                    n == "Hero ruin pier" ||
                    n == "Hero ruin arch stone" ||
                    n == "Broken ruin crown")
                {
                    t.gameObject.SetActive(false);
                }
            }
        }

        private static WorldArtPalette BuildPalette(Shader shader)
        {
            return new WorldArtPalette()
                .Set(WorldArtSurfaceRole.Stone, Stone(shader))
                .Set(WorldArtSurfaceRole.Moss, Moss(shader));
        }

        private static Material Stone(Shader shader)
        {
            Material m = new Material(shader) { name = "AAA pale weathered ruin stone" };
            SetColor(m, "_BaseColor", new Color(0.88f, 0.83f, 0.71f));
            SetColor(m, "_SecondaryColor", new Color(0.70f, 0.64f, 0.53f));
            SetColor(m, "_TopColor", new Color(0.96f, 0.91f, 0.79f));
            SetFloat(m, "_SurfaceKind", 2f);
            SetFloat(m, "_TextureScale", 0.30f);
            SetFloat(m, "_TextureStrength", 0.54f);
            SetFloat(m, "_DetailScale", 0.070f);
            SetFloat(m, "_DetailStrength", 0.12f);
            SetFloat(m, "_TopStrength", 0.34f);
            SetFloat(m, "_RimStrength", 0.045f);
            SetFloat(m, "_Smoothness", 0.055f);
            return m;
        }

        private static Material Moss(Shader shader)
        {
            Material m = new Material(shader) { name = "AAA ruin moss" };
            SetColor(m, "_BaseColor", new Color(0.37f, 0.58f, 0.15f));
            SetColor(m, "_SecondaryColor", new Color(0.25f, 0.42f, 0.10f));
            SetColor(m, "_TopColor", new Color(0.55f, 0.70f, 0.22f));
            SetFloat(m, "_SurfaceKind", 1f);
            SetFloat(m, "_TextureScale", 0.28f);
            SetFloat(m, "_TextureStrength", 0.28f);
            SetFloat(m, "_DetailStrength", 0.09f);
            SetFloat(m, "_TopStrength", 0.42f);
            return m;
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }
    }
}
