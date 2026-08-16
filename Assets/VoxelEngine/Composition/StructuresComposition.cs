using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Application-owned parameters for the hero-arch look-development build. The scene owns
    /// all authoring policy; Composition only translates those stable values into the concrete
    /// Structures.Runtime implementation.
    /// </summary>
    public struct ArchLookdevBuildRequest
    {
        public int ClearSpan;
        public int PierHeight;
        public int RingThickness;
        public int Depth;
        public int VoussoirCount;
        public int ShoulderWidth;
        public int TopMargin;
        public int FaceRecess;
        public int PlinthHeight;
        public int ImpostHeight;
        public int Damage;
        public uint DamageSeed;
        public int DamageScale;
        public int ProfileJointHalfWidthQ4;
        public int ProfileBevelQ4;
        public int ProfileProjectionQ4;
        public int ProfileDepthQ4;
        public byte StoneMaterial;
        public ushort SurfaceStyle;
        public byte Coating;
        public int CoatingCoverage;
        public int BrushBudget;
    }

    /// <summary>Stable result of a Composition-owned arch authoring pass.</summary>
    public readonly struct ArchLookdevBuildResult
    {
        public readonly IProfileBlockReadSource ProfileBlocks;
        public readonly int Width;
        public readonly int Height;

        public ArchLookdevBuildResult(
            IProfileBlockReadSource profileBlocks,
            int width,
            int height)
        {
            ProfileBlocks = profileBlocks;
            Width = width;
            Height = height;
        }
    }

    /// <summary>Composition-owned incremental castle authoring session.</summary>
    public interface ICastleBuildSession
    {
        bool IsComplete { get; }
        int StageNumber { get; }
        long TotalVoxelsWritten { get; }
        bool Step();
    }

    /// <summary>
    /// Runtime-ready castle planning bundle. Composition keeps a detached snapshot of the
    /// dimensions, terrain-resolved spatial plan, and terrain seed so dependency bounds,
    /// realization, interaction, and presentation always observe the same castle. Public spatial
    /// access returns another detached copy; caller mutation cannot change the bundle after creation.
    /// </summary>
    public readonly struct PlannedCastleBuild
    {
        private readonly CastlePlan _dimensions;
        private readonly CastleSpatialPlan _spatial;

        public CastlePlan Dimensions => _dimensions;
        public CastleSpatialPlan Spatial =>
            _spatial != null ? CastleSpatialPlanSnapshot.CloneDetached(_spatial) : null;
        public uint TerrainSeed { get; }
        public CastleSpatialProjection Projection =>
            CastleSpatialProjection.Create(in _dimensions, _spatial);

        internal PlannedCastleBuild(
            in CastlePlan dimensions,
            CastleSpatialPlan spatial,
            uint terrainSeed)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));

            _dimensions = dimensions;
            _spatial = CastleSpatialPlanSnapshot.CloneRuntimeReady(in dimensions, spatial);
            TerrainSeed = terrainSeed;
        }
    }

    /// <summary>Stable retained-profile handle; mutable Runtime storage stays private.</summary>
    public interface IStructureProfileStore : IProfileBlockReadSource
    {
    }

    public readonly struct ReferenceArchBuildResult
    {
        public readonly int3 Min;
        public readonly int3 Max;
        public readonly int VoxelsWritten;

        public ReferenceArchBuildResult(int3 min, int3 max, int voxelsWritten)
        {
            Min = min;
            Max = max;
            VoxelsWritten = voxelsWritten;
        }
    }

    /// <summary>Application wiring for structure planning and authoring.</summary>
    public static class StructuresComposition
    {
        /// <summary>
        /// Draws the deterministic castle plan through the Structures.Api planning boundary.
        /// The returned plan stays independent of concrete voxel authoring/runtime types.
        /// </summary>
        public static CastlePlan PlanCastle(int3 centre, uint seed) =>
            CastlePlanner.Create(centre, seed);

        /// <summary>
        /// Resolves semantic topology and spatial placement through pure Structures.Api planners.
        /// A HighestGround keep intentionally remains unresolved until a terrain seed is supplied.
        /// Runtime never re-plans an in-flight castle.
        /// </summary>
        public static CastleSpatialPlan PlanCastleSpatial(in CastlePlan plan)
        {
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(plan.Seed);
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            if (!CastleSpatialPlanValidator.TryValidate(
                    in plan, spatial, out CastleSpatialPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle spatial planning produced an invalid plan: {issue}.");
            }
            return spatial;
        }

        /// <summary>
        /// Produces a fully runtime-ready spatial plan, including deterministic terrain resolution
        /// for a HighestGround keep when that topology was selected.
        /// </summary>
        public static CastleSpatialPlan PlanCastleSpatial(in CastlePlan plan, uint terrainSeed)
        {
            CastleSpatialPlan spatial = PlanCastleSpatial(in plan);
            return CastleTerrainPlanning.Resolve(in plan, spatial, terrainSeed);
        }

        /// <summary>
        /// Produces the complete castle planning input an application needs for realization,
        /// interaction, and presentation without making the scene repeat planner/projection wiring.
        /// </summary>
        public static PlannedCastleBuild PlanCastleBuild(
            int3 centre,
            uint seed,
            uint terrainSeed)
        {
            CastlePlan dimensions = PlanCastle(centre, seed);
            CastleSpatialPlan spatial = PlanCastleSpatial(in dimensions, terrainSeed);
            return new PlannedCastleBuild(in dimensions, spatial, terrainSeed);
        }

        /// <summary>
        /// Wires the hero-arch lookdev request into Structures.Runtime without exposing concrete
        /// feature definitions, profile storage, rasterizers, brushes, or weathering helpers to
        /// scene code. The structure algorithm remains owned by Structures.Runtime.
        /// </summary>
        public static ArchLookdevBuildResult BuildArchLookdev(
            IVoxelStorageRuntime storage,
            in ArchLookdevBuildRequest request)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));

            ArchBayAuthoringPipeline.Author(
                storage.Reads,
                storage.Mutations,
                storage.MaterialAuthoring,
                request.ClearSpan,
                request.PierHeight,
                request.RingThickness,
                request.Depth,
                request.VoussoirCount,
                request.ShoulderWidth,
                request.TopMargin,
                request.FaceRecess,
                request.PlinthHeight,
                request.ImpostHeight,
                request.Damage,
                request.DamageScale,
                request.DamageSeed,
                request.ProfileJointHalfWidthQ4,
                request.ProfileBevelQ4,
                request.ProfileProjectionQ4,
                request.ProfileDepthQ4,
                request.StoneMaterial,
                request.SurfaceStyle,
                request.Coating,
                (byte)request.CoatingCoverage,
                request.BrushBudget,
                out IProfileBlockReadSource profiles,
                out int width,
                out int height);

            storage.PublishAllResidentRegions();
            return new ArchLookdevBuildResult(profiles, width, height);
        }
        public static IStructureProfileStore CreateProfileStore() => new StructureProfileStore();

        public static ICastleBuildSession BeginCastleBuild(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            in CastlePlan plan,
            uint terrainSeed,
            IMaterialAuthoringCatalogue materials)
        {
            if (reads == null) throw new ArgumentNullException(nameof(reads));
            if (mutations == null) throw new ArgumentNullException(nameof(mutations));
            return new CastleBuildSession(reads, mutations, in plan, terrainSeed, materials);
        }

        /// <summary>
        /// Starts a spatially planned build. Composition completes any outstanding site-aware
        /// planning before Runtime snapshots the plan; every orientation/placement-sensitive stage
        /// then consumes that validated spatial result.
        /// </summary>
        public static ICastleBuildSession BeginCastleBuild(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            in CastlePlan plan,
            CastleSpatialPlan spatialPlan,
            uint terrainSeed,
            IMaterialAuthoringCatalogue materials)
        {
            if (reads == null) throw new ArgumentNullException(nameof(reads));
            if (mutations == null) throw new ArgumentNullException(nameof(mutations));
            if (spatialPlan == null) throw new ArgumentNullException(nameof(spatialPlan));

            CastleSpatialPlan resolvedSpatialPlan = CastleTerrainPlanning.Resolve(
                in plan, spatialPlan, terrainSeed);
            return new CastleBuildSession(
                reads, mutations, in plan, resolvedSpatialPlan, terrainSeed, materials);
        }

        /// <summary>
        /// Starts a build from the runtime-ready bundle returned by <see cref="PlanCastleBuild"/>.
        /// The bundle owns the terrain seed and the completed spatial plan used by realization.
        /// </summary>
        public static ICastleBuildSession BeginCastleBuild(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            in PlannedCastleBuild planned,
            IMaterialAuthoringCatalogue materials)
        {
            CastlePlan dimensions = planned.Dimensions;
            CastleSpatialPlan spatial = planned.Spatial;
            if (spatial == null)
                throw new ArgumentException("Planned castle build has no spatial plan.", nameof(planned));
            if (reads == null) throw new ArgumentNullException(nameof(reads));
            if (mutations == null) throw new ArgumentNullException(nameof(mutations));

            return new CastleBuildSession(
                reads,
                mutations,
                in dimensions,
                spatial,
                planned.TerrainSeed,
                materials);
        }

        public static ReferenceArchBuildResult BuildReferenceArch(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            IMaterialAuthoringCatalogue materials,
            ISurfaceStyleAuthoringCatalogue surfaces,
            ICoatingAuthoringCatalogue coatings,
            IStructureProfileStore profiles,
            int3 origin,
            byte stoneMaterial,
            ushort pierStyle,
            ushort ringStyle,
            byte coating)
        {
            if (reads == null) throw new ArgumentNullException(nameof(reads));
            if (mutations == null) throw new ArgumentNullException(nameof(mutations));
            if (materials == null) throw new ArgumentNullException(nameof(materials));
            if (surfaces == null) throw new ArgumentNullException(nameof(surfaces));
            if (coatings == null) throw new ArgumentNullException(nameof(coatings));
            if (!(profiles is StructureProfileStore profileStore))
                throw new ArgumentException("Profiles must be created by StructuresComposition.", nameof(profiles));

            var arch = new ArchFeatureDefinition
            {
                ClearSpan = 64,
                PierHeight = 48,
                RingThickness = 10,
                Depth = 12,
                VoussoirCount = 13,
                StoneMaterial = stoneMaterial,
                PierStyle = pierStyle,
                RingStyle = ringStyle,
                Coating = coating
            };

            var primitives = new NativeList<Primitive>(arch.Metadata.MaxPrimitives, Allocator.Temp);
            try
            {
                ArchValidationError validation = arch.Validate(materials, surfaces, coatings);
                if (validation != ArchValidationError.None || !arch.Emit(origin, primitives, profileStore.Runtime))
                    throw new InvalidOperationException($"The built-in reference arch is invalid: {validation}.");

                int3 max = origin + arch.Metadata.Footprint;
                RasterResult result = PrimitiveRasteriser.Rasterise(
                    primitives.AsArray(), origin, max, reads, mutations);
                return new ReferenceArchBuildResult(origin, max, result.VoxelsWritten);
            }
            finally
            {
                primitives.Dispose();
            }
        }

        private sealed class CastleBuildSession : ICastleBuildSession
        {
            private readonly CastleBuildPipeline _build;

            public CastleBuildSession(
                IRegionReadSource reads, IRegionMutationStore mutations,
                in CastlePlan plan, uint terrainSeed, IMaterialAuthoringCatalogue materials)
            {
                _build = new CastleBuildPipeline(
                    reads, mutations, in plan, terrainSeed, materials);
            }

            public CastleBuildSession(
                IRegionReadSource reads, IRegionMutationStore mutations,
                in CastlePlan plan, CastleSpatialPlan spatialPlan,
                uint terrainSeed, IMaterialAuthoringCatalogue materials)
            {
                _build = new CastleBuildPipeline(
                    reads, mutations, in plan, spatialPlan, terrainSeed, materials);
            }

            public bool IsComplete => _build.IsComplete;
            public int StageNumber => _build.StageNumber;
            public long TotalVoxelsWritten => _build.TotalVoxelsWritten;
            public bool Step() => _build.Step();
        }

        private sealed class StructureProfileStore : IStructureProfileStore
        {
            internal ProfileBlockStore Runtime { get; } = new ProfileBlockStore();
            public uint Version => Runtime.Version;
            public int Count => Runtime.Count;
            public ProfileBlock[] Snapshot() => Runtime.Snapshot();
        }

    }
}
