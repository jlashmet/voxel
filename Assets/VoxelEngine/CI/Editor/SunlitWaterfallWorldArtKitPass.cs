using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Structures;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Replaces the remaining one-off reference geometry with reusable WorldArtKit pieces. This
    /// is intentionally a lookdev/demo assembly pass: every visible landmark is built from the
    /// same socketable components that procedural generation can place later.
    /// </summary>
    internal static class SunlitWaterfallWorldArtKitPass
    {
        private static bool _done;

        public static void Apply(Camera camera)
        {
            if (_done || camera == null) return;
            _done = true;

            GameObject scene = GameObject.Find("Sunlit Coherent Terrain Scene");
            if (scene == null) return;

            HideSupersededReferenceGeometry(scene.transform);

            GameObject rootObject = new GameObject("World Art Kit Reference Scene");
            rootObject.transform.SetParent(scene.transform, false);
            Transform root = rootObject.transform;

            Shader shader = Shader.Find("VoxelEngine/SunlitSmooth");
            if (shader == null)
            {
                Debug.LogError("Sunlit Waterfall: WorldArtKit could not find VoxelEngine/SunlitSmooth.");
                return;
            }

            WorldArtPalette palette = BuildPalette(shader);
            Vector3 o = camera.transform.position - new Vector3(0.10f, 5.05f, -22.6f);

            BuildHeroRuins(root, o, palette);
            BuildCascade(root, o, palette);
            BuildCastle(root, o, palette);
            BuildFloatingGarden(root, o, palette);
            BuildDenseDressing(root, o, palette);
        }

        private static void HideSupersededReferenceGeometry(Transform scene)
        {
            Renderer[] renderers = scene.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                string n = renderer.gameObject.name.ToLowerInvariant();

                // CompositionPass's entire right-hand scene is replaced below by socketed kit pieces.
                if (n.StartsWith("reference4 ") ||
                    n.Contains("reference ruin stone pier") ||
                    n.Contains("reference ruin stone arch") ||
                    n.Contains("reference broken ruin crown"))
                {
                    renderer.gameObject.SetActive(false);
                }
            }
        }

        private static WorldArtPalette BuildPalette(Shader shader)
        {
            return new WorldArtPalette()
                .Set(WorldArtSurfaceRole.Rock, Mat(shader, "WorldArtKit cliff rock", new Color(0.47f, 0.48f, 0.31f)))
                .Set(WorldArtSurfaceRole.Turf, Mat(shader, "WorldArtKit grass turf", new Color(0.55f, 0.72f, 0.23f)))
                .Set(WorldArtSurfaceRole.Stone, Mat(shader, "WorldArtKit ruin stone", new Color(0.88f, 0.83f, 0.70f)))
                .Set(WorldArtSurfaceRole.Moss, Mat(shader, "WorldArtKit moss leaf", new Color(0.36f, 0.59f, 0.14f)))
                .Set(WorldArtSurfaceRole.Water, Mat(shader, "WorldArtKit turquoise water", new Color(0.035f, 0.53f, 0.72f, 0.89f), true, 4f))
                .Set(WorldArtSurfaceRole.Waterfall, Mat(shader, "WorldArtKit waterfall cascade", new Color(0.58f, 0.88f, 0.97f, 0.87f), true, 5f))
                .Set(WorldArtSurfaceRole.Foam, Mat(shader, "WorldArtKit waterfall foam", new Color(0.98f, 0.995f, 1f, 0.80f), true, 6f))
                .Set(WorldArtSurfaceRole.Bark, Mat(shader, "WorldArtKit bark trunk", new Color(0.33f, 0.22f, 0.11f)))
                .Set(WorldArtSurfaceRole.Leaf, Mat(shader, "WorldArtKit leaf canopy", new Color(0.36f, 0.60f, 0.15f)))
                .Set(WorldArtSurfaceRole.Roof, Mat(shader, "WorldArtKit roof spire", new Color(0.42f, 0.46f, 0.56f)))
                .Set(WorldArtSurfaceRole.FlowerWarm, Mat(shader, "WorldArtKit flower warm", new Color(0.96f, 0.48f, 0.48f)))
                .Set(WorldArtSurfaceRole.FlowerCool, Mat(shader, "WorldArtKit flower cool", new Color(0.74f, 0.56f, 0.96f)));
        }

        private static void BuildHeroRuins(Transform root, Vector3 o, WorldArtPalette palette)
        {
            WorldArtPiece hero = WorldArtKit.RoundedArch(root, "WorldArtKit hero arch",
                o + new Vector3(-6.25f, 1.85f, 5.35f),
                1.48f, 7, new Vector3(0.60f, 0.54f, 0.82f), 0.84f, true, 101, palette);

            WorldArtKit.RoundedArch(root, "WorldArtKit lower arch",
                o + new Vector3(-6.75f, 0.45f, 8.30f),
                0.82f, 4, new Vector3(0.42f, 0.40f, 0.58f), 0.60f, false, 131, palette);

            Vector3 crown = hero.Socket("crown").position;
            WorldArtKit.Vine(root, "WorldArtKit hero ivy", crown + new Vector3(-0.55f, -0.10f, -0.40f),
                hero.Socket("left-base").position + new Vector3(0.10f, 0.25f, -0.46f), 0.035f, 7, palette);
            WorldArtKit.Vine(root, "WorldArtKit hero ivy", crown + new Vector3(0.22f, -0.16f, -0.42f),
                hero.Socket("right-pier").position + new Vector3(-0.10f, -0.55f, -0.40f), 0.030f, 19, palette);

            WorldArtKit.FlowerPatch(root, "WorldArtKit ruin flowers",
                hero.Socket("left-base").position + new Vector3(-0.25f, 0.12f, -0.62f), 0.78f, 15, 17, palette);
            WorldArtKit.FlowerPatch(root, "WorldArtKit ruin flowers",
                hero.Socket("right-base").position + new Vector3(0.22f, 0.10f, -0.55f), 0.64f, 11, 29, palette);
        }

        private static void BuildCascade(Transform root, Vector3 o, WorldArtPalette palette)
        {
            WorldArtPiece[] ledges =
            {
                WorldArtKit.CliffLedge(root, "WorldArtKit cascade ledge 1", o + new Vector3(4.15f, 0.55f, 6.40f), new Vector3(5.20f, 2.10f, 3.90f), 13, palette),
                WorldArtKit.CliffLedge(root, "WorldArtKit cascade ledge 2", o + new Vector3(4.90f, 1.75f, 9.70f), new Vector3(4.85f, 2.00f, 3.70f), 29, palette),
                WorldArtKit.CliffLedge(root, "WorldArtKit cascade ledge 3", o + new Vector3(5.55f, 2.95f, 13.10f), new Vector3(4.45f, 1.95f, 3.45f), 47, palette),
                WorldArtKit.CliffLedge(root, "WorldArtKit cascade ledge 4", o + new Vector3(6.15f, 4.08f, 16.55f), new Vector3(4.05f, 1.85f, 3.20f), 67, palette)
            };

            WorldArtPiece[] pools =
            {
                WorldArtKit.Pool(ledges[0].Socket("top"), "WorldArtKit cascade pool 1", new Vector3(-0.20f, 0.02f, 0.10f), 2.02f, 1.14f, 11, palette),
                WorldArtKit.Pool(ledges[1].Socket("top"), "WorldArtKit cascade pool 2", new Vector3(-0.18f, 0.02f, 0.10f), 1.84f, 1.00f, 23, palette),
                WorldArtKit.Pool(ledges[2].Socket("top"), "WorldArtKit cascade pool 3", new Vector3(-0.14f, 0.02f, 0.08f), 1.66f, 0.91f, 37, palette),
                WorldArtKit.Pool(ledges[3].Socket("top"), "WorldArtKit cascade pool 4", new Vector3(-0.10f, 0.02f, 0.06f), 1.45f, 0.80f, 53, palette)
            };

            WorldArtPiece foreground = WorldArtKit.Pool(root, "WorldArtKit foreground pool",
                o + new Vector3(3.10f, 0.20f, 3.60f), 3.10f, 1.52f, 71, palette);

            WorldArtKit.WaterfallBetween(root, "WorldArtKit upper fall", pools[3].Socket("front-lip"), pools[2].Socket("back"), 1.12f, 91, palette);
            WorldArtKit.WaterfallBetween(root, "WorldArtKit middle fall 2", pools[2].Socket("front-lip"), pools[1].Socket("back"), 1.34f, 109, palette);
            WorldArtKit.WaterfallBetween(root, "WorldArtKit middle fall 1", pools[1].Socket("front-lip"), pools[0].Socket("back"), 1.52f, 127, palette);
            WorldArtKit.WaterfallBetween(root, "WorldArtKit lower fall", pools[0].Socket("front-lip"), foreground.Socket("back"), 1.72f, 149, palette);

            for (int i = 0; i < ledges.Length; i++)
            {
                Transform edge = (i & 1) == 0 ? ledges[i].Socket("left-edge") : ledges[i].Socket("right-edge");
                WorldArtKit.MossCluster(root, "WorldArtKit cascade moss", edge.position,
                    0.48f - i * 0.035f, 203 + i * 13, palette.Get(WorldArtSurfaceRole.Moss));
            }
        }

        private static void BuildCastle(Transform root, Vector3 o, WorldArtPalette palette)
        {
            WorldArtPiece hill = WorldArtKit.CliffLedge(root, "WorldArtKit distant castle hill",
                o + new Vector3(8.25f, 5.65f, 25.2f), new Vector3(4.20f, 2.30f, 3.35f), 103, palette);
            Transform top = hill.Socket("top");

            WorldArtKit.BeveledBlock(top, "WorldArtKit keep hall stone", new Vector3(-0.10f, 0.62f, -0.10f),
                new Vector3(1.36f, 1.24f, 0.82f), 0.07f, WorldArtSurfaceRole.Stone, palette);
            WorldArtPiece main = WorldArtKit.CastleTower(top, "WorldArtKit keep main", new Vector3(0.02f, 0f, -0.08f), 0.50f, 3.65f, palette);
            WorldArtKit.CastleTower(top, "WorldArtKit keep left", new Vector3(-0.90f, -0.05f, 0.22f), 0.34f, 2.48f, palette);
            WorldArtKit.CastleTower(top, "WorldArtKit keep right", new Vector3(0.86f, -0.16f, 0.30f), 0.31f, 2.12f, palette);

            WorldArtKit.Vine(root, "WorldArtKit castle ivy", main.Socket("ivy").position,
                main.Socket("base").position + new Vector3(-0.22f, 0.18f, -0.28f), 0.022f, 211, palette);
        }

        private static void BuildFloatingGarden(Transform root, Vector3 o, WorldArtPalette palette)
        {
            WorldArtPiece island = WorldArtKit.CliffLedge(root, "WorldArtKit floating garden",
                o + new Vector3(2.15f, 7.45f, 22.4f), new Vector3(1.55f, 1.62f, 1.30f), 151, palette);
            WorldArtKit.StorybookTree(island.Socket("top"), "WorldArtKit floating tree",
                new Vector3(0f, 0.02f, 0f), 1.75f, 0.72f, 229, palette);
        }

        private static void BuildDenseDressing(Transform root, Vector3 o, WorldArtPalette palette)
        {
            Vector3[] flowerCenters =
            {
                o + new Vector3(-3.85f, 0.28f, 3.30f),
                o + new Vector3(-2.30f, 0.30f, 5.05f),
                o + new Vector3(1.10f, 0.34f, 4.20f),
                o + new Vector3(2.20f, 0.38f, 5.55f),
                o + new Vector3(3.15f, 1.56f, 6.18f)
            };
            for (int i = 0; i < flowerCenters.Length; i++)
                WorldArtKit.FlowerPatch(root, "WorldArtKit garden flowers", flowerCenters[i],
                    0.55f + (i % 2) * 0.18f, 8 + (i % 3) * 3, 271 + i * 19, palette);

            WorldArtKit.MossCluster(root, "WorldArtKit foreground moss", o + new Vector3(-4.55f, 0.34f, 4.10f),
                0.72f, 311, palette.Get(WorldArtSurfaceRole.Moss));
            WorldArtKit.MossCluster(root, "WorldArtKit foreground moss", o + new Vector3(1.85f, 0.40f, 3.25f),
                0.62f, 337, palette.Get(WorldArtSurfaceRole.Moss));
        }

        private static Material Mat(Shader shader, string name, Color color, bool transparent = false, float kind = 0f)
        {
            Material material = new Material(shader) { name = name };
            SetColor(material, "_BaseColor", color);
            SetColor(material, "_SecondaryColor", color * 0.88f);
            SetColor(material, "_TopColor", Color.Lerp(color, Color.white, 0.12f));
            SetColor(material, "_EmissionColor", Color.black);
            SetFloat(material, "_SurfaceKind", kind);
            SetFloat(material, "_TextureScale", 0.28f);
            SetFloat(material, "_TextureStrength", 0.20f);
            SetFloat(material, "_DetailScale", 0.075f);
            SetFloat(material, "_DetailStrength", 0.06f);
            SetFloat(material, "_TopStrength", 0.20f);
            SetFloat(material, "_RimStrength", 0.04f);
            SetFloat(material, "_Cull", transparent ? 0f : 2f);
            SetFloat(material, "_ZWrite", transparent ? 0f : 1f);
            material.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
            return material;
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
