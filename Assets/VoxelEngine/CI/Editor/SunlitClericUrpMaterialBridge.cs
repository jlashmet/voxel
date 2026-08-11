using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Visual-only bridge for the Sunlit Cleric CI composition. The destructible voxel world is
    /// still the substrate; this pass art-directs the deterministic capture around the generated
    /// reference composition and upgrades runtime-created smooth materials to the project-native
    /// storybook URP shader. Nothing here is used by the shipping game.
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
            CompressVoxelReliefAroundHero();
            DisableOriginalWater();
            MoveHeroTreeOutOfCentre();
            ReframeCamera(camera);
            AddReferenceSetPieces();
        }

        private static Shader SmoothShader()
        {
            Shader shader = Shader.Find("VoxelEngine/SunlitSmooth");
            if (shader == null)
                Debug.LogError("Sunlit Cleric: VoxelEngine/SunlitSmooth was not found.");
            return shader;
        }

        private static void UpgradeRuntimeStandardMaterials()
        {
            Shader smooth = SmoothShader();
            if (smooth == null) return;

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
                    Color emission = material.HasProperty("_EmissionColor")
                        ? material.GetColor("_EmissionColor")
                        : Color.black;
                    bool transparent = material.renderQueue >= (int)RenderQueue.Transparent || colour.a < 0.999f;

                    material.shader = smooth;
                    material.SetTexture("_MainTex", texture != null ? texture : Texture2D.whiteTexture);
                    material.SetTextureScale("_MainTex", textureScale);
                    material.SetTextureOffset("_MainTex", textureOffset);
                    material.SetColor("_BaseColor", colour);
                    material.SetFloat("_Smoothness", smoothness);
                    material.SetColor("_EmissionColor", emission);
                    material.SetFloat("_Cull", transparent ? 0f : 2f);
                    material.SetFloat("_ZWrite", transparent ? 0f : 1f);
                    material.renderQueue = transparent
                        ? (int)RenderQueue.Transparent
                        : (int)RenderQueue.Geometry;

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
                    if (n.Contains("sungrass")) SetTint(material, new Color(0.27f, 0.48f, 0.14f));
                    else if (n.Contains("ruinmoss")) SetTint(material, new Color(0.20f, 0.38f, 0.10f));
                    else if (n.Contains("sunstone")) SetTint(material, new Color(0.72f, 0.64f, 0.50f));
                    else if (n.Contains("cliffrock")) SetTint(material, new Color(0.33f, 0.32f, 0.28f));
                    else if (n.Contains("warmpath")) SetTint(material, new Color(0.40f, 0.46f, 0.25f));
                    else if (n.Contains("rootwood")) SetTint(material, new Color(0.29f, 0.17f, 0.08f));
                }
            }
        }

        private static void SetTint(Material material, Color colour)
        {
            if (material.HasProperty("_Tint")) material.SetColor("_Tint", colour);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
        }

        private static void CompressVoxelReliefAroundHero()
        {
            GameObject cleric = GameObject.Find("Madeline Lookdev Proxy");
            GameObject voxelRoot = GameObject.Find("Voxel Surface");
            if (cleric == null || voxelRoot == null) return;

            // Preserve the hero ground plane while compressing extreme background relief. The
            // initial procedural cliff was physically plausible but compositionally dominated the
            // portrait. This keeps the same destructible geometry and stepping, just art-directs
            // its vertical exaggeration for the target shot.
            const float verticalScale = 0.63f;
            float pivotY = cleric.transform.position.y;
            voxelRoot.transform.localScale = new Vector3(1f, verticalScale, 1f);
            voxelRoot.transform.position = new Vector3(0f, pivotY * (1f - verticalScale), 0f);
        }

        private static void DisableOriginalWater()
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || !go.scene.IsValid()) continue;
                if (go.name == "Waterfall Body" || go.name == "Waterfall Sun Streak" ||
                    go.name == "Waterfall Mist" || go.name == "Turquoise Pool" ||
                    go.name == "Foreground Stream")
                {
                    go.SetActive(false);
                }
            }
        }

        private static void ReframeCamera(Camera camera)
        {
            GameObject cleric = GameObject.Find("Madeline Lookdev Proxy");
            if (cleric == null) return;

            Vector3 feet = cleric.transform.position;
            Vector3 heroCentre = feet + new Vector3(0f, 1.02f, 0f);

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.fieldOfView = 31f;
            camera.backgroundColor = new Color(0.07f, 0.49f, 0.88f, 1f);
            camera.transform.position = heroCentre + new Vector3(0.40f, 1.65f, -7.35f);
            camera.transform.LookAt(heroCentre + new Vector3(0.28f, 0.72f, 2.55f));

            RenderSettings.skybox = null;
            RenderSettings.fogColor = new Color(0.48f, 0.73f, 0.91f);
            RenderSettings.fogStartDistance = 32f;
            RenderSettings.fogEndDistance = 70f;
        }

        private static void MoveHeroTreeOutOfCentre()
        {
            GameObject tree = GameObject.Find("Sunlit Oak");
            if (tree == null) return;
            tree.transform.position += new Vector3(-2.8f, -0.18f, 0.35f);
            tree.transform.localScale *= 0.76f;
        }

        private static void AddReferenceSetPieces()
        {
            GameObject cleric = GameObject.Find("Madeline Lookdev Proxy");
            if (cleric == null) return;
            Vector3 hero = cleric.transform.position;

            Material cloud = CreateSmoothMaterial("Sunlit Cloud", new Color(0.99f, 0.99f, 0.97f), 0.08f);
            Material castleStone = CreateSmoothMaterial("Distant Castle Stone", new Color(0.73f, 0.69f, 0.59f), 0.04f);
            Material castleRoof = CreateSmoothMaterial("Distant Castle Roof", new Color(0.31f, 0.41f, 0.51f), 0.08f);
            Material archStone = CreateSmoothMaterial("Warm Arch Stone", new Color(0.73f, 0.65f, 0.51f), 0.04f);
            Material moss = CreateSmoothMaterial("Smooth Moss Cushions", new Color(0.28f, 0.48f, 0.14f), 0.03f);
            Material water = CreateTransparentSmoothMaterial("Waterfall Blue", new Color(0.45f, 0.86f, 0.96f, 0.78f), 0.05f);
            Material waterLight = CreateTransparentSmoothMaterial("Waterfall White", new Color(0.92f, 0.98f, 1.0f, 0.48f), 0.02f);
            Material pool = CreateTransparentSmoothMaterial("Pool Turquoise", new Color(0.18f, 0.72f, 0.84f, 0.70f), 0.08f);
            Material foam = CreateTransparentSmoothMaterial("Water Foam", new Color(0.94f, 0.99f, 1.0f, 0.62f), 0.02f);

            Created.Add(cloud); Created.Add(castleStone); Created.Add(castleRoof);
            Created.Add(archStone); Created.Add(moss); Created.Add(water);
            Created.Add(waterLight); Created.Add(pool); Created.Add(foam);

            CreateBlockArch(hero + new Vector3(-3.25f, 0.10f, 3.25f), archStone, moss);
            CreateReferenceWaterfalls(hero, water, waterLight, pool, foam);

            CreateCloud(hero + new Vector3(-2.8f, 8.0f, 23.5f), new Vector3(1.25f, 0.92f, 0.72f), cloud);
            CreateCloud(hero + new Vector3(6.8f, 8.8f, 26.5f), new Vector3(1.05f, 0.82f, 0.68f), cloud);
            CreateCloud(hero + new Vector3(1.8f, 9.6f, 29.5f), new Vector3(1.45f, 1.02f, 0.78f), cloud);

            var castle = new GameObject("Distant Sunlit Castle Proxy");
            castle.transform.position = hero + new Vector3(8.2f, 5.6f, 19.5f);
            Created.Add(castle);
            BuildCastleTower(castle.transform, new Vector3(0f, 2.25f, 0f), new Vector3(1.40f, 4.50f, 1.40f), castleStone, castleRoof, 5);
            BuildCastleTower(castle.transform, new Vector3(-1.65f, 1.55f, 0.20f), new Vector3(1.00f, 3.10f, 1.00f), castleStone, castleRoof, 4);
            BuildCastleTower(castle.transform, new Vector3(1.65f, 1.35f, 0.15f), new Vector3(0.92f, 2.70f, 0.92f), castleStone, castleRoof, 4);
            BuildCastleTower(castle.transform, new Vector3(0.85f, 3.65f, 0.35f), new Vector3(0.72f, 2.15f, 0.72f), castleStone, castleRoof, 4);

            CreateMoss(hero + new Vector3(-2.05f, 0.02f, 1.55f), new Vector3(1.25f, 0.32f, 0.95f), moss);
            CreateMoss(hero + new Vector3(2.10f, -0.02f, 2.25f), new Vector3(1.45f, 0.34f, 1.05f), moss);
            CreateMoss(hero + new Vector3(-3.25f, 0.06f, 4.25f), new Vector3(1.05f, 0.28f, 0.90f), moss);
            CreateMoss(hero + new Vector3(3.40f, 0.08f, 4.45f), new Vector3(1.20f, 0.30f, 0.95f), moss);
        }

        private static void CreateReferenceWaterfalls(Vector3 hero, Material water, Material highlight,
                                                      Material pool, Material foam)
        {
            CreateWaterfallCurtain(hero + new Vector3(4.30f, 3.20f, 3.10f), 1.28f, 3.55f, water, highlight);
            CreateWaterfallCurtain(hero + new Vector3(5.85f, 4.00f, 4.00f), 0.92f, 2.85f, water, highlight);
            CreateWaterfallCurtain(hero + new Vector3(7.10f, 4.80f, 5.10f), 0.66f, 2.15f, water, highlight);

            GameObject poolPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            poolPlane.name = "Reference Turquoise Pool";
            poolPlane.transform.position = hero + new Vector3(4.55f, -0.38f, 2.65f);
            poolPlane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            poolPlane.transform.localScale = new Vector3(4.8f, 2.6f, 1f);
            Object.DestroyImmediate(poolPlane.GetComponent<Collider>());
            poolPlane.GetComponent<MeshRenderer>().sharedMaterial = pool;
            Created.Add(poolPlane);

            Vector3[] foamPositions =
            {
                hero + new Vector3(4.30f, -0.18f, 2.85f),
                hero + new Vector3(5.85f, 0.45f, 3.75f),
                hero + new Vector3(7.10f, 1.25f, 4.85f),
            };
            foreach (Vector3 p in foamPositions)
            {
                for (int i = 0; i < 4; i++)
                {
                    GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    puff.name = "Reference Water Foam";
                    puff.transform.position = p + new Vector3((i - 1.5f) * 0.16f, 0.03f, (i & 1) * 0.08f);
                    puff.transform.localScale = new Vector3(0.48f, 0.13f, 0.30f);
                    Object.DestroyImmediate(puff.GetComponent<Collider>());
                    puff.GetComponent<MeshRenderer>().sharedMaterial = foam;
                    Created.Add(puff);
                }
            }
        }

        private static void CreateWaterfallCurtain(Vector3 centre, float width, float height,
                                                   Material water, Material highlight)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Quad);
            body.name = "Reference Waterfall";
            body.transform.position = centre;
            body.transform.localScale = new Vector3(width, height, 1f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<MeshRenderer>().sharedMaterial = water;
            Created.Add(body);

            GameObject streak = GameObject.CreatePrimitive(PrimitiveType.Quad);
            streak.name = "Reference Waterfall Highlight";
            streak.transform.position = centre + new Vector3(-width * 0.16f, 0f, -0.025f);
            streak.transform.localScale = new Vector3(width * 0.32f, height * 0.96f, 1f);
            Object.DestroyImmediate(streak.GetComponent<Collider>());
            streak.GetComponent<MeshRenderer>().sharedMaterial = highlight;
            Created.Add(streak);
        }

        private static void CreateBlockArch(Vector3 basePosition, Material stone, Material moss)
        {
            var root = new GameObject("Reference Block Arch");
            root.transform.position = basePosition;
            Created.Add(root);

            const float block = 0.52f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int y = 0; y < 6; y++)
                {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "Arch Stone Block";
                    cube.transform.SetParent(root.transform, false);
                    cube.transform.localPosition = new Vector3(side * 1.30f, 0.30f + y * 0.50f, 0f);
                    cube.transform.localScale = new Vector3(block * 1.08f, block, block * 0.90f);
                    Object.DestroyImmediate(cube.GetComponent<Collider>());
                    cube.GetComponent<MeshRenderer>().sharedMaterial = stone;
                }
            }

            const int archBlocks = 9;
            for (int i = 0; i < archBlocks; i++)
            {
                float t = i / (float)(archBlocks - 1);
                float angle = Mathf.Lerp(180f, 0f, t) * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * 1.30f;
                float y = 3.02f + Mathf.Sin(angle) * 1.25f;
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Arch Crown Block";
                cube.transform.SetParent(root.transform, false);
                cube.transform.localPosition = new Vector3(x, y, 0f);
                cube.transform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Cos(angle) * 28f);
                cube.transform.localScale = new Vector3(block * 1.12f, block, block * 0.92f);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.GetComponent<MeshRenderer>().sharedMaterial = stone;
            }

            CreateMoss(basePosition + new Vector3(-1.25f, 3.05f, -0.05f), new Vector3(0.85f, 0.22f, 0.50f), moss);
            CreateMoss(basePosition + new Vector3(0.65f, 4.08f, -0.03f), new Vector3(0.72f, 0.20f, 0.46f), moss);
        }

        private static Material CreateSmoothMaterial(string name, Color colour, float smoothness)
        {
            Shader shader = SmoothShader();
            var material = new Material(shader) { name = name };
            material.SetTexture("_MainTex", Texture2D.whiteTexture);
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_Cull", 2f);
            material.SetFloat("_ZWrite", 1f);
            return material;
        }

        private static Material CreateTransparentSmoothMaterial(string name, Color colour, float emission)
        {
            Shader shader = SmoothShader();
            var material = new Material(shader) { name = name };
            material.SetTexture("_MainTex", Texture2D.whiteTexture);
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.08f);
            material.SetColor("_EmissionColor", new Color(colour.r, colour.g, colour.b, 1f) * emission);
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)RenderQueue.Transparent;
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

        private static void CreateMoss(Vector3 position, Vector3 scale, Material material)
        {
            GameObject moss = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moss.name = "Smooth Moss Cushion";
            moss.transform.position = position;
            moss.transform.localScale = scale;
            Object.DestroyImmediate(moss.GetComponent<Collider>());
            var renderer = moss.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            Created.Add(moss);
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
                float width = size.x * Mathf.Lerp(0.90f, 0.16f, t);
                GameObject tier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tier.name = "Castle Spire Tier";
                tier.transform.SetParent(parent, false);
                tier.transform.localPosition = new Vector3(localPosition.x, y + 0.15f + i * 0.21f, localPosition.z);
                tier.transform.localScale = new Vector3(width, 0.23f, width);
                Object.DestroyImmediate(tier.GetComponent<Collider>());
                tier.GetComponent<MeshRenderer>().sharedMaterial = roof;
            }
        }
    }
}
