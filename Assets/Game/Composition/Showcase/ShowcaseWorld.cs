using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using VoxelEngine.Edits.Api;
using VoxelEngine.Edits.Runtime;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Runtime.Occupancy;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Storage.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// The streamed voxel world behind the showcase scene.
    ///
    /// Regions become resident as the camera approaches and are evicted behind it, using
    /// the world-local wanted set and Storage.Api residency mechanics for eviction. A region is
    /// 64 bricks on a side — 51.2 m at 10 cm voxels — so flying in a straight line
    /// continuously loads and discards them, which is the thing worth watching in the HUD.
    ///
    /// This application world constructor is compiled into the Composition assembly so concrete
    /// Runtime storage/edit/structure wiring stays behind the scene boundary. Public collaborators
    /// still consume Storage/Structures/Edits API contracts; the direct physical operations here are
    /// implementation details of the composition root and remain available to the hot generation path.
    /// </summary>
    public sealed partial class ShowcaseWorld : IDisposable
    {
        private static readonly ProfilerMarker s_StreamingMarker =
            new("Voxel.Streaming.ShowcaseStep");
        private static readonly ProfilerMarker s_RefreshPendingMarker =
            new("Voxel.Streaming.RefreshWantedSet");
        private static readonly ProfilerMarker s_TerrainSliceMarker =
            new("Voxel.Streaming.TerrainSlice");
        private static readonly ProfilerMarker s_FinishRegionMarker =
            new("Voxel.Streaming.RegionCommit");
        private static readonly ProfilerMarker s_FeatureMarker =
            new("Voxel.Streaming.FeatureGeneration");
        private static readonly ProfilerMarker s_CastleMarker =
            new("Voxel.Streaming.CastleStage");
        private static readonly ProfilerMarker s_EvictionMarker =
            new("Voxel.Streaming.Eviction");
        public const float VoxelSize = 0.1f;
        // -- material indices ----------------------------------------------------

        public const byte MatStone = 1;
        public const byte MatWood = 2;
        public const byte MatSand = 3;
        public const byte MatGlass = 4;
        public const byte MatBedrock = 5;

        /// <summary>Materials the player can build with, in hotkey order (1..4).</summary>
        public static readonly byte[] BuildableMaterials = { MatStone, MatWood, MatSand, MatGlass };

        public static readonly string[] MaterialNames =
        {
            "empty", "stone", "wood", "sand", "glass", "bedrock",
            "darkstone", "slate", "tile", "cloth", "grass", "water", "gold", "dirt", "moss",
            "lit window", "cascade", "crystal"
        };

        // -- geometry constants --------------------------------------------------

        /// <summary>Voxels along a region edge: 512, i.e. 51.2 m.</summary>
        public const int RegionVoxelEdge = VoxelGrid.RegionVoxelEdge;

        /// <summary>Metres along a region edge.</summary>
        public const float RegionMetres = RegionVoxelEdge * 0.1f;

        /// <summary>Base terrain height in voxels. Terrain stays inside the y = 0 region layer.</summary>
        private const int BaseHeight = 220;

        /// <summary>
        /// Exposed for the renderer's implicit far field, which evaluates this same terrain from
        /// the seed for everything beyond the resident set.
        /// </summary>
        public const int BaseHeightVoxels = BaseHeight;

        // -- state ---------------------------------------------------------------

        // A single Composition lifetime owns the physical store. Ref-return aliases preserve
        // direct native hot paths here without duplicating allocation, adapter wiring, or disposal.
        private readonly VoxelEngineBootstrap.StorageRuntimeLifetime _storage;
        private ref RegionTable _table => ref _storage.Table;
        private ref BrickPool _pool => ref _storage.Pool;
        private RegionReadSource _readSource => _storage.ReadSource;
        private RegionMutationStore _mutationStore => _storage.MutationStore;
        private RegionSnapshotMutationStore _snapshotMutationStore => _storage.SnapshotMutationStore;
        private RegionResidencyStore _residencyStore => _storage.ResidencyStore;

        // Borrow one Storage.Api region view across tight collapse/connectivity scans. Reacquire
        // only when the scan crosses a region or the logical storage version changes; this keeps
        // the clean API boundary without reintroducing a sparse-table lookup per voxel.
        private RegionReadView _cachedReadView;
        private int3 _cachedReadRegion;
        private ulong _cachedReadVersion;
        private bool _hasCachedReadView;

        private FeatureCatalogue _catalogue;
        private ShowcaseStartupSource _startupSource = ShowcaseStartupSource.Bake;
        private bool _includeCastle = true;

        /// <summary>Where the showcase's landmark sits. The spawn camera is aimed here.</summary>
        public const int LandmarkCentreX = RegionVoxelEdge / 2;
        public const int LandmarkCentreZ = RegionVoxelEdge / 2 + 120;
        private ref MaterialPalette _palette => ref _storage.Materials;
        private MaterialSimulationView _materialSimulation;
        private ref SurfaceCatalogue _surfaceCatalogue => ref _storage.Surfaces;
        private ref CoatingCatalogue _coatingCatalogue => ref _storage.Coatings;
        private MaterialAdjacencyCatalogue _materialAdjacencyCatalogue;
        private readonly IStructureProfileStore _profileBlocks = StructuresComposition.CreateProfileStore();
        private uint _editCounter;
        private CastlePlan _castlePlan;
        private bool _hasCastlePlan;
        private bool _castleTrapdoorOpen;
        private bool _castleFrontGateOpen;
        private CastlePlan _pendingCastlePlan;
        private ICastleBuildSession _castleBuild;
        private readonly List<int3> _castleRegions = new();
        private readonly HashSet<int3> _castleRegionSet = new();
        private readonly Queue<int3> _deferredFeatureRegions = new();
        private bool _castleTerrainQueued;

        // Regions whose terrain is committed but whose features have not been built yet, nearest
        // first, plus the one currently being sliced.
        private readonly List<int3> _pendingFeatureRegions = new();
        private FeatureRegionBuild _featureBuild;

        private readonly HashSet<int3> _generated = new();
        private readonly Queue<DetachedVoxelChunk> _detachedChunks = new();

        private static readonly int3[] s_Neighbours =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0),
            new(0, 0, 1), new(0, 0, -1), new(0, -1, 0),
        };

        private struct FallingVoxel
        {
            public int3 Position;
            public byte Material;
            public byte Coating;
        }

        public sealed class DetachedVoxelChunk
        {
            public int3[] Voxels;
            public byte[] Materials;
            public byte[] Coatings;
            public int SourceVoxelCount;
            public float3 ImpactMetres;
            public float3 ImpulseDirection;
        }

        private sealed class VisualBucket
        {
            public readonly List<FallingVoxel> Samples = new(RenderInstancesPerDetachedChunk);
            public int SourceVoxelCount;
            public uint Priority;
        }

        private struct BrickCollapseInfo
        {
            public int OccupiedCount;
            public bool HasStructuralMarker;
        }

        // Must stay in sync with the presentation upload budget, but is owned here so
        // the authoritative Composition world does not depend on scene presentation.
        private const int RenderInstancesPerDetachedChunk = 16;
        private const int MaxCollapseComponentVoxels = 1_048_576;
        private const int FallingChunkEdge = 8;
        public const int MaxQueuedDetachedChunks = 256;
        private const int MaxVisualChunksPerCollapse = 192;

        private VoxelChangeJournal _changes => _storage.ChangeJournal;
        private readonly List<int3> _pendingLoads = new();
        private readonly HashSet<int3> _pendingLoadSet = new();
        private readonly ShowcasePendingLoadComparer _pendingLoadComparer = new();

        public IRegionReadSource ReadStorage
        {
            get
            {
                _readSource.Refresh(in _table, in _pool);
                return _readSource;
            }
        }
        public IVoxelSurfaceQuery SurfaceQuery
        {
            get
            {
                _readSource.Refresh(in _table, in _pool);
                return _readSource;
            }
        }
        public IRegionMutationStore MutationStorage
        {
            get
            {
                _mutationStore.Refresh(in _table, in _pool);
                return _mutationStore;
            }
        }
        public IRegionSnapshotSource SnapshotStorage
        {
            get
            {
                _readSource.Refresh(in _table, in _pool);
                return _readSource;
            }
        }
        public IRegionSnapshotMutationStore SnapshotMutationStorage
        {
            get
            {
                _snapshotMutationStore.Refresh(in _table, in _pool);
                return _snapshotMutationStore;
            }
        }
        public MaterialPaletteView Palette => _palette;
        public SurfaceCatalogueView SurfaceRules => _surfaceCatalogue;
        public CoatingCatalogueView CoatingRules => _coatingCatalogue;
        public IVoxelChangeSource Changes => _changes;
        public StoragePressure StoragePressure
        {
            get
            {
                _residencyStore.Refresh(in _table, in _pool);
                return _residencyStore.Pressure;
            }
        }

        public uint Seed { get; }

        /// <summary>Regions in the wanted set that have not been generated yet.</summary>
        public int PendingRegionLoads => _pendingLoads.Count;
        public int RequiredCastleRegions => _castleRegions.Count;
        public int ReadyCastleRegions
        {
            get
            {
                int ready = 0;
                for (int i = 0; i < _castleRegions.Count; i++)
                    if (_generated.Contains(_castleRegions[i])) ready++;
                return ready;
            }
        }
        public int CastleBuildStage => _castleBuild != null ? _castleBuild.StageNumber : 0;
        public int LastCastleStage { get; private set; }
        public double LastCastleStageMs { get; private set; }
        public int MaxCastleStage { get; private set; }
        public double MaxCastleStageMs { get; private set; }

        public int PendingDetachedChunks => _detachedChunks.Count;

        public int RegionsGenerated { get; private set; }
        public int RegionsEvicted { get; private set; }
        public double LastGenerateMs { get; private set; }
        public int LastEditVoxels { get; private set; }

        /// <summary>
        /// Load radius in regions. Deliberately far smaller than
        /// the shipping streaming load radius (500 m): the shipping engine
        /// covers that distance with mip-level far-field data, whereas this demo builds full
        /// voxel detail plus a triangle mesh for every resident region. This is a demo budget,
        /// not a tiering parameter — Constitution Principle IV is about device class, and this
        /// number is the same on every device.
        /// </summary>
        public int LoadRadiusRegions { get; }

        public int UnloadRadiusRegions { get; }

        public ShowcaseWorld(uint seed, int brickPoolCapacity, int loadRadiusRegions,
                             int unloadRadiusRegions,
                             long maxMixedBrickAllocationBytes =
                                 VoxelEngineBootstrap.MaximumMixedBrickAllocationBytes)
        {
            Seed = seed;
            LoadRadiusRegions = math.max(1, loadRadiusRegions);
            UnloadRadiusRegions = math.max(LoadRadiusRegions + 1, unloadRadiusRegions);

            _storage = new VoxelEngineBootstrap.StorageRuntimeLifetime(
                64, brickPoolCapacity, 4096, maxMixedBrickAllocationBytes);

            const uint weatherCoatings = (1u << Coatings.Moss) | (1u << Coatings.Snow)
                                        | (1u << Coatings.Soot) | (1u << Coatings.Wet);
            _palette.Register(MatStone, 200, DestructionClass.Crumble,
                              SurfaceStyles.Smooth, weatherCoatings);
            _palette.Register(MatWood, 90, DestructionClass.Splinter,
                              SurfaceStyles.Planar, weatherCoatings);
            _palette.Register(MatSand, 20, DestructionClass.Powder,
                              SurfaceStyles.Smooth, 1u << Coatings.Wet);
            _palette.Register(MatGlass, 10, DestructionClass.Powder,
                              SurfaceStyles.Sharp, 1u << Coatings.Wet);
            _palette.Register(MatBedrock, 255, DestructionClass.None,
                              SurfaceStyles.Planar, 0u);

            // Castle materials. Weathering and roofing read as different stone, which is most of
            // what stops masonry looking extruded.
            _palette.Register(6, 210, DestructionClass.Crumble,
                              SurfaceStyles.Smooth, weatherCoatings); // dark stone
            _palette.Register(7, 120, DestructionClass.Crumble,
                              SurfaceStyles.Planar, weatherCoatings); // slate
            _palette.Register(8, 110, DestructionClass.Crumble,
                              SurfaceStyles.Planar, weatherCoatings); // tile
            _palette.Register(9, 15, DestructionClass.Splinter,
                              SurfaceStyles.Planar, weatherCoatings); // cloth
            _palette.Register(10, 25, DestructionClass.Powder,
                              SurfaceStyles.Smooth, weatherCoatings); // grass
            _palette.Register(11, 5, DestructionClass.Spreading,
                              SurfaceStyles.Smooth, 0u); // water
            _palette.Register(12, 180, DestructionClass.Crumble,
                              SurfaceStyles.Sharp, 1u << Coatings.Soot); // gold
            _palette.Register(13, 30, DestructionClass.Powder,
                              SurfaceStyles.Smooth, weatherCoatings); // dirt
            _palette.Register(14, 40, DestructionClass.Powder,
                              SurfaceStyles.Smooth, weatherCoatings); // legacy moss material
            _palette.Register(15, 18, DestructionClass.Powder,
                              SurfaceStyles.Sharp, 1u << Coatings.Wet); // leaded window glass
            _palette.Register(Mat.MasonrySmall, 200, DestructionClass.Crumble,
                              SurfaceStyles.MasonryJoint, weatherCoatings);
            _palette.Register(Mat.MasonryMedium, 210, DestructionClass.Crumble,
                              SurfaceStyles.MasonryJoint, weatherCoatings);
            _palette.Register(Mat.MasonryLarge, 220, DestructionClass.Crumble,
                              SurfaceStyles.MasonryJoint, weatherCoatings);

            _materialSimulation = _palette.SimulationView;

            _materialAdjacencyCatalogue = default;

            _catalogue = ShowcaseCatalogue.Build(seed, Allocator.Persistent);
        }

        private static int3 PositionToRegion(float3 position)
        {
            return new int3(
                (int)math.floor(position.x / RegionMetres),
                (int)math.floor(position.y / RegionMetres),
                (int)math.floor(position.z / RegionMetres));
        }

        // -- streaming -----------------------------------------------------------

        /// <summary>
        /// Advances streaming for up to <paramref name="budgetMs"/> milliseconds.
        ///
        /// Generation is interruptible: a region is built in slices and the work stops at the
        /// budget wherever it happens to be, resuming next frame. Whole regions were the
        /// obvious unit and the wrong one — a region costs tens of milliseconds to fill, which
        /// is several frames' worth, so the unit of work has to be smaller than the unit of
        /// data.
        /// </summary>
        public void StepStreaming(float3 cameraMetres, double budgetMs)
        {
            using var streamingScope = s_StreamingMarker.Auto();
            var centre = PositionToRegion(cameraMetres);

            // IncrementalBuild holds handle-like RegionTable/BrickPool snapshots whose scalar
            // allocator bookkeeping is published after each stage. No other world writer may
            // allocate between those stages. Give the castle exclusive mutation ownership until
            // its atomic commit, one semantic stage per frame. The pending terrain list cannot be
            // consumed while the castle owns writes, so rebuilding and sorting it here would be
            // dead work on every castle-build frame.
            if (_castleBuild != null && !_castleBuild.IsComplete)
            {
                var castleDeadline = Time.realtimeSinceStartupAsDouble + budgetMs * 0.001;
                var castleStart = Time.realtimeSinceStartupAsDouble;
                // Spend the frame's budget, rather than taking a single step and returning.
                //
                // The site stage sub-steps a fixed four rows per call and the keep sub-steps one
                // storey, so stepping once per frame pinned the whole build to a rate that has
                // nothing to do with the budget: the site's two phases alone are 811 rows each,
                // which is roughly 650 frames of sculpting before stage 2 begins. The showcase
                // raises the budget to 12 ms precisely for this window and about 3 ms of it was
                // being used, so the castle was still absent long after any caller stopped
                // waiting — no gate, no keep floor, no recorded far-field silhouette.
                //
                // Looping here does not weaken the atomicity this branch exists to protect. The
                // hazard is another world writer allocating between two castle stages, and the
                // early return below still keeps terrain streaming out until the build commits;
                // all this changes is how many of the castle's own stages fit in one frame.
                using (s_CastleMarker.Auto())
                {
                    do
                    {
                        StepLandmarks();
                    }
                    while (_castleBuild != null && !_castleBuild.IsComplete
                           && Time.realtimeSinceStartupAsDouble < castleDeadline);
                }
                LastGenerateMs = (Time.realtimeSinceStartupAsDouble - castleStart) * 1000.0;
                return;
            }

            using (s_RefreshPendingMarker.Auto()) RefreshPending(centre);

            var deadline = Time.realtimeSinceStartupAsDouble + budgetMs * 0.001;
            var start = Time.realtimeSinceStartupAsDouble;
            bool didWork = false;
            bool landmarkStepped = false;

            // Features belong to regions whose terrain is already committed, so this queue is the
            // only thing between the player and a settlement that is still bare ground. Give it a
            // share of the frame ahead of new terrain, and let terrain keep the remainder: the
            // castle branch above has already returned if a landmark owns the world's writes.
            StepFeatureQueue(cameraMetres, start + budgetMs * 0.001 * FeatureBudgetShare);
            if (_featureBuild != null || _pendingFeatureRegions.Count > 0) didWork = true;

            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (!_gen.Active)
                {
                    if (_pendingLoads.Count == 0)
                    {
                        if (StepLandmarks()) didWork = landmarkStepped = true;
                        break;
                    }

                    BeginRegion(_pendingLoads[0]);
                    _pendingLoads.RemoveAt(0);
                }

                didWork = true;
                bool regionComplete;
                using (s_TerrainSliceMarker.Auto()) regionComplete = StepRegion();
                if (regionComplete)
                {
                    using (s_FinishRegionMarker.Auto()) FinishRegion();
                    if (StepLandmarks())
                    {
                        didWork = landmarkStepped = true;
                        break;
                    }
                }
            }

            // Landmark construction is admitted only after terrain streaming has yielded its
            // budget. One semantic castle stage may still be substantial, but this removes the
            // former unbounded all-regions + all-stages scene-load operation.
            if (!landmarkStepped && !_gen.Active && _pendingLoads.Count == 0
                && StepLandmarks()) didWork = true;

            if (didWork) LastGenerateMs = (Time.realtimeSinceStartupAsDouble - start) * 1000.0;

            using (s_EvictionMarker.Auto()) EvictDistantRegions(centre);
        }

        /// <summary>
        /// Generates one region to completion regardless of budget. Used once at spawn: the
        /// character needs ground under it before physics can run at all.
        /// </summary>
        public void GenerateRegionBlocking(int3 regionCoord)
        {
            if (_generated.Contains(regionCoord)) return;
            if (_gen.Active) FinishRegionForced();

            BeginRegion(regionCoord);
            while (!StepRegion()) { }
            FinishRegion();

            // Spawn cannot leave this region half-authored: the character is placed on whatever
            // surface it finds, and a queued feature build would move the ground out from under
            // it a few frames later. This is the one path that is allowed to ignore the budget.
            if (_pendingFeatureRegions.Remove(regionCoord))
            {
                // A build for some other region may be mid-slice; it keeps its place.
                FeatureRegionBuild interrupted = _featureBuild;
                _featureBuild = new FeatureRegionBuild(regionCoord);
                _readSource.Refresh(in _table, in _pool);
                _mutationStore.Refresh(in _table, in _pool);
                while (!_featureBuild.Step(in _catalogue, Seed, _readSource, _mutationStore,
                                           int.MaxValue)) { }
                CompleteFeatureBuild();
                _featureBuild = interrupted;
            }
        }

        public bool IsGenerated(int3 regionCoord) => _generated.Contains(regionCoord);

        /// <summary>
        /// Radius, in metres, of the largest disc around a point in which every ground region is
        /// generated. The far-field clipmap opens its hole to exactly this.
        ///
        /// The hole used to be a fixed radius fixed at startup, which is wrong in both
        /// directions. Regions stream in over seconds on a few milliseconds per frame, so during
        /// load the hole was empty of voxels *and* of far mesh — the player watched the ground
        /// appear around them in squares. After a teleport it was worse, because eviction empties
        /// the neighbourhood and the hole stayed open anyway.
        ///
        /// Measured by expanding square shells outward and stopping at the first shell with a
        /// missing ground layer, so the answer is the largest radius that is completely filled
        /// rather than the furthest region that happens to exist. Erring inward is deliberate:
        /// the far mesh overlapping resident voxels is depth-tested away, whereas erring outward
        /// is the hole this method exists to close.
        /// </summary>
        /// <summary>
        /// Largest radius around <paramref name="cameraMetres"/> that lies wholly inside the
        /// block of regions out to <paramref name="completeShells"/>.
        ///
        /// The shell count is a region-grid measurement and the hole is centred on the camera,
        /// which stands at an arbitrary point inside its own region. Returning shell * 51.2 m
        /// therefore over-reports by however far the camera sits from its region's centre — up
        /// to half a region — and that sliver is a ring of columns with no voxels and no far
        /// mesh. Measuring to the nearest edge of the resident block instead is exact.
        /// </summary>
        private static float GuaranteedRadius(float3 cameraMetres, int3 centre, int completeShells)
        {
            if (completeShells < 0) return 0f;

            float minX = (centre.x - completeShells) * RegionMetres;
            float maxX = (centre.x + completeShells + 1) * RegionMetres;
            float minZ = (centre.z - completeShells) * RegionMetres;
            float maxZ = (centre.z + completeShells + 1) * RegionMetres;

            float toEdge = math.min(
                math.min(cameraMetres.x - minX, maxX - cameraMetres.x),
                math.min(cameraMetres.z - minZ, maxZ - cameraMetres.z));
            return math.max(0f, toEdge);
        }

        public float ResidentGroundRadiusMetres(float3 cameraMetres)
        {
            var centre = PositionToRegion(cameraMetres);

            for (int shell = 0; shell <= LoadRadiusRegions; shell++)
            {
                for (int dx = -shell; dx <= shell; dx++)
                for (int dz = -shell; dz <= shell; dz++)
                {
                    // Perimeter of this shell only; the interior was cleared by earlier passes.
                    if (math.max(math.abs(dx), math.abs(dz)) != shell) continue;
                    // The far-terrain hole is centred on the actual camera, not on the integer
                    // coordinate of its current region. Check the same physical footprint used by
                    // RefreshPending so a sub-region camera offset cannot classify an unloaded
                    // fringe column as safely covered near terrain.
                    int rx = centre.x + dx;
                    int rz = centre.z + dz;
                    if (!ShowcaseResidencyFootprint.ColumnIntersectsRadius(
                            cameraMetres, rx, rz, LoadRadiusRegions * RegionMetres))
                        continue;

                    SurfaceLayerSpan(rx, rz, out int minLayer, out int maxLayer);
                    if (maxLayer - minLayer > MaxSurfaceLayersPerColumn)
                        maxLayer = minLayer + MaxSurfaceLayersPerColumn;

                    for (int ry = minLayer; ry <= maxLayer; ry++)
                        if (!_generated.Contains(new int3(rx, ry, rz)))
                            return GuaranteedRadius(cameraMetres, centre, shell - 1);
                }
            }

            return math.min(LoadRadiusRegions * RegionMetres,
                            GuaranteedRadius(cameraMetres, centre, LoadRadiusRegions));
        }

        /// <summary>
        /// Coarse record of built content for far-field rendering. Outlives region residency:
        /// evicting a region discards its voxels but not its silhouette.
        /// </summary>
        public FarFieldStructureStore FarField { get; } = new();

        /// <summary>Region containing a world position in metres.</summary>
        public static int3 RegionAt(Vector3 metres) => new int3(
            Mathf.FloorToInt(metres.x / RegionMetres),
            Mathf.FloorToInt(metres.y / RegionMetres),
            Mathf.FloorToInt(metres.z / RegionMetres));

        /// <summary>
        /// The span of region layers the terrain surface passes through over one horizontal
        /// region column, as an inclusive [min, max] in region-y.
        ///
        /// This is what makes kilometre-scale mountains affordable. A 5 km peak spans about a
        /// hundred region layers, and making that column resident would cost a hundred megabytes
        /// of brick pointers for a single position on the map. But terrain is a surface, not a
        /// volume: over any one column the ground occupies only the few layers it actually
        /// crosses, however tall the mountain is. Residency follows that surface, so height
        /// costs generation time rather than memory.
        ///
        /// Sampled on a coarse lattice rather than every column — the surface is smooth at
        /// region scale, and a full 512x512 sample per region would dominate the frame.
        /// </summary>
        private void SurfaceLayerSpan(int regionX, int regionZ, out int minLayer, out int maxLayer)
        {
            // Cached per column. The height field is static, so a column's span is fixed for the
            // life of the world, but residency refreshes every time the viewer crosses a region
            // and each miss costs 81 height samples. Recomputing it was adding roughly 200 ms
            // across showcase startup — enough to push the castle build past its budget.
            var key = new int2(regionX, regionZ);
            if (_surfaceSpanCache.TryGetValue(key, out int2 cached))
            {
                minLayer = cached.x;
                maxLayer = cached.y;
                return;
            }

            ComputeSurfaceLayerSpan(regionX, regionZ, out minLayer, out maxLayer);
            _surfaceSpanCache[key] = new int2(minLayer, maxLayer);
        }

        private void ComputeSurfaceLayerSpan(int regionX, int regionZ,
                                             out int minLayer, out int maxLayer)
        {
            int originX = regionX * RegionVoxelEdge;
            int originZ = regionZ * RegionVoxelEdge;

            int lowest = int.MaxValue;
            int highest = int.MinValue;
            const int step = RegionVoxelEdge / 8;
            for (int z = 0; z <= RegionVoxelEdge; z += step)
            for (int x = 0; x <= RegionVoxelEdge; x += step)
            {
                int h = TerrainSampler.HeightAt(originX + x, originZ + z, Seed);
                if (h < lowest) lowest = h;
                if (h > highest) highest = h;
            }

            // A margin of one brick covers the sample lattice missing a local extremum between
            // its taps, which would otherwise leave a hole at a ridge line.
            lowest -= VoxelDimensions.BrickEdge;
            highest += VoxelDimensions.BrickEdge;

            minLayer = lowest >> VoxelDimensions.RegionVoxelEdgeLog2;
            maxLayer = highest >> VoxelDimensions.RegionVoxelEdgeLog2;
            if (minLayer < 0) minLayer = 0;
            if (maxLayer < minLayer) maxLayer = minLayer;
        }

        /// <summary>
        /// Ceiling on region layers loaded for one column in a single refresh. Sized so an
        /// ordinary slope loads in full and only genuine cliff faces are deferred.
        /// </summary>
        private const int MaxSurfaceLayersPerColumn = 3;

        private readonly Dictionary<int2, int2> _surfaceSpanCache = new();

        private void QueueRegion(int3 rc)
        {
            if (_generated.Contains(rc)) return;
            if (_gen.Active && _gen.Coord.Equals(rc)) return;
            if (!_pendingLoadSet.Add(rc)) return;
            _pendingLoads.Add(rc);
        }

        private void RefreshPending(int3 centre)
        {
            _pendingLoads.Clear();
            _pendingLoadSet.Clear();

            // Residency follows the terrain surface through the vertical region stack rather
            // than pinning a single layer. An empty region still costs 1 MB of brick pointers,
            // so only the layers the ground actually crosses are loaded — plus the layer the
            // camera occupies, so standing in mid-air over a valley still has a region to
            // stand in and to collide against.
            for (int dx = -LoadRadiusRegions; dx <= LoadRadiusRegions; dx++)
            for (int dz = -LoadRadiusRegions; dz <= LoadRadiusRegions; dz++)
            {
                if (dx * dx + dz * dz > LoadRadiusRegions * LoadRadiusRegions) continue;

                int rx = centre.x + dx;
                int rz = centre.z + dz;
                SurfaceLayerSpan(rx, rz, out int minLayer, out int maxLayer);

                // Bound the span. A near-vertical column on a mountain face can legitimately
                // cross many layers, but loading an unbounded run of them stalls streaming for
                // one cliff, so the surface is followed from its floor upward and the rest is
                // left to be picked up as the viewer climbs.
                if (maxLayer - minLayer > MaxSurfaceLayersPerColumn)
                    maxLayer = minLayer + MaxSurfaceLayersPerColumn;

                for (int ry = minLayer; ry <= maxLayer; ry++)
                    QueueRegion(new int3(rx, ry, rz));

                // The viewer's own layer, when the surface does not already cover it — one
                // layer, not the fill between. Extending the span to reach the camera meant
                // that standing a kilometre above the ground queued every layer in between:
                // hundreds of regions per column, none of which contain anything.
                if (centre.y < minLayer || centre.y > maxLayer)
                    QueueRegion(new int3(rx, centre.y, rz));
            }

            // The castle is atomic: its builder cannot start until every terrain region it
            // touches exists. Keep those dependencies in the same bounded queue even if a
            // future castle plan grows beyond the ordinary camera residency radius.
            if (_castleTerrainQueued && !_hasCastlePlan)
            {
                for (int i = 0; i < _castleRegions.Count; i++)
                    QueueRegion(_castleRegions[i]);
            }

            // Landmark dependencies first, then nearest camera residency. Appending castle
            // regions after sorting made a complete landmark wait behind the entire radius and
            // could never meet the startup contract.
            _pendingLoadComparer.Centre = centre;
            _pendingLoadComparer.PrioritizeCastle = _castleTerrainQueued && !_hasCastlePlan;
            _pendingLoadComparer.CastleRegions = _castleRegionSet;
            _pendingLoads.Sort(_pendingLoadComparer);
        }

        private void EvictDistantRegions(int3 centre)
        {
            _residencyStore.Refresh(in _table, in _pool);
            int cursor = 0;

            while (_table.TryGetNextResidentCoord(ref cursor, out int3 rc))
            {
                // The in-flight generator owns this Region value until FinishRegion commits it.
                // Evicting it here disposes BrickRefs out from under the next StepRegion call.
                if (_gen.Active && rc.Equals(_gen.Coord)) continue;
                if (_castleTerrainQueued && !_hasCastlePlan && _castleRegionSet.Contains(rc))
                    continue;

                int dx = rc.x - centre.x;
                int dz = rc.z - centre.z;

                if (dx * dx + dz * dz <= UnloadRadiusRegions * UnloadRadiusRegions) continue;

                // No write-back: the client owns no truth, so eviction discards and the region
                // regenerates from the seed on return.
                _residencyStore.EvictRegion(rc);
                _generated.Remove(rc);
                CancelFeatureWork(rc);
                _changes.PublishRegion(rc, VoxelChangeKind.Residency);
                RegionsEvicted++;
            }
        }

        // -- terrain generation --------------------------------------------------

        /// <summary>
        /// In-progress generation of a single region.
        ///
        /// Heights are computed first because every brick column needs the min and max surface
        /// height across its footprint to decide whether it is uniform. Both phases resume from
        /// a cursor, which is what makes generation interruptible.
        /// </summary>
        private struct GenState
        {
            public bool Active;
            public int3 Coord;
            public Region Region;
            public NativeArray<int> Heights;
            public JobHandle HeightJob;
            public bool HeightJobScheduled;
            public int Phase;      // 0 = heights, 1 = bricks
            public int Cursor;     // rows done, then brick columns done
        }

        private GenState _gen;

        /// <summary>Voxel rows of height sampled per slice.</summary>
        private const int HeightRowsPerSlice = 24;

        /// <summary>Brick columns filled per slice. One column is 64 bricks tall.</summary>
        private const int BrickColumnsPerSlice = 48;

        /// <summary>Fraction of the in-flight region that is built, 0..1.</summary>
        public float GenerationProgress => !_gen.Active ? 1f
            : _gen.Phase == 0
                ? _gen.Cursor / (float)RegionVoxelEdge * 0.3f
                : 0.3f + _gen.Cursor / (float)(VoxelDimensions.RegionEdge * VoxelDimensions.RegionEdge) * 0.7f;

        private void BeginRegion(int3 regionCoord)
        {
            _gen.Active = true;
            _gen.Coord = regionCoord;
            _gen.Region = _table.LoadRegion(regionCoord);
            _gen.Heights = new NativeArray<int>(RegionVoxelEdge * RegionVoxelEdge, Allocator.Persistent);
            _gen.Phase = 0;
            _gen.Cursor = 0;
            int3 originVoxel = regionCoord * RegionVoxelEdge;
            _gen.HeightJob = new ShowcaseHeightJob
            {
                Heights = _gen.Heights,
                Origin = new int2(originVoxel.x, originVoxel.z),
                Edge = RegionVoxelEdge,
                Seed = Seed,
            }.Schedule(_gen.Heights.Length, 256);
            _gen.HeightJobScheduled = true;
        }

        /// <summary>Advances the in-flight region by one slice. Returns true when it is complete.</summary>
        private bool StepRegion()
        {
            int3 originVoxel = _gen.Coord * RegionVoxelEdge;

            if (_gen.Phase == 0)
            {
                if (!_gen.HeightJob.IsCompleted) return false;
                _gen.HeightJob.Complete();
                _gen.HeightJobScheduled = false;
                _gen.Phase = 1;
                _gen.Cursor = 0;
                return false;
            }

            int columns = VoxelDimensions.RegionEdge * VoxelDimensions.RegionEdge;
            int endColumn = math.min(_gen.Cursor + BrickColumnsPerSlice, columns);

            for (int c = _gen.Cursor; c < endColumn; c++)
                FillBrickColumn(originVoxel, c % VoxelDimensions.RegionEdge, c / VoxelDimensions.RegionEdge);

            _gen.Cursor = endColumn;
            return _gen.Cursor >= columns;
        }

        /// <summary>
        /// Fills one 64-brick-tall column.
        ///
        /// Bricks entirely below the surface become uniform references and cost nothing; bricks
        /// entirely above become empty references and cost nothing. Only bricks the surface
        /// passes through take a pool slot, and those are written straight into the pool rather
        /// than through <see cref="VoxelAccess.SetVoxel"/> — the per-write collapse-to-uniform
        /// check is exactly right for edits and exactly wrong for bulk fill, where it would
        /// rescan 512 voxels on every one of them.
        /// </summary>
        private void FillBrickColumn(int3 originVoxel, int bx, int bz)
        {
            int minH = int.MaxValue, maxH = int.MinValue;

            for (int vz = 0; vz < VoxelDimensions.BrickEdge; vz++)
            for (int vx = 0; vx < VoxelDimensions.BrickEdge; vx++)
            {
                int h = _gen.Heights[(bx * VoxelDimensions.BrickEdge + vx)
                                     + (bz * VoxelDimensions.BrickEdge + vz) * RegionVoxelEdge];
                if (h < minH) minH = h;
                if (h > maxH) maxH = h;
            }

            for (int by = 0; by < VoxelDimensions.RegionEdge; by++)
            {
                int brickBaseY = originVoxel.y + by * VoxelDimensions.BrickEdge;
                int brickTopY = brickBaseY + VoxelDimensions.BrickEdge - 1;
                int idx = Region.BrickIndex(bx, by, bz);

                if (brickBaseY > maxH)
                {
                    _gen.Region.BrickRefs[idx] = BrickRef.Empty;
                    continue;
                }

                if (brickTopY <= minH)
                {
                    _gen.Region.BrickRefs[idx] = BrickRef.Uniform(
                        brickTopY < minH - DeepDepth ? MatBedrock : MatStone);
                    continue;
                }

                int poolIndex = _pool.Allocate();

                for (int vz = 0; vz < VoxelDimensions.BrickEdge; vz++)
                for (int vy = 0; vy < VoxelDimensions.BrickEdge; vy++)
                for (int vx = 0; vx < VoxelDimensions.BrickEdge; vx++)
                {
                    int h = _gen.Heights[(bx * VoxelDimensions.BrickEdge + vx)
                                         + (bz * VoxelDimensions.BrickEdge + vz) * RegionVoxelEdge];

                    _pool.SetVoxel(poolIndex,
                                   OccupancyMask.VoxelIndex(vx, vy, vz),
                                   MaterialAt(brickBaseY + vy, h));
                }

                if (_pool.TryGetUniformMaterial(poolIndex, out var uniform))
                {
                    _pool.Free(poolIndex);
                    _gen.Region.BrickRefs[idx] = uniform == VoxelDimensions.MaterialEmpty
                        ? BrickRef.Empty
                        : BrickRef.Uniform(uniform);
                }
                else
                {
                    _gen.Region.BrickRefs[idx] = BrickRef.FromPoolIndex(poolIndex);
                }
            }
        }

        private void FinishRegion()
        {
            var coord = _gen.Coord;

            // Generation fills the region's bricks directly, which does not maintain the block
            // occupancy summary that surface discovery reads. Without this the terrain exists in
            // storage and renders as nothing at all.
            _mutationStore.Refresh(in _table, in _pool);
            _mutationStore.RefreshRegionSummary(ref _gen.Region);

            _table.CommitRegion(_gen.Region);

            _generated.Add(coord);
            _changes.PublishRegion(coord, VoxelChangeKind.All);

            // Neighbours must re-mesh too: faces along the shared border were meshed as the edge
            // of the loaded world and are now interior.

            // Features are generated after terrain, so they carve and build against finished
            // ground. Everything here is a function of (seed, catalogue, region coordinate) —
            // no neighbour is consulted, which is why regions may arrive in any order, and why
            // the work can be queued rather than paid for in the frame the terrain lands.
            bool deferFeatures = _castleTerrainQueued && !_hasCastlePlan
                              && _castleRegionSet.Contains(coord);
            if (deferFeatures)
            {
                _deferredFeatureRegions.Enqueue(coord);
                CaptureFarField(coord);
            }
            else if (_catalogue.IsCreated)
            {
                if (!_pendingFeatureRegions.Contains(coord)) _pendingFeatureRegions.Add(coord);
            }
            else
            {
                CaptureFarField(coord);
            }

            RegionsGenerated++;

            // The pointer grid is only final now. Anything uploaded earlier described a
            // half-built region.
            _changes.PublishRegion(coord, VoxelChangeKind.All);

            FinishRegionForced();

            if (coord.Equals(int3.zero)) QueueLandmarks();
        }

        /// <summary>
        /// Records built content in a form that survives eviction, so the castle and Kentridge
        /// stay visible at the distance terrain is drawn rather than popping in at the streaming
        /// radius. Regions that are plain terrain store nothing.
        ///
        /// Called once a region's features are built, never before. Capturing at the end of
        /// terrain generation was strictly too early for any region a settlement stands in: the
        /// store correctly saw plain ground, recorded nothing, and the buildings that arrived
        /// afterwards had no silhouette — the same failure the castle path documents.
        /// </summary>
        private void CaptureFarField(int3 coord) => FarField.CaptureRegion(coord, ReadStorage, Seed);

        /// <summary>Storage-block primitive tiles rasterised before checking the clock again.</summary>
        private const int FeatureTilesPerSlice = 4;

        /// <summary>
        /// Share of a streaming frame's budget reserved for queued features. Terrain keeps the
        /// rest: ground under the player outranks what stands on it, but a settlement that never
        /// drains is worse than terrain arriving a little later.
        /// </summary>
        private const double FeatureBudgetShare = 0.5;

        /// <summary>
        /// Advances queued feature generation until <paramref name="deadlineSeconds"/>.
        ///
        /// Regions are taken nearest-first, because the queue is what stands between the player
        /// and a settlement that is still an empty street grid, and the nearest one is the one
        /// they are about to walk into.
        /// </summary>
        private void StepFeatureQueue(float3 cameraMetres, double deadlineSeconds)
        {
            if (!_catalogue.IsCreated) return;
            if (_featureBuild == null && _pendingFeatureRegions.Count == 0) return;

            var featureStart = Time.realtimeSinceStartupAsDouble;
            bool didWork = false;

            using var featureScope = s_FeatureMarker.Auto();
            while (Time.realtimeSinceStartupAsDouble < deadlineSeconds)
            {
                if (_featureBuild == null)
                {
                    if (_pendingFeatureRegions.Count == 0) break;
                    _featureBuild = new FeatureRegionBuild(TakeNearestFeatureRegion(cameraMetres));
                }

                _readSource.Refresh(in _table, in _pool);
                _mutationStore.Refresh(in _table, in _pool);
                didWork = true;

                if (!_featureBuild.Step(in _catalogue, Seed, _readSource, _mutationStore,
                                        FeatureTilesPerSlice))
                    continue;

                CompleteFeatureBuild();
            }

            if (didWork)
                LastFeatureMs = (Time.realtimeSinceStartupAsDouble - featureStart) * 1000.0;
        }

        private int3 TakeNearestFeatureRegion(float3 cameraMetres)
        {
            int3 centre = PositionToRegion(cameraMetres);
            int best = 0;
            long bestDistance = long.MaxValue;
            for (int i = 0; i < _pendingFeatureRegions.Count; i++)
            {
                int3 candidate = _pendingFeatureRegions[i];
                long dx = candidate.x - centre.x;
                long dz = candidate.z - centre.z;
                long distance = dx * dx + dz * dz;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }

            int3 coord = _pendingFeatureRegions[best];
            _pendingFeatureRegions.RemoveAt(best);
            return coord;
        }

        private void CompleteFeatureBuild()
        {
            int3 coord = _featureBuild.RegionCoord;
            FeatureGenerationReport report = _featureBuild.Report;
            _featureBuild.Dispose();
            _featureBuild = null;

            FeatureVoxelsBuilt += report.VoxelsWritten;
            FeatureInstancesBuilt += report.InstancesRasterised;

            if (report.BudgetExceeded)
                Debug.LogWarning($"Feature budget exceeded in region {coord}; " +
                                 "content was refused rather than truncated.");

            // The region's pointer grid is only final now. Anything meshed while its features
            // were still arriving described a half-built region.
            _changes.PublishRegion(coord, VoxelChangeKind.All);
            CaptureFarField(coord);
        }

        /// <summary>
        /// Drops queued feature work for a region that is no longer resident. The mutation path
        /// makes a region resident on its first authored voxel, so building into an evicted
        /// coordinate would silently resurrect it outside the residency radius.
        /// </summary>
        private void CancelFeatureWork(int3 coord)
        {
            _pendingFeatureRegions.Remove(coord);
            if (_featureBuild != null && _featureBuild.RegionCoord.Equals(coord))
            {
                _featureBuild.Dispose();
                _featureBuild = null;
            }
        }

        /// <summary>Releases the in-flight generation state without publishing it.</summary>
        private void FinishRegionForced()
        {
            if (_gen.HeightJobScheduled) _gen.HeightJob.Complete();
            if (_gen.Heights.IsCreated) _gen.Heights.Dispose();
            _gen = default;
        }

        /// <summary>Depth below the surface at which stone gives way to indestructible bedrock.</summary>
        private const int DeepDepth = 40;

        private static byte MaterialAt(int y, int surface)
        {
            if (y > surface) return VoxelDimensions.MaterialEmpty;
            if (y == surface) return SurfaceMaterialAt(surface);
            if (y > surface - DeepDepth) return MatStone;
            return MatBedrock;
        }

        /// <summary>
        /// The material of the topmost voxel in a column, given that column's surface height.
        ///
        /// Split out of <see cref="MaterialAt"/> because the far-field clipmap needs the same
        /// answer and has no voxels to read it from. Two implementations of this rule means the
        /// ground changes colour as you cross the streaming radius, which is exactly the drift
        /// the far field shipped with: it had no material channel at all and drew every distant
        /// mountain in one flat grey.
        /// </summary>
        public static byte SurfaceMaterialAt(int surface) =>
            surface < BaseHeight ? MatSand : Mat.Grass;

        /// <summary>
        /// Surface height in voxels, from the engine's canonical sampler.
        ///
        /// This used to be a demo-side copy of the noise, written because
        /// <c>TerrainGenerator.SampleSurfaceHeight</c> reduced its inputs modulo the region edge
        /// and produced identical terrain in every region. That is fixed, so the copy is gone:
        /// the showcase and the feature generator must agree about where the ground is, and two
        /// implementations of the same function are two things that can drift.
        /// </summary>
        public int SurfaceHeight(int wx, int wz) => TerrainSampler.HeightAt(wx, wz, Seed);

        // -- landmarks -----------------------------------------------------------

        /// <summary>
        /// Hand-built structures at the spawn point, so there is something with corners and
        /// distinct materials to blow up before you go looking at terrain.
        /// </summary>
        /// <summary>
        /// The castle: sited, built, furnished, and undermined by its own dungeon.
        ///
        /// Built once when the origin region completes. It sculpts its own outcrop, so it must run
        /// after terrain rather than alongside it.
        /// </summary>
        private void QueueLandmarks()
        {
            if (!_includeCastle) return;
            if (_castleTerrainQueued || _hasCastlePlan) return;
            int cx = LandmarkCentreX;
            int cz = LandmarkCentreZ;
            int ground = SurfaceHeight(cx, cz);

            var plan = StructuresComposition.PlanCastle(new int3(cx, ground, cz), Seed);
            // Every region the castle reaches into must exist *before* it is built. A castle is
            // wider than a region, and terrain generation writes a region's brick pointers
            // wholesale — so a neighbour generated afterwards silently erases the half of the
            // castle that stood in it. That is what left a scatter of blocks and a terraced
            // quarry where a castle should be.
            // Enumerate every coordinate in the touched range. Sampling centre +/- reach and
            // treating those samples as neighbours skipped intermediate coordinates whenever
            // reach exceeded one region; the castle then wrote into a default-empty region and
            // left enormous void wedges where its terrain foundation should have been.
            int reach = math.max(plan.PlateauRadius + plan.CliffDrop + 8, RegionVoxelEdge);
            int minRx = (cx - reach) >> VoxelDimensions.RegionVoxelEdgeLog2;
            int maxRx = (cx + reach) >> VoxelDimensions.RegionVoxelEdgeLog2;
            int minRz = (cz - reach) >> VoxelDimensions.RegionVoxelEdgeLog2;
            int maxRz = (cz + reach) >> VoxelDimensions.RegionVoxelEdgeLog2;

            _pendingCastlePlan = plan;
            _castleRegions.Clear();
            _castleRegionSet.Clear();
            for (int rz = minRz; rz <= maxRz; rz++)
            for (int rx = minRx; rx <= maxRx; rx++)
            {
                int3 region = new int3(rx, 0, rz);
                _castleRegions.Add(region);
                _castleRegionSet.Add(region);
            }
            _castleTerrainQueued = true;
        }

        private bool StepLandmarks()
        {
            if (!_castleTerrainQueued || _hasCastlePlan) return false;
            for (int i = 0; i < _castleRegions.Count; i++)
                if (!_generated.Contains(_castleRegions[i])) return false;

            double stageStart = Time.realtimeSinceStartupAsDouble;
            int stage = _castleBuild != null ? _castleBuild.StageNumber : 1;

            if (_castleBuild == null)
            {
                _readSource.Refresh(in _table, in _pool);
                _mutationStore.Refresh(in _table, in _pool);
                IMaterialAuthoringCatalogue materials = _palette.IsCreated
                    ? (IMaterialAuthoringCatalogue)_palette
                    : null;
                _castleBuild = StructuresComposition.BeginCastleBuild(
                    _readSource, _mutationStore, in _pendingCastlePlan, Seed, materials);
            }
            bool castleComplete;
            using (s_CastleMarker.Auto())
                castleComplete = _castleBuild.Step();
            if (!castleComplete)
            {
                RecordCastleStage(stage, stageStart);
                return true;
            }

            CastlePlan plan = _pendingCastlePlan;
            int cx = plan.Centre.x;
            int cz = plan.Centre.z;
            int referenceArchVoxels = BuildReferenceArch(new int3(cx - 120, 0, cz - 210));
            _castlePlan = plan;
            _hasCastlePlan = true;
            _castleTrapdoorOpen = false;
            _castleFrontGateOpen = false;
            BuildCastlePresentationLights(in plan);

            // The bake path loads this at startup because the plan is already complete there. When
            // the world generates during the scene the plan only exists now, and the spawn that
            // would have loaded it has long since run, so it has to be loaded here instead.
            if (_startupSource == ShowcaseStartupSource.Generate)
                EnsureCastleWorldObjectSceneLoaded();

            CastleVoxels = _castleBuild.TotalVoxelsWritten + referenceArchVoxels;
            // These regions were intentionally kept free of generic features while the castle
            // authored its atomic footprint. Do not replay them over the completed landmark;
            // castle-owned dressing and semantic vegetation are generated by the castle plan.
            _deferredFeatureRegions.Clear();

            // Everything the castle touched has to be re-meshed and re-uploaded, and re-captured
            // for the far field.
            //
            // FinishRegion captures each region as it generates, which for a castle region is
            // strictly too early: at that point the region is plain terrain, the store correctly
            // decides there is no built content worth keeping, and the castle that arrives
            // afterwards is never recorded. The silhouette then vanishes at the streaming radius
            // — the exact failure the store was added to prevent.
            for (int i = 0; i < _castleRegions.Count; i++)
            {
                _changes.PublishRegion(_castleRegions[i], VoxelChangeKind.All);
                FarField.CaptureRegion(_castleRegions[i], ReadStorage, Seed);
            }
            RecordCastleStage(stage, stageStart);
            return true;
        }

        private void RecordCastleStage(int stage, double startSeconds)
        {
            LastCastleStage = stage;
            LastCastleStageMs = (Time.realtimeSinceStartupAsDouble - startSeconds) * 1000.0;
            if (LastCastleStageMs > MaxCastleStageMs)
            {
                MaxCastleStage = stage;
                MaxCastleStageMs = LastCastleStageMs;
            }
        }

        private int BuildReferenceArch(int3 horizontalOrigin)
        {
            int ground = SurfaceHeight(horizontalOrigin.x, horizontalOrigin.z);
            int3 origin = new(horizontalOrigin.x, ground + 1, horizontalOrigin.z);
            _readSource.Refresh(in _table, in _pool);
            _mutationStore.Refresh(in _table, in _pool);
            ReferenceArchBuildResult result = StructuresComposition.BuildReferenceArch(
                _readSource, _mutationStore, _palette, _surfaceCatalogue, _coatingCatalogue,
                _profileBlocks, origin, Mat.DarkStone, SurfaceStyles.Rounded,
                SurfaceStyles.MasonryJoint, Coatings.Moss);
            ReferenceArchMin = result.Min;
            ReferenceArchMax = result.Max;
            return result.VoxelsWritten;
        }

        /// <summary>Voxels the castle wrote. Reported in the HUD so its cost is visible.</summary>
        public long CastleVoxels { get; private set; }
        public int3 ReferenceArchMin { get; private set; }
        public int3 ReferenceArchMax { get; private set; }
        public IProfileBlockReadSource ProfileBlocks => _profileBlocks;

        public Vector4[] CastlePresentationLights { get; private set; } = Array.Empty<Vector4>();
        public Vector4[] CastlePresentationLightColours { get; private set; } = Array.Empty<Vector4>();

        private void BuildCastlePresentationLights(in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int keepMinZ = plan.Centre.z - plan.KeepHalfZ + 60;
            int keepCentreZ = keepMinZ + plan.KeepHalfZ;
            int keepMaxX = plan.Centre.x + plan.KeepHalfX;
            int wingWidth = math.max(96, plan.KeepHalfX * 4 / 5);
            int wingDepth = math.max(80, plan.KeepHalfZ * 2 - 72);
            int wingCentreX = keepMaxX - 4 + wingWidth / 2;
            int wingCentreZ = keepMinZ + 24 + wingDepth / 2;
            int chapelWidth = math.max(78, plan.KeepHalfX * 2 / 3);
            int chapelDepth = math.max(96, plan.KeepHalfZ * 6 / 5);
            int chapelCentreX = plan.Centre.x - plan.KeepHalfX - chapelWidth / 2 + 4;
            int chapelCentreZ = keepMinZ + plan.KeepHalfZ * 2 - chapelDepth / 2 - 38;
            int cellarY = baseY - 46;
            int dungeonY = cellarY - 120;
            int trapZ = keepMinZ + plan.KeepHalfZ + 40;
            int caveZ = trapZ - 411;
            int3 bellTower = CastleLayout.ChapelBellTowerCentre(in plan);

            static Vector4 LightAt(int x, int y, int z, float radiusMetres) =>
                new(x * 0.1f, y * 0.1f, z * 0.1f, radiusMetres);

            CastlePresentationLights = new[]
            {
                LightAt(plan.Centre.x - 45, baseY + 26, keepCentreZ - 28, 8.0f),
                LightAt(plan.Centre.x + 42, baseY + 26, keepCentreZ + 30, 8.0f),
                LightAt(plan.Centre.x, baseY + plan.FloorHeight + 17, keepCentreZ, 8.0f),
                LightAt(plan.Centre.x, baseY + plan.FloorHeight * 3 + 17, keepCentreZ, 7.0f),
                LightAt(wingCentreX, baseY + 17, wingCentreZ, 7.5f),
                LightAt(wingCentreX, baseY + plan.FloorHeight + 17, wingCentreZ, 7.0f),
                LightAt(chapelCentreX - 18, baseY + 24, chapelCentreZ, 7.5f),
                LightAt(chapelCentreX + 22, baseY + 27, chapelCentreZ, 7.5f),
                LightAt(plan.Centre.x - 55, cellarY + 17, keepCentreZ, 7.0f),
                LightAt(plan.Centre.x + 58, cellarY + 17, keepCentreZ, 7.0f),
                LightAt(plan.Centre.x - 55, dungeonY + 18, trapZ, 8.5f),
                LightAt(plan.Centre.x + 55, dungeonY + 18, trapZ, 8.5f),
                LightAt(plan.Centre.x + 226, dungeonY + 16, trapZ, 8.0f),
                LightAt(plan.Centre.x - 226, dungeonY + 15, trapZ, 8.0f),
                LightAt(plan.Centre.x - 40, dungeonY + 9, caveZ - 15, 11.5f),
                LightAt(plan.Centre.x + 45, dungeonY + 11, caveZ + 24, 11.5f),
                LightAt(plan.Centre.x + 145, dungeonY + 12, caveZ + 25, 10.5f),
                LightAt(plan.Centre.x - 52, baseY + plan.FloorHeight + 16,
                        keepCentreZ + 27, 6.5f),
                LightAt(plan.Centre.x, baseY + plan.FloorHeight * 3 + 17,
                        keepCentreZ - 42, 6.0f),
                LightAt(plan.Centre.x, baseY + plan.FloorHeight * 3 + 17,
                        keepCentreZ + 42, 6.0f),
                LightAt(bellTower.x, baseY + 17, bellTower.z, 5.5f),
                LightAt(bellTower.x, baseY + plan.FloorHeight * 2 + 17,
                        bellTower.z, 5.5f),
                LightAt(bellTower.x, baseY + plan.FloorHeight * 3 + 17,
                        bellTower.z, 5.0f),
            };

            var hallWarm = new Vector4(1.00f, 0.38f, 0.10f, 1.85f);
            var upperWarm = new Vector4(1.00f, 0.40f, 0.13f, 1.05f);
            var chapelWarm = new Vector4(1.00f, 0.42f, 0.14f, 1.15f);
            var cellarWarm = new Vector4(1.00f, 0.28f, 0.06f, 2.05f);
            var sideRoomWarm = new Vector4(1.00f, 0.34f, 0.09f, 1.05f);
            var caveWarm = new Vector4(1.00f, 0.27f, 0.06f, 2.35f);
            var caveBlue = new Vector4(0.10f, 0.58f, 1.00f, 2.05f);
            CastlePresentationLightColours = new[]
            {
                hallWarm, hallWarm, upperWarm, upperWarm, hallWarm, upperWarm,
                chapelWarm, chapelWarm,
                cellarWarm, cellarWarm, cellarWarm, cellarWarm, sideRoomWarm, sideRoomWarm,
                caveWarm, caveWarm, caveBlue,
                upperWarm, upperWarm, upperWarm,
                chapelWarm, upperWarm, upperWarm,
            };
        }

        public bool CastleTrapdoorOpen => _castleTrapdoorOpen;
        public bool CastleFrontGateOpen => _castleFrontGateOpen;

        public Vector3 CastleFrontGatePosition
        {
            get
            {
                if (!_hasCastlePlan) return Vector3.positiveInfinity;
                int3 min = CastleLayout.FrontGateMinimum(in _castlePlan);
                return new Vector3(min.x + CastleLayout.FrontGateWidth * 0.5f,
                                   min.y,
                                   min.z - 8f) * VoxelSize;
            }
        }

        public bool CanOpenCastleFrontGate(Vector3 playerFeetMetres)
        {
            if (!_hasCastlePlan || _castleFrontGateOpen) return false;
            Vector3 delta = playerFeetMetres - CastleFrontGatePosition;
            return new Vector2(delta.x, delta.z).sqrMagnitude <= 4.2f * 4.2f
                && math.abs(delta.y) <= 3.0f;
        }

        public bool TryOpenCastleFrontGate(Vector3 playerFeetMetres)
        {
            if (!CanOpenCastleFrontGate(playerFeetMetres)) return false;

            int3 min = CastleLayout.FrontGateMinimum(in _castlePlan);
            int half = CastleLayout.FrontGateWidth / 2;
            int archTop = CastleLayout.FrontGateHeight - half;
            var gateVoxels = new List<FallingVoxel>(CastleLayout.FrontGateWidth
                                                    * CastleLayout.FrontGateHeight
                                                    * CastleLayout.FrontGateDepth);
            for (int d = 0; d < CastleLayout.FrontGateDepth; d++)
            for (int w = 0; w < CastleLayout.FrontGateWidth; w++)
            for (int h = 0; h < CastleLayout.FrontGateHeight; h++)
            {
                int dx = w - half;
                if (h > archTop && dx * dx + (h - archTop) * (h - archTop) > half * half)
                    continue;

                var voxel = new int3(min.x + w, min.y + h, min.z + d);
                gateVoxels.Add(new FallingVoxel { Position = voxel, Material = MatWood });
            }
            ClearVoxelsBulk(gateVoxels);

            _castleFrontGateOpen = true;
            return true;
        }

        public Vector3 CastleTrapdoorPosition
        {
            get
            {
                if (!_hasCastlePlan) return Vector3.positiveInfinity;
                int3 centre = CastleLayout.TrapdoorCentre(in _castlePlan);
                return ((Vector3)(float3)centre + new Vector3(0.5f, 0.2f, 0.5f)) * 0.1f;
            }
        }

        /// <summary>True while the player is close enough to operate the closed cellar hatch.</summary>
        public bool CanOpenCastleTrapdoor(Vector3 playerFeetMetres)
        {
            if (!_hasCastlePlan || _castleTrapdoorOpen) return false;
            Vector3 delta = playerFeetMetres - CastleTrapdoorPosition;
            return new Vector2(delta.x, delta.z).sqrMagnitude <= 3.2f * 3.2f
                && math.abs(delta.y) <= 2.5f;
        }

        /// <summary>
        /// Opens the secret hatch without invoking destruction physics. This is authored moving
        /// architecture, so only the known lid bounds change and the stair beneath remains intact.
        /// </summary>
        public bool TryOpenCastleTrapdoor(Vector3 playerFeetMetres)
        {
            if (!CanOpenCastleTrapdoor(playerFeetMetres)) return false;

            int3 centre = CastleLayout.TrapdoorCentre(in _castlePlan);
            int half = CastleLayout.TrapdoorHalfSize;
            for (int y = centre.y; y < centre.y + 4; y++)
            for (int z = centre.z - half; z < centre.z + half; z++)
            for (int x = centre.x - half; x < centre.x + half; x++)
            {
                var voxel = new int3(x, y, z);
                if (SetMaterialApi(voxel, VoxelGrid.MaterialEmpty))
                    MarkDirty(voxel);
            }
            _castleTrapdoorOpen = true;
            return true;
        }

        // -- edits ---------------------------------------------------------------

        /// <summary>
        /// Carves a spherical blast.
        ///
        /// The palette decides the outcome per voxel: indestructible materials survive, and
        /// harder materials survive proportionally more of the blast's outer shell, so one
        /// explosion leaves a clean crater in sand and a ragged one in stone. Selection is a
        /// seeded integer draw, so the same blast resolves identically on every client.
        /// </summary>
        public int Explode(int3 centre, ushort radius, float3 impulseDirection = default)
        {
            var rng = new DeterministicRandom(MixSeed(centre, radius));
            var voxels = BuildBrushes.PlaceSphere(centre, radius, VoxelGrid.MaterialEmpty, Seed);
            var removed = new List<FallingVoxel>(math.min(voxels.Length, 8192));

            int radiusSq = radius * radius;
            int changed = 0;

            for (int i = 0; i < voxels.Length; i++)
            {
                var v = voxels[i];
                if (!TryReadCellApi(v, out VoxelCell cell)) continue;
                byte existing = cell.BaseMaterialId;
                if (existing == VoxelGrid.MaterialEmpty) continue;
                if (!_materialSimulation.IsDestructible(existing)) continue;

                var d = v - centre;
                int distSq = d.x * d.x + d.y * d.y + d.z * d.z;
                int rimFactor = radiusSq == 0 ? 0 : (distSq * 255) / radiusSq;
                int resistance = (_materialSimulation.GetHardness(existing) * rimFactor) / 255;

                if ((int)(rng.NextUint() & 0xFF) < resistance) continue;

                removed.Add(new FallingVoxel
                {
                    Position = v,
                    Material = existing,
                    Coating = cell.Surface.CoatingId,
                });
            }

            voxels.Dispose();
            changed = ClearVoxelsBulk(removed);
            int collapsed = ResolveUnsupportedAfterRemoval(removed, centre, radius, impulseDirection);
            _editCounter++;
            LastEditVoxels = changed + collapsed;
            return changed + collapsed;
        }

        /// <summary>
        /// Removes one exact voxel and runs the same support/collapse pass as an impact. Exposed
        /// for deterministic physics tests and for future non-explosive cutting tools.
        /// </summary>
        public int RemoveAndResolveCollapse(int3 voxel)
        {
            if (!TryReadCellApi(voxel, out VoxelCell cell)) return 0;
            byte material = cell.BaseMaterialId;
            if (material == VoxelGrid.MaterialEmpty || !_materialSimulation.IsDestructible(material))
                return 0;
            var removed = new List<FallingVoxel>(1)
            {
                new()
                {
                    Position = voxel,
                    Material = material,
                    Coating = cell.Surface.CoatingId,
                },
            };
            if (ClearVoxelsBulk(removed) == 0) return 0;
            int collapsed = ResolveUnsupportedAfterRemoval(removed, voxel, 1, default);
            LastEditVoxels = 1 + collapsed;
            return 1 + collapsed;
        }

        public bool TryDequeueDetachedChunk(out DetachedVoxelChunk chunk) =>
            _detachedChunks.TryDequeue(out chunk);

        /// <summary>Returns the centre height at which a detached chunk should meet solid world.</summary>
        public float FindLandingCentreY(float3 pivotMetres, float halfHeightMetres)
        {
            int x = (int)math.floor(pivotMetres.x / VoxelSize);
            int z = (int)math.floor(pivotMetres.z / VoxelSize);
            int startY = (int)math.floor((pivotMetres.y - halfHeightMetres)
                                        / VoxelSize) - 1;
            for (int y = math.min(startY, RegionVoxelEdge - 1); y >= 0; y--)
                if (IsSolidApi(new int3(x, y, z)))
                    return (y + 1) * VoxelSize + halfHeightMetres;
            return halfHeightMetres;
        }

        /// <summary>
        /// Clears a destruction batch without running the mixed-to-uniform 512-byte scan after
        /// every voxel. Each touched brick is normalized once at the end. Large tower failures
        /// used to spend almost all of their frame repeating that same scan thousands of times.
        /// </summary>
        private int ClearVoxelsBulk(List<FallingVoxel> voxels)
        {
            if (voxels.Count == 0) return 0;

            var touchedBricks = new HashSet<int3>();
            var touchedRegions = new HashSet<int3>();
            int cleared = 0;

            for (int i = 0; i < voxels.Count; i++)
            {
                int3 position = voxels[i].Position;
                VoxelAccess.Decompose(position, out int3 regionCoord,
                                      out int3 brickInRegion, out int3 voxelInBrick);
                if (!_table.TryGetRegion(regionCoord, out var region)) continue;

                int brickIndex = Region.BrickIndex(brickInRegion.x, brickInRegion.y,
                                                   brickInRegion.z);
                BrickRef brick = region.BrickRefs[brickIndex];
                if (brick.IsUniform)
                {
                    if (brick.UniformMaterial == VoxelDimensions.MaterialEmpty) continue;
                    int poolIndex = _pool.Allocate();
                    _pool.FillBrick(poolIndex, brick.UniformMaterial);
                    brick = BrickRef.FromPoolIndex(poolIndex);
                    region.BrickRefs[brickIndex] = brick;
                }

                int voxelIndex = VoxelEngine.Storage.Runtime.Occupancy.OccupancyMask.VoxelIndex(
                    voxelInBrick.x, voxelInBrick.y, voxelInBrick.z);
                if (_pool.GetVoxel(brick.PoolIndex, voxelIndex)
                    == VoxelDimensions.MaterialEmpty) continue;

                int writableIndex = _pool.EnsureWritable(brick.PoolIndex);
                if (writableIndex != brick.PoolIndex)
                {
                    brick = BrickRef.FromPoolIndex(writableIndex);
                    region.BrickRefs[brickIndex] = brick;
                }
                _pool.SetVoxel(brick.PoolIndex, voxelIndex, VoxelDimensions.MaterialEmpty);
                touchedBricks.Add(position >> VoxelDimensions.BrickEdgeLog2);
                touchedRegions.Add(regionCoord);
                cleared++;
            }

            foreach (int3 worldBrick in touchedBricks)
            {
                int3 regionCoord = worldBrick >> VoxelDimensions.RegionEdgeLog2;
                int3 localBrick = worldBrick & VoxelDimensions.RegionEdgeMask;
                if (!_table.TryGetRegion(regionCoord, out var region)) continue;
                int brickIndex = Region.BrickIndex(localBrick.x, localBrick.y, localBrick.z);
                BrickRef brick = region.BrickRefs[brickIndex];
                if (brick.IsMixed
                    && _pool.TryGetUniformMaterial(brick.PoolIndex, out byte uniform))
                {
                    _pool.Free(brick.PoolIndex);
                    region.BrickRefs[brickIndex] = BrickRef.Uniform(uniform);
                }

                MarkDirtyBrick(worldBrick);
            }

            foreach (int3 regionCoord in touchedRegions)
            {
                if (!_table.TryGetRegion(regionCoord, out var region)) continue;
                region.Dirty = true;
                _table.CommitRegion(region);
            }

            return cleared;
        }

        private void MarkDirtyBrick(int3 worldBrick)
        {
            int3 min = worldBrick << VoxelDimensions.BrickEdgeLog2;
            int3 regionCoord = worldBrick >> VoxelDimensions.RegionEdgeLog2;
            _changes.Publish(regionCoord, min, min + VoxelDimensions.BrickEdge,
                VoxelChangeKind.Occupancy | VoxelChangeKind.BaseMaterial
                | VoxelChangeKind.SurfaceStyle | VoxelChangeKind.Coating);
        }

        private int ResolveUnsupportedAfterRemoval(List<FallingVoxel> removed, int3 impact, int radius,
                                                    float3 impulseDirection)
        {
            if (removed.Count == 0) return 0;

            // Deduplicate the solid boundary once. The previous removed×6 nested scan repeatedly
            // revisited empty neighbours and was the dominant cost when several tornadoes hit.
            var candidates = new HashSet<int3>();
            for (int r = 0; r < removed.Count; r++)
            for (int d = 0; d < s_Neighbours.Length; d++)
            {
                int3 candidate = removed[r].Position + s_Neighbours[d];
                if (IsSolidApi(candidate)) candidates.Add(candidate);
            }

            int collapsed = ResolveDisconnectedCandidates(candidates, impact, impulseDirection);

            // Load failure is brick-granular for speed and can cut through a larger connected
            // structure at the edge of its bounded analysis volume. Recheck that exact new edge
            // afterwards so clipped roofs, beams, banners, and ornaments cannot remain floating.
            var overloadBoundary = new HashSet<int3>();
            collapsed += ResolveOverloadedSupport(impact, radius, impulseDirection,
                                                  overloadBoundary);
            collapsed += ResolveDisconnectedCandidates(overloadBoundary, impact,
                                                       impulseDirection);
            return collapsed;
        }

        private int ResolveDisconnectedCandidates(HashSet<int3> candidates, int3 impact,
                                                  float3 impulseDirection)
        {
            if (candidates.Count == 0) return 0;

            var classified = new HashSet<int3>();
            var knownSupported = new HashSet<int3>();
            int collapsed = 0;

            foreach (int3 seed in candidates)
            {
                if (classified.Contains(seed)) continue;

                var component = new List<FallingVoxel>(512);
                var stack = new Stack<int3>();
                var localSeen = new HashSet<int3>();
                stack.Push(seed);
                localSeen.Add(seed);
                bool anchored = false;
                bool overflow = false;

                while (stack.Count > 0)
                {
                    int3 current = stack.Pop();
                    if (knownSupported.Contains(current)) { anchored = true; break; }
                    byte material = ReadMaterialApi(current);
                    if (material == VoxelDimensions.MaterialEmpty) continue;

                    if (!_materialSimulation.IsDestructible(material) || current.y <= 0)
                    {
                        anchored = true;
                        break;
                    }

                    if (!TryReadCellApi(current, out VoxelCell cell)) continue;
                    component.Add(new FallingVoxel
                    {
                        Position = current,
                        Material = material,
                        Coating = cell.Surface.CoatingId,
                    });
                    if (component.Count >= MaxCollapseComponentVoxels)
                    {
                        overflow = true;
                        break;
                    }

                    // Push downward last so the LIFO traversal follows columns toward ground
                    // before spreading sideways across an entire terrain surface.
                    for (int n = 0; n < s_Neighbours.Length; n++)
                    {
                        int3 next = current + s_Neighbours[n];
                        if (localSeen.Contains(next)
                            || !IsSolidApi(next)) continue;
                        localSeen.Add(next);
                        stack.Push(next);
                    }
                }

                if (anchored || overflow)
                {
                    foreach (int3 voxel in localSeen)
                    {
                        classified.Add(voxel);
                        knownSupported.Add(voxel);
                    }
                    continue;
                }

                collapsed += DetachComponent(component, impact, impulseDirection);
            }
            return collapsed;
        }

        /// <summary>
        /// Connectivity is necessary but not sufficient: a surviving one-voxel neck cannot carry
        /// an entire tower. Find the weakest vertical contact plane through the damaged band,
        /// then compare the mass above it with material-weighted contact capacity.
        /// </summary>
        private int ResolveOverloadedSupport(int3 impact, int radius, float3 impulseDirection,
                                             HashSet<int3> detachedBoundary)
        {
            int influenceRadius = math.clamp(radius * 2 + 56, 64, 96);
            int scanRadius = influenceRadius;
            int weakestPlane = impact.y;
            // A sphere's widest cut—and therefore its weakest remaining contact—is its centre
            // plane. Scanning every layer (and later three representative layers) only repeated
            // tens of thousands of sparse lookups before debris could start moving.
            if (CountVerticalContacts(impact.x, impact.z, weakestPlane, scanRadius) == 0) return 0;

            int collapsed = 0;
            int seedY = weakestPlane + 1;
            int minBrickX = (impact.x - influenceRadius) >> VoxelDimensions.BrickEdgeLog2;
            int maxBrickX = (impact.x + influenceRadius) >> VoxelDimensions.BrickEdgeLog2;
            int minBrickZ = (impact.z - influenceRadius) >> VoxelDimensions.BrickEdgeLog2;
            int maxBrickZ = (impact.z + influenceRadius) >> VoxelDimensions.BrickEdgeLog2;
            int minBrickY = seedY >> VoxelDimensions.BrickEdgeLog2;
            // Castle towers are under 26 m tall. Do not probe empty sky to the top of the
            // 51.2 m region after every impact.
            int maxBrickY = math.min(RegionVoxelEdge - 1, seedY + 256)
                          >> VoxelDimensions.BrickEdgeLog2;
            int influenceRadiusSq = influenceRadius * influenceRadius;
            var candidates = new Dictionary<int3, BrickCollapseInfo>();

            // Structural overload is intentionally brick-granular. A castle-scale tower can
            // contain hundreds of thousands of voxels; walking a HashSet node for every one was
            // the remaining impact hitch. At 8^3 voxels per brick this bounds the same search to
            // a few thousand compact records, while voxel collision remains exact until failure.
            for (int bz = minBrickZ; bz <= maxBrickZ; bz++)
            for (int by = minBrickY; by <= maxBrickY; by++)
            for (int bx = minBrickX; bx <= maxBrickX; bx++)
            {
                int centreX = (bx << VoxelDimensions.BrickEdgeLog2) + 4 - impact.x;
                int centreZ = (bz << VoxelDimensions.BrickEdgeLog2) + 4 - impact.z;
                if (centreX * centreX + centreZ * centreZ
                    > influenceRadiusSq + 64) continue;
                int3 worldBrick = new int3(bx, by, bz);
                BrickCollapseInfo info = ReadBrickCollapseInfo(worldBrick, seedY);
                if (info.OccupiedCount > 0) candidates.Add(worldBrick, info);
            }

            var visited = new HashSet<int3>();
            foreach (var pair in candidates)
            {
                int3 seed = pair.Key;
                if (seed.y != minBrickY || visited.Contains(seed))
                    continue;

                var component = new List<int3>(128);
                var stack = new Stack<int3>();
                stack.Push(seed);
                visited.Add(seed);
                long supportCapacity = 0;
                bool containsStructuralMaterial = false;
                int occupiedCount = 0;

                while (stack.Count > 0)
                {
                    int3 current = stack.Pop();
                    BrickCollapseInfo info = candidates[current];
                    containsStructuralMaterial |= info.HasStructuralMarker;
                    occupiedCount += info.OccupiedCount;
                    component.Add(current);

                    if (current.y == minBrickY)
                    {
                        int brickOriginX = current.x << VoxelDimensions.BrickEdgeLog2;
                        int brickOriginZ = current.z << VoxelDimensions.BrickEdgeLog2;
                        for (int lz = 0; lz < VoxelDimensions.BrickEdge; lz++)
                        for (int lx = 0; lx < VoxelDimensions.BrickEdge; lx++)
                        {
                            int3 upper = new int3(brickOriginX + lx, seedY,
                                                  brickOriginZ + lz);
                            if (!IsSolidApi(upper)) continue;
                            byte below = ReadMaterialApi(upper + new int3(0, -1, 0));
                            if (below != VoxelDimensions.MaterialEmpty)
                            {
                                // One intact voxel column carries dozens of voxels above it, not
                                // merely one short course. The former 6..12 capacity made even a
                                // fully supported tower fail after any nearby impact. Material
                                // hardness still matters, while a one-voxel thread remains far
                                // too weak for a castle-scale mass.
                                supportCapacity += 48 + _materialSimulation.GetHardness(below);
                            }
                        }
                    }

                    for (int n = 0; n < s_Neighbours.Length; n++)
                    {
                        int3 next = current + s_Neighbours[n];
                        if (visited.Contains(next) || !candidates.ContainsKey(next)) continue;
                        visited.Add(next);
                        stack.Push(next);
                    }
                }

                if (containsStructuralMaterial && occupiedCount > supportCapacity)
                    collapsed += DetachBrickComponent(component, seedY, impact, impulseDirection,
                                                      detachedBoundary);
            }

            return collapsed;
        }

        private BrickCollapseInfo ReadBrickCollapseInfo(int3 worldBrick, int minimumVoxelY)
        {
            int3 regionCoord = worldBrick >> VoxelDimensions.RegionEdgeLog2;
            int3 localBrick = worldBrick & VoxelDimensions.RegionEdgeMask;
            if (!_table.TryGetRegion(regionCoord, out var region)) return default;
            BrickRef brick = region.GetBrick(localBrick.x, localBrick.y, localBrick.z);
            int firstY = math.max(0, minimumVoxelY
                                  - (worldBrick.y << VoxelDimensions.BrickEdgeLog2));
            if (firstY >= VoxelDimensions.BrickEdge || brick.IsEmpty) return default;

            if (brick.IsUniform)
            {
                byte material = brick.UniformMaterial;
                if (!_materialSimulation.IsDestructible(material)) return default;
                return new BrickCollapseInfo
                {
                    OccupiedCount = (VoxelDimensions.BrickEdge - firstY)
                                    * VoxelDimensions.BrickEdge * VoxelDimensions.BrickEdge,
                    HasStructuralMarker = IsStructuralMaterial(material),
                };
            }

            int count = 0;
            bool marker = false;
            for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
            for (int y = firstY; y < VoxelDimensions.BrickEdge; y++)
            for (int x = 0; x < VoxelDimensions.BrickEdge; x++)
            {
                int index = VoxelEngine.Storage.Runtime.Occupancy.OccupancyMask.VoxelIndex(x, y, z);
                byte material = _pool.GetVoxel(brick.PoolIndex, index);
                if (material == VoxelDimensions.MaterialEmpty
                    || !_materialSimulation.IsDestructible(material)) continue;
                count++;
                marker |= IsStructuralMaterial(material);
            }
            return new BrickCollapseInfo
            {
                OccupiedCount = count,
                HasStructuralMarker = marker,
            };
        }

        private int DetachBrickComponent(List<int3> component, int minimumVoxelY, int3 impact,
                                         float3 impulseDirection,
                                         HashSet<int3> detachedBoundary)
        {
            var buckets = new List<VisualBucket>(component.Count);
            var touchedRegions = new HashSet<int3>();
            int detached = 0;

            for (int b = 0; b < component.Count; b++)
            {
                int3 worldBrick = component[b];
                int3 regionCoord = worldBrick >> VoxelDimensions.RegionEdgeLog2;
                int3 localBrick = worldBrick & VoxelDimensions.RegionEdgeMask;
                if (!_table.TryGetRegion(regionCoord, out var region)) continue;
                int brickIndex = Region.BrickIndex(localBrick.x, localBrick.y, localBrick.z);
                BrickRef brick = region.BrickRefs[brickIndex];
                if (brick.IsEmpty) continue;

                int firstY = math.max(0, minimumVoxelY
                                      - (worldBrick.y << VoxelDimensions.BrickEdgeLog2));
                var visual = new VisualBucket { Priority = VisualHash(worldBrick) };

                if (brick.IsUniform && firstY == 0 && _materialSimulation.IsDestructible(brick.UniformMaterial))
                {
                    byte material = brick.UniformMaterial;
                    for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
                    for (int y = 0; y < VoxelDimensions.BrickEdge; y++)
                    for (int x = 0; x < VoxelDimensions.BrickEdge; x++)
                    {
                        int3 position = (worldBrick << VoxelDimensions.BrickEdgeLog2)
                                      + new int3(x, y, z);
                        AddVisualSample(visual,
                            new FallingVoxel { Position = position, Material = material });
                    }
                    region.BrickRefs[brickIndex] = BrickRef.Empty;
                    detached += visual.SourceVoxelCount;
                }
                else
                {
                    if (brick.IsUniform)
                    {
                        if (!_materialSimulation.IsDestructible(brick.UniformMaterial)) continue;
                        int poolIndex = _pool.Allocate();
                        _pool.FillBrick(poolIndex, brick.UniformMaterial);
                        brick = BrickRef.FromPoolIndex(poolIndex);
                        region.BrickRefs[brickIndex] = brick;
                    }

                    int writableIndex = _pool.EnsureWritable(brick.PoolIndex);
                    if (writableIndex != brick.PoolIndex)
                    {
                        brick = BrickRef.FromPoolIndex(writableIndex);
                        region.BrickRefs[brickIndex] = brick;
                    }

                    for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
                    for (int y = firstY; y < VoxelDimensions.BrickEdge; y++)
                    for (int x = 0; x < VoxelDimensions.BrickEdge; x++)
                    {
                        int index = VoxelEngine.Storage.Runtime.Occupancy.OccupancyMask.VoxelIndex(x, y, z);
                        byte material = _pool.GetVoxel(brick.PoolIndex, index);
                        if (material == VoxelDimensions.MaterialEmpty
                            || !_materialSimulation.IsDestructible(material)) continue;
                        int3 position = (worldBrick << VoxelDimensions.BrickEdgeLog2)
                                      + new int3(x, y, z);
                        byte coating = _pool.GetSurface(brick.PoolIndex, index).CoatingId;
                        AddVisualSample(visual,
                            new FallingVoxel
                            {
                                Position = position,
                                Material = material,
                                Coating = coating,
                            });
                        _pool.SetVoxel(brick.PoolIndex, index, VoxelDimensions.MaterialEmpty);
                    }

                    if (_pool.TryGetUniformMaterial(brick.PoolIndex, out byte uniform))
                    {
                        _pool.Free(brick.PoolIndex);
                        region.BrickRefs[brickIndex] = BrickRef.Uniform(uniform);
                    }
                    detached += visual.SourceVoxelCount;
                }

                if (visual.SourceVoxelCount == 0) continue;
                buckets.Add(visual);
                touchedRegions.Add(regionCoord);
                MarkDirtyBrick(worldBrick);
            }

            foreach (int3 regionCoord in touchedRegions)
            {
                if (!_table.TryGetRegion(regionCoord, out var region)) continue;
                region.Dirty = true;
                _table.CommitRegion(region);
            }

            CollectSolidBrickBoundary(component, minimumVoxelY, detachedBoundary);

            buckets.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            int visualCount = math.min(MaxVisualChunksPerCollapse,
                                       MaxQueuedDetachedChunks - _detachedChunks.Count);
            visualCount = math.min(visualCount, buckets.Count);
            int represented = 0;
            for (int b = 0; b < visualCount; b++) represented += buckets[b].SourceVoxelCount;
            float coverageScale = represented > 0 ? detached / (float)represented : 1f;
            for (int b = 0; b < visualCount; b++)
            {
                VisualBucket bucket = buckets[b];
                var chunk = new DetachedVoxelChunk
                {
                    Voxels = new int3[bucket.Samples.Count],
                    Materials = new byte[bucket.Samples.Count],
                    Coatings = new byte[bucket.Samples.Count],
                    SourceVoxelCount = math.max(bucket.SourceVoxelCount,
                                                (int)math.ceil(bucket.SourceVoxelCount
                                                               * coverageScale)),
                    ImpactMetres = ((float3)impact + 0.5f) * VoxelSize,
                    ImpulseDirection = impulseDirection,
                };
                for (int i = 0; i < bucket.Samples.Count; i++)
                {
                    chunk.Voxels[i] = bucket.Samples[i].Position;
                    chunk.Materials[i] = bucket.Samples[i].Material;
                    chunk.Coatings[i] = bucket.Samples[i].Coating;
                }
                _detachedChunks.Enqueue(chunk);
            }

            return detached;
        }

        /// <summary>
        /// Collects only the surviving voxel faces immediately adjacent to a brick-granular
        /// failure. Scanning perimeter faces instead of every detached voxel keeps the cascade
        /// proportional to the exposed fracture surface.
        /// </summary>
        private void CollectSolidBrickBoundary(List<int3> component, int minimumVoxelY,
                                               HashSet<int3> boundary)
        {
            var clearedBricks = new HashSet<int3>(component);
            int minimumBrickY = minimumVoxelY >> VoxelDimensions.BrickEdgeLog2;

            for (int i = 0; i < component.Count; i++)
            {
                int3 brick = component[i];
                for (int d = 0; d < s_Neighbours.Length; d++)
                {
                    int3 neighbour = brick + s_Neighbours[d];
                    if (clearedBricks.Contains(neighbour)) continue;
                    AddSolidFaceSeeds(neighbour, s_Neighbours[d], minimumVoxelY, boundary);
                }

                // The lowest brick is only cleared above the failure plane. Include the exact
                // surviving face inside that same mixed brick as a possible support/remnant.
                if (brick.y == minimumBrickY && minimumVoxelY > 0)
                {
                    int originX = brick.x << VoxelDimensions.BrickEdgeLog2;
                    int originZ = brick.z << VoxelDimensions.BrickEdgeLog2;
                    for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
                    for (int x = 0; x < VoxelDimensions.BrickEdge; x++)
                    {
                        int3 voxel = new int3(originX + x, minimumVoxelY - 1, originZ + z);
                        if (IsSolidApi(voxel)) boundary.Add(voxel);
                    }
                }
            }
        }

        private void AddSolidFaceSeeds(int3 worldBrick, int3 directionFromCleared,
                                       int minimumVoxelY, HashSet<int3> boundary)
        {
            int3 origin = worldBrick << VoxelDimensions.BrickEdgeLog2;
            int edge = VoxelDimensions.BrickEdge - 1;

            for (int v = 0; v < VoxelDimensions.BrickEdge; v++)
            for (int u = 0; u < VoxelDimensions.BrickEdge; u++)
            {
                int3 local;
                if (directionFromCleared.x != 0)
                    local = new int3(directionFromCleared.x > 0 ? 0 : edge, v, u);
                else if (directionFromCleared.y != 0)
                    local = new int3(u, directionFromCleared.y > 0 ? 0 : edge, v);
                else
                    local = new int3(u, v, directionFromCleared.z > 0 ? 0 : edge);

                int3 voxel = origin + local;
                if (voxel.y < minimumVoxelY
                    || !IsSolidApi(voxel)) continue;
                boundary.Add(voxel);
            }
        }

        private static void AddVisualSample(VisualBucket bucket, FallingVoxel voxel)
        {
            bucket.SourceVoxelCount++;
            int capacity = RenderInstancesPerDetachedChunk;
            if (bucket.Samples.Count < capacity)
            {
                bucket.Samples.Add(voxel);
                return;
            }

            uint sampleHash = VisualHash(voxel.Position)
                            ^ (uint)bucket.SourceVoxelCount * 0x9E3779B9u;
            uint selected = sampleHash % (uint)bucket.SourceVoxelCount;
            if (selected < capacity) bucket.Samples[(int)selected] = voxel;
        }

        private int CountVerticalContacts(int centreX, int centreZ, int planeY, int radius)
        {
            int count = 0;
            int radiusSq = radius * radius;
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dz * dz > radiusSq) continue;
                int3 lower = new int3(centreX + dx, planeY, centreZ + dz);
                if (IsSolidApi(lower)
                    && IsSolidApi(lower + new int3(0, 1, 0)))
                    count++;
            }
            return count;
        }

        private static bool IsStructuralMaterial(byte material) => material switch
        {
            MatWood or MatGlass or 6 or 7 or 8 or 9 or 12 or 15 => true,
            _ => false,
        };

        private int DetachComponent(List<FallingVoxel> component, int3 impact,
                                    float3 impulseDirection)
        {
            int detachedCount = ClearVoxelsBulk(component);
            if (detachedCount == 0) return 0;

            // Preserve the whole silhouette, not merely the first part visited by the flood
            // fill. Every spatial bucket gets a tiny reservoir sample, then a deterministic
            // subset of buckets spanning the component is handed to the GPU.
            var buckets = new Dictionary<int3, VisualBucket>();
            for (int i = 0; i < component.Count; i++)
            {
                var voxel = component[i];
                int3 p = voxel.Position;
                int3 bucketCoord = new int3(FloorDiv(p.x, FallingChunkEdge),
                                            FloorDiv(p.y, FallingChunkEdge),
                                            FloorDiv(p.z, FallingChunkEdge));
                if (!buckets.TryGetValue(bucketCoord, out var bucket))
                {
                    bucket = new VisualBucket { Priority = VisualHash(bucketCoord) };
                    buckets.Add(bucketCoord, bucket);
                }

                bucket.SourceVoxelCount++;
                int capacity = RenderInstancesPerDetachedChunk;
                if (bucket.Samples.Count < capacity)
                    bucket.Samples.Add(voxel);
                else
                {
                    uint sampleHash = VisualHash(p) ^ (uint)bucket.SourceVoxelCount * 0x9E3779B9u;
                    uint selected = sampleHash % (uint)bucket.SourceVoxelCount;
                    if (selected < capacity) bucket.Samples[(int)selected] = voxel;
                }
            }

            var selectedBuckets = new List<VisualBucket>(buckets.Values);
            selectedBuckets.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            int visualCount = math.min(MaxVisualChunksPerCollapse,
                                       MaxQueuedDetachedChunks - _detachedChunks.Count);
            visualCount = math.min(visualCount, selectedBuckets.Count);
            int represented = 0;
            for (int b = 0; b < visualCount; b++)
                represented += selectedBuckets[b].SourceVoxelCount;
            float coverageScale = represented > 0 ? detachedCount / (float)represented : 1f;
            for (int b = 0; b < visualCount; b++)
            {
                VisualBucket bucket = selectedBuckets[b];
                var voxels = bucket.Samples;
                var chunk = new DetachedVoxelChunk
                {
                    Voxels = new int3[voxels.Count],
                    Materials = new byte[voxels.Count],
                    Coatings = new byte[voxels.Count],
                    SourceVoxelCount = math.max(bucket.SourceVoxelCount,
                                                (int)math.ceil(bucket.SourceVoxelCount
                                                               * coverageScale)),
                    ImpactMetres = ((float3)impact + 0.5f) * VoxelSize,
                    ImpulseDirection = impulseDirection,
                };
                for (int i = 0; i < voxels.Count; i++)
                {
                    chunk.Voxels[i] = voxels[i].Position;
                    chunk.Materials[i] = voxels[i].Material;
                    chunk.Coatings[i] = voxels[i].Coating;
                }
                _detachedChunks.Enqueue(chunk);
            }

            return detachedCount;
        }

        private static uint VisualHash(int3 value)
        {
            uint hash = (uint)(value.x * 73856093)
                      ^ (uint)(value.y * 19349663)
                      ^ (uint)(value.z * 83492791);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            return hash ^ (hash >> 16);
        }

        private static int FloorDiv(int value, int divisor) =>
            value >= 0 ? value / divisor : -((-value + divisor - 1) / divisor);

        /// <summary>Places a solid sphere — the build half of the loop.</summary>
        public int Place(int3 centre, ushort radius, byte material)
        {
            var voxels = BuildBrushes.PlaceSphere(centre, radius, material, Seed);
            int changed = 0;

            for (int i = 0; i < voxels.Length; i++)
            {
                if (voxels[i].y < 0) continue;
                if (SetMaterialApi(voxels[i], material))
                {
                    changed++;
                    MarkDirty(voxels[i]);
                }
            }

            voxels.Dispose();
            _editCounter++;
            LastEditVoxels = changed;
            return changed;
        }

        private bool TryReadCellApi(int3 voxel, out VoxelCell cell)
        {
            int3 regionCoord = voxel >> VoxelGrid.RegionVoxelEdgeLog2;
            ulong version = _readSource.Version;

            if (!_hasCachedReadView
                || !math.all(regionCoord == _cachedReadRegion)
                || version != _cachedReadVersion)
            {
                if (!_readSource.TryAcquireRegion(regionCoord, out _cachedReadView))
                {
                    _hasCachedReadView = false;
                    cell = default;
                    return false;
                }

                _cachedReadRegion = regionCoord;
                _cachedReadVersion = version;
                _hasCachedReadView = true;
            }

            int3 localVoxel = voxel - (regionCoord << VoxelGrid.RegionVoxelEdgeLog2);
            return _cachedReadView.TryReadCell(localVoxel, out cell);
        }

        private byte ReadMaterialApi(int3 voxel) =>
            TryReadCellApi(voxel, out VoxelCell cell)
                ? cell.BaseMaterialId
                : VoxelGrid.MaterialEmpty;

        private bool IsSolidApi(int3 voxel) =>
            TryReadCellApi(voxel, out VoxelCell cell) && cell.IsSolid;

        /// <summary>
        /// Voxel-level compatibility helper implemented entirely through Storage.Api. Using the
        /// cell-authoring block path preserves the legacy first-write region creation behavior,
        /// while Storage still owns mixed materialisation, occupancy, metadata and collapse.
        /// </summary>
        private bool SetMaterialApi(int3 voxel, byte material)
        {
            int3 worldBlock = voxel >> VoxelReadGrid.BlockEdgeLog2;
            if (!_mutationStore.TryBeginCellBlock(worldBlock, false, out VoxelBlockMutation mutation))
                return false;

            int3 inner = voxel & VoxelReadGrid.BlockEdgeMask;
            int voxelIndex = inner.x
                           | (inner.y << VoxelReadGrid.BlockEdgeLog2)
                           | (inner.z << (VoxelReadGrid.BlockEdgeLog2 * 2));
            bool payloadChanged = mutation.SetMaterial(voxelIndex, material);
            return _mutationStore.CompletePartialBlock(ref mutation, payloadChanged);
        }

        /// <summary>
        /// Publishes the exact changed cell. The scheduler expands the extraction halo.
        /// </summary>
        private void MarkDirty(int3 voxel)
        {
            var rc = new int3(voxel.x >> VoxelGrid.RegionVoxelEdgeLog2,
                              voxel.y >> VoxelGrid.RegionVoxelEdgeLog2,
                              voxel.z >> VoxelGrid.RegionVoxelEdgeLog2);

            // Storage may allocate or collapse a mixed brick, but render domains consume only
            // this logical changed range and expand their own extraction halos.
            _changes.Publish(rc, voxel, voxel + 1,
                VoxelChangeKind.Occupancy | VoxelChangeKind.BaseMaterial
                | VoxelChangeKind.SurfaceStyle | VoxelChangeKind.Coating);

        }

        // -- brush stamps --------------------------------------------------------

        private void FillBox(int3 minCorner, int3 size, byte material)
        {
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            for (int z = 0; z < size.z; z++)
            {
                var v = minCorner + new int3(x, y, z);
                if (SetMaterialApi(v, material)) MarkDirty(v);
            }
        }

        private void StampCylinder(int3 baseCentre, ushort radius, int height, byte material)
        {
            var voxels = BuildBrushes.PlaceCylinder(baseCentre, radius, height, material, Seed);
            for (int i = 0; i < voxels.Length; i++)
                if (SetMaterialApi(voxels[i], material)) MarkDirty(voxels[i]);
            voxels.Dispose();
        }

        private void StampSphere(int3 centre, ushort radius, byte material)
        {
            var voxels = BuildBrushes.PlaceSphere(centre, radius, material, Seed);
            for (int i = 0; i < voxels.Length; i++)
                if (SetMaterialApi(voxels[i], material)) MarkDirty(voxels[i]);
            voxels.Dispose();
        }

        private uint MixSeed(int3 centre, ushort radius) =>
            Seed ^ (uint)(centre.x * 73856093) ^ (uint)(centre.y * 19349663)
                 ^ (uint)(centre.z * 83492791) ^ ((uint)radius << 3) ^ (_editCounter * 2654435761u);

        /// <summary>
        /// Spawn point in metres, on the open southern approach to the castle.
        ///
        /// The former z=166 position lies inside the castle's sculpted outcrop. Grounding there
        /// against the unmodified terrain sampler put the character's body inside raised rock.
        /// This point is beyond both the cliff skirt and the longest possible gate bridge.
        /// </summary>
        public Vector3 SpawnPosition()
        {
            // The castle spawn stands off far enough to take in a landmark hundreds of voxels
            // across. A world whose only landmark is one house has to spawn beside it instead:
            // this point is 62.6 m from the house, past a 51.2 m streaming radius, so from
            // spawn the house was not in the voxel world at all and the far field's smooth
            // structure proxy stood in for it — which reads as a broken LOD and is not one.
            if (!_includeCastle)
            {
                const int houseViewOffsetVoxels = 180;   // 18 m: the whole house in frame
                int hx = LandmarkCentreX;
                int hz = LandmarkCentreZ - houseViewOffsetVoxels;
                int houseGround = SurfaceHeight(hx, hz);
                return new Vector3(hx * 0.1f, (houseGround + 40) * 0.1f, hz * 0.1f);
            }

            // Offset east of the bridge axis so the first view layers the eastern tower,
            // gatehouse, keep, and west wing instead of collapsing them into a flat symmetric
            // elevation. This remains beyond the sculpted cliff skirt and bridge footprint.
            int cx = RegionVoxelEdge / 2 + 190;
            const int cz = -220;
            int h = SurfaceHeight(cx, cz);
            return new Vector3(cx * 0.1f, (h + 40) * 0.1f, cz * 0.1f);
        }

        /// <summary>
        /// Highest occupied voxel in a generated world column.
        ///
        /// Unlike <see cref="SurfaceHeight"/>, this includes landmarks and player edits. Spawn
        /// and respawn must use the world that collision actually reads, not the terrain that
        /// existed before the castle sculpted it.
        /// </summary>
        public int OccupiedSurfaceHeight(int wx, int wz)
        {
            for (int y = RegionVoxelEdge - 1; y >= 0; y--)
            {
                if (ReadMaterialApi(new int3(wx, y, wz)) != VoxelGrid.MaterialEmpty) return y;
            }

            // A generated terrain column always has a surface, but retaining the canonical
            // sampler fallback makes this method safe if called before its region is resident.
            return SurfaceHeight(wx, wz);
        }

        /// <summary>
        /// Feature voxels written so far, accumulated. Reporting only the most recent region
        /// showed zero almost always, since most regions contain no features.
        /// </summary>
        public int FeatureVoxelsBuilt { get; private set; }
        public int FeatureInstancesBuilt { get; private set; }
        public double LastFeatureMs { get; private set; }

        public void Dispose()
        {
            FinishRegionForced();
            _featureBuild?.Dispose();
            _featureBuild = null;
            _detachedChunks.Clear();
            if (_catalogue.IsCreated) _catalogue.Dispose();
            _storage.Dispose();
        }
    }
}
