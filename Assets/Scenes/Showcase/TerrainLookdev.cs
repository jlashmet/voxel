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
    /// Terrain-only look-development scene. Authoring writes semantic voxel cells into the normal
    /// RegionTable/BrickPool and hands that world to VoxelRenderBridge; the production surface
    /// extractor and SmoothSurface shader remain the only rendering path.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed partial class TerrainLookdev : MonoBehaviour
    {
        private const uint Seed = 0x51A17u;
        private const int TerrainXMin = -125;
        private const int TerrainXMax = 125;
        private const int TerrainZMin = -55;
        private const int TerrainZMax = 420;
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

            _table = new RegionTable(16, Allocator.Persistent);
            _pool = new BrickPool(112_000, Allocator.Persistent);
            _palette = default;
            const uint weather = (1u << Coatings.Moss) | (1u << Coatings.Wet);
            _palette.Register(Mat.TerrainTurf, 24, DestructionClass.Powder,
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

            var writer = new VoxelBrush(_table, _pool, in _palette, 5_000_000);
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

        private void AuthorTerrain(ref VoxelBrush writer)
        {
            BuildValley(ref writer);
            BuildTurfCushions(ref writer);
            BuildRockFields(ref writer);
            BuildPath(ref writer);
            BuildFlowers(ref writer);
        }

        private static void BuildValley(ref VoxelBrush writer)
        {
            for (int z = TerrainZMin; z <= TerrainZMax; z++)
            for (int x = TerrainXMin; x <= TerrainXMax; x++)
            {
                int top = HeightVoxel(x, z);
                writer.FillColumnBulk(x, top - 5, top, z, Mat.TerrainEarth);
                writer.SetStyled(x, top, z, Mat.TerrainTurf, SurfaceStyles.Smooth);
            }
        }

        private static void BuildPath(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0x2231u);
            int z = -48;
            while (z < 190)
            {
                float progress = math.saturate((z + 48f) / 238f);
                int centreX = PathCenterVoxel(z);
                int halfWidth = math.max(3, (int)math.round(math.lerp(13f, 4f, progress)));
                int lateralStride = progress < 0.45f ? 6 : 5;

                for (int lateral = -halfWidth; lateral <= halfWidth; lateral += lateralStride)
                {
                    if (rng.NextFloat() < math.lerp(0.08f, 0.30f, progress)) continue;
                    int px = centreX + lateral + rng.NextInt(-2, 3);
                    int pz = z + rng.NextInt(-2, 3);
                    int hx = rng.NextInt(2, progress < 0.35f ? 5 : 4);
                    int hz = rng.NextInt(2, progress < 0.35f ? 5 : 4);
                    int py = HeightVoxel(px, pz) + 1;
                    StampRoundedBox(ref writer, new int3(px, py, pz),
                        new int3(hx, 1, hz), 1, Mat.TerrainPathStone,
                        SurfaceStyles.Rounded, false);
                }

                z += rng.NextInt(7, 12) + (int)math.round(progress * 3f);
            }
        }

        private static void BuildRockFields(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed);

            // Long, low shelves are the dominant stone language in the reference: clusters of
            // softened limestone blocks following contours, usually with turf/moss on top.
            for (int shelf = 0; shelf < 78; shelf++)
            {
                int z = rng.NextInt(-42, 410);
                int centreX = rng.NextInt(TerrainXMin + 14, TerrainXMax - 14);
                int path = PathCenterVoxel(z);
                if (z < 205 && math.abs(centreX - path) < 18)
                    centreX += centreX < path ? -22 : 22;

                int count = rng.NextInt(4, 10);
                int stride = rng.NextInt(5, 9);
                int direction = rng.NextInt(0, 2) == 0 ? -1 : 1;
                for (int i = 0; i < count; i++)
                {
                    int x = centreX + direction * (i - count / 2) * stride + rng.NextInt(-2, 3);
                    int zz = z + rng.NextInt(-4, 5) + (i - count / 2) / 2;
                    if (x <= TerrainXMin + 3 || x >= TerrainXMax - 3) continue;

                    int hx = rng.NextInt(2, 6);
                    int hy = rng.NextInt(2, 5);
                    int hz = rng.NextInt(2, 6);
                    int y = HeightVoxel(x, zz) + hy - rng.NextInt(1, 3);
                    StampRoundedBox(ref writer, new int3(x, y, zz), new int3(hx, hy, hz),
                        math.min(2, math.min(hx, hz)), Mat.TerrainLimestone,
                        SurfaceStyles.Rounded, rng.NextFloat() < 0.76f);
                }
            }

            // Smaller stones fill the gaps so the scene reads as a weathered limestone valley,
            // not a clean grass plane sprinkled with a few hero boulders.
            for (int i = 0; i < 310; i++)
            {
                int z = rng.NextInt(-48, 415);
                int x = rng.NextInt(TerrainXMin + 6, TerrainXMax - 6);
                int path = PathCenterVoxel(z);
                int keepClear = z < 190 ? 13 : 6;
                if (math.abs(x - path) < keepClear)
                    x += x < path ? -keepClear : keepClear;

                int hx = rng.NextInt(2, 6);
                int hy = rng.NextInt(2, 5);
                int hz = rng.NextInt(2, 6);
                if (z < 35 && rng.NextFloat() < 0.36f)
                {
                    hx += 2;
                    hy += 1;
                    hz += 1;
                }

                int y = HeightVoxel(x, z) + hy - rng.NextInt(1, 3);
                StampRoundedBox(ref writer, new int3(x, y, z), new int3(hx, hy, hz),
                    math.min(2, math.min(hx, hz)), Mat.TerrainLimestone,
                    SurfaceStyles.Rounded, rng.NextFloat() < 0.52f);
            }

            BuildForegroundOutcrop(ref writer, new int3(-91, 0, -35), 12, ref rng);
            BuildForegroundOutcrop(ref writer, new int3(83, 0, -27), 12, ref rng);
            BuildForegroundOutcrop(ref writer, new int3(-78, 0, 18), 9, ref rng);
            BuildForegroundOutcrop(ref writer, new int3(78, 0, 55), 9, ref rng);
            BuildForegroundOutcrop(ref writer, new int3(-67, 0, 105), 8, ref rng);
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
                        2, Mat.TerrainLimestone, SurfaceStyles.Rounded, rng.NextFloat() < 0.82f);
                }
            }
        }

        private static void BuildTurfCushions(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0x7B19u);

            for (int i = 0; i < 860; i++)
            {
                int z = rng.NextInt(-52, 416);
                int x = rng.NextInt(TerrainXMin + 3, TerrainXMax - 3);
                int rx = rng.NextInt(2, 8);
                int rz = rng.NextInt(2, 9);
                int ry = rng.NextInt(1, 4);
                StampEllipsoid(ref writer, new int3(x, HeightVoxel(x, z) + ry, z),
                    new int3(rx, ry, rz), Mat.TerrainTurf, SurfaceStyles.Smooth);
            }

            // Elongated low mounds break the visible height-field contour bands and create the
            // dense overlapping grassy pillows seen throughout the reference.
            for (int i = 0; i < 180; i++)
            {
                int z = rng.NextInt(-45, 410);
                int x = rng.NextInt(TerrainXMin + 6, TerrainXMax - 6);
                int rx = rng.NextInt(5, 11);
                int rz = rng.NextInt(2, 6);
                int ry = rng.NextInt(1, 3);
                StampEllipsoid(ref writer, new int3(x, HeightVoxel(x, z) + ry, z),
                    new int3(rx, ry, rz), Mat.TerrainTurf, SurfaceStyles.Smooth);
            }
        }

        private static void BuildFlowers(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0xD451u);
            for (int i = 0; i < 760; i++)
            {
                int z = rng.NextInt(-48, 405);
                float distance = math.saturate((z + 48f) / 453f);
                if (rng.NextFloat() < distance * 0.30f) continue;
                int x = rng.NextInt(TerrainXMin + 5, TerrainXMax - 5);
                int y = HeightVoxel(x, z) + 2;
                byte flower = Mat.FlowerWhite;
                float colour = rng.NextFloat();
                if (colour > 0.78f && colour <= 0.90f) flower = Mat.FlowerYellow;
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
                writer.SetStyled(centre.x + x, centre.y + y, centre.z + z,
                    material, style);
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
            float x = 0.70f * Mathf.Sin(zm * 0.17f + 0.65f)
                    + 0.62f * Mathf.Sin(zm * 0.071f - 0.65f) - 0.12f;
            return Mathf.RoundToInt(x * 10f);
        }

        private static int HeightVoxel(int x, int z)
        {
            float xm = x * 0.1f;
            float zm = z * 0.1f;
            float valleyCenter = 0.92f * Mathf.Sin(zm * 0.105f - 0.45f)
                               + 0.28f * Mathf.Sin(zm * 0.31f + 1.15f);
            float dx = xm - valleyCenter;
            float sideRise = 0.020f * dx * dx;
            float farRise = Mathf.Max(0f, zm - 8f) * 0.055f;
            float rolling = 0.76f * Mathf.Sin(xm * 0.21f + zm * 0.145f)
                          + 0.43f * Mathf.Sin(xm * 0.49f - zm * 0.089f + 1.8f)
                          + 0.28f * Mathf.Cos(xm * 0.78f + zm * 0.18f)
                          + 0.16f * Mathf.Sin(xm * 1.24f + zm * 0.41f);
            float broad = 0.64f * Mathf.Sin(zm * 0.072f + 0.7f)
                        + 0.42f * Mathf.Cos((xm + zm) * 0.098f)
                        + 0.24f * Mathf.Sin((xm - zm) * 0.17f + 0.4f);
            float channel = -0.54f * Mathf.Exp(-(dx * dx) / 18f);
            float metres = 0.62f + sideRise + farRise + rolling + broad + channel;

            // A weak terrace quantization gives the valley broad ledges without turning the
            // entire ground into obvious voxel stairs; the dense turf mounds soften the result.
            float terraced = Mathf.Floor(metres / 0.32f) * 0.32f;
            metres = Mathf.Lerp(metres, terraced, 0.24f);
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
