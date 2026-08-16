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
        private CastleSiteRealizer.State _legacySite;
        private CastlePlannedSiteRealizer.State _plannedSite;
        private CastleSitePlan _sitePlan;
        private CastleWallPlan _wallPlan;

        private bool _hasSpatialFortifications;
        private bool _hasSpatialKeep;
        private int2[] _outerWardVertices;
        private int2[] _innerWardVertices;
        private CastleTowerPlacementSpec[] _outerTowerSpecs;
        private CastleTowerPlacementSpec[] _innerTowerSpecs;
        private CastleGatePlacementSpec _primaryGate;
        private CastleApproachFrame _approach;
        private CastleGatehousePlan _gatehousePlan;
        private bool _hasPosternGate;
        private CastleGatePlacementSpec _posternGate;
        private CastleWallDoorPlan _posternDoorPlan;
        private bool _hasInnerGate;
        private CastleGatePlacementSpec _innerGate;
        private CastleWallDoorPlan _innerWardDoorPlan;
        private bool _hasSpatialWell;
        private int2 _spatialWellCentre;
        private CastleCourtyardBuildingSpec[] _courtyardBuildings;
        private CastleKeepFloorPlan[] _keepFloorPlans;
        private CastleKeepTurretSpec[] _keepTurrets;
        private CastleKeepCirculationPlan _keepCirculation;
        private CastleKeepWindowSpec[] _keepWindows;
        private CastleKeepAnnexPlan _keepAnnexes;
        private int2 _worldKeepCentre;
        private DungeonPlan _spatialDungeonPlan;
        private CavePlan _spatialCavePlan;
        private CastleCaveDecorationPlan _spatialCaveDecorationPlan;
        private CastleLandscapePlan _spatialLandscapePlan;

        public CastleBuildPipeline(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            in CastlePlan plan,
            uint terrainSeed,
            IMaterialAuthoringCatalogue materials)
            : this(reads, mutations, in plan, null, terrainSeed, materials)
        {
        }

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

            _plan = plan;
            _spatialKeepPlan = plan;
            _sitePlan = default;
            _wallPlan = default;
            _posternDoorPlan = default;
            _innerWardDoorPlan = default;
            _outerTowerSpecs = Array.Empty<CastleTowerPlacementSpec>();
            _innerTowerSpecs = Array.Empty<CastleTowerPlacementSpec>();
            _courtyardBuildings = Array.Empty<CastleCourtyardBuildingSpec>();
            _keepFloorPlans = Array.Empty<CastleKeepFloorPlan>();
            _keepTurrets = Array.Empty<CastleKeepTurretSpec>();
            _keepCirculation = default;
            _keepWindows = Array.Empty<CastleKeepWindowSpec>();
            _keepAnnexes = default;
            _worldKeepCentre = default;
            _spatialDungeonPlan = null;
            _spatialCavePlan = null;
            _spatialCaveDecorationPlan = null;
            _spatialLandscapePlan = null;
            _gatehousePlan = default;

            if (spatialPlan != null)
                SnapshotSpatialPlan(in plan, spatialPlan);

            _terrainSeed = terrainSeed;
            _stage = 1;
            _keepStage = 0;
            _legacySite = default;
            _plannedSite = default;
        }

        public bool IsComplete => _stage > 8;
        public int StageNumber => _stage;
        public long TotalVoxelsWritten => _brush.TotalVoxelsWritten;

        internal VoxelBrush Brush => _brush;

        public bool Step()
        {
            if (IsComplete) return true;

            switch (_stage)
            {
                case 1:
                {
                    bool siteComplete = _hasSpatialFortifications
                        ? CastlePlannedSiteRealizer.Step(
                            ref _brush,
                            in _plan,
                            _terrainSeed,
                            in _approach,
                            in _sitePlan,
                            ref _plannedSite)
                        : CastleSiteRealizer.Step(
                            ref _brush, in _plan, _terrainSeed, ref _legacySite);
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
                        CastlePlannedGatehouseRealizer.Build(
                            ref _brush,
                            in _plan,
                            in _primaryGate,
                            in _gatehousePlan,
                            in _wallPlan);

                        if (_hasPosternGate)
                        {
                            CastlePosternRealizer.BuildDoor(
                                ref _brush,
                                in _plan,
                                in _posternGate,
                                in _posternDoorPlan);
                        }
                        if (_hasInnerGate)
                        {
                            CastleWallDoorRealizer.BuildArchedDoor(
                                ref _brush,
                                in _plan,
                                in _innerGate,
                                in _innerWardDoorPlan);
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
                        CastlePlannedCourtyardRealizer.Build(
                            ref _brush,
                            in _plan,
                            _outerWardVertices,
                            _hasSpatialWell,
                            _spatialWellCentre,
                            in _sitePlan,
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

                    if (_hasSpatialKeep)
                    {
                        int keepStage = _keepStage;
                        bool keepComplete = CastlePlannedKeepRealizer.Step(
                            ref _brush,
                            in _plan,
                            in keepPlan,
                            _worldKeepCentre,
                            _keepFloorPlans,
                            _keepTurrets,
                            in _keepCirculation,
                            _keepWindows,
                            in _keepAnnexes,
                            ref _keepStage);

                        if (!keepComplete)
                        {
                            RequireBudget($"keep {keepStage + 1}");
                            return false;
                        }

                        return CompleteStage("keep 7");
                    }

                    if (_keepStage < 6)
                    {
                        string keepStage = $"keep {_keepStage + 1}";
                        if (!CastleKeepRealizer.TryStep(
                                ref _brush, in keepPlan, ref _keepStage))
                        {
                            throw new InvalidOperationException(
                                "CastleKeepRealizer refused a migrated keep substage.");
                        }

                        RequireBudget(keepStage);
                        return false;
                    }

                    CastleKeepAnnexRealizer.Build(ref _brush, in keepPlan);
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
                            ref _brush,
                            _spatialDungeonPlan,
                            _spatialCavePlan,
                            _spatialCaveDecorationPlan);
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
                        if (_spatialLandscapePlan == null)
                        {
                            throw new InvalidOperationException(
                                "Spatial castle reached landscape realization without a planned landscape.");
                        }

                        CastlePlannedLandscapeRealizer.Build(
                            ref _brush, in _plan, _spatialLandscapePlan);
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
            _sitePlan = spatialPlan.Topology.Site;
            _hasPosternGate = spatialPlan.HasPosternGate;
            _posternGate = spatialPlan.PosternGate;
            _hasInnerGate = spatialPlan.HasInnerGate;
            _innerGate = spatialPlan.InnerGate;
            _hasSpatialWell = spatialPlan.HasWell;
            _spatialWellCentre = spatialPlan.WellCentre;
            _courtyardBuildings = (CastleCourtyardBuildingSpec[])spatialPlan.CourtyardBuildings.Clone();
            _keepFloorPlans = (CastleKeepFloorPlan[])spatialPlan.KeepFloors.Clone();
            _keepCirculation = spatialPlan.KeepCirculation;
            _keepWindows = (CastleKeepWindowSpec[])spatialPlan.KeepWindows.Clone();

            CastleTopologyPlan topology = spatialPlan.Topology;
            _wallPlan = topology.Walls;
            if (_hasPosternGate)
                _posternDoorPlan = topology.PosternDoor;
            if (_hasInnerGate)
                _innerWardDoorPlan = topology.InnerWardDoor;
            _keepAnnexes = topology.KeepAnnexes;
            _keepTurrets = topology.KeepTurrets.Snapshot();
            _gatehousePlan = topology.Gatehouse;

            _spatialDungeonPlan = spatialPlan.Dungeon != null
                ? DungeonPlanSnapshot.CloneValidated(spatialPlan.Dungeon)
                : null;
            _spatialCavePlan = spatialPlan.Cave != null
                ? CavePlanSnapshot.CloneValidated(spatialPlan.Cave)
                : null;
            _spatialCaveDecorationPlan = spatialPlan.CaveDecoration != null
                ? spatialPlan.CaveDecoration.Snapshot()
                : null;
            _spatialLandscapePlan = spatialPlan.Landscape != null
                ? CastleLandscapePlanSnapshot.CloneValidated(spatialPlan.Landscape)
                : null;
            _outerTowerSpecs = (CastleTowerPlacementSpec[])spatialPlan.Towers.Clone();
            _innerTowerSpecs = (CastleTowerPlacementSpec[])spatialPlan.InnerTowers.Clone();

            CastleSpatialProjection projection = CastleSpatialProjection.Create(
                in plan, spatialPlan);
            _spatialKeepPlan = projection.KeepPlan;
            _worldKeepCentre = projection.KeepCentreWorld;
            _hasSpatialKeep = true;
        }

        private void BuildPlannedWalls()
        {
            CastlePlannedPerimeterRealizer.Walls(
                ref _brush,
                in _plan,
                _outerWardVertices,
                _primaryGate.EdgeIndex,
                _primaryGate.Centre,
                in _wallPlan);

            if (_hasPosternGate)
            {
                CastlePosternRealizer.CarveOpening(
                    ref _brush,
                    in _plan,
                    in _posternGate,
                    in _posternDoorPlan);
            }

            if (!_hasInnerGate || _innerWardVertices.Length == 0)
                return;

            CastlePlannedPerimeterRealizer.Walls(
                ref _brush,
                in _plan,
                _innerWardVertices,
                in _wallPlan);
            CastleWallDoorRealizer.CarveArchedOpening(
                ref _brush,
                in _plan,
                in _innerGate,
                in _innerWardDoorPlan);
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
