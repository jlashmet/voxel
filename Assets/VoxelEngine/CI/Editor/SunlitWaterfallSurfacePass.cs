using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Converts the final lookdev scene from flat prototype colours to the reusable surface
    /// vocabulary used by the target: world-space stylized texture projection, grassy cliff tops,
    /// warm mottled ruin stone, luminous water/cascades and softer atmospheric contrast.
    /// </summary>
    internal static class SunlitWaterfallSurfacePass
    {
        private static bool _done;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            GameObject scene = GameObject.Find("Sunlit Coherent Terrain Scene");
            if (scene == null) return;

            Texture2D grass = Load("grass_color.png");
            Texture2D rock = Load("rock_color.png");
            Texture2D stone = Load("stone_color.png");
            Texture2D wood = Load("wood_color.png");
            Texture2D dirt = Load("dirt_color.png");
            Texture2D slate = Load("slate_color.png");

            Renderer[] renderers = scene.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null || material.shader == null || material.shader.name != "VoxelEngine/SunlitSmooth")
                        continue;

                    string key = (renderer.gameObject.name + " " + material.name).ToLowerInvariant();

                    if (ContainsAny(key, "waterfall foam", "water foam", "foam"))
                    {
                        Configure(material, null,
                            new Color(0.95f, 0.995f, 1.0f, 0.78f),
                            new Color(0.88f, 0.97f, 1.0f),
                            new Color(1f, 1f, 1f),
                            6f, 0.22f, 0f, 0.34f, 0.025f, 0f, 0.02f,
                            new Color(0.14f, 0.18f, 0.20f));
                    }
                    else if (ContainsAny(key, "waterfall", "cascade"))
                    {
                        Configure(material, null,
                            new Color(0.58f, 0.86f, 0.95f, 0.86f),
                            new Color(0.38f, 0.72f, 0.88f),
                            new Color(0.91f, 0.99f, 1.0f),
                            5f, 0.26f, 0f, 0.16f, 0.09f, 0f, 0.035f,
                            new Color(0.07f, 0.16f, 0.20f));
                    }
                    else if (ContainsAny(key, "water", "pool", "basin", "turquoise", "channel plane"))
                    {
                        // Less neon than the blockout: darker cyan depth with a soft turquoise lift.
                        Configure(material, null,
                            new Color(0.035f, 0.49f, 0.69f, 0.88f),
                            new Color(0.018f, 0.33f, 0.52f),
                            new Color(0.18f, 0.77f, 0.87f),
                            4f, 0.18f, 0f, 0.12f, 0.055f, 0.12f, 0.045f,
                            new Color(0.012f, 0.060f, 0.080f));
                    }
                    else if (ContainsAny(key, "cloud"))
                    {
                        // Use the foam response plus a small emission lift so distance fog keeps
                        // the clouds white instead of turning the individual puffs blue.
                        Configure(material, null,
                            new Color(0.985f, 0.990f, 1.0f),
                            new Color(0.96f, 0.98f, 1.0f),
                            Color.white,
                            6f, 0.20f, 0f, 0.0f, 0.02f, 0f, 0.045f,
                            new Color(0.12f, 0.13f, 0.14f));
                    }
                    else if (ContainsAny(key, "haze", "mist mountain"))
                    {
                        Configure(material, null,
                            new Color(0.45f, 0.64f, 0.61f),
                            new Color(0.38f, 0.55f, 0.54f),
                            new Color(0.52f, 0.69f, 0.62f),
                            7f, 0.13f, 0f, 0.035f, 0.018f, 0.18f, 0.02f, Color.black);
                    }
                    else if (ContainsAny(key, "ridge"))
                    {
                        Configure(material, rock,
                            new Color(0.39f, 0.53f, 0.31f),
                            new Color(0.48f, 0.44f, 0.31f),
                            new Color(0.53f, 0.67f, 0.25f),
                            3f, 0.22f, 0.34f, 0.07f, 0.075f, 0.55f, 0.035f, Color.black);
                    }
                    else if (ContainsAny(key, "stone", "masonry", "ruin", "ashlar", "castle", "tower", "pillar", "lintel", "block"))
                    {
                        Configure(material, stone,
                            new Color(0.88f, 0.82f, 0.69f),
                            new Color(0.71f, 0.65f, 0.54f),
                            new Color(0.96f, 0.90f, 0.74f),
                            2f, 0.32f, 0.52f, 0.075f, 0.11f, 0.30f, 0.055f, Color.black);
                    }
                    else if (ContainsAny(key, "rock", "cliff", "earth", "slope"))
                    {
                        Texture2D source = ContainsAny(key, "earth") ? dirt : rock;
                        Configure(material, source,
                            new Color(0.46f, 0.48f, 0.31f),
                            new Color(0.55f, 0.45f, 0.31f),
                            new Color(0.48f, 0.63f, 0.20f),
                            3f, 0.24f, 0.40f, 0.075f, 0.075f, 0.60f, 0.04f, Color.black);
                    }
                    else if (ContainsAny(key, "grass", "turf", "garden", "crown", "terrain patch"))
                    {
                        // The source grass has long directional strokes. Keep it as low-frequency
                        // material character instead of letting it dominate the landform silhouette.
                        Configure(material, grass,
                            new Color(0.53f, 0.70f, 0.22f),
                            new Color(0.41f, 0.49f, 0.20f),
                            new Color(0.66f, 0.79f, 0.29f),
                            1f, 0.24f, 0.30f, 0.065f, 0.072f, 0.68f, 0.04f, Color.black);
                    }
                    else if (ContainsAny(key, "moss", "leaf", "canopy", "bush"))
                    {
                        Configure(material, grass,
                            new Color(0.39f, 0.59f, 0.16f),
                            new Color(0.27f, 0.43f, 0.12f),
                            new Color(0.55f, 0.70f, 0.22f),
                            1f, 0.28f, 0.28f, 0.08f, 0.095f, 0.42f, 0.055f, Color.black);
                    }
                    else if (ContainsAny(key, "bark", "trunk", "branch"))
                    {
                        Configure(material, wood,
                            new Color(0.34f, 0.22f, 0.11f),
                            new Color(0.24f, 0.15f, 0.075f),
                            new Color(0.42f, 0.29f, 0.14f),
                            8f, 0.40f, 0.58f, 0.085f, 0.10f, 0.10f, 0.025f, Color.black);
                    }
                    else if (ContainsAny(key, "roof", "spire"))
                    {
                        Configure(material, slate,
                            new Color(0.43f, 0.46f, 0.55f),
                            new Color(0.31f, 0.35f, 0.44f),
                            new Color(0.57f, 0.58f, 0.63f),
                            2f, 0.34f, 0.42f, 0.065f, 0.09f, 0.20f, 0.04f, Color.black);
                    }
                }
            }

            camera.backgroundColor = new Color(0.20f, 0.62f, 0.93f, 1f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.66f, 0.80f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(0.57f, 0.62f, 0.51f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.29f, 0.18f);
            RenderSettings.ambientIntensity = 0.76f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.56f, 0.79f, 0.95f);
            RenderSettings.fogStartDistance = 32f;
            RenderSettings.fogEndDistance = 66f;
        }

        private static Texture2D Load(string file)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Stylized/" + file);
        }

        private static void Configure(Material m, Texture texture, Color baseColor, Color secondary,
            Color top, float kind, float textureScale, float textureStrength, float detailStrength,
            float detailScale, float topStrength, float rimStrength, Color emission)
        {
            if (m == null) return;
            if (texture != null) m.SetTexture("_MainTex", texture);
            else m.SetTexture("_MainTex", Texture2D.whiteTexture);
            SetColor(m, "_BaseColor", baseColor);
            SetColor(m, "_SecondaryColor", secondary);
            SetColor(m, "_TopColor", top);
            SetColor(m, "_EmissionColor", emission);
            SetFloat(m, "_SurfaceKind", kind);
            SetFloat(m, "_TextureScale", textureScale);
            SetFloat(m, "_TextureStrength", textureStrength);
            SetFloat(m, "_DetailStrength", detailStrength);
            SetFloat(m, "_DetailScale", detailScale);
            SetFloat(m, "_TopStrength", topStrength);
            SetFloat(m, "_RimStrength", rimStrength);
        }

        private static void SetColor(Material m, string property, Color value)
        {
            if (m.HasProperty(property)) m.SetColor(property, value);
        }

        private static void SetFloat(Material m, string property, float value)
        {
            if (m.HasProperty(property)) m.SetFloat(property, value);
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            for (int i = 0; i < terms.Length; i++)
                if (value.Contains(terms[i])) return true;
            return false;
        }
    }
}
