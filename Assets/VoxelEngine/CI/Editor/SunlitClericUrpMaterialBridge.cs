using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Visual-only bridge for the Sunlit Cleric CI composition. The capture builds its smooth
    /// proxy materials at runtime with the built-in Standard shader, while the project runs URP.
    /// This upgrades those temporary materials just before the named camera renders and then
    /// performs the final concept-art composition pass: camera framing, palette, visible water,
    /// soft clouds and a distant castle silhouette. Nothing here is used by the shipping game.
    /// </summary>
    [InitializeOnLoad]
    internal static class SunlitClericUrpMaterialBridge
    {
        private static bool _prepared;
        private static readonly List<Object> Created = new();

        static SunlitClericUrpMaterialBridge()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera.name != "Sunlit Cleric Camera") return;

            UpgradeRuntimeStandardMaterials();
            if (_prepared) return;
            _prepared = true;

            TuneVoxelPalette();
            PullWaterOntoCliffFace();
            ReframeCamera(camera);
            AddReferenceBackground(camera);
        }

        private static void UpgradeRuntimeStandardMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("Sunlit Cleric: Universal Render Pipeline/Lit was not found.");
                return;
            }

            foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (renderer == null) continue;
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null || material.shader == null || material.shader.name != "Standard")
                        continue;

                    Color colour = material.HasProperty("_Color")
                        ? material.GetColor("_Color")
                        : Color.white;
                    Texture texture = material.HasProperty("_MainTex")
                        ? material.GetTexture("_MainTex")
                        : null;
                    Vector2 textureScale = material.HasProperty("_MainTex")
                        ? material.GetTextureScale("_MainTex")
                        : Vector2.one;
                    Vector2 textureOffset = material.HasProperty("_MainTex")
                        ? material.GetTextureOffset("_MainTex")
                        : Vector2.zero;
                    float smoothness = material.HasProperty("_Glossiness")
                        ? material.GetFloat("_Glossiness")
                        : 0.05f;
                    float metallic = material.HasProperty("_Metallic")
                        ? material.GetFloat("_Metallic")
                        : 0f;
                    Color emission = material.HasProperty("_EmissionColor")
                        ? material.GetColor("_EmissionColor")
                        : Color.black;
                    bool transparent = material.renderQueue >= (int)RenderQueue.Transparent || colour.a < 0.999f;

                    material.shader = urpLit;
                    material.SetTexture("_BaseMap", texture != null ? texture : Texture2D.whiteTexture);
                    material.SetTextureScale("_BaseMap", textureScale);
                    material.SetTextureOffset("_BaseMap", textureOffset);
                    material.SetColor("_BaseColor", colour);
                    material.SetColor("_Color", colour);
                    material.SetFloat("_Smoothness", smoothness);
                    material.SetFloat("_Metallic", metallic);

                    if (emission.maxColorComponent > 0.001f)
                    {
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", emission);
                    }

                    if (transparent)
                    {
                        material.SetOverrideTag("RenderType", "Transparent");
                        material.SetFloat("_Surface", 1f);
                        material.SetFloat("_Blend", 0f);
                        material.SetFloat("_AlphaClip", 0f);
                        material.SetFloat("_Cull", 0f); // water ribbons must read from either side
                        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                        material.SetInt("_ZWrite", 0);
                        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        material.DisableKeyword("_ALPHATEST_ON");
                        material.renderQueue = (int)RenderQueue.Transparent;
                    }
                    else
                    {
                        material.SetOverrideTag("RenderType", "Opaque");
                        material.SetFloat("_Surface", 0f);
                        material.SetFloat("_Cull", 2f);
                        material.SetInt("_ZWrite", 1);
                        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        material.renderQueue = (int)RenderQueue.Geometry;
                    }

                    materials[i] = material;
                    changed = true;
                }

                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private static void TuneVoxelPalette()
        {
            foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (renderer == null) continue;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null ||
                        material.shader.name != "VoxelEngine/WorldArtLookdev")
                        continue;

                    string n = material.name.ToLowerInvariant();
                    if (n.Contains("sungrass")) SetTint(material, new Color(0.24f, 0.43f, 0.12f));
                    else if (n.Contains("ruinmoss")) SetTint(material, new Color(0.18f, 0.34f, 0.09f));
                    else if (n.Contains("sunstone")) SetTint(material, new Color(0.64f, 0.56f, 0.43f));
                    else if (n.Contains("cliffrock")) SetTint(material, new Color(0.29f, 0.29f, 0.26f));
                    else if (n.Contains("warmpath")) SetTint(material, new Color(0.34f, 0.39f, 0.20f));
                    else if (n.Contains("rootwood")) SetTint(material, new Color(0.27f, 0.16f, 0.075f));
                }
            }
        }

        private static void SetTint(Material material, Color colour)
        {
            if (material.HasProperty("_Tint")) material.SetColor("_Tint", colour);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
        }

        private static void PullWaterOntoCliffFace()
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || !go.scene.IsValid()) continue;

                if (go.name == "Waterfall Body" || go.name == "Waterfall Sun Streak")
                {
                    // The procedural cliff extends farther toward the camera than the initial
                    // concept coordinates. Pull the ribbons onto its visible face.
                    go.transform.position += new Vector3(0f, 0f, -5.6f);
                }
                else if (go.name == "Turquoise Pool")
                {
                    go.transform.position += new Vector3(0.45f, 0.12f, -2.7f);
                    go.transform.localScale = new Vector3(1.20f, 1f, 1.15f);
                }
                else if (go.name == "Foreground Stream")
                {
                    go.transform.position += new Vector3(0.6f, 0.10f, -1.4f);
                    go.transform.localScale = new Vector3(1.24f, 1f, 1.18f);
                }
                else if (go.name == "Waterfall Mist")
                {
                    go.transform.position += new Vector3(0f, 0f, -4.9f);
                }
            }
        }

        private static void ReframeCamera(Camera camera)
        {
            GameObject cleric = GameObject.Find("Madeline Lookdev Proxy");
            if (cleric == null) return;

            Vector3 feet = cleric.transform.position;
            Vector3 heroCentre = feet + new Vector3(0f, 1.05f, 0f);

            // Match the generated reference: portrait lens, full-body hero occupying most of the
            // vertical frame, arch over her left shoulder and waterfalls/castle over her right.
            camera.fieldOfView = 31f;
            camera.backgroundColor = new Color(0.11f, 0.50f, 0.86f, 1f);
            camera.transform.position = heroCentre + new Vector3(2.55f, 1.70f, -8.35f);
            camera.transform.LookAt(heroCentre + new Vector3(0.72f, 0.82f, 3.25f));

            RenderSettings.fogColor = new Color(0.48f, 0.72f, 0.88f);
            RenderSettings.fogStartDistance = 31f;
            RenderSettings.fogEndDistance = 68f;
        }

        private static void AddReferenceBackground(Camera camera)
        {
            GameObject cleric = GameObject.Find("Madeline Lookdev Proxy");
            if (cleric == null) return;
            Vector3 hero = cleric.transform.position;

            Material cloud = CreateUrpMaterial("Sunlit Cloud", new Color(0.96f, 0.97f, 0.94f), 0.16f);
            Material castleStone = CreateUrpMaterial("Distant Castle Stone", new Color(0.68f, 0.66f, 0.58f), 0.04f);
            Material castleRoof = CreateUrpMaterial("Distant Castle Roof", new Color(0.30f, 0.39f, 0.46f), 0.10f);
            Created.Add(cloud);
            Created.Add(castleStone);
            Created.Add(castleRoof);

            CreateCloud(hero + new Vector3(-2.0f, 8.7f, 22.5f), new Vector3(1.35f, 1.0f, 0.75f), cloud);
            CreateCloud(hero + new Vector3(8.2f, 9.6f, 25.5f), new Vector3(1.10f, 0.86f, 0.70f), cloud);
            CreateCloud(hero + new Vector3(2.9f, 10.3f, 28.5f), new Vector3(1.55f, 1.08f, 0.82f), cloud);

            // The far castle is intentionally simple because it is a composition proxy. The
            // destructible foreground remains the real brickmap world; this gives the render the
            // same vertical fantasy landmark as the target while that production asset is built.
            var castle = new GameObject("Distant Sunlit Castle Proxy");
            castle.transform.position = hero + new Vector3(9.8f, 2.8f, 20.8f);
            Created.Add(castle);
            BuildCastleTower(castle.transform, new Vector3(0f, 2.25f, 0f), new Vector3(1.40f, 4.50f, 1.40f), castleStone, castleRoof, 4);
            BuildCastleTower(castle.transform, new Vector3(-1.65f, 1.55f, 0.20f), new Vector3(1.00f, 3.10f, 1.00f), castleStone, castleRoof, 3);
            BuildCastleTower(castle.transform, new Vector3(1.65f, 1.35f, 0.15f), new Vector3(0.92f, 2.70f, 0.92f), castleStone, castleRoof, 3);
            BuildCastleTower(castle.transform, new Vector3(0.85f, 3.65f, 0.35f), new Vector3(0.72f, 2.15f, 0.72f), castleStone, castleRoof, 3);
        }

        private static Material CreateUrpMaterial(string name, Color colour, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = name };
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.SetColor("_BaseColor", colour);
            material.SetColor("_Color", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            return material;
        }

        private static void CreateCloud(Vector3 position, Vector3 scale, Material material)
        {
            var root = new GameObject("Reference Cloud Cluster");
            root.transform.position = position;
            Created.Add(root);

            Vector3[] offsets =
            {
                new(-1.00f, 0.00f, 0f), new(-0.35f, 0.32f, 0f), new(0.30f, 0.24f, 0f),
                new(0.92f, -0.02f, 0f), new(0.12f, -0.22f, -0.10f),
            };
            Vector3[] sizes =
            {
                new(1.25f, 0.82f, 0.65f), new(1.55f, 1.05f, 0.78f), new(1.62f, 1.08f, 0.82f),
                new(1.18f, 0.78f, 0.62f), new(1.45f, 0.66f, 0.72f),
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = "Cloud Puff";
                puff.transform.SetParent(root.transform, false);
                puff.transform.localPosition = Vector3.Scale(offsets[i], scale);
                puff.transform.localScale = Vector3.Scale(sizes[i], scale);
                Object.DestroyImmediate(puff.GetComponent<Collider>());
                var renderer = puff.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void BuildCastleTower(Transform parent, Vector3 localPosition, Vector3 size,
                                             Material stone, Material roof, int roofSteps)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Castle Tower";
            body.transform.SetParent(parent, false);
            body.transform.localPosition = localPosition;
            body.transform.localScale = size;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<MeshRenderer>().sharedMaterial = stone;

            float y = localPosition.y + size.y * 0.5f;
            for (int i = 0; i < roofSteps; i++)
            {
                float t = i / (float)roofSteps;
                float width = size.x * Mathf.Lerp(0.90f, 0.18f, t);
                GameObject tier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tier.name = "Castle Spire Tier";
                tier.transform.SetParent(parent, false);
                tier.transform.localPosition = new Vector3(localPosition.x, y + 0.16f + i * 0.22f, localPosition.z);
                tier.transform.localScale = new Vector3(width, 0.24f, width);
                Object.DestroyImmediate(tier.GetComponent<Collider>());
                tier.GetComponent<MeshRenderer>().sharedMaterial = roof;
            }
        }
    }
}
