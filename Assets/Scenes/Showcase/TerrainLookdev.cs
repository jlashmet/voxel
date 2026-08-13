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
            _pool = new BrickPool(96_000, Allocator.Persistent);
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

            var writer = new VoxelBrush(_table, _pool, in _palette, 4_000_000);
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
            BuildPath(ref writer);
        }

        private static void BuildValley(ref VoxelBrush writer)
        {
            for (int z = TerrainZMin; z <= TerrainZMax; z++)
            for (int x = TerrainXMin; x <= TerrainXMax; x++)
            {
                int top = HeightVoxel(x, z);
                writer.FillColumnBulk(x, top - 4, top, z, Mat.TerrainEarth);
                writer.SetStyled(x, top, z, Mat.TerrainTurf, SurfaceStyles.Smooth);
            }
        }

        private static void BuildPath(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0x2231u);
            int z = -50;
            while (z < 330)
            {
                float progress = math.saturate((z + 50f) / 380f);
                int centreX = PathCenterVoxel(z);
                int halfWidth = math.max(3, (int)math.round(math.lerp(15f, 4f, progress)));
                int halfX = math.max(2, (int)math.round(math.lerp(5f, 2f, progress)));
                int halfZ = math.max(2, (int)math.round(math.lerp(4f, 2f, progress)));
                int stride = halfX * 2 - 1;
                for (int x = -halfWidth; x <= halfWidth; x += stride)
                {
                    int px = centreX + x + rng.NextInt(-2, 3);
                    int pz = z + rng.NextInt(-2, 3);
                    int py = HeightVoxel(px, pz) + 1;
                    StampRoundedBox(ref writer, new int3(px, py, pz),
                        new int3(halfX, 1, halfZ), 1, Mat.TerrainPathStone,
                        SurfaceStyles.Rounded, false);
                }
                z += rng.NextInt(6, 10) + (int)math.round(progress * 3f);
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
            float x = 0.45f * Mathf.Sin(zm * 0.18f + 0.7f)
                    + 1.05f * Mathf.Sin(zm * 0.075f - 0.9f) - 0.28f;
            return Mathf.RoundToInt(x * 10f);
        }

        private static int HeightVoxel(int x, int z)
        {
            float xm = x * 0.1f;
            float zm = z * 0.1f;
            float valleyCenter = 0.65f * Mathf.Sin(zm * 0.105f - 0.45f);
            float dx = xm - valleyCenter;
            float sideRise = 0.0105f * dx * dx;
            float farRise = Mathf.Max(0f, zm - 12f) * 0.050f;
            float rolling = 0.58f * Mathf.Sin(xm * 0.20f + zm * 0.115f)
                          + 0.34f * Mathf.Sin(xm * 0.43f - zm * 0.073f + 1.8f)
                          + 0.20f * Mathf.Cos(xm * 0.71f + zm * 0.16f);
            float broad = 0.48f * Mathf.Sin(zm * 0.060f + 0.7f)
                        + 0.32f * Mathf.Cos((xm + zm) * 0.086f);
            float channel = -0.65f * Mathf.Exp(-(dx * dx) / 22f);
            return Mathf.RoundToInt((0.40f + sideRise + farRise + rolling + broad + channel) * 10f);
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
