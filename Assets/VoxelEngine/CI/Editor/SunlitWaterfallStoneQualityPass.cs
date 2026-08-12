using UnityEngine;
using VoxelEngine.Structures;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Stone-only quality pass. Replaces generic and one-off ruin vocabulary with the reusable
    /// architectural arch bay so masonry quality can be judged independently of terrain work.
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

            // Keep the full hero bay inside the frame so every structural layer is inspectable.
            WorldArtPiece hero = WorldArtArchBay.Build(root, "AAA hero architectural arch bay",
                o + new Vector3(-4.55f, 1.28f, 5.12f),
                1.52f, 3.62f, 1.18f, 0.50f, 0.56f, 0.92f,
                101, palette, WorldArtArchDamage.BrokenLeftHaunch);

            WorldArtPiece lower = WorldArtArchBay.Build(root, "AAA lower architectural arch bay",
                o + new Vector3(-5.35f, 0.34f, 8.26f),
                0.84f, 1.72f, 0.74f, 0.38f, 0.34f, 0.62f,
                131, palette, WorldArtArchDamage.Intact);

            // Dressing stays sparse until the stone survives close inspection.
            WorldArtKit.MossCluster(root, "AAA hero keystone moss",
                hero.Socket("keystone").position + new Vector3(-0.28f, 0.035f, -0.04f),
                0.30f, 503, palette.Get(WorldArtSurfaceRole.Moss));
            WorldArtKit.MossCluster(root, "AAA lower arch moss",
                lower.Socket("crown").position + new Vector3(0.18f, 0.02f, -0.16f),
                0.20f, 541, palette.Get(WorldArtSurfaceRole.Moss));
        }

        private static void Disable(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) go.SetActive(false);
        }

        private static void DisableLegacyRuinGeometry()
        {
            // There are multiple historical arch implementations in the lookdev stack. Disable
            // every named masonry part from those implementations so the CI image contains only
            // the reusable WorldArtArchBay geometry we are actually judging.
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
                    n == "Broken ruin crown" ||
                    n == "Hero arch pier" ||
                    n == "Hero arch ring" ||
                    n == "Lower arch pier" ||
                    n == "Lower arch ring")
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
