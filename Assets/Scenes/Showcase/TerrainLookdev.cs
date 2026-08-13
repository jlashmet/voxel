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
