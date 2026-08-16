using System;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Composition
{
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
    /// realization, interaction, and presentation always observe the same castle.
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
        public CastleGatehousePlan Gatehouse =>
            _spatial != null ? _spatial.Topology.Gatehouse : default;

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

    public static partial class StructuresComposition
    {
        public static CastlePlan PlanCastle(int3 centre, uint seed) =>
            CastlePlanner.Create(centre, seed);

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

        public static CastleSpatialPlan PlanCastleSpatial(in CastlePlan plan, uint terrainSeed)
        {
            CastleSpatialPlan spatial = PlanCastleSpatial(in plan);
            return CastleTerrainPlanning.Resolve(in plan, spatial, terrainSeed);
        }

        public static PlannedCastleBuild PlanCastleBuild(
            int3 centre,
            uint seed,
            uint terrainSeed)
        {
            CastlePlan dimensions = PlanCastle(centre, seed);
            CastleSpatialPlan spatial = PlanCastleSpatial(in dimensions, terrainSeed);
            return new PlannedCastleBuild(in dimensions, spatial, terrainSeed);
        }

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

        private sealed class CastleBuildSession : ICastleBuildSession
        {
            private readonly CastleBuildPipeline _build;

            public CastleBuildSession(
                IRegionReadSource reads,
                IRegionMutationStore mutations,
                in CastlePlan plan,
                uint terrainSeed,
                IMaterialAuthoringCatalogue materials)
            {
                _build = new CastleBuildPipeline(
                    reads, mutations, in plan, terrainSeed, materials);
            }

            public CastleBuildSession(
                IRegionReadSource reads,
                IRegionMutationStore mutations,
                in CastlePlan plan,
                CastleSpatialPlan spatialPlan,
                uint terrainSeed,
                IMaterialAuthoringCatalogue materials)
            {
                _build = new CastleBuildPipeline(
                    reads, mutations, in plan, spatialPlan, terrainSeed, materials);
            }

            public bool IsComplete => _build.IsComplete;
            public int StageNumber => _build.StageNumber;
            public long TotalVoxelsWritten => _build.TotalVoxelsWritten;
            public bool Step() => _build.Step();
        }
    }
}
