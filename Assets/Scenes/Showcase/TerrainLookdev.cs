using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using Mat = Game.Materials.Api.GameMaterialIds;   // engine-side Mat constants were removed

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Terrain-only look-development scene authored into the normal voxel world. Rendering stays
    /// entirely on Storage.Api read capabilities -> Composition -> production surface extraction.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed partial class TerrainLookdev : MonoBehaviour
    {
        private const uint Seed = 0x51A17u;
        private const int TerrainXMin = -170;
        private const int TerrainXMax = 170;
        private const int TerrainZMin = -70;
        private const int TerrainZMax = 560;

        public const int CapturedCompositionLandmarkCount = 5;

        private IVoxelStorageRuntime _storage;
        private bool _built;

        public Camera SceneCamera => GetComponent<Camera>();

        private void OnEnable()
        {
            if (Application.isPlaying) Rebuild();
        }

        private void OnDisable()
        {
            if (Application.isPlaying) Shutdown();
        }

        private void OnDestroy()
        {
            if (_built) Shutdown();
        }

        [ContextMenu("Rebuild Terrain Lookdev")]
        public void Rebuild()
        {
            Shutdown();
            ConfigureEnvironment();
            ApplyReferenceEnvironment();

            _storage = VoxelEngineBootstrap.CreateStorage(16, 220_000);
            const uint weather = (1u << Coatings.Moss) | (1u << Coatings.Wet);

            _storage.RegisterMaterial(Mat.Grass, 24, DestructionClass.Powder,
                              SurfaceStyles.Smooth, weather);
            _storage.RegisterMaterial(Mat.Moss, 24, DestructionClass.Powder,
                              SurfaceStyles.Smooth, weather);
            _storage.RegisterMaterial(Mat.Sand, 28, DestructionClass.Powder,
                              SurfaceStyles.Smooth, weather);
            // Limestone and pavers deliberately use the production faceted path. Planar styles
            // are emitted as merged faces by CpuTransvoxelChunkCache instead of being melted by
            // continuous rounded reconstruction, which gives the reference's squat cuboid rocks.
            _storage.RegisterMaterial(Mat.TerrainLimestone, 210, DestructionClass.Crumble,
                              SurfaceStyles.Planar, weather);
            _storage.RegisterMaterial(Mat.TerrainEarth, 32, DestructionClass.Powder,
                              SurfaceStyles.Smooth, weather);
            _storage.RegisterMaterial(Mat.TerrainPathStone, 180, DestructionClass.Crumble,
                              SurfaceStyles.Planar, weather);
            _storage.RegisterMaterial(Mat.FlowerWhite, 4, DestructionClass.Powder,
                              SurfaceStyles.Rounded, 0u);
            _storage.RegisterMaterial(Mat.FlowerYellow, 4, DestructionClass.Powder,
                              SurfaceStyles.Rounded, 0u);
            _storage.RegisterMaterial(Mat.FlowerPink, 4, DestructionClass.Powder,
                              SurfaceStyles.Rounded, 0u);
            _storage.RegisterMaterial(Mat.FlowerBlue, 4, DestructionClass.Powder,
                              SurfaceStyles.Rounded, 0u);

            var writer = VoxelEngineBootstrap.CreateStructureAuthoring(_storage, 9_000_000);
            AuthorTerrain(writer);
            if (writer.BudgetExceeded)
                throw new System.InvalidOperationException("Terrain lookdev exceeded voxel authoring budget.");

            _storage.PublishAllResidentRegions();

            var renderingWorld = new RenderingWorldBinding(
                _storage.Reads,
                _storage.MaterialPresentation,
                _storage.SurfacePresentation,
                _storage.CoatingPresentation);
            RenderingComposition.ConfigureWorld(
                in renderingWorld, _storage.Changes, Seed,
                solidBuildBudgetMs: 12.0, waterBuildBudgetMs: 0.0, farFieldEnabled: false);
            _built = true;
        }

        private void ApplyReferenceEnvironment()
        {
            Camera camera = SceneCamera;
            camera.backgroundColor = new Color(0.74f, 0.75f, 0.44f, 1f);
            camera.fieldOfView = 28f;
            camera.farClipPlane = 160f;
            camera.transform.position = new Vector3(-0.60f, 23.0f, -20.0f);
            camera.transform.LookAt(new Vector3(0.10f, 2.0f, 12.0f));

            RenderingComposition.ConfigureEnvironment(
                Color.white,
                new Vector3(-0.43f, 0.87f, -0.24f).normalized,
                new Color(0.94f, 0.87f, 0.49f, 1f),
                new Color(0.82f, 0.80f, 0.46f, 1f));
        }

        private void AuthorTerrain(IStructureAuthoringSession writer)
        {
            BuildValley(writer);
            BuildRockFields(writer);
            BuildTurfCushions(writer);
            BuildPath(writer);
            BuildFlowers(writer);
        }

        private static void BuildValley(IStructureAuthoringSession writer)
        {
            for (int z = TerrainZMin; z <= TerrainZMax; z++)
            for (int x = TerrainXMin; x <= TerrainXMax; x++)
            {
                int top = HeightVoxel(x, z);
                writer.FillColumnBulk(x, top - 8, top, z, Mat.TerrainEarth);
                writer.SetStyled(x, top, z, TurfMaterial(x, z), SurfaceStyles.Smooth);
            }
        }

        private static byte TurfMaterial(int x, int z)
        {
            float shoulder = math.abs(x - PathCenterVoxel(z));
            float field = math.sin(x * 0.032f + z * 0.019f)
                        + 0.55f * math.sin(x * 0.016f - z * 0.027f + 1.4f);
            if (z < 90 && shoulder > 58 && field > 0.78f)
                return Mat.Moss;
            return Mat.Grass;
        }

        private static float Hash01(int x, int z)
        {
            uint h = (uint)(x * 374761393 + z * 668265263) ^ Seed;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) * (1f / 16777216f);
        }

        private static void BuildPath(IStructureAuthoringSession writer)
        {
            // The captured replay camera looks steeply across the meadow. Sparse isolated pavers
            // collapse to beige dots from that pose, so establish one continuous, tapered ground
            // ribbon first. The irregular edge keeps the route organic while preserving an S-curve
            // that reads from foreground through the far valley.
            for (int z = -55; z <= 355; z++)
            {
                float progress = math.saturate((z + 55f) / 410f);
                int centreX = PathCenterVoxel(z);
                int halfWidth = math.max(4, (int)math.round(math.lerp(16f, 5f, progress)));
                for (int lateral = -halfWidth; lateral <= halfWidth; lateral++)
                {
                    int x = centreX + lateral;
                    if (x <= TerrainXMin || x >= TerrainXMax) continue;
                    int edge = halfWidth - math.abs(lateral);
                    if (edge < 2 && Hash01(x, z) < 0.36f) continue;
                    writer.SetStyled(x, HeightVoxel(x, z), z, Mat.Sand, SurfaceStyles.Smooth);
                }
            }

            // Retain a restrained cobble rhythm on top of the readable earth ribbon.
            var rng = new Unity.Mathematics.Random(Seed ^ 0x2231u);
            int paverZ = -48;
            while (paverZ < 315)
            {
                float progress = math.saturate((paverZ + 48f) / 363f);
                int centreX = PathCenterVoxel(paverZ);
                int halfWidth = math.max(4, (int)math.round(math.lerp(14f, 5f, progress)));

                for (int lateral = -halfWidth; lateral <= halfWidth; lateral += 5)
                {
                    if (rng.NextFloat() < math.lerp(0.20f, 0.46f, progress)) continue;
                    int px = centreX + lateral + rng.NextInt(-1, 2);
                    int pz = paverZ + rng.NextInt(-1, 2);
                    int hx = rng.NextInt(1, progress < 0.30f ? 3 : 2);
                    int hz = rng.NextInt(1, progress < 0.30f ? 3 : 2);
                    int py = HeightVoxel(px, pz);
                    StampRoundedBox(writer, new int3(px, py + 1, pz),
                        new int3(hx, 1, hz), 1, Mat.TerrainPathStone,
                        SurfaceStyles.Planar, false);
                }
                paverZ += rng.NextInt(7, 12) + (int)math.round(progress * 3f);
            }
        }

        private static void BuildRockFields(IStructureAuthoringSession writer)
        {
            var rng = new Unity.Mathematics.Random(Seed);

            // The reference depends on a few strong rock masses framing a readable valley, not an
            // even carpet of equally important blocks. Keep incidental side clusters sparse and
            // leave the path corridor visually quiet.
            for (int cluster = 0; cluster < 28; cluster++)
            {
                int z = rng.NextInt(45, 440);
                float zm = z * 0.1f;
                int centre = Mathf.RoundToInt(ValleyCenterMetres(zm) * 10f);
                int side = rng.NextBool() ? -1 : 1;
                int distance = rng.NextInt(62, 150);
                int centreX = centre + side * distance;
                if (centreX < TerrainXMin + 10 || centreX > TerrainXMax - 10) continue;

                int count = rng.NextInt(2, 4);
                int stride = rng.NextInt(7, 12);
                for (int i = 0; i < count; i++)
                {
                    int x = centreX + (i - count / 2) * stride + rng.NextInt(-2, 3);
                    int zz = z + rng.NextInt(-5, 6);
                    if (x <= TerrainXMin + 5 || x >= TerrainXMax - 5) continue;

                    int maxHalf = z < 190 ? 6 : (z < 330 ? 5 : 4);
                    int hx = rng.NextInt(2, maxHalf + 1);
                    int hz = rng.NextInt(2, maxHalf + 1);
                    int hy = rng.NextInt(1, z < 190 ? 4 : 3);
                    int y = HeightVoxel(x, zz) + hy;
                    StampRoundedBox(writer, new int3(x, y, zz), new int3(hx, hy, hz),
                        1, Mat.TerrainLimestone, SurfaceStyles.Planar,
                        rng.NextFloat() < 0.58f);
                }
            }

            for (int i = 0; i < 72; i++)
            {
                int z = rng.NextInt(35, 455);
                int x = rng.NextInt(TerrainXMin + 7, TerrainXMax - 7);
                int path = PathCenterVoxel(z);
                if (math.abs(x - path) < 28)
                    x += x < path ? -32 : 32;
                if (x <= TerrainXMin + 6 || x >= TerrainXMax - 6) continue;

                int maxHalf = z > 330 ? 4 : (z > 175 ? 5 : 6);
                int hx = rng.NextInt(2, maxHalf + 1);
                int hz = rng.NextInt(2, maxHalf + 1);
                int hy = rng.NextInt(1, z > 280 ? 3 : 4);
                int y = HeightVoxel(x, z) + hy;
                StampRoundedBox(writer, new int3(x, y, z), new int3(hx, hy, hz),
                    1, Mat.TerrainLimestone, SurfaceStyles.Planar,
                    rng.NextFloat() < 0.42f);
            }

            for (int i = 0; i < CapturedCompositionLandmarkCount; i++)
            {
                int3 anchor = CapturedCompositionLandmarkVoxel(i);
                BuildForegroundOutcrop(writer, anchor, CapturedCompositionLandmarkScale(i), ref rng);
            }
        }

        public static Vector3 CapturedCompositionLandmarkWorld(int index)
        {
            int3 anchor = CapturedCompositionLandmarkVoxel(index);
            return new Vector3(anchor.x * 0.1f, HeightVoxel(anchor.x, anchor.z) * 0.1f + 1.0f,
                anchor.z * 0.1f);
        }

        private static int3 CapturedCompositionLandmarkVoxel(int index)
        {
            switch (index)
            {
                case 0: return new int3(-105, 0, 65);
                case 1: return new int3(108, 0, 88);
                case 2: return new int3(-78, 0, 165);
                case 3: return new int3(72, 0, 205);
                case 4: return new int3(-48, 0, 285);
                default: throw new System.ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static int CapturedCompositionLandmarkScale(int index)
        {
            switch (index)
            {
                case 0: return 16;
                case 1: return 15;
                case 2: return 11;
                case 3: return 10;
                case 4: return 8;
                default: throw new System.ArgumentOutOfRangeException(nameof(index));
            }
        }

        public static Vector3 CapturedPathProbeWorld(int zVoxel)
        {
            int x = PathCenterVoxel(zVoxel);
            return new Vector3(x * 0.1f, HeightVoxel(x, zVoxel) * 0.1f + 0.05f, zVoxel * 0.1f);
        }

        private static void BuildForegroundOutcrop(IStructureAuthoringSession writer, int3 centre, int scale,
            ref Unity.Mathematics.Random rng)
        {
            for (int layer = 0; layer < 3; layer++)
            {
                int count = 7 - layer;
                for (int i = 0; i < count; i++)
                {
                    int x = centre.x + (i - count / 2) * (scale - 4) + rng.NextInt(-2, 3);
                    int z = centre.z + layer * 5 + rng.NextInt(-2, 3);
                    int hx = rng.NextInt(3, math.max(5, scale / 2 + 1));
                    int hy = rng.NextInt(2, 5);
                    int hz = rng.NextInt(3, math.max(5, scale / 2 + 1));
                    int y = HeightVoxel(x, z) + layer * 2 + hy;
                    StampRoundedBox(writer, new int3(x, y, z), new int3(hx, hy, hz),
                        1, Mat.TerrainLimestone, SurfaceStyles.Planar,
                        rng.NextFloat() < 0.68f);
                }
            }
        }

        private static void BuildTurfCushions(IStructureAuthoringSession writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0x7B19u);

            for (int i = 0; i < 520; i++)
            {
                int z = rng.NextInt(-48, 525);
                int x = rng.NextInt(TerrainXMin + 4, TerrainXMax - 4);
                if (z < 315 && math.abs(x - PathCenterVoxel(z)) < 22) continue;
                int rx = rng.NextInt(2, 6);
                int rz = rng.NextInt(2, 7);
                int ry = rng.NextFloat() < 0.25f ? 2 : 1;
                StampEllipsoid(writer, new int3(x, HeightVoxel(x, z) + ry, z),
                    new int3(rx, ry, rz), TurfMaterial(x, z), SurfaceStyles.Smooth);
            }

            for (int i = 0; i < 90; i++)
            {
                int z = rng.NextInt(-35, 500);
                int x = rng.NextInt(TerrainXMin + 8, TerrainXMax - 8);
                if (z < 315 && math.abs(x - PathCenterVoxel(z)) < 24) continue;
                int rx = rng.NextInt(5, 9);
                int rz = rng.NextInt(4, 8);
                int ry = rng.NextInt(1, 3);
                StampEllipsoid(writer, new int3(x, HeightVoxel(x, z) + ry, z),
                    new int3(rx, ry, rz), TurfMaterial(x, z), SurfaceStyles.Smooth);
            }
        }

        private static void BuildFlowers(IStructureAuthoringSession writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0xD451u);
            for (int i = 0; i < 1750; i++)
            {
                int z = rng.NextInt(-55, 535);
                float distance = math.saturate((z + 55f) / 590f);
                if (rng.NextFloat() < distance * 0.08f) continue;
                int x = rng.NextInt(TerrainXMin + 5, TerrainXMax - 5);
                if (z < 315 && math.abs(x - PathCenterVoxel(z)) < 18) continue;
                int y = HeightVoxel(x, z) + 2;
                byte flower = Mat.FlowerWhite;
                float colour = rng.NextFloat();
                if (colour > 0.82f && colour <= 0.94f) flower = Mat.FlowerYellow;
                else if (colour > 0.94f && colour <= 0.985f) flower = Mat.FlowerPink;
                else if (colour > 0.985f) flower = Mat.FlowerBlue;
                writer.SetStyled(x, y, z, flower, SurfaceStyles.Rounded);
            }
        }

        private static void StampEllipsoid(IStructureAuthoringSession writer, int3 centre, int3 radius,
            byte material, ushort style)
        {
            float3 inv = 1f / math.max((float3)radius, 1f);
            for (int z = -radius.z; z <= radius.z; z++)
            for (int y = -radius.y; y <= radius.y; y++)
            for (int x = -radius.x; x <= radius.x; x++)
            {
                float3 p = new float3(x, y, z) * inv;
                if (math.lengthsq(p) > 1f) continue;
                writer.SetStyled(centre.x + x, centre.y + y, centre.z + z, material, style);
            }
        }

        private static void StampRoundedBox(IStructureAuthoringSession writer, int3 centre, int3 half,
            int radius, byte material, ushort style, bool mossTop)
        {
            radius = math.max(1, radius);
            int3 inner = math.max(half - radius, 0);
            for (int z = -half.z; z <= half.z; z++)
            for (int y = -half.y; y <= half.y; y++)
            for (int x = -half.x; x <= half.x; x++)
            {
                float3 q = math.abs(new float3(x, y, z)) - (float3)inner;
                float3 outside = math.max(q, 0f);
                float signed = math.length(outside) + math.min(math.cmax(q), 0f) - radius;
                if (signed > 0.15f) continue;
                byte coating = mossTop && y >= half.y - 1 ? Coatings.Moss : Coatings.None;
                writer.SetStyled(centre.x + x, centre.y + y, centre.z + z,
                    material, style, coating);
            }
        }

        private static int PathCenterVoxel(int z)
        {
            float zm = z * 0.1f;
            float x = 0.92f * Mathf.Sin(zm * 0.135f + 0.55f)
                    + 0.42f * Mathf.Sin(zm * 0.052f - 0.8f)
                    + 0.18f * Mathf.Sin(zm * 0.31f + 1.4f) - 0.08f;
            return Mathf.RoundToInt(x * 10f);
        }

        private static float ValleyCenterMetres(float zm)
        {
            return 1.38f * Mathf.Sin(zm * 0.075f - 0.30f)
                 + 0.58f * Mathf.Sin(zm * 0.190f + 1.05f)
                 + 0.22f * Mathf.Sin(zm * 0.410f - 0.55f);
        }

        private static int HeightVoxel(int x, int z)
        {
            float xm = x * 0.1f;
            float zm = z * 0.1f;
            float valleyCenter = ValleyCenterMetres(zm);
            float dx = xm - valleyCenter;
            float distance = math.abs(dx);

            float sideRise = 0.020f * dx * dx;
            float farRise = Mathf.Max(0f, zm - 6f) * 0.19f;
            float channel = -0.42f * Mathf.Exp(-(dx * dx) / 18f);

            float wx = xm
                     + 0.95f * Mathf.Sin(zm * 0.31f)
                     + 0.34f * Mathf.Sin(zm * 0.83f + 1.4f);
            float wz = zm
                     + 0.78f * Mathf.Sin(xm * 0.28f - 0.6f)
                     + 0.26f * Mathf.Sin(xm * 0.91f + 0.2f);

            float broad = 0.76f * Mathf.Sin(wz * 0.24f + wx * 0.08f + 0.5f)
                        + 0.52f * Mathf.Sin(wz * 0.43f - wx * 0.15f + 1.7f)
                        + 0.38f * Mathf.Cos(wx * 0.37f + wz * 0.17f)
                        + 0.26f * Mathf.Sin((wx - wz) * 0.57f + 0.3f);

            float shoulders = 0.30f * Mathf.Sin(distance * 0.63f + wz * 0.29f)
                            + 0.17f * Mathf.Cos(distance * 1.11f - wz * 0.21f);

            float tuftRelief = 0.20f * Mathf.Sin(wx * 1.10f + wz * 0.74f)
                                             * Mathf.Sin(wx * 0.43f - wz * 0.97f + 0.8f)
                              + 0.13f * Mathf.Sin(wx * 1.83f - wz * 1.37f + 2.1f)
                              + 0.08f * Mathf.Cos((wx + wz) * 2.15f);

            float farBlend = math.saturate((zm - 12f) / 34f);
            float farRolling = farBlend *
                (0.48f * Mathf.Sin(wz * 0.58f + wx * 0.22f + 0.4f)
               + 0.34f * Mathf.Cos(wz * 0.91f - wx * 0.31f + 1.2f));

            float metres = 0.85f + sideRise + farRise + channel
                         + broad + shoulders + tuftRelief + farRolling;
            return Mathf.RoundToInt(metres * 10f);
        }

        private IStructureAuthoringSession CreateWriter(int budget) =>
            VoxelEngineBootstrap.CreateStructureAuthoring(_storage, budget);

        private void PublishAllResidentRegions() => _storage.PublishAllResidentRegions();

        public void Shutdown()
        {
            if (!_built && _storage == null) return;
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;
            _built = false;
        }
    }
}
