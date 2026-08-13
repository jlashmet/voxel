using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering;
using VoxelEngine.Structures;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Terrain-only look-development scene authored into the normal voxel world. Rendering stays
    /// entirely on RegionTable/BrickPool -> VoxelRenderBridge -> production surface extraction.
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

        private RegionTable _table;
        private BrickPool _pool;
        private MaterialPalette _palette;
        private SurfaceCatalogue _surfaces;
        private CoatingCatalogue _coatings;
        private ProfileBlockStore _profiles;
        private VoxelChangeJournal _changes;
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

            _table = new RegionTable(16, Allocator.Persistent);
            _pool = new BrickPool(180_000, Allocator.Persistent);
            _palette = default;
            const uint weather = (1u << Coatings.Moss) | (1u << Coatings.Wet);

            _palette.Register(Mat.Grass, 24, DestructionClass.Powder,
                              SurfaceStyles.Smooth, weather);
            _palette.Register(Mat.Moss, 24, DestructionClass.Powder,
                              SurfaceStyles.Smooth, weather);
            _palette.Register(Mat.Sand, 28, DestructionClass.Powder,
                              SurfaceStyles.Smooth, weather);
            _palette.Register(Mat.TerrainLimestone, 210, DestructionClass.Crumble,
                              SurfaceStyles.Rounded, weather);
            _palette.Register(Mat.TerrainEarth, 32, DestructionClass.Powder,
                              SurfaceStyles.Smooth, weather);
            _palette.Register(Mat.TerrainPathStone, 180, DestructionClass.Crumble,
                              SurfaceStyles.Rounded, weather);
            _palette.Register(Mat.FlowerWhite, 4, DestructionClass.Powder,
                              SurfaceStyles.Rounded, 0u);
            _palette.Register(Mat.FlowerYellow, 4, DestructionClass.Powder,
                              SurfaceStyles.Rounded, 0u);
            _palette.Register(Mat.FlowerPink, 4, DestructionClass.Powder,
                              SurfaceStyles.Rounded, 0u);
            _palette.Register(Mat.FlowerBlue, 4, DestructionClass.Powder,
                              SurfaceStyles.Rounded, 0u);

            _surfaces = SurfaceCatalogue.CreateBuiltIns();
            _coatings = CoatingCatalogue.CreateBuiltIns();
            _profiles = new ProfileBlockStore();

            var writer = new VoxelBrush(_table, _pool, in _palette, 7_000_000);
            AuthorTerrain(ref writer);
            if (writer.BudgetExceeded)
                throw new System.InvalidOperationException("Terrain lookdev exceeded voxel authoring budget.");
            _table = writer.Table;
            _pool = writer.Pool;

            _changes = new VoxelChangeJournal();
            using (NativeArray<int3> regions = _table.GetResidentCoords(Allocator.Temp))
                for (int i = 0; i < regions.Length; i++) _changes.PublishRegion(regions[i]);

            VoxelRenderBridge.Changes = _changes;
            VoxelRenderBridge.Source = WorldView;
            VoxelRenderBridge.SolidBuildBudgetMs = 12.0;
            VoxelRenderBridge.WaterBuildBudgetMs = 0.0;
            VoxelRenderBridge.FarFieldEnabled = false;
            VoxelRenderBridge.TerrainSeed = Seed;
            _built = true;
        }

        private void ApplyReferenceEnvironment()
        {
            Camera camera = SceneCamera;
            camera.backgroundColor = new Color(0.74f, 0.75f, 0.44f, 1f);
            camera.fieldOfView = 27f;
            camera.farClipPlane = 160f;
            camera.transform.position = new Vector3(-0.25f, 10.2f, -12.5f);
            camera.transform.LookAt(new Vector3(0.15f, 5.3f, 25.0f));

            VoxelRenderBridge.SurfaceDebugTint = Color.white;
            VoxelRenderBridge.SunDirection = new Vector3(-0.43f, 0.87f, -0.24f).normalized;
            VoxelRenderBridge.SkyHorizon = new Color(0.92f, 0.88f, 0.50f, 1f);
            VoxelRenderBridge.SkyZenith = new Color(0.79f, 0.82f, 0.48f, 1f);
        }

        private void AuthorTerrain(ref VoxelBrush writer)
        {
            BuildValley(ref writer);
            BuildRockFields(ref writer);
            BuildTurfCushions(ref writer);
            BuildPath(ref writer);
            BuildFlowers(ref writer);
        }

        private static void BuildValley(ref VoxelBrush writer)
        {
            for (int z = TerrainZMin; z <= TerrainZMax; z++)
            for (int x = TerrainXMin; x <= TerrainXMax; x++)
            {
                int top = HeightVoxel(x, z);
                writer.FillColumnBulk(x, top - 7, top, z, Mat.TerrainEarth);
                writer.SetStyled(x, top, z, TurfMaterial(x, z), SurfaceStyles.Smooth);
            }
        }

        private static byte TurfMaterial(int x, int z)
        {
            if (z < 15 && math.abs(x) > 78 && Hash01(x, z) < 0.42f)
                return Mat.Moss;

            float depth = math.saturate((z - 70f) / 430f);
            float warmChance = math.lerp(0.015f, 0.30f, depth);
            return Hash01(x / 4, z / 4) < warmChance ? Mat.Sand : Mat.Grass;
        }

        private static float Hash01(int x, int z)
        {
            uint h = (uint)(x * 374761393 + z * 668265263) ^ Seed;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) * (1f / 16777216f);
        }

        private static void BuildPath(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0x2231u);
            int z = -58;
            while (z < 285)
            {
                float progress = math.saturate((z + 58f) / 343f);
                int centreX = PathCenterVoxel(z);
                int halfWidth = math.max(4, (int)math.round(math.lerp(12f, 5f, progress)));

                for (int lateral = -halfWidth; lateral <= halfWidth; lateral += 4)
                {
                    if (rng.NextFloat() < math.lerp(0.05f, 0.36f, progress)) continue;
                    int px = centreX + lateral + rng.NextInt(-2, 3);
                    int pz = z + rng.NextInt(-2, 3);
                    int hx = rng.NextInt(2, progress < 0.32f ? 5 : 4);
                    int hz = rng.NextInt(2, progress < 0.32f ? 5 : 4);
                    int py = HeightVoxel(px, pz) + 1;
                    StampRoundedBox(ref writer, new int3(px, py, pz),
                        new int3(hx, 1, hz), 1, Mat.TerrainPathStone,
                        SurfaceStyles.Rounded, false);
                }
                z += rng.NextInt(6, 10) + (int)math.round(progress * 3f);
            }
        }

        private static void BuildRockFields(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed);

            for (int shelf = 0; shelf < 72; shelf++)
            {
                int z = rng.NextInt(-45, 545);
                float zm = z * 0.1f;
                int centre = Mathf.RoundToInt(ValleyCenterMetres(zm) * 10f);
                int side = rng.NextBool() ? -1 : 1;
                int distance = rng.NextInt(48, 145);
                int centreX = centre + side * distance;
                if (centreX < TerrainXMin + 12 || centreX > TerrainXMax - 12) continue;

                int count = rng.NextInt(4, 10);
                int stride = rng.NextInt(5, 9);
                for (int i = 0; i < count; i++)
                {
                    int x = centreX + (i - count / 2) * stride + rng.NextInt(-2, 3);
                    int zz = z + rng.NextInt(-4, 5) + (i - count / 2) / 2;
                    if (x <= TerrainXMin + 3 || x >= TerrainXMax - 3) continue;

                    int hx = rng.NextInt(2, 6);
                    int hy = rng.NextInt(2, 4);
                    int hz = rng.NextInt(2, 6);
                    int y = HeightVoxel(x, zz) + hy - 1;
                    StampRoundedBox(ref writer, new int3(x, y, zz), new int3(hx, hy, hz),
                        math.min(2, math.min(hx, hz)), Mat.TerrainLimestone,
                        SurfaceStyles.Rounded, rng.NextFloat() < 0.22f);
                }
            }

            for (int i = 0; i < 150; i++)
            {
                int z = rng.NextInt(-55, 535);
                int x = rng.NextInt(TerrainXMin + 7, TerrainXMax - 7);
                int path = PathCenterVoxel(z);
                if (z < 285 && math.abs(x - path) < 13)
                    x += x < path ? -16 : 16;

                int hx = rng.NextInt(2, 5);
                int hy = rng.NextInt(2, 4);
                int hz = rng.NextInt(2, 5);
                int y = HeightVoxel(x, z) + hy - 1;
                StampRoundedBox(ref writer, new int3(x, y, z), new int3(hx, hy, hz),
                    math.min(2, math.min(hx, hz)), Mat.TerrainLimestone,
                    SurfaceStyles.Rounded, rng.NextFloat() < 0.14f);
            }

            BuildForegroundOutcrop(ref writer, new int3(-108, 0, -46), 13, ref rng);
            BuildForegroundOutcrop(ref writer, new int3(106, 0, -38), 12, ref rng);
            BuildForegroundOutcrop(ref writer, new int3(-132, 0, 34), 10, ref rng);
            BuildForegroundOutcrop(ref writer, new int3(127, 0, 68), 9, ref rng);
        }

        private static void BuildForegroundOutcrop(ref VoxelBrush writer, int3 centre, int scale,
            ref Unity.Mathematics.Random rng)
        {
            for (int layer = 0; layer < 3; layer++)
            {
                int count = 7 - layer;
                for (int i = 0; i < count; i++)
                {
                    int x = centre.x + (i - count / 2) * (scale - 3) + rng.NextInt(-2, 3);
                    int z = centre.z + layer * 5 + rng.NextInt(-2, 3);
                    int hx = rng.NextInt(3, scale / 2 + 2);
                    int hy = rng.NextInt(3, 6);
                    int hz = rng.NextInt(3, scale / 2 + 2);
                    int y = HeightVoxel(x, z) + layer * 3 + hy - 2;
                    StampRoundedBox(ref writer, new int3(x, y, z), new int3(hx, hy, hz),
                        2, Mat.TerrainLimestone, SurfaceStyles.Rounded, rng.NextFloat() < 0.28f);
                }
            }
        }

        private static void BuildTurfCushions(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0x7B19u);

            for (int i = 0; i < 390; i++)
            {
                int z = rng.NextInt(-58, 545);
                int x = rng.NextInt(TerrainXMin + 4, TerrainXMax - 4);
                int rx = rng.NextInt(2, 8);
                int rz = rng.NextInt(3, 10);
                int ry = rng.NextInt(1, 3);
                StampEllipsoid(ref writer, new int3(x, HeightVoxel(x, z) + ry, z),
                    new int3(rx, ry, rz), TurfMaterial(x, z), SurfaceStyles.Smooth);
            }

            for (int i = 0; i < 95; i++)
            {
                int z = rng.NextInt(-48, 530);
                int x = rng.NextInt(TerrainXMin + 8, TerrainXMax - 8);
                int rx = rng.NextInt(7, 15);
                int rz = rng.NextInt(4, 9);
                int ry = rng.NextInt(1, 3);
                StampEllipsoid(ref writer, new int3(x, HeightVoxel(x, z) + ry, z),
                    new int3(rx, ry, rz), TurfMaterial(x, z), SurfaceStyles.Smooth);
            }
        }

        private static void BuildFlowers(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0xD451u);
            for (int i = 0; i < 980; i++)
            {
                int z = rng.NextInt(-55, 525);
                float distance = math.saturate((z + 55f) / 580f);
                if (rng.NextFloat() < distance * 0.18f) continue;
                int x = rng.NextInt(TerrainXMin + 5, TerrainXMax - 5);
                int y = HeightVoxel(x, z) + 2;
                byte flower = Mat.FlowerWhite;
                float colour = rng.NextFloat();
                if (colour > 0.76f && colour <= 0.90f) flower = Mat.FlowerYellow;
                else if (colour > 0.90f && colour <= 0.97f) flower = Mat.FlowerPink;
                else if (colour > 0.97f) flower = Mat.FlowerBlue;
                writer.SetStyled(x, y, z, flower, SurfaceStyles.Rounded);
            }
        }

        private static void StampEllipsoid(ref VoxelBrush writer, int3 centre, int3 radius,
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

        private static void StampRoundedBox(ref VoxelBrush writer, int3 centre, int3 half,
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
            float x = 0.82f * Mathf.Sin(zm * 0.15f + 0.55f)
                    + 0.48f * Mathf.Sin(zm * 0.057f - 0.8f) - 0.10f;
            return Mathf.RoundToInt(x * 10f);
        }

        private static float ValleyCenterMetres(float zm)
        {
            return 1.25f * Mathf.Sin(zm * 0.082f - 0.35f)
                 + 0.46f * Mathf.Sin(zm * 0.215f + 1.10f);
        }

        private static int HeightVoxel(int x, int z)
        {
            float xm = x * 0.1f;
            float zm = z * 0.1f;
            float valleyCenter = ValleyCenterMetres(zm);
            float dx = xm - valleyCenter;

            float sideRise = 0.036f * dx * dx;
            float farRise = Mathf.Max(0f, zm - 7f) * 0.31f;
            float channel = -0.62f * Mathf.Exp(-(dx * dx) / 22f);

            float broad = 0.92f * Mathf.Sin(zm * 0.205f + xm * 0.045f + 0.7f)
                        + 0.62f * Mathf.Sin(zm * 0.39f - xm * 0.073f + 1.9f)
                        + 0.42f * Mathf.Cos((xm + zm) * 0.12f)
                        + 0.28f * Mathf.Sin((xm - zm) * 0.21f + 0.4f);
            float shoulderRoll = 0.48f * Mathf.Sin(math.abs(dx) * 0.55f + zm * 0.18f)
                               + 0.24f * Mathf.Cos(math.abs(dx) * 0.88f - zm * 0.12f);

            float metres = 0.95f + sideRise + farRise + channel + broad + shoulderRoll;
            float terraced = Mathf.Floor(metres / 0.38f) * 0.38f;
            metres = Mathf.Lerp(metres, terraced, 0.14f);
            return Mathf.RoundToInt(metres * 10f);
        }

        private VoxelWorldView WorldView() => new()
        {
            Table = _table,
            Pool = _pool,
            Palette = _palette,
            SurfaceCatalogue = _surfaces,
            CoatingCatalogue = _coatings,
            ProfileBlocks = _profiles,
        };

        public void Shutdown()
        {
            if (!_built && !_table.IsCreated && !_pool.IsCreated) return;
            VoxelRenderBridge.Source = null;
            VoxelRenderBridge.Changes = null;
            if (_table.IsCreated) _table.Dispose();
            if (_pool.IsCreated) _pool.Dispose();
            _table = default;
            _pool = default;
            _built = false;
        }
    }
}
