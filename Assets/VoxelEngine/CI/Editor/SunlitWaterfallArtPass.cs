using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Final art-direction pass for the environment-only Sunlit Waterfall target.
    ///
    /// The capture still builds the destructible voxel substrate first. This pass replaces the
    /// rough visualization skin with a small reusable 3D kit: rounded terraces with separate turf
    /// and cliff surfaces, chunky rounded masonry, waterfall ribbons/pools, organic foliage and a
    /// simplified distant castle. These pieces are intentionally generic enough to become actual
    /// biome building blocks rather than one-off screenshot geometry.
    /// </summary>
    internal static class SunlitWaterfallArtPass
    {
        private static bool _prepared;
        private static readonly List<Object> Created = new();
        private static Transform _root;

        private static Material _grass;
        private static Material _grassLight;
        private static Material _cliff;
        private static Material _cliffLight;
        private static Material _stone;
        private static Material _stoneLight;
        private static Material _moss;
        private static Material _mossLight;
        private static Material _bark;
        private static Material _leaf;
        private static Material _leafLight;
        private static Material _water;
        private static Material _waterfall;
        private static Material _foam;
        private static Material _cloud;
        private static Material _sky;
        private static Material _roof;
        private static Material _flowerWhite;
        private static Material _flowerPink;
        private static Material _flowerBlue;
        private static Material _flowerYellow;

        public static void Apply(Camera camera)
        {
            if (_prepared || camera == null) return;
            _prepared = true;

            GameObject prototype = GameObject.Find("Sunlit Waterfall Art Kit");
            Vector3 origin = ResolveOrigin(prototype);
            if (prototype != null) prototype.SetActive(false);

            var root = new GameObject("Sunlit Waterfall Target Scene");
            _root = root.transform;
            Created.Add(root);

            BuildPalette();
            ConfigureShot(camera, origin);
            BuildSky(origin);
            BuildTerrain(origin);
            BuildRuins(origin);
            BuildWater(origin);
            BuildTreeAndShrubs(origin);
            BuildFlowers(origin);
            BuildDistantCastle(origin);
        }

        private static Vector3 ResolveOrigin(GameObject prototype)
        {
            if (prototype != null)
            {
                foreach (Transform t in prototype.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Foreground turf")
                        return t.position - new Vector3(-0.15f, 0.10f, -3.70f);
                }
            }
            return Vector3.zero;
        }

        // ------------------------------------------------------------------
        // Shot / palette
        // ------------------------------------------------------------------

        private static void ConfigureShot(Camera camera, Vector3 o)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.50f, 0.86f, 1f);
            camera.fieldOfView = 34f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.position = o + new Vector3(0.65f, 4.25f, -21.3f);
            camera.transform.LookAt(o + new Vector3(0.0f, 3.25f, 3.6f));

            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.74f, 0.91f);
            RenderSettings.ambientEquatorColor = new Color(0.54f, 0.56f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.25f, 0.17f);
            RenderSettings.ambientIntensity = 0.72f;

            Light existing = Object.FindFirstObjectByType<Light>();
            if (existing != null)
            {
                existing.color = new Color(1.0f, 0.92f, 0.73f);
                existing.intensity = 1.12f;
                existing.shadows = LightShadows.Soft;
                existing.shadowStrength = 0.42f;
                existing.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            }
        }

        private static void BuildPalette()
        {
            _grass       = Smooth("Target Sunlit Grass", new Color(0.42f, 0.60f, 0.17f), 0.025f);
            _grassLight  = Smooth("Target Grass Highlight", new Color(0.53f, 0.68f, 0.22f), 0.025f);
            _cliff       = Smooth("Target Garden Cliff", new Color(0.38f, 0.39f, 0.32f), 0.025f);
            _cliffLight  = Smooth("Target Warm Cliff", new Color(0.48f, 0.47f, 0.38f), 0.025f);
            _stone       = Smooth("Target Warm Ashlar", new Color(0.68f, 0.63f, 0.52f), 0.045f);
            _stoneLight  = Smooth("Target Sunlit Ashlar", new Color(0.80f, 0.74f, 0.61f), 0.045f);
            _moss        = Smooth("Target Moss", new Color(0.28f, 0.46f, 0.12f), 0.02f);
            _mossLight   = Smooth("Target Moss Sun", new Color(0.42f, 0.59f, 0.16f), 0.02f);
            _bark        = Smooth("Target Bark", new Color(0.29f, 0.18f, 0.085f), 0.02f);
            _leaf        = Smooth("Target Leaves", new Color(0.25f, 0.46f, 0.12f), 0.02f);
            _leafLight   = Smooth("Target Leaves Sun", new Color(0.45f, 0.62f, 0.18f), 0.02f);
            _water       = Transparent("Target Turquoise Water", new Color(0.08f, 0.65f, 0.82f, 0.86f), new Color(0.02f, 0.11f, 0.15f));
            _waterfall   = Transparent("Target Cascade Water", new Color(0.62f, 0.90f, 0.98f, 0.84f), new Color(0.12f, 0.21f, 0.24f));
            _foam        = Transparent("Target Foam", new Color(0.95f, 0.99f, 1f, 0.70f), new Color(0.24f, 0.28f, 0.30f));
            _cloud       = Smooth("Target Cloud", new Color(0.98f, 0.985f, 0.96f), 0.01f, new Color(0.10f, 0.10f, 0.09f));
            _sky         = Smooth("Target Blue Sky", new Color(0.055f, 0.45f, 0.86f), 0f, new Color(0.10f, 0.23f, 0.46f));
            _roof        = Smooth("Target Castle Roof", new Color(0.43f, 0.43f, 0.40f), 0.06f);
            _flowerWhite = Smooth("Target White Flower", new Color(0.98f, 0.97f, 0.88f), 0.02f);
            _flowerPink  = Smooth("Target Pink Flower", new Color(0.95f, 0.49f, 0.58f), 0.02f);
            _flowerBlue  = Smooth("Target Blue Flower", new Color(0.35f, 0.66f, 0.91f), 0.02f);
            _flowerYellow= Smooth("Target Yellow Flower", new Color(0.98f, 0.72f, 0.14f), 0.02f);
        }

        // ------------------------------------------------------------------
        // Environment composition
        // ------------------------------------------------------------------

        private static void BuildSky(Vector3 o)
        {
            GameObject sky = Primitive(PrimitiveType.Quad, "Physical Saturated Blue Sky", _sky);
            sky.transform.position = o + new Vector3(0f, 9.5f, 48f);
            sky.transform.localScale = new Vector3(42f, 28f, 1f);
            Renderer skyRenderer = sky.GetComponent<Renderer>();
            skyRenderer.shadowCastingMode = ShadowCastingMode.Off;
            skyRenderer.receiveShadows = false;

            Cloud(o + new Vector3(-7.0f, 11.0f, 30f), 1.05f);
            Cloud(o + new Vector3(0.2f, 12.7f, 33f), 1.42f);
            Cloud(o + new Vector3(9.5f, 11.5f, 31f), 1.20f);
            Cloud(o + new Vector3(4.7f, 8.9f, 28f), 0.72f);
        }

        private static void BuildTerrain(Vector3 o)
        {
            // Foreground hero island: an open playable lawn framed by chunky stone and water.
            Terrace("Foreground hero terrace", o + new Vector3(0f, 0.20f, -3.6f),
                    11.6f, 7.0f, 2.3f, 2.2f, _grass, _cliff, true);

            // Left raised garden bank supports the arch and oak.
            Terrace("Left ruin bank", o + new Vector3(-7.2f, 2.25f, 1.0f),
                    7.5f, 7.8f, 3.5f, 1.8f, _grass, _cliffLight, false);

            // Layered garden behind the empty character position.
            Terrace("Middle garden", o + new Vector3(-1.8f, 2.75f, 5.2f),
                    10.5f, 6.8f, 2.8f, 2.0f, _grassLight, _cliff, false);

            // Right-side cascade rises into the distance. Each shelf has a clean flat top and a
            // rounded cliff face rather than the prototype's full ellipsoid/pancake silhouette.
            Terrace("Cascade shelf 0", o + new Vector3(5.1f, 0.55f, -0.25f),
                    7.2f, 5.2f, 2.1f, 1.45f, _grass, _cliff, false);
            Terrace("Cascade shelf 1", o + new Vector3(6.2f, 2.55f, 3.0f),
                    7.0f, 5.0f, 2.2f, 1.40f, _grassLight, _cliffLight, false);
            Terrace("Cascade shelf 2", o + new Vector3(7.1f, 4.55f, 6.3f),
                    6.6f, 4.8f, 2.2f, 1.35f, _grass, _cliff, false);
            Terrace("Cascade shelf 3", o + new Vector3(7.8f, 6.50f, 9.5f),
                    6.2f, 4.6f, 2.25f, 1.30f, _grassLight, _cliffLight, false);

            // Small stepping islands in the foreground water are a key part of the reference.
            Terrace("Front left island", o + new Vector3(-5.8f, -0.52f, -7.2f),
                    4.2f, 3.0f, 1.25f, 1.0f, _grass, _cliff, false);
            Terrace("Front right island", o + new Vector3(5.8f, -0.70f, -6.5f),
                    4.6f, 3.2f, 1.35f, 1.05f, _grassLight, _cliff, false);

            // A few visible exposed stone blocks preserve the voxel/constructed language.
            for (int i = 0; i < 8; i++)
            {
                float x = -4.1f + i * 1.18f;
                RoundedStone(o + new Vector3(x, 0.55f + (i % 2) * 0.06f, -7.05f + (i % 3) * 0.08f),
                             new Vector3(0.95f, 0.58f, 0.78f), i % 3 == 0 ? _stoneLight : _stone,
                             i * 31);
            }
        }

        private static void BuildRuins(Vector3 o)
        {
            // Main left arch: complete readable semicircle with an intentionally chipped crown.
            Vector3 c = o + new Vector3(-6.05f, 4.45f, -0.15f);
            const float r = 2.65f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 7; row++)
                {
                    Vector3 p = c + new Vector3(side * r, -3.95f + row * 0.72f, 0f);
                    RoundedStone(p, new Vector3(0.86f, 0.66f, 0.82f),
                                 (row + side) % 3 == 0 ? _stoneLight : _stone, row * 17 + side * 3);
                }
            }

            for (int i = 0; i <= 13; i++)
            {
                if (i == 9) continue; // one missing crown block makes the ruin feel broken.
                float t = i / 13f;
                float a = Mathf.Lerp(180f, 0f, t) * Mathf.Deg2Rad;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                GameObject stone = RoundedStone(p, new Vector3(0.88f, 0.64f, 0.82f),
                                                i % 4 == 0 ? _stoneLight : _stone, 100 + i);
                stone.transform.rotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg + 90f);
            }

            // Lower arch peeking through foliage below the main one.
            Vector3 lc = o + new Vector3(-6.4f, 0.95f, -3.3f);
            const float lr = 1.45f;
            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 4; row++)
                    RoundedStone(lc + new Vector3(side * lr, -1.72f + row * 0.56f, 0f),
                                 new Vector3(0.62f, 0.50f, 0.66f), _stone, 200 + row + side);
            for (int i = 0; i <= 9; i++)
            {
                float a = Mathf.Lerp(180f, 0f, i / 9f) * Mathf.Deg2Rad;
                GameObject s = RoundedStone(lc + new Vector3(Mathf.Cos(a)*lr, Mathf.Sin(a)*lr, 0f),
                                            new Vector3(0.64f,0.48f,0.66f), i%3==0?_stoneLight:_stone,240+i);
                s.transform.rotation = Quaternion.Euler(0f,0f,-a*Mathf.Rad2Deg+90f);
            }

            // Broken wall stubs / rubble on both sides of the water channel.
            for (int i=0;i<5;i++)
                RoundedStone(o + new Vector3(6.8f + (i%2)*0.8f, 0.25f + i*0.48f, -4.7f + (i%2)*0.15f),
                             new Vector3(0.82f,0.66f,0.78f), i%2==0?_stone:_stoneLight,300+i);
            for (int i=0;i<4;i++)
                RoundedStone(o + new Vector3(-4.7f-i*0.8f, -0.15f+i*0.22f, -5.5f+(i%2)*0.2f),
                             new Vector3(0.80f,0.54f,0.72f), _stone,330+i);

            AddMossAndIvy(o, c);
        }

        private static void BuildWater(Vector3 o)
        {
            // Foreground water wraps around the hero terrace instead of reading as one giant lake.
            Pool("Front turquoise channel", o + new Vector3(1.7f, -1.04f, -6.2f), 13.0f, 5.2f, 2.0f);
            Pool("Lower right pool", o + new Vector3(5.0f, -0.06f, -1.35f), 6.5f, 3.2f, 1.3f);
            Pool("Mid cascade pool", o + new Vector3(6.2f, 1.98f, 2.15f), 5.5f, 2.8f, 1.1f);
            Pool("Upper cascade pool", o + new Vector3(7.2f, 3.98f, 5.55f), 4.8f, 2.5f, 1.0f);
            Pool("Top cascade pool", o + new Vector3(7.9f, 5.96f, 8.75f), 4.0f, 2.2f, 0.9f);

            Waterfall(o + new Vector3(5.55f, 2.05f, 1.05f), o + new Vector3(5.0f, 0.05f, -0.15f), 2.15f);
            Waterfall(o + new Vector3(6.65f, 4.04f, 4.45f), o + new Vector3(6.15f, 2.04f, 3.2f), 1.92f);
            Waterfall(o + new Vector3(7.55f, 6.02f, 7.65f), o + new Vector3(7.08f, 4.03f, 6.45f), 1.70f);

            // Small final spill on the near right edge.
            Waterfall(o + new Vector3(8.0f, 0.55f, -3.8f), o + new Vector3(7.7f, -1.2f, -5.1f), 1.0f);
        }

        private static void BuildTreeAndShrubs(Vector3 o)
        {
            Vector3 b = o + new Vector3(-9.3f, 1.0f, 0.0f);
            Capsule("Oak trunk", b, b + new Vector3(0.25f, 7.2f, 0.4f), 0.72f, _bark);
            Capsule("Oak bough left", b + new Vector3(0f,4.4f,0f), b + new Vector3(-3.1f,7.7f,0.5f), 0.38f, _bark);
            Capsule("Oak bough right", b + new Vector3(0.1f,4.9f,0.2f), b + new Vector3(3.2f,7.5f,1.2f), 0.35f, _bark);
            Capsule("Oak bough crown", b + new Vector3(0.2f,5f,0.2f), b + new Vector3(-0.4f,8.6f,0.8f), 0.31f, _bark);

            Vector3[] crown =
            {
                new(-3.4f,7.6f,0.2f), new(-2.1f,8.6f,0.4f), new(-0.4f,8.9f,0.8f),
                new(1.5f,8.5f,1.0f), new(3.0f,7.7f,1.1f), new(-4.0f,6.7f,0.6f),
                new(-1.2f,7.2f,-0.3f), new(1.3f,7.1f,-0.2f), new(3.8f,6.7f,0.6f),
            };
            for (int i=0;i<crown.Length;i++)
            {
                GameObject leaf = Primitive(PrimitiveType.Sphere, "Rounded oak canopy", i%3==0?_leafLight:_leaf);
                leaf.transform.position = b + crown[i];
                leaf.transform.localScale = new Vector3(2.8f+(i%2)*0.45f, 2.1f+(i%3)*0.18f, 2.3f+(i%2)*0.35f);
            }

            Shrub(o + new Vector3(-4.9f,0.95f,-5.4f),1.1f,_moss);
            Shrub(o + new Vector3(-7.8f,3.1f,-0.5f),1.0f,_mossLight);
            Shrub(o + new Vector3(3.9f,0.75f,-4.4f),0.9f,_moss);
            Shrub(o + new Vector3(6.0f,2.9f,2.4f),0.72f,_mossLight);
            Shrub(o + new Vector3(7.5f,4.9f,5.6f),0.68f,_moss);
        }

        private static void BuildFlowers(Vector3 o)
        {
            FlowerPatch(o + new Vector3(-3.5f,1.40f,-4.9f), _flowerWhite, 1);
            FlowerPatch(o + new Vector3(-1.9f,1.42f,-5.4f), _flowerPink, 2);
            FlowerPatch(o + new Vector3(2.7f,1.34f,-4.1f), _flowerBlue, 3);
            FlowerPatch(o + new Vector3(-7.5f,4.2f,-0.6f), _flowerYellow, 4);
            FlowerPatch(o + new Vector3(-6.5f,4.0f,1.0f), _flowerWhite, 5);
            FlowerPatch(o + new Vector3(5.0f,1.6f,-0.1f), _flowerPink, 6);
        }

        private static void BuildDistantCastle(Vector3 o)
        {
            // Castle sits on its own garden stack so it reads as a distant landmark rather than
            // giant foreground cylinders.
            Vector3 hill = o + new Vector3(8.8f, 7.8f, 22.5f);
            Terrace("Distant castle cliff", hill, 8.3f, 6.4f, 5.6f, 1.8f, _grassLight, _cliffLight, false);

            Vector3 basePos = hill + new Vector3(0f, 0.55f, 0f);
            CastleTower(basePos + new Vector3(0f,0f,0f), 0.78f, 5.7f);
            CastleTower(basePos + new Vector3(-1.8f,-0.2f,-0.15f), 0.58f, 4.2f);
            CastleTower(basePos + new Vector3(1.8f,-0.1f,0.3f), 0.56f, 4.0f);
            CastleTower(basePos + new Vector3(0.8f,1.4f,0.2f), 0.43f, 3.5f);

            GameObject keep = Primitive(PrimitiveType.Cube, "Distant pale castle keep", _stoneLight);
            keep.transform.position = basePos + new Vector3(0f,1.55f,0.5f);
            keep.transform.localScale = new Vector3(3.8f,3.0f,2.2f);

            Waterfall(hill + new Vector3(-2.8f, -0.1f, -1.3f), hill + new Vector3(-3.0f,-4.7f,-1.6f), 1.05f);
        }

        // ------------------------------------------------------------------
        // Reusable vocabulary
        // ------------------------------------------------------------------

        private static void Terrace(string name, Vector3 topCentre, float width, float depth,
                                    float height, float radius, Material turf, Material cliff,
                                    bool addPath)
        {
            Mesh sideMesh = RoundedTerraceMesh(width, depth, height, radius, 5, false);
            Mesh topMesh = RoundedTerraceMesh(width * 0.965f, depth * 0.965f, 0.05f,
                                              Mathf.Max(0.15f, radius * 0.94f), 5, true);
            Created.Add(sideMesh); Created.Add(topMesh);

            GameObject sides = MeshObject(name + " cliff", sideMesh, cliff);
            sides.transform.position = topCentre;
            GameObject top = MeshObject(name + " turf", topMesh, turf);
            top.transform.position = topCentre + Vector3.up * 0.035f;

            // Soft moss lip around selected foreground terraces.
            if (width > 7f)
            {
                for (int i=0;i<5;i++)
                {
                    float f=(i-2)/2f;
                    Shrub(topCentre + new Vector3(f*width*0.36f,0.16f,-depth*0.46f),0.42f+(i%2)*0.08f,_moss);
                }
            }

            if (addPath)
            {
                for (int i=0;i<7;i++)
                {
                    RoundedStone(topCentre + new Vector3(-1.9f+i*0.65f,0.18f,-0.55f+(i%2)*0.05f),
                                 new Vector3(0.52f,0.12f,0.45f), i%3==0?_stoneLight:_stone,700+i);
                }
            }
        }

        private static GameObject RoundedStone(Vector3 position, Vector3 scale, Material material, int seed)
        {
            // Capsules give us deliberately softened ashlar without introducing a high-detail mesh
            // library. They remain simple procedural pieces and their silhouette matches the concept
            // much better than perfect cubes.
            GameObject stone = Primitive(PrimitiveType.Capsule, "Rounded storybook ashlar", material);
            stone.transform.position = position;
            stone.transform.localScale = new Vector3(scale.x, scale.y * 0.52f, scale.z);
            stone.transform.rotation = Quaternion.Euler((Hash01(seed+5)-0.5f)*3f,
                                                        (Hash01(seed+11)-0.5f)*6f,
                                                        (Hash01(seed+19)-0.5f)*3f);
            return stone;
        }

        private static void Pool(string name, Vector3 centre, float width, float depth, float radius)
        {
            Mesh mesh = RoundedTopMesh(width, depth, radius, 7);
            Created.Add(mesh);
            GameObject water = MeshObject(name, mesh, _water);
            water.transform.position = centre;
        }

        private static void Waterfall(Vector3 top, Vector3 bottom, float width)
        {
            Mesh main = RibbonMesh(top, bottom, width, 18, false);
            Mesh streak = RibbonMesh(top + new Vector3(0f,0.02f,-0.015f), bottom + new Vector3(0f,0.03f,-0.02f), width*0.38f, 18, true);
            Created.Add(main); Created.Add(streak);
            MeshObject("Soft waterfall sheet", main, _waterfall);
            MeshObject("Waterfall sun streak", streak, _foam);

            for (int i=0;i<7;i++)
            {
                float f=(i-3)/3f;
                GameObject puff=Primitive(PrimitiveType.Sphere,"Waterfall foam puff",_foam);
                puff.transform.position=bottom+new Vector3(f*width*0.45f,0.02f+(i%2)*0.08f,((i*5)%3-1)*0.09f);
                puff.transform.localScale=new Vector3(width*0.22f,0.13f,0.28f);
            }
        }

        private static void Cloud(Vector3 centre, float scale)
        {
            Vector3[] offsets={new(-1.15f,-0.05f,0f),new(-0.45f,0.35f,0.05f),new(0.25f,0.45f,0.08f),new(1.0f,0f,0f),new(0.1f,-0.28f,-0.03f)};
            Vector3[] sizes={new(1.25f,0.85f,0.85f),new(1.55f,1.20f,1.0f),new(1.65f,1.25f,1.05f),new(1.25f,0.85f,0.85f),new(1.55f,0.72f,0.95f)};
            for(int i=0;i<offsets.Length;i++)
            {
                GameObject puff=Primitive(PrimitiveType.Sphere,"Puffy storybook cloud",_cloud);
                puff.transform.position=centre+offsets[i]*scale;
                puff.transform.localScale=sizes[i]*scale;
                Renderer r=puff.GetComponent<Renderer>();r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            }
        }

        private static void AddMossAndIvy(Vector3 o, Vector3 archCentre)
        {
            for(int i=0;i<7;i++)
            {
                GameObject moss=Primitive(PrimitiveType.Sphere,"Arch moss cushion",i%2==0?_mossLight:_moss);
                moss.transform.position=archCentre+new Vector3(-2.5f+i*0.72f,2.7f+(i%3)*0.18f,-0.35f);
                moss.transform.localScale=new Vector3(0.85f,0.22f,0.48f);
            }
            for(int strand=0;strand<4;strand++)
            {
                Vector3 start=archCentre+new Vector3(-2.1f+strand*1.25f,2.65f-strand*0.08f,-0.42f);
                int count=5+strand;
                for(int i=0;i<count;i++)
                {
                    GameObject leaf=Primitive(PrimitiveType.Sphere,"Hanging arch ivy",_leaf);
                    leaf.transform.position=start+new Vector3(Mathf.Sin(i*1.5f)*0.12f,-i*0.35f,0f);
                    leaf.transform.localScale=new Vector3(0.26f,0.18f,0.10f);
                }
            }
        }

        private static void Shrub(Vector3 centre,float scale,Material material)
        {
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*0.63f;
                GameObject lump=Primitive(PrimitiveType.Sphere,"Rounded moss shrub",material);
                lump.transform.position=centre+new Vector3(Mathf.Cos(a)*scale*0.32f,(i&1)*scale*0.08f,Mathf.Sin(a)*scale*0.24f);
                lump.transform.localScale=new Vector3(scale*0.72f,scale*0.42f,scale*0.58f);
            }
        }

        private static void FlowerPatch(Vector3 centre,Material petals,int seed)
        {
            for(int i=0;i<5;i++)
            {
                Vector3 p=centre+new Vector3((Hash01(seed*19+i)-0.5f)*0.72f,0f,(Hash01(seed*29+i+7)-0.5f)*0.55f);
                float h=0.18f+(i%3)*0.035f;
                Capsule("Flower stem",p,p+Vector3.up*h,0.012f,_leaf);
                GameObject middle=Primitive(PrimitiveType.Sphere,"Flower centre",_flowerYellow);
                middle.transform.position=p+Vector3.up*h;middle.transform.localScale=Vector3.one*0.040f;
                for(int j=0;j<5;j++)
                {
                    float a=j*Mathf.PI*2f/5f;
                    GameObject petal=Primitive(PrimitiveType.Sphere,"Flower petal",petals);
                    petal.transform.position=p+Vector3.up*h+new Vector3(Mathf.Cos(a)*0.06f,0f,Mathf.Sin(a)*0.06f);
                    petal.transform.localScale=new Vector3(0.070f,0.022f,0.045f);
                }
            }
        }

        private static void CastleTower(Vector3 basePos,float radius,float height)
        {
            GameObject tower=Primitive(PrimitiveType.Cylinder,"Distant pale tower",_stoneLight);
            tower.transform.position=basePos+Vector3.up*height*0.5f;
            tower.transform.localScale=new Vector3(radius,height*0.5f,radius);
            Mesh cone=ConeMesh(radius*1.25f,height*0.42f,18);Created.Add(cone);
            GameObject roof=MeshObject("Distant pointed roof",cone,_roof);
            roof.transform.position=basePos+Vector3.up*(height+height*0.20f);
        }

        // ------------------------------------------------------------------
        // Meshes / materials
        // ------------------------------------------------------------------

        private static Mesh RoundedTerraceMesh(float width,float depth,float height,float radius,int cornerSegments,bool topOnly)
        {
            List<Vector2> outline=RoundedOutline(width,depth,radius,cornerSegments);
            if(topOnly)
            {
                var verts=new List<Vector3>{Vector3.zero};
                foreach(Vector2 p in outline) verts.Add(new Vector3(p.x,0f,p.y));
                var tris=new List<int>();
                for(int i=0;i<outline.Count;i++){int next=(i+1)%outline.Count;tris.Add(0);tris.Add(i+1);tris.Add(next+1);}
                var m=new Mesh{name="Rounded turf top"};m.SetVertices(verts);m.SetTriangles(tris,0);m.RecalculateNormals();m.RecalculateBounds();return m;
            }

            int count=outline.Count;var v=new Vector3[count*2];var tri=new int[count*6];
            for(int i=0;i<count;i++)
            {
                Vector2 p=outline[i];Vector2 bottom=p*1.055f;
                v[i]=new Vector3(p.x,0f,p.y);v[i+count]=new Vector3(bottom.x,-height,bottom.y);
                int next=(i+1)%count;int q=i*6;
                tri[q]=i;tri[q+1]=next;tri[q+2]=i+count;tri[q+3]=next;tri[q+4]=next+count;tri[q+5]=i+count;
            }
            var mesh=new Mesh{name="Rounded terrace cliff"};mesh.vertices=v;mesh.triangles=tri;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private static Mesh RoundedTopMesh(float width,float depth,float radius,int cornerSegments)
            => RoundedTerraceMesh(width,depth,0.02f,radius,cornerSegments,true);

        private static List<Vector2> RoundedOutline(float width,float depth,float radius,int segments)
        {
            float hx=width*0.5f,hz=depth*0.5f;r=Mathf.Min(radius,Mathf.Min(hx,hz)-0.01f);
            var points=new List<Vector2>((segments+1)*4);
            AddCorner(points,new Vector2(hx-r,hz-r),r,0f,90f,segments);
            AddCorner(points,new Vector2(-hx+r,hz-r),r,90f,180f,segments);
            AddCorner(points,new Vector2(-hx+r,-hz+r),r,180f,270f,segments);
            AddCorner(points,new Vector2(hx-r,-hz+r),r,270f,360f,segments);
            return points;
        }

        private static void AddCorner(List<Vector2> points,Vector2 centre,float radius,float startDeg,float endDeg,int segments)
        {
            for(int i=0;i<=segments;i++){float a=Mathf.Lerp(startDeg,endDeg,i/(float)segments)*Mathf.Deg2Rad;points.Add(centre+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius);}
        }

        private static Mesh RibbonMesh(Vector3 top,Vector3 bottom,float width,int segments,bool narrow)
        {
            Vector3 d=bottom-top;Vector3 side=Vector3.Cross(d.normalized,Vector3.up);if(side.sqrMagnitude<0.001f)side=Vector3.right;side.Normalize();
            var v=new Vector3[(segments+1)*2];var uv=new Vector2[v.Length];var tri=new int[segments*12];
            for(int i=0;i<=segments;i++)
            {
                float t=i/(float)segments;Vector3 c=Vector3.Lerp(top,bottom,t);c+=side*Mathf.Sin(t*11f)*width*0.025f;float w=width*(narrow?0.42f:1f)*(0.95f+Mathf.Sin(t*7f)*0.04f);int q=i*2;v[q]=c-side*w*0.5f;v[q+1]=c+side*w*0.5f;uv[q]=new Vector2(0f,t*3f);uv[q+1]=new Vector2(1f,t*3f);
                if(i<segments){int k=i*12;tri[k]=q;tri[k+1]=q+2;tri[k+2]=q+1;tri[k+3]=q+1;tri[k+4]=q+2;tri[k+5]=q+3;tri[k+6]=q+1;tri[k+7]=q+2;tri[k+8]=q;tri[k+9]=q+3;tri[k+10]=q+2;tri[k+11]=q+1;}
            }
            var m=new Mesh{name="Double sided waterfall ribbon"};m.vertices=v;m.uv=uv;m.triangles=tri;m.RecalculateNormals();m.RecalculateBounds();return m;
        }

        private static Mesh ConeMesh(float radius,float height,int segments)
        {
            var v=new Vector3[segments+1];var tri=new int[segments*3];v[0]=Vector3.up*height*0.5f;
            for(int i=0;i<segments;i++){float a=i*Mathf.PI*2f/segments;v[i+1]=new Vector3(Mathf.Cos(a)*radius,-height*0.5f,Mathf.Sin(a)*radius);int next=(i+1)%segments;tri[i*3]=0;tri[i*3+1]=i+1;tri[i*3+2]=next+1;}
            var m=new Mesh{name="Castle cone roof"};m.vertices=v;m.triangles=tri;m.RecalculateNormals();m.RecalculateBounds();return m;
        }

        private static Material Smooth(string name,Color colour,float smoothness=0.05f,Color? emission=null)
        {
            Shader shader=Shader.Find("VoxelEngine/SunlitSmooth");
            Material m=new Material(shader){name=name};m.SetTexture("_MainTex",Texture2D.whiteTexture);m.SetColor("_BaseColor",colour);m.SetColor("_EmissionColor",emission??Color.black);m.SetFloat("_Smoothness",smoothness);m.SetFloat("_Cull",2f);m.SetFloat("_ZWrite",1f);Created.Add(m);return m;
        }

        private static Material Transparent(string name,Color colour,Color emission)
        {Material m=Smooth(name,colour,0.06f,emission);m.SetFloat("_Cull",0f);m.SetFloat("_ZWrite",0f);m.renderQueue=(int)RenderQueue.Transparent;return m;}

        private static GameObject Primitive(PrimitiveType type,string name,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(_root,false);Collider c=go.GetComponent<Collider>();if(c!=null)Object.DestroyImmediate(c);Renderer r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=material.renderQueue>=(int)RenderQueue.Transparent?ShadowCastingMode.Off:ShadowCastingMode.On;r.receiveShadows=true;Created.Add(go);return go;
        }

        private static GameObject MeshObject(string name,Mesh mesh,Material material)
        {GameObject go=new GameObject(name);go.transform.SetParent(_root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;MeshRenderer r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=material.renderQueue>=(int)RenderQueue.Transparent?ShadowCastingMode.Off:ShadowCastingMode.On;r.receiveShadows=true;Created.Add(go);return go;}

        private static void Capsule(string name,Vector3 a,Vector3 b,float radius,Material material)
        {Vector3 d=b-a;if(d.sqrMagnitude<0.0001f)return;GameObject c=Primitive(PrimitiveType.Capsule,name,material);c.transform.position=(a+b)*0.5f;c.transform.rotation=Quaternion.FromToRotation(Vector3.up,d.normalized);c.transform.localScale=new Vector3(radius*2f,Mathf.Max(radius,d.magnitude*0.5f),radius*2f);}

        private static float Hash01(int n)
        {unchecked{uint x=(uint)n;x^=x>>16;x*=0x7feb352d;x^=x>>15;x*=0x846ca68b;x^=x>>16;return(x&0x00ffffff)/16777215f;}}
    }
}
