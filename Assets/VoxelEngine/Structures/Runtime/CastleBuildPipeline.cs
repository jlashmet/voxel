using System;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Incremental realization pipeline for a planned castle.
    /// Planning determines what exists; this pipeline only realizes that plan into voxel storage.
    /// </summary>
    public sealed class CastleBuildPipeline
    {
        private VoxelBrush _brush;
        private CastlePlan _plan;
        private CastlePlan _spatialKeepPlan;
        private uint _terrainSeed;
        private int _stage;
        private int _keepStage;
        private CastleSiteRealizer.State _site;

        // Spatial planning now drives every stage that depends on castle orientation or placement.
        // The legacy landscape recipe remains available only to compatibility builds.
        private bool _hasSpatialFortifications;
        private bool _hasSpatialKeep;
        private int2[] _outerWardVertices;
        private int2[] _innerWardVertices;
        private CastleTowerPlacementSpec[] _outerTowerSpecs;
        private CastleTowerPlacementSpec[] _innerTowerSpecs;
        private CastleGatePlacementSpec _primaryGate;
        private CastleApproachFrame _approach;
        private bool _hasPosternGate;
        private CastleGatePlacementSpec _posternGate;
        private bool _hasInnerGate;
        private CastleGatePlacementSpec _innerGate;
        private bool _hasSpatialWell;
        private int2 _spatialWellCentre;
        private CastleCourtyardBuildingSpec[] _courtyardBuildings;
        private CastleKeepFloorPlan[] _keepFloorPlans;
        private CastleKeepCirculationPlan _keepCirculation;
        private CastleKeepAnnexPlan _keepAnnexes;
        private DungeonPlan _spatialDungeonPlan;
        private CavePlan _spatialCavePlan;

        public CastleBuildPipeline(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            in CastlePlan plan,
            uint terrainSeed,
            IMaterialAuthoringCatalogue materials)
            : this(reads, mutations, in plan, null, terrainSeed, materials)
        {
        }

        /// <summary>
        /// Builds with a precomputed spatial plan for migrated realization stages. The caller owns
        /// planning; Runtime validates and snapshots supplied geometry before any voxel writes so
        /// later caller mutation cannot change the in-flight build.
        /// </summary>
        public CastleBuildPipeline(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            in CastlePlan plan,
            CastleSpatialPlan spatialPlan,
            uint terrainSeed,
            IMaterialAuthoringCatalogue materials)
        {
            if (reads == null) throw new ArgumentNullException(nameof(reads));
            if (mutations == null) throw new ArgumentNullException(nameof(mutations));

            _brush = new VoxelBrush(reads, mutations, materials);
            CastleBuildPreflightResult preflight = spatialPlan == null
                ? CastleBuildPreflight.Evaluate(in plan, _brush.WriteBudget)
                : CastleBuildPreflight.EvaluateRuntimeReady(
                    in plan, spatialPlan, _brush.WriteBudget);

            if (!preflight.IsValid)
            {
                if (preflight.Issue == CastleBuildPreflightIssue.InvalidPlan)
                {
                    throw new InvalidOperationException(
                        $"Castle plan is structurally invalid: {preflight.PlanIssue}.");
                }

                if (preflight.Issue == CastleBuildPreflightIssue.InvalidSpatialPlan)
                {
                    throw new InvalidOperationException(
                        $"Castle spatial plan is structurally invalid: {preflight.SpatialPlanIssue}.");
                }

                if (preflight.Issue == CastleBuildPreflightIssue.IncompleteSpatialPlan)
                {
                    throw new InvalidOperationException(
                        $"Castle spatial plan is not runtime-ready: {preflight.ReadinessIssue}.");
                }

                throw new InvalidOperationException(
                    $"Castle build preflight rejected ~{preflight.EstimatedWrites:N0} expensive-write " +
                    $"equivalents against a {preflight.WriteBudget:N0} write budget.");
            }

            if (spatialPlan != null)
            {
                CastleTopologyPlan topology = spatialPlan.Topology;
                if (!CastleKeepAnnexBuildReadiness.TryValidate(
                        in topology, out CastleKeepAnnexBuildReadinessIssue annexReadiness))
                {
                    throw new InvalidOperationException(
                        $"Castle keep annex plan is not runtime-ready: {annexReadiness}.");
                }
            }

            _plan = plan;
            _spatialKeepPlan = plan;
            _outerTowerSpecs = Array.Empty<CastleTowerPlacementSpec>();
            _innerTowerSpecs = Array.Empty<CastleTowerPlacementSpec>();
            _courtyardBuildings = Array.Empty<CastleCourtyardBuildingSpec>();
            _keepFloorPlans = Array.Empty<CastleKeepFloorPlan>();
            _keepCirculation = default;
            _keepAnnexes = default;
            _spatialDungeonPlan = null;
            _spatialCavePlan = null;

            if (spatialPlan != null)
                SnapshotSpatialPlan(in plan, spatialPlan);

            _terrainSeed = terrainSeed;
            _stage = 1;
            _keepStage = 0;
            _site = default;
        }

        public bool IsComplete => _stage > 8;
        public int StageNumber => _stage;
        public long TotalVoxelsWritten => _brush.TotalVoxelsWritten;

        internal VoxelBrush Brush => _brush;

        /// <summary>Executes one bounded unit of the current semantic stage.</summary>
        public bool Step()
        {
            if (IsComplete) return true;

            switch (_stage)
            {
                case 1:
                {
                    bool siteComplete = _hasSpatialFortifications
                        ? CastleSiteRealizer.StepPlanned(
                            ref _brush, in _plan, _terrainSeed, in _approach, ref _site)
                        : CastleSiteRealizer.Step(
                            ref _brush, in _plan, _terrainSeed, ref _site);
                    if (!siteComplete)
                    {
                        RequireBudget("site");
                        return false;
                    }
                    return CompleteStage("site");
                }

                case 2:
                    if (_hasSpatialFortifications)
                        BuildPlannedWalls();
                    else
                        CastleFortificationRealizer.CurtainWalls(ref _brush, in _plan);
                    return CompleteStage("curtain walls");

                case 3:
                    if (_hasSpatialFortifications)
                    {
                        CastlePlannedTowerRealizer.BuildAll(
                            ref _brush, in _plan, _outerTowerSpecs);
                        CastleInnerWardTowerRealizer.BuildAll(
                            ref _brush, in _plan, _innerTowerSpecs);
                    }
                    else
                    {
                        CastleFortificationRealizer.CornerTowers(ref _brush, in _plan);
                    }
                    return CompleteStage("corner towers");

                case 4:
                    if (_hasSpatialFortifications)
                    {
                        CastlePerimeterRealizer.Gatehouse(
                            ref _brush, in _plan, _primaryGate.Centre, _primaryGate.Outward);
                        if (_hasPosternGate)
                            CastlePosternRealizer.BuildDoor(ref _brush, in _plan, in _posternGate);
                        if (_hasInnerGate)
                        {
                            CastleWallDoorRealizer.BuildArchedDoor(
                                ref _brush,
                                in _plan,
                                in _innerGate,
                                CastleLayout.FrontGateWidth,
                                CastleLayout.FrontGateHeight,
                                CastleLayout.FrontGateDepth);
                        }
                    }
                    else
                    {
                        CastleFortificationRealizer.Gatehouse(ref _brush, in _plan);
                    }
                    return CompleteStage("gatehouse");

                case 5:
                    if (_hasSpatialFortifications)
                    {
                        CastleCourtyardRealizer.BuildPlanned(
                            ref _brush,
                            in _plan,
                            _outerWardVertices,
                            _hasSpatialWell,
                            _spatialWellCentre,
                            _courtyardBuildings);
                    }
                    else
                    {
                        CastleCourtyardRealizer.Build(ref _brush, in _plan);
                    }
                    return CompleteStage("courtyard");

                case 6:
                {
                    CastlePlan keepPlan = _hasSpatialKeep ? _spatialKeepPlan : _plan;

                    // Historical keep substage 4 is circulation. Spatial builds realize its
                    // planner-owned anchors directly; compatibility builds keep the legacy path.
                    if (_hasSpatialKeep && _keepStage == 3)
                    {
                        CastlePlannedKeepCirculationRealizer.Build(
                            ref _brush, in keepPlan, in _keepCirculation);
                        _keepStage++;
                        RequireBudget("keep 4");
                        return false;
                    }

                    if (_keepStage < 6)
                    {
                        string keepStage = $"keep {_keepStage + 1}";
                        bool realized = _hasSpatialKeep
                            ? CastleKeepRealizer.TryStep(
                                ref _brush, in keepPlan, _keepFloorPlans, ref _keepStage)
                            : CastleKeepRealizer.TryStep(
                                ref _brush, in keepPlan, ref _keepStage);
                        if (!realized)
                        {
                            throw new InvalidOperationException(
                                "CastleKeepRealizer refused a migrated keep substage.");
                        }

                        RequireBudget(keepStage);
                        return false;
                    }

                    if (_hasSpatialKeep)
                    {
                        CastlePlannedKeepAnnexRealizer.Build(
                            ref _brush, in keepPlan, in _keepAnnexes);
                    }
                    else
                    {
                        CastleKeepAnnexRealizer.Build(ref _brush, in keepPlan);
                    }
                    _keepStage++;
                    return CompleteStage("keep 7");
                }

                case 7:
                {
                    CastlePlan dungeonPlan = _hasSpatialKeep ? _spatialKeepPlan : _plan;
                    if (_hasSpatialKeep)
                    {
                        if (_spatialDungeonPlan == null)
                        {
                            throw new InvalidOperationException(
                                "Spatial castle reached dungeon realization without a planned dungeon.");
                        }

                        CastlePlannedDungeonRealizer.Build(
                            ref _brush, _spatialDungeonPlan, _spatialCavePlan);
                    }
                    else
                    {
                        CastleDungeonRealizer.Build(ref _brush, in dungeonPlan);
                    }
                    return CompleteStage("dungeon");
                }

                case 8:
                    if (_hasSpatialFortifications)
                    {
                        CastleSpatialLandscapeRealizer.Build(
                            ref _brush, in _plan, _outerWardVertices, in _approach);
                    }
                    else
                    {
                        CastleLandscapeRealizer.Build(ref _brush, in _plan, _terrainSeed);
                    }
                    return CompleteStage("landscape details");

                default:
                    return true;
            }
        }

        private void SnapshotSpatialPlan(in CastlePlan plan, CastleSpatialPlan spatialPlan)
        {
            _hasSpatialFortifications = true;
            _outerWardVertices = (int2[])spatialPlan.OuterWardVertices.Clone();
            _innerWardVertices = (int2[])spatialPlan.InnerWardVertices.Clone();
            _primaryGate = spatialPlan.PrimaryGate;
            _approach = CastleApproachFrame.FromGate(in _primaryGate);
            _hasPosternGate = spatialPlan.HasPosternGate;
            _posternGate = spatialPlan.PosternGate;
            _hasInnerGate = spatialPlan.HasInnerGate;
            _innerGate = spatialPlan.InnerGate;
            _hasSpatialWell = spatialPlan.HasWell;
            _spatialWellCentre = spatialPlan.WellCentre;
            _courtyardBuildings = (CastleCourtyardBuildingSpec[])spatialPlan.CourtyardBuildings.Clone();
            _keepFloorPlans = (CastleKeepFloorPlan[])spatialPlan.KeepFloors.Clone();

            CastleKeepCirculationPlan circulation = spatialPlan.KeepCirculation;
            if (!CastleKeepCirculationPlanner.TryValidate(
                    in plan, in circulation, out CastleKeepCirculationPlanIssue circulationIssue))
            {
                throw new InvalidOperationException(
                    $"Spatial castle reached Runtime with invalid keep circulation: {circulationIssue}.");
            }
            _keepCirculation = circulation;

            CastleTopologyPlan topology = spatialPlan.Topology;
            _keepAnnexes = topology.KeepAnnexes;

            _spatialDungeonPlan = spatialPlan.Dungeon != null
                ? DungeonPlanSnapshot.CloneValidated(spatialPlan.Dungeon)
                : null;
            _spatialCavePlan = spatialPlan.Cave != null
                ? CavePlanSnapshot.CloneValidated(spatialPlan.Cave)
                : null;
            _outerTowerSpecs = (CastleTowerPlacementSpec[])spatialPlan.Towers.Clone();
            _innerTowerSpecs = (CastleTowerPlacementSpec[])spatialPlan.InnerTowers.Clone();

            CastleSpatialProjection projection = CastleSpatialProjection.Create(
                in plan, spatialPlan);
            _spatialKeepPlan = projection.KeepPlan;
            _hasSpatialKeep = true;
        }

        private void BuildPlannedWalls()
        {
            int outerGateWidth = math.max(
                CastleLayout.FrontGateWidth + 12,
                _plan.WallThickness * 2);
            CastlePerimeterRealizer.Walls(
                ref _brush,
                in _plan,
                _outerWardVertices,
                _primaryGate.EdgeIndex,
                _primaryGate.Centre,
                outerGateWidth);

            if (_hasPosternGate)
                CastlePosternRealizer.CarveOpening(ref _brush, in _plan, in _posternGate);

            if (!_hasInnerGate || _innerWardVertices.Length == 0)
                return;

            CastlePerimeterRealizer.Walls(
                ref _brush,
                in _plan,
                _innerWardVertices);
            CastleWallDoorRealizer.CarveArchedOpening(
                ref _brush,
                in _plan,
                in _innerGate,
                CastleLayout.FrontGateWidth,
                CastleLayout.FrontGateHeight);
        }

        private bool CompleteStage(string stage)
        {
            RequireBudget(stage);
            _stage++;
            return IsComplete;
        }

        private void RequireBudget(string stage)
        {
            if (!_brush.BudgetExceeded) return;

            throw new InvalidOperationException(
                $"Castle build exceeded its {_brush.WriteBudget:N0}-write budget while " +
                $"building the {stage}, after {_brush.TotalVoxelsWritten:N0} changed voxels. " +
                "A partial castle is invalid.");
        }
    }
}
