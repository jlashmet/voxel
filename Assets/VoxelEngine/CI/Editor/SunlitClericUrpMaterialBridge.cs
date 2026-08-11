using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    [InitializeOnLoad]
    internal static class SunlitClericUrpMaterialBridge
    {
        private static bool _prepared;
        private static readonly List<Object> Created = new();
        private static Mesh _beveledCube;

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

            GameObject hero = GameObject.Find("Madeline Lookdev Proxy");
            if (hero == null) return;
            HideShowcaseScenery();
            EnhanceCleric(hero.transform);
            ConfigureLight();
            ConfigureCamera(camera, hero.transform.position);
            BuildReferenceDiorama(hero.transform.position);
        }

        private static Shader SmoothShader()
        {
            Shader shader = Shader.Find("VoxelEngine/SunlitSmooth");
            if (shader == null) Debug.LogError("Sunlit Cleric: VoxelEngine/SunlitSmooth was not found.");
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
                    if (material == null || material.shader == null || material.shader.name != "Standard") continue;
                    Color colour = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                    Texture texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                    Vector2 scale = material.HasProperty("_MainTex") ? material.GetTextureScale("_MainTex") : Vector2.one;
                    Vector2 offset = material.HasProperty("_MainTex") ? material.GetTextureOffset("_MainTex") : Vector2.zero;
                    float smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.05f;
                    Color emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                    bool transparent = material.renderQueue >= (int)RenderQueue.Transparent || colour.a < 0.999f;
                    material.shader = smooth;
                    material.SetTexture("_MainTex", texture != null ? texture : Texture2D.whiteTexture);
                    material.SetTextureScale("_MainTex", scale);
                    material.SetTextureOffset("_MainTex", offset);
                    material.SetColor("_BaseColor", colour);
                    material.SetColor("_EmissionColor", emission);
                    material.SetFloat("_Smoothness", smoothness);
                    material.SetFloat("_Cull", transparent ? 0f : 2f);
                    material.SetFloat("_ZWrite", transparent ? 0f : 1f);
                    material.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private static void HideShowcaseScenery()
        {
            GameObject voxelSurface = GameObject.Find("Voxel Surface");
            if (voxelSurface != null) voxelSurface.SetActive(false);
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || !go.scene.IsValid()) continue;
                if (go.name == "Waterfall Body" || go.name == "Waterfall Sun Streak" || go.name == "Waterfall Mist" ||
                    go.name == "Turquoise Pool" || go.name == "Foreground Stream" || go.name == "Sunlit Oak" || go.name == "Far Garden Tree")
                    go.SetActive(false);
            }
        }

        private static void ConfigureLight()
        {
            GameObject sunObject = GameObject.Find("Sunlit Cleric Sun");
            if (sunObject == null) return;
            Light sun = sunObject.GetComponent<Light>();
            if (sun != null)
            {
                sun.color = new Color(1.0f, 0.92f, 0.76f);
                sun.intensity = 1.50f;
                sun.shadowStrength = 0.50f;
            }
            sunObject.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
        }

        private static void ConfigureCamera(Camera camera, Vector3 hero)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.50f, 0.89f, 1f);
            camera.fieldOfView = 33f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.transform.position = hero + new Vector3(0.25f, 2.00f, -4.50f);
            camera.transform.LookAt(hero + new Vector3(0.10f, 0.50f, 2.00f));
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.33f, 0.68f, 0.89f);
            RenderSettings.fogStartDistance = 8.0f;
            RenderSettings.fogEndDistance = 20.0f;
        }

        private static void EnhanceCleric(Transform hero)
        {
            hero.localScale = new Vector3(0.84f, 1.0f, 0.84f);
            Material hair = Smooth("Detail Blonde", new Color(0.92f, 0.68f, 0.22f));
            Material gold = Smooth("Detail Gold", new Color(0.91f, 0.65f, 0.15f));
            Material blue = Smooth("Detail Blue", new Color(0.36f, 0.65f, 0.82f));
            Material brown = Smooth("Detail Leather", new Color(0.25f, 0.12f, 0.055f));
            Material dark = Smooth("Detail Dark Brown", new Color(0.16f, 0.065f, 0.035f));
            Material mouth = Smooth("Detail Mouth", new Color(0.45f, 0.12f, 0.10f));
            Material eyeWhite = Smooth("Anime Eye White", new Color(0.98f, 0.97f, 0.94f));

            Transform leftEye = hero.Find("Left Brown Eye");
            Transform rightEye = hero.Find("Right Brown Eye");
            if (leftEye != null) leftEye.localScale = new Vector3(0.044f, 0.041f, 0.024f);
            if (rightEye != null) rightEye.localScale = new Vector3(0.044f, 0.041f, 0.024f);
            foreach (float x in new[] { -0.078f, 0.078f })
            {
                GameObject white = Primitive(PrimitiveType.Sphere, "Anime Eye Sclera", eyeWhite, hero);
                white.transform.localPosition = new Vector3(x, 1.56f, -0.286f);
                white.transform.localScale = new Vector3(0.073f, 0.054f, 0.021f);
            }

            Transform oldLeftLock = hero.Find("Left Hair Lock");
            Transform oldRightLock = hero.Find("Right Hair Lock");
            if (oldLeftLock != null) oldLeftLock.gameObject.SetActive(false);
            if (oldRightLock != null) oldRightLock.gameObject.SetActive(false);
            foreach (string sleeveName in new[] { "Left Sleeve", "Right Sleeve" })
            {
                Transform sleeve = hero.Find(sleeveName);
                if (sleeve != null) sleeve.localScale = new Vector3(sleeve.localScale.x * 0.72f, sleeve.localScale.y * 0.88f, sleeve.localScale.z * 0.72f);
            }
            foreach (string handName in new[] { "Left Hand", "Staff Hand" })
            {
                Transform hand = hero.Find(handName);
                if (hand != null) hand.localScale *= 0.80f;
            }

            GameObject smile = Primitive(PrimitiveType.Sphere, "Small Smile", mouth, hero);
            smile.transform.localPosition = new Vector3(0f, 1.475f, -0.302f);
            smile.transform.localScale = new Vector3(0.055f, 0.016f, 0.014f);

            Vector3[] hairTop =
            {
                new(-0.22f,1.67f,0.02f), new(-0.16f,1.70f,0.09f), new(-0.08f,1.72f,0.13f),
                new(0.08f,1.72f,0.13f), new(0.16f,1.70f,0.09f), new(0.22f,1.67f,0.02f)
            };
            Vector3[] hairBottom =
            {
                new(-0.34f,1.12f,0.10f), new(-0.27f,1.02f,0.18f), new(-0.15f,0.98f,0.23f),
                new(0.15f,0.98f,0.23f), new(0.27f,1.02f,0.18f), new(0.34f,1.12f,0.10f)
            };
            for (int i = 0; i < hairTop.Length; i++) CapsuleLocal($"Long Hair {i}", hairTop[i], hairBottom[i], 0.056f, hair, hero);
            CapsuleLocal("Front Hair Left", new Vector3(-0.04f,1.78f,-0.13f), new Vector3(-0.19f,1.61f,-0.245f), 0.036f, hair, hero);
            CapsuleLocal("Front Hair Right", new Vector3(0.04f,1.78f,-0.13f), new Vector3(0.19f,1.61f,-0.245f), 0.036f, hair, hero);

            Mesh cape = new Mesh { name = "Sunlit Cleric Cape" };
            cape.vertices = new[] { new Vector3(-0.20f,1.20f,0.10f), new Vector3(0.20f,1.20f,0.10f), new Vector3(0.46f,0.30f,0.16f), new Vector3(-0.46f,0.30f,0.16f) };
            cape.triangles = new[] { 0,1,2, 0,2,3, 2,1,0, 3,2,0 };
            cape.RecalculateNormals();
            Created.Add(cape);
            MeshObject("Blue Cape Lining", cape, blue, hero);

            GameObject stripe = Primitive(PrimitiveType.Cube, "Gold Robe Stripe", gold, hero);
            stripe.transform.localPosition = new Vector3(0f,0.52f,-0.225f);
            stripe.transform.localScale = new Vector3(0.030f,0.78f,0.018f);
            GameObject clasp = Primitive(PrimitiveType.Sphere, "Gold Collar Clasp", gold, hero);
            clasp.transform.localPosition = new Vector3(0f,1.19f,-0.25f);
            clasp.transform.localScale = Vector3.one * 0.075f;

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject boot = Primitive(PrimitiveType.Sphere, side < 0 ? "Left Boot" : "Right Boot", brown, hero);
                boot.transform.localPosition = new Vector3(side * 0.16f,0.035f,-0.17f);
                boot.transform.localScale = new Vector3(0.15f,0.11f,0.28f);
            }
            GameObject pouch = Primitive(PrimitiveType.Cube, "Cleric Book Pouch", brown, hero);
            pouch.transform.localPosition = new Vector3(0.30f,0.77f,-0.22f);
            pouch.transform.localRotation = Quaternion.Euler(0f,0f,-8f);
            pouch.transform.localScale = new Vector3(0.16f,0.25f,0.065f);
            GameObject pouchCross = Primitive(PrimitiveType.Cube, "Book Gold Cross", gold, hero);
            pouchCross.transform.localPosition = new Vector3(0.30f,0.77f,-0.290f);
            pouchCross.transform.localScale = new Vector3(0.022f,0.13f,0.012f);

            Transform shaft = hero.Find("Staff Shaft");
            if (shaft != null)
            {
                shaft.localPosition = new Vector3(-0.49f,0.845f,-0.10f);
                shaft.localScale = new Vector3(shaft.localScale.x,0.805f,shaft.localScale.z);
                Renderer r = shaft.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = dark;
            }
            string[] headParts = { "Staff Sun Core", "Staff Blue Crystal", "Staff Ray Up", "Staff Ray Down", "Staff Ray Left", "Staff Ray Right" };
            foreach (string part in headParts)
            {
                Transform t = hero.Find(part);
                if (t == null) continue;
                Vector3 p = t.localPosition;
                p.x = -Mathf.Abs(p.x);
                p.y -= 0.21f;
                t.localPosition = p;
                t.localScale *= 0.64f;
            }
            Vector3 ringCentre = new(-0.49f,1.70f,-0.10f);
            const int segments = 12;
            const float radius = 0.145f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                CapsuleLocal($"Staff Ring {i}", ringCentre + new Vector3(Mathf.Cos(a0)*radius,Mathf.Sin(a0)*radius,0f), ringCentre + new Vector3(Mathf.Cos(a1)*radius,Mathf.Sin(a1)*radius,0f), 0.012f, gold, hero);
            }
        }

        private static void BuildReferenceDiorama(Vector3 hero)
        {
            Material sky = Smooth("Reference Blue Sky", new Color(0.07f,0.50f,0.89f), 0f, new Color(0.018f,0.10f,0.18f));
            Material cloud = Smooth("Reference Cloud", new Color(0.99f,0.99f,0.98f));
            Material stone = Lookdev("Reference Warm Stone", "stone", new Color(0.75f,0.68f,0.55f), 0.20f);
            Material darkStone = Lookdev("Reference Dark Stone", "rock", new Color(0.47f,0.46f,0.40f), 0.16f);
            Material moss = Smooth("Reference Moss", new Color(0.40f,0.61f,0.19f));
            Material mossDark = Smooth("Reference Moss Dark", new Color(0.26f,0.47f,0.13f));
            Material trunk = Smooth("Reference Tree Wood", new Color(0.31f,0.18f,0.08f));
            Material leaf = Smooth("Reference Leaves", new Color(0.42f,0.63f,0.21f));
            Material leafLight = Smooth("Reference Leaves Light", new Color(0.58f,0.72f,0.27f));
            Material water = Transparent("Reference Water", new Color(0.16f,0.72f,0.88f,0.56f), new Color(0.025f,0.11f,0.16f));
            Material waterWhite = Transparent("Reference Water Highlight", new Color(0.94f,0.99f,1f,0.70f), new Color(0.06f,0.08f,0.10f));
            Material castleStone = Smooth("Reference Castle", new Color(0.70f,0.70f,0.65f));
            Material castleRoof = Smooth("Reference Castle Roof", new Color(0.33f,0.43f,0.53f));
            Material lantern = Smooth("Lantern Glow", new Color(1.0f,0.72f,0.18f), 0.1f, new Color(0.35f,0.16f,0.02f));

            GameObject skyPlane = Primitive(PrimitiveType.Quad, "Physical Blue Sky", sky, null);
            skyPlane.transform.position = hero + new Vector3(0f,3.7f,18f);
            skyPlane.transform.localScale = new Vector3(28f,22f,1f);
            CreateCloudCluster(hero + new Vector3(-1.15f,1.85f,14.5f),0.80f,cloud);
            CreateCloudCluster(hero + new Vector3(0.45f,2.10f,15.5f),0.92f,cloud);
            CreateCloudCluster(hero + new Vector3(1.90f,1.75f,14.0f),0.76f,cloud);

            CreateIsland(hero + new Vector3(0f,-0.55f,0.78f), new Vector3(4.0f,0.46f,2.85f), stone,moss,9);
            CreateBlock(hero + new Vector3(-1.85f,-0.60f,-0.15f), new Vector3(0.72f,0.55f,0.85f), stone, Quaternion.Euler(0f,5f,0f));
            CreateBlock(hero + new Vector3(1.90f,-0.58f,0.12f), new Vector3(0.75f,0.56f,0.80f), stone, Quaternion.Euler(0f,-7f,0f));

            CreateArch(hero + new Vector3(-1.20f,0f,3.30f),0.90f,2.40f,stone,moss);
            CreateArch(hero + new Vector3(-1.45f,-0.10f,6.20f),0.52f,1.45f,stone,mossDark);
            CreateTree(hero + new Vector3(-2.02f,-0.10f,4.20f),0.52f,trunk,leaf,leafLight);
            CreateLantern(hero + new Vector3(-1.55f,-0.05f,1.80f), stone, lantern);

            CreateIsland(hero + new Vector3(0.90f,0f,4.20f), new Vector3(2.10f,0.72f,1.95f),darkStone,moss,7);
            CreateIsland(hero + new Vector3(1.20f,0.42f,6.80f), new Vector3(2.15f,0.88f,2.00f),stone,moss,7);
            CreateIsland(hero + new Vector3(1.50f,0.82f,9.10f), new Vector3(2.20f,0.96f,2.00f),stone,mossDark,7);
            CreateShrub(hero + new Vector3(0.55f,0.42f,3.85f),0.38f,moss);
            CreateShrub(hero + new Vector3(1.48f,0.88f,6.35f),0.34f,mossDark);

            CreatePool(hero + new Vector3(1.55f,-0.10f,0.45f), new Vector3(2.20f,1.05f,1f),water);
            CreatePool(hero + new Vector3(1.48f,-0.02f,2.00f), new Vector3(2.05f,0.95f,1f),water);
            CreatePool(hero + new Vector3(1.25f,0.12f,3.45f), new Vector3(1.75f,0.82f,1f),water);
            CreateWaterfall(hero + new Vector3(1.02f,0.72f,3.05f),0.34f,1.02f,water,waterWhite);
            CreateWaterfall(hero + new Vector3(1.20f,0.88f,5.55f),0.27f,0.84f,water,waterWhite);
            CreateWaterfall(hero + new Vector3(1.48f,1.02f,7.85f),0.22f,0.70f,water,waterWhite);

            CreateCastle(hero + new Vector3(1.80f,-1.42f,11.80f),castleStone,castleRoof,mossDark);
            CreateIsland(hero + new Vector3(0.62f,1.05f,11.50f), new Vector3(0.86f,0.38f,0.70f),darkStone,moss,4);

            CreateShrub(hero + new Vector3(-1.70f,-0.10f,0.00f),0.62f,mossDark);
            CreateShrub(hero + new Vector3(-1.48f,-0.02f,1.10f),0.52f,moss);
            CreateShrub(hero + new Vector3(1.68f,-0.08f,0.08f),0.55f,mossDark);
            CreateShrub(hero + new Vector3(1.72f,0.04f,1.75f),0.46f,moss);
            CreateShrub(hero + new Vector3(-0.55f,-0.18f,-0.70f),0.46f,moss);
            CreateShrub(hero + new Vector3(0.55f,-0.18f,-0.72f),0.42f,mossDark);
            AddFlowerPatch(hero + new Vector3(-1.35f,-0.02f,0.20f));
            AddFlowerPatch(hero + new Vector3(1.30f,-0.02f,0.38f));
            AddFlowerPatch(hero + new Vector3(-1.50f,0.10f,1.90f));
        }

        private static void CreateIsland(Vector3 centre, Vector3 size, Material stone, Material moss, int rimBlocks)
        {
            // Build the rock mass from several chamfered pieces rather than one rectangular slab.
            int pieces = size.x > 3f ? 5 : 3;
            float pieceWidth = size.x / pieces;
            for (int i = 0; i < pieces; i++)
            {
                float x = -size.x * 0.5f + pieceWidth * (i + 0.5f);
                float yWobble = (i % 3 - 1) * size.y * 0.05f;
                float zWobble = ((i * 7) % 3 - 1) * size.z * 0.035f;
                CreateBlock(centre + new Vector3(x,yWobble,zWobble),
                    new Vector3(pieceWidth * 1.08f,size.y * (0.94f + (i & 1) * 0.08f),size.z * (0.90f + (i % 3) * 0.04f)),
                    stone, Quaternion.Euler((i & 1) * 1.5f,(i % 3 - 1) * 3.5f,(i % 2) * 1.5f));
            }

            Vector3[] offsets = { new(-0.18f,0f,0.02f), new(0.22f,0.01f,0.10f), new(0.02f,0.03f,-0.18f) };
            Vector3[] scales = { new(0.44f,0.20f,0.43f), new(0.34f,0.18f,0.36f), new(0.37f,0.16f,0.31f) };
            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject cap = Primitive(PrimitiveType.Sphere,"Moss Island Cap",moss,null);
                cap.transform.position = centre + new Vector3(offsets[i].x*size.x,size.y*0.47f+offsets[i].y,offsets[i].z*size.z);
                cap.transform.localScale = new Vector3(size.x*scales[i].x,Mathf.Max(0.16f,size.y*scales[i].y),size.z*scales[i].z);
            }

            for (int i = 0; i < rimBlocks; i++)
            {
                float t = i / (float)Mathf.Max(1,rimBlocks-1);
                float x = Mathf.Lerp(-size.x*0.46f,size.x*0.46f,t);
                float z = -size.z*0.48f + ((i&1)==0 ? 0.05f : -0.03f);
                CreateBlock(centre + new Vector3(x,size.y*0.43f,z), new Vector3(size.x/rimBlocks*1.05f,0.30f,0.35f), stone, Quaternion.Euler(0f,(i%3-1)*4f,(i&1)*2f));
            }
        }

        private static void CreateArch(Vector3 basePos, float radius, float height, Material stone, Material moss)
        {
            float block = Mathf.Max(0.30f,radius*0.34f);
            int pillarCount = Mathf.Max(4,Mathf.RoundToInt(height*0.70f/(block*0.92f)));
            for (int side=-1; side<=1; side+=2)
                for (int y=0; y<pillarCount; y++)
                    CreateBlock(basePos + new Vector3(side*radius,block*0.5f+y*block*0.92f,0f), new Vector3(block*1.04f,block,block*0.85f), stone, Quaternion.Euler(0f,(y%3-1)*3f,side*(y%2)*2f));
            int archBlocks=11;
            float springY=height*0.62f;
            for (int i=0; i<archBlocks; i++)
            {
                float t=i/(float)(archBlocks-1);
                float angle=Mathf.Lerp(180f,0f,t)*Mathf.Deg2Rad;
                CreateBlock(basePos + new Vector3(Mathf.Cos(angle)*radius,springY+Mathf.Sin(angle)*radius,0f), new Vector3(block*1.05f,block,block*0.90f), stone, Quaternion.Euler(0f,0f,-Mathf.Cos(angle)*26f));
            }
            CreateShrub(basePos + new Vector3(-radius,springY+radius*0.75f,-0.12f),radius*0.35f,moss);
            CreateShrub(basePos + new Vector3(radius*0.25f,springY+radius+0.05f,-0.10f),radius*0.28f,moss);
        }

        private static void CreateTree(Vector3 basePos, float scale, Material trunk, Material leaf, Material leafLight)
        {
            CapsuleWorld("Storybook Tree Trunk",basePos,basePos+new Vector3(0.18f,3.6f*scale,0.05f),0.25f*scale,trunk);
            CapsuleWorld("Storybook Tree Branch",basePos+new Vector3(0.10f,2.3f*scale,0f),basePos+new Vector3(0.95f*scale,3.25f*scale,0.10f),0.15f*scale,trunk);
            CapsuleWorld("Storybook Tree Branch",basePos+new Vector3(0.05f,2.4f*scale,0f),basePos+new Vector3(-0.90f*scale,3.30f*scale,0.12f),0.15f*scale,trunk);
            Vector3 crown=basePos+new Vector3(0.08f,3.65f*scale,0.1f);
            Vector3[] offsets={new(-1.10f,0f,0f),new(-0.55f,0.48f,0.05f),new(0f,0.62f,0f),new(0.62f,0.46f,0.05f),new(1.10f,0.02f,0f),new(-0.10f,-0.18f,-0.05f)};
            for(int i=0;i<offsets.Length;i++)
            {
                GameObject canopy=Primitive(PrimitiveType.Sphere,"Storybook Leaf Clump",(i&1)==0?leaf:leafLight,null);
                canopy.transform.position=crown+offsets[i]*scale;
                canopy.transform.localScale=new Vector3(1.30f,0.95f,1.05f)*scale;
            }
        }

        private static void CreateLantern(Vector3 basePos, Material stone, Material glow)
        {
            CreateBlock(basePos + new Vector3(0f,0.16f,0f),new Vector3(0.28f,0.30f,0.28f),stone,Quaternion.identity);
            GameObject light = Primitive(PrimitiveType.Cube,"Warm Ruin Lantern",glow,null);
            light.transform.position = basePos + new Vector3(0f,0.40f,-0.02f);
            light.transform.localScale = new Vector3(0.16f,0.23f,0.16f);
            CreateBlock(basePos + new Vector3(0f,0.55f,0f),new Vector3(0.24f,0.08f,0.24f),stone,Quaternion.Euler(0f,45f,0f));
        }

        private static void CreateCastle(Vector3 basePos, Material stone, Material roof, Material moss)
        {
            BuildTower(basePos,new Vector3(0.92f,3.10f,0.92f),stone,roof,6);
            BuildTower(basePos+new Vector3(-0.95f,-0.25f,0.22f),new Vector3(0.70f,2.20f,0.70f),stone,roof,5);
            BuildTower(basePos+new Vector3(0.92f,-0.35f,0.20f),new Vector3(0.62f,1.90f,0.62f),stone,roof,4);
            BuildTower(basePos+new Vector3(0.52f,0.65f,0.35f),new Vector3(0.52f,1.60f,0.52f),stone,roof,5);
            CreateBlock(basePos+new Vector3(0f,-0.48f,0f),new Vector3(2.35f,0.88f,1.28f),stone,Quaternion.identity);
            CreateShrub(basePos+new Vector3(-0.85f,0.38f,-0.42f),0.34f,moss);
        }

        private static void BuildTower(Vector3 basePos, Vector3 size, Material stone, Material roof, int roofSteps)
        {
            CreateBlock(basePos+new Vector3(0f,size.y*0.5f,0f),size,stone,Quaternion.identity);
            float y=basePos.y+size.y;
            for(int i=0;i<roofSteps;i++)
            {
                float t=i/(float)roofSteps;
                float width=size.x*Mathf.Lerp(1.05f,0.14f,t);
                CreateBlock(new Vector3(basePos.x,y+0.10f+i*0.15f,basePos.z),new Vector3(width,0.18f,width),roof,Quaternion.Euler(0f,i*3f,0f));
            }
        }

        private static void CreateWaterfall(Vector3 centre, float width, float height, Material water, Material white)
        {
            GameObject body=Primitive(PrimitiveType.Quad,"Waterfall Curtain",water,null);
            body.transform.position=centre;
            body.transform.localScale=new Vector3(width,height,1f);
            GameObject streak=Primitive(PrimitiveType.Quad,"Waterfall Sun Streak",white,null);
            streak.transform.position=centre+new Vector3(-width*0.16f,0f,-0.025f);
            streak.transform.localScale=new Vector3(width*0.22f,height*0.94f,1f);
            for(int i=0;i<4;i++)
            {
                GameObject foam=Primitive(PrimitiveType.Sphere,"Waterfall Foam",white,null);
                foam.transform.position=centre+new Vector3((i-1.5f)*width*0.20f,-height*0.51f,-0.04f);
                foam.transform.localScale=new Vector3(width*0.32f,0.07f,0.16f);
            }
        }

        private static void CreatePool(Vector3 centre, Vector3 scale, Material water)
        {
            GameObject pool=Primitive(PrimitiveType.Quad,"Turquoise Stream",water,null);
            pool.transform.position=centre;
            pool.transform.rotation=Quaternion.Euler(90f,0f,0f);
            pool.transform.localScale=scale;
        }

        private static void CreateCloudCluster(Vector3 centre,float scale,Material cloud)
        {
            Vector3[] offsets={new(-0.95f,0f,0f),new(-0.35f,0.35f,0f),new(0.32f,0.38f,0f),new(0.95f,0.03f,0f),new(0.08f,-0.22f,-0.05f)};
            Vector3[] sizes={new(1.10f,0.78f,0.62f),new(1.35f,1.0f,0.72f),new(1.42f,1.03f,0.74f),new(1.05f,0.75f,0.60f),new(1.30f,0.60f,0.66f)};
            for(int i=0;i<offsets.Length;i++)
            {
                GameObject puff=Primitive(PrimitiveType.Sphere,"Soft Cloud Puff",cloud,null);
                puff.transform.position=centre+offsets[i]*scale;
                puff.transform.localScale=sizes[i]*scale;
                Renderer r=puff.GetComponent<Renderer>();
                r.shadowCastingMode=ShadowCastingMode.Off;
                r.receiveShadows=false;
            }
        }

        private static void CreateShrub(Vector3 centre,float scale,Material moss)
        {
            for(int i=0;i<4;i++)
            {
                float angle=i*Mathf.PI*0.58f;
                GameObject lump=Primitive(PrimitiveType.Sphere,"Rounded Moss Shrub",moss,null);
                lump.transform.position=centre+new Vector3(Mathf.Cos(angle)*scale*0.35f,(i&1)*scale*0.12f,Mathf.Sin(angle)*scale*0.24f);
                lump.transform.localScale=new Vector3(scale*0.72f,scale*0.46f,scale*0.60f);
            }
        }

        private static void AddFlowerPatch(Vector3 basePos)
        {
            Material stem=Smooth("Flower Stem",new Color(0.22f,0.43f,0.10f));
            Material white=Smooth("Flower White",new Color(0.98f,0.96f,0.90f));
            Material pink=Smooth("Flower Pink",new Color(0.94f,0.50f,0.60f));
            Material yellow=Smooth("Flower Yellow",new Color(0.98f,0.76f,0.16f));
            Material blue=Smooth("Flower Blue",new Color(0.42f,0.67f,0.90f));
            Material[] petals={white,pink,blue,white,pink};
            for(int i=0;i<5;i++)
            {
                Vector3 p=basePos+new Vector3((i-2)*0.18f,0f,((i*7)%3-1)*0.14f);
                float h=0.18f+(i%3)*0.030f;
                CapsuleWorld("Flower Stem",p,p+Vector3.up*h,0.010f,stem);
                GameObject centre=Primitive(PrimitiveType.Sphere,"Flower Centre",yellow,null);
                centre.transform.position=p+Vector3.up*h;
                centre.transform.localScale=Vector3.one*0.040f;
                for(int petal=0;petal<5;petal++)
                {
                    float a=petal*Mathf.PI*2f/5f;
                    GameObject petalObject=Primitive(PrimitiveType.Sphere,"Flower Petal",petals[i],null);
                    petalObject.transform.position=p+Vector3.up*h+new Vector3(Mathf.Cos(a)*0.055f,0f,Mathf.Sin(a)*0.055f);
                    petalObject.transform.localScale=new Vector3(0.065f,0.020f,0.043f);
                }
            }
        }

        private static Material Smooth(string name,Color colour,float smoothness=0.06f,Color? emission=null)
        {
            Material m=new Material(SmoothShader()){name=name};
            m.SetTexture("_MainTex",Texture2D.whiteTexture);
            m.SetColor("_BaseColor",colour);
            m.SetColor("_EmissionColor",emission??Color.black);
            m.SetFloat("_Smoothness",smoothness);
            m.SetFloat("_Cull",2f);
            m.SetFloat("_ZWrite",1f);
            Created.Add(m);
            return m;
        }

        private static Material Transparent(string name,Color colour,Color emission)
        {
            Material m=Smooth(name,colour,0.05f,emission);
            m.SetFloat("_Cull",0f);
            m.SetFloat("_ZWrite",0f);
            m.renderQueue=(int)RenderQueue.Transparent;
            return m;
        }

        private static Material Lookdev(string name,string textureName,Color tint,float influence)
        {
            Shader shader=Shader.Find("VoxelEngine/WorldArtLookdev")??SmoothShader();
            Material m=new Material(shader){name=name};
            Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/Stylized/{textureName}_color.png");
            if(m.HasProperty("_MainTex"))m.SetTexture("_MainTex",texture!=null?texture:Texture2D.whiteTexture);
            if(m.HasProperty("_Tint"))m.SetColor("_Tint",tint);
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",tint);
            if(m.HasProperty("_TextureScale"))m.SetFloat("_TextureScale",0.24f);
            if(m.HasProperty("_TextureInfluence"))m.SetFloat("_TextureInfluence",influence);
            if(m.HasProperty("_TopLight"))m.SetFloat("_TopLight",0.14f);
            Created.Add(m);
            return m;
        }

        private static GameObject Primitive(PrimitiveType type,string name,Material material,Transform parent)
        {
            GameObject go=GameObject.CreatePrimitive(type);
            go.name=name;
            if(parent!=null)go.transform.SetParent(parent,false);
            Collider c=go.GetComponent<Collider>();
            if(c!=null)Object.DestroyImmediate(c);
            Renderer r=go.GetComponent<Renderer>();
            r.sharedMaterial=material;
            r.shadowCastingMode=material.renderQueue>=(int)RenderQueue.Transparent?ShadowCastingMode.Off:ShadowCastingMode.On;
            r.receiveShadows=true;
            Created.Add(go);
            return go;
        }

        private static GameObject MeshObject(string name,Mesh mesh,Material material,Transform parent)
        {
            GameObject go=new GameObject(name);
            if(parent!=null)go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer renderer=go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=material.renderQueue>=(int)RenderQueue.Transparent?ShadowCastingMode.Off:ShadowCastingMode.On;
            renderer.receiveShadows=true;
            Created.Add(go);
            return go;
        }

        private static void CreateBlock(Vector3 position,Vector3 scale,Material material,Quaternion rotation)
        {
            GameObject block=MeshObject("Storybook Stone Block",BeveledCube(),material,null);
            block.transform.position=position;
            block.transform.rotation=rotation;
            block.transform.localScale=scale;
        }

        private static Mesh BeveledCube()
        {
            if(_beveledCube!=null)return _beveledCube;
            const float h=0.5f;
            const float i=0.41f;
            var vertices=new List<Vector3>(128);
            var normals=new List<Vector3>(128);
            var triangles=new List<int>(192);

            void AddQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward)
            {
                Vector3 n=Vector3.Cross(b-a,c-a).normalized;
                if(Vector3.Dot(n,outward)<0f){Vector3 tmp=b;b=d;d=tmp;n=Vector3.Cross(b-a,c-a).normalized;}
                int s=vertices.Count;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                normals.Add(n);normals.Add(n);normals.Add(n);normals.Add(n);
                triangles.Add(s);triangles.Add(s+1);triangles.Add(s+2);triangles.Add(s);triangles.Add(s+2);triangles.Add(s+3);
            }
            void AddTri(Vector3 a,Vector3 b,Vector3 c,Vector3 outward)
            {
                Vector3 n=Vector3.Cross(b-a,c-a).normalized;
                if(Vector3.Dot(n,outward)<0f){Vector3 tmp=b;b=c;c=tmp;n=Vector3.Cross(b-a,c-a).normalized;}
                int s=vertices.Count;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);
                normals.Add(n);normals.Add(n);normals.Add(n);
                triangles.Add(s);triangles.Add(s+1);triangles.Add(s+2);
            }

            AddQuad(new Vector3(h,-i,-i),new Vector3(h,i,-i),new Vector3(h,i,i),new Vector3(h,-i,i),Vector3.right);
            AddQuad(new Vector3(-h,-i,i),new Vector3(-h,i,i),new Vector3(-h,i,-i),new Vector3(-h,-i,-i),Vector3.left);
            AddQuad(new Vector3(-i,h,-i),new Vector3(-i,h,i),new Vector3(i,h,i),new Vector3(i,h,-i),Vector3.up);
            AddQuad(new Vector3(-i,-h,i),new Vector3(-i,-h,-i),new Vector3(i,-h,-i),new Vector3(i,-h,i),Vector3.down);
            AddQuad(new Vector3(-i,-i,h),new Vector3(i,-i,h),new Vector3(i,i,h),new Vector3(-i,i,h),Vector3.forward);
            AddQuad(new Vector3(i,-i,-h),new Vector3(-i,-i,-h),new Vector3(-i,i,-h),new Vector3(i,i,-h),Vector3.back);

            for(int sy=-1;sy<=1;sy+=2)for(int sz=-1;sz<=1;sz+=2)
                AddQuad(new Vector3(-i,sy*h,sz*i),new Vector3(i,sy*h,sz*i),new Vector3(i,sy*i,sz*h),new Vector3(-i,sy*i,sz*h),new Vector3(0,sy,sz));
            for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)
                AddQuad(new Vector3(sx*h,-i,sz*i),new Vector3(sx*i,-i,sz*h),new Vector3(sx*i,i,sz*h),new Vector3(sx*h,i,sz*i),new Vector3(sx,0,sz));
            for(int sx=-1;sx<=1;sx+=2)for(int sy=-1;sy<=1;sy+=2)
                AddQuad(new Vector3(sx*h,sy*i,-i),new Vector3(sx*i,sy*h,-i),new Vector3(sx*i,sy*h,i),new Vector3(sx*h,sy*i,i),new Vector3(sx,sy,0));
            for(int sx=-1;sx<=1;sx+=2)for(int sy=-1;sy<=1;sy+=2)for(int sz=-1;sz<=1;sz+=2)
                AddTri(new Vector3(sx*h,sy*i,sz*i),new Vector3(sx*i,sy*h,sz*i),new Vector3(sx*i,sy*i,sz*h),new Vector3(sx,sy,sz));

            _beveledCube=new Mesh{name="Runtime Chamfered Storybook Block"};
            _beveledCube.SetVertices(vertices);
            _beveledCube.SetNormals(normals);
            _beveledCube.SetTriangles(triangles,0);
            _beveledCube.RecalculateBounds();
            return _beveledCube;
        }

        private static void CapsuleWorld(string name,Vector3 a,Vector3 b,float radius,Material material)
        {
            Vector3 d=b-a;if(d.sqrMagnitude<0.0001f)return;
            GameObject capsule=Primitive(PrimitiveType.Capsule,name,material,null);
            capsule.transform.position=(a+b)*0.5f;
            capsule.transform.rotation=Quaternion.FromToRotation(Vector3.up,d.normalized);
            capsule.transform.localScale=new Vector3(radius*2f,Mathf.Max(radius,d.magnitude*0.5f),radius*2f);
        }

        private static void CapsuleLocal(string name,Vector3 a,Vector3 b,float radius,Material material,Transform parent)
        {
            Vector3 d=b-a;if(d.sqrMagnitude<0.0001f)return;
            GameObject capsule=Primitive(PrimitiveType.Capsule,name,material,parent);
            capsule.transform.localPosition=(a+b)*0.5f;
            capsule.transform.localRotation=Quaternion.FromToRotation(Vector3.up,d.normalized);
            capsule.transform.localScale=new Vector3(radius*2f,Mathf.Max(radius,d.magnitude*0.5f),radius*2f);
        }
    }
}
