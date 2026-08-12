using UnityEngine;
using UnityEngine.Rendering;
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

            WorldArtPiece hero = WorldArtArchBay.Build(root, "AAA hero architectural arch bay",
                o + new Vector3(-4.55f, 1.28f, 5.12f),
                1.52f, 3.62f, 1.18f, 0.50f, 0.56f, 0.92f,
                101, palette, WorldArtArchDamage.BrokenLeftHaunch);

            WorldArtPiece lower = WorldArtArchBay.Build(root, "AAA lower architectural arch bay",
                o + new Vector3(-5.35f, 0.34f, 8.26f),
                0.84f, 1.72f, 0.74f, 0.38f, 0.34f, 0.62f,
                131, palette, WorldArtArchDamage.Intact);

            WorldArtKit.MossCluster(root, "AAA hero keystone moss",
                hero.Socket("keystone").position + new Vector3(-0.28f, 0.035f, -0.04f),
                0.30f, 503, palette.Get(WorldArtSurfaceRole.Moss));
            WorldArtKit.MossCluster(root, "AAA lower arch moss",
                lower.Socket("crown").position + new Vector3(0.18f, 0.02f, -0.16f),
                0.20f, 541, palette.Get(WorldArtSurfaceRole.Moss));

            // Several later lookdev stages instantiate superseded arch families. Cull those exact
            // families immediately before the actual camera render, after all scene passes have run.
            Camera.onPreCull -= CleanupBeforeRender;
            Camera.onPreCull += CleanupBeforeRender;
        }

        private static void CleanupBeforeRender(Camera camera)
        {
            Camera.onPreCull -= CleanupBeforeRender;
            DisableLegacyRuinGeometry();
            HideLegacyVoxelStone();
        }

        private static void HideLegacyVoxelStone()
        {
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                {
                    Material m = materials[j];
                    if (m == null || m.name != "Voxel Warm Stone") continue;

                    Color clear = new Color(0f, 0f, 0f, 0f);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", clear);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", clear);
                    if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 3f);
                    if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                    if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
                    m.DisableKeyword("_ALPHATEST_ON");
                    m.EnableKeyword("_ALPHABLEND_ON");
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.renderQueue = (int)RenderQueue.Transparent;
                }
            }
        }

        private static void DisableLegacyRuinGeometry()
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || !t.gameObject.scene.IsValid()) continue;

                string n = t.name;
                bool oldWorldArtKitArch =
                    n.StartsWith("WorldArtKit hero arch") ||
                    n.StartsWith("WorldArtKit lower arch");
                bool oldReferenceArch =
                    n == "Rounded ashlar" ||
                    n == "Hero ruin pier" ||
                    n == "Hero ruin arch stone" ||
                    n == "Broken ruin crown" ||
                    n == "Hero arch pier" ||
                    n == "Hero arch ring" ||
                    n == "Lower arch pier" ||
                    n == "Lower arch ring";

                if (oldWorldArtKitArch || oldReferenceArch)
                    t.gameObject.SetActive(false);
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
