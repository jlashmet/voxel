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
        private uint _terrainSeed;
        private int _stage;
        private int _keepStage;
        private CastleSiteRealizer.State _site;

        // Spatial planning is consumed incrementally. Only fortification geometry is migrated so
        // far; courtyard/keep/dungeon stages intentionally continue to use CastlePlan dimensions.
        private bool _hasSpatialFortifications;
        private int2[] _outerWardVertices;
        private int2[] _innerWardVertices;
        private int2[] _towerCentres;
        private int _cornerTowerCount;
        private CastleGatePlacementSpec _primaryGate;
        private bool _hasInnerGate;
        private CastleGatePlacementSpec _innerGate;

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
        /// Builds with a precomputed spatial plan for the migrated fortification stages. The
        /// caller owns planning; Runtime validates and snapshots the supplied geometry before any
        /// voxel writes so later caller mutation cannot change the in-flight build.
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
            CastleBuildPreflightResult preflight = CastleBuildPreflight.Evaluate(
                in plan, _brush.WriteBudget);

            if (!preflight.IsValid)
            {
                if (preflight.Issue == CastleBuildPreflightIssue.InvalidPlan)
                {
                    throw new InvalidOperationException(
                        $"Castle plan is structurally invalid: {preflight.PlanIssue}.");
                }

                throw new InvalidOperationException(
                    $"Castle build preflight rejected ~{preflight.EstimatedWrites:N0} expensive-write " +
                    $"equivalents against a {preflight.WriteBudget:N0} write budget.");
            }

            if (spatialPlan != null)
            {
                if (!CastleSpatialPlanValidator.TryValidate(
                        in plan, spatialPlan, out CastleSpatialPlanIssue spatialIssue))
                {
                    throw new InvalidOperationException(
                        $"Castle spatial plan is structurally invalid: {spatialIssue}.");
                }

                SnapshotSpatialFortifications(spatialPlan);
            }

            _plan = plan;
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
                    if (!CastleSiteRealizer.Step(
                            ref _brush, in _plan, _terrainSeed, ref _site))
                    {
                        RequireBudget("site");
                        return false;
                    }
                    return CompleteStage("site");

                case 2:
                    if (_hasSpatialFortifications)
                        BuildPlannedWalls();
                    else
                        CastleFortificationRealizer.CurtainWalls(ref _brush, in _plan);
                    return CompleteStage("curtain walls");

                case 3:
                    if (_hasSpatialFortifications)
                    {
                        CastlePerimeterRealizer.Towers(
                            ref _brush, in _plan, _towerCentres, _cornerTowerCount);
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
                    }
                    else
                    {
                        CastleFortificationRealizer.Gatehouse(ref _brush, in _plan);
                    }
                    return CompleteStage("gatehouse");

                case 5:
                    CastleCourtyardRealizer.Build(ref _brush, in _plan);
                    return CompleteStage("courtyard");

                case 6:
                    // Preserve the historical seven keep substages so streaming cadence remains
                    // unchanged while the realization responsibilities are now decomposed.
                    if (_keepStage < 6)
                    {
                        string keepStage = $"keep {_keepStage + 1}";
                        if (!CastleKeepRealizer.TryStep(ref _brush, in _plan, ref _keepStage))
                        {
                            throw new InvalidOperationException(
                                "CastleKeepRealizer refused a migrated keep substage.");
                        }

                        RequireBudget(keepStage);
                        return false;
                    }

                    CastleKeepAnnexRealizer.Build(ref _brush, in _plan);
                    _keepStage++;
                    return CompleteStage("keep 7");

                case 7:
                    CastleDungeonRealizer.Build(ref _brush, in _plan);
                    return CompleteStage("dungeon");

                case 8:
                    CastleLandscapeRealizer.Build(ref _brush, in _plan, _terrainSeed);
                    return CompleteStage("landscape details");

                default:
                    return true;
            }
        }

        private void SnapshotSpatialFortifications(CastleSpatialPlan spatialPlan)
        {
            _hasSpatialFortifications = true;
            _outerWardVertices = (int2[])spatialPlan.OuterWardVertices.Clone();
            _innerWardVertices = (int2[])spatialPlan.InnerWardVertices.Clone();
            _primaryGate = spatialPlan.PrimaryGate;
            _hasInnerGate = spatialPlan.HasInnerGate;
            _innerGate = spatialPlan.InnerGate;

            CastleTowerPlacementSpec[] towers = spatialPlan.Towers;
            _towerCentres = new int2[towers.Length];
            int cursor = 0;
            for (int i = 0; i < towers.Length; i++)
            {
                if (towers[i].Role != CastleTowerPlacementRole.Corner) continue;
                _towerCentres[cursor++] = towers[i].Centre;
            }
            _cornerTowerCount = cursor;
            for (int i = 0; i < towers.Length; i++)
            {
                if (towers[i].Role == CastleTowerPlacementRole.Corner) continue;
                _towerCentres[cursor++] = towers[i].Centre;
            }
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

            if (!_hasInnerGate || _innerWardVertices.Length == 0)
                return;

            int innerGateWidth = math.max(
                CastleLayout.FrontGateWidth,
                _plan.WallThickness * 2);
            CastlePerimeterRealizer.Walls(
                ref _brush,
                in _plan,
                _innerWardVertices,
                _innerGate.EdgeIndex,
                _innerGate.Centre,
                innerGateWidth);
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
