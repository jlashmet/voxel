using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Incremental game-owned castle authoring orchestration. The engine supplies one generic
    /// structure-authoring capability; this class owns castle stages, content ordering, write
    /// estimates, and the decision about which game materials/shapes compose the landmark.
    /// </summary>
    public sealed class CastleAuthoringBuild
    {
        private IStructureAuthoringSession _authoring;
        private CastlePlan _plan;
        private CastleComponentConfig _components;
        private uint _terrainSeed;
        private CastleSiteAuthoringState _siteState;
        private int _stage;
        private int _keepStage;

        public CastleAuthoringBuild(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            uint terrainSeed)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CastleComponentConfig components = CastleComponentPresets.Compatibility(in plan, in palette);
            Initialize(authoring, in plan, in components, terrainSeed);
        }

        /// <summary>
        /// Compatibility seam used while castle stages migrate from CastlePlan fields to the
        /// reusable structure-component contracts. The plan still owns castle-only semantics and
        /// placement; migrated geometry policy is supplied by <paramref name="components"/>.
        /// </summary>
        public CastleAuthoringBuild(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            CastleComponentConfig components,
            uint terrainSeed)
        {
            Initialize(authoring, in plan, in components, terrainSeed);
        }

        public bool IsComplete => _stage > 8;
        public int StageNumber => _stage;
        public long TotalVoxelsWritten => _authoring.TotalVoxelsWritten;

        /// <summary>Executes one bounded semantic authoring step.</summary>
        public bool Step()
        {
            if (IsComplete) return true;

            string stageName;
            switch (_stage)
            {
                case 1:
                    stageName = "site";
                    // The legacy site stage owns bounded terrain sculpting. BaileyFootprint declares
                    // shared bounds but deliberately does not author a second terrain foundation.
                    if (!CastleSiteAuthoring.Step(
                            _authoring,
                            in _plan,
                            _terrainSeed,
                            ref _siteState))
                    {
                        RequireBudget(stageName);
                        return false;
                    }
                    break;

                case 2:
                    stageName = "curtain walls";
                    CastleCurtainAuthoring.Author(
                        _authoring,
                        in _plan,
                        in _components.CurtainWallX,
                        in _components.CurtainWallZ,
                        in _components.CurtainBattlements);
                    break;

                case 3:
                    stageName = "corner towers";
                    CastleTowerAuthoring.AuthorCornerTowers(
                        _authoring,
                        in _plan,
                        in _components.CornerTowers);
                    break;

                case 4:
                    stageName = "gatehouse";
                    CastleGatehouseAuthoring.Author(
                        _authoring,
                        in _plan,
                        in _components.GateTowers,
                        in _components.MainGate,
                        in _components.GatehouseBattlements);
                    break;

                case 5:
                    stageName = "courtyard";
                    CastleCourtyardAuthoring.Author(_authoring, in _plan);
                    break;

                case 6:
                    stageName = $"keep {_keepStage + 1}";
                    if (!StepKeep())
                    {
                        RequireBudget(stageName);
                        return false;
                    }
                    break;

                case 7:
                    stageName = "dungeon";
                    CastleDungeonAuthoring.Author(_authoring, in _plan);
                    break;

                case 8:
                    stageName = "landscape details";
                    CastleLandscapeAuthoring.Author(_authoring, in _plan, _terrainSeed);
                    break;

                default:
                    return true;
            }

            RequireBudget(stageName);
            _stage++;
            return IsComplete;
        }

        private void Initialize(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in CastleComponentConfig components,
            uint terrainSeed)
        {
            _authoring = authoring
                ?? throw new System.ArgumentNullException(nameof(authoring));
            if (!components.IsWellFormed)
                throw new System.ArgumentException(
                    "Castle authoring refused: shared castle component configuration is invalid.",
                    nameof(components));

            _plan = plan;
            _components = components;
            _terrainSeed = terrainSeed;
            _stage = 1;
            _keepStage = 0;

            long estimate = CastlePlanner.EstimateWrites(in plan);
            if (estimate > authoring.WriteBudget)
            {
                throw new System.InvalidOperationException(
                    $"Castle authoring refused: plan implies ~{estimate:N0} expensive-write " +
                    $"equivalents, budget is {authoring.WriteBudget:N0}. Reduce PlateauRadius " +
                    $"({plan.PlateauRadius}) or the primary structure dimensions before retrying.");
            }
        }

        private bool StepKeep()
        {
            int baseY = _plan.Centre.y + _plan.PlateauHeight;
            int3 min = CastleKeepCoreAuthoring.Minimum(in _plan);
            int3 size = CastleKeepCoreAuthoring.Size(in _plan);

            switch (_keepStage)
            {
                case 0:
                    CastleKeepCoreAuthoring.AuthorShell(
                        _authoring,
                        in _plan,
                        in _components.KeepFoundation,
                        _components.KeepFoundationTopOffset,
                        in _components.KeepWalls,
                        in _components.Palette);
                    break;

                case 1:
                    CastleKeepCoreAuthoring.AuthorCornerTurrets(_authoring, in _plan);
                    break;

                case 2:
                    // Preserve the legacy write order exactly: each upper timber slab is written
                    // immediately before that floor's partitions/furnishing, rather than writing
                    // every slab first and then furnishing every floor.
                    for (int floor = 0; floor < _components.KeepFloors.FloorCount; floor++)
                    {
                        int y = baseY + floor * _components.KeepFloors.LevelHeight;
                        if (floor > 0)
                        {
                            _authoring.Box(
                                new int3(min.x + _components.KeepWalls.Thickness, y,
                                         min.z + _components.KeepWalls.Thickness),
                                new int3(
                                    size.x - 2 * _components.KeepWalls.Thickness,
                                    _components.KeepFloors.SlabThickness,
                                    size.z - 2 * _components.KeepWalls.Thickness),
                                _components.Palette.Resolve(_components.KeepFloors.SlabMaterialRole));
                        }

                        CastleKeepRoomAuthoring.AuthorFloor(
                            _authoring,
                            in _plan,
                            min,
                            size,
                            y,
                            floor);
                    }
                    break;

                case 3:
                    CastleKeepCoreAuthoring.AuthorCirculation(_authoring, in _plan);
                    break;

                case 4:
                    CastleKeepCoreAuthoring.AuthorWindows(_authoring, in _plan);
                    break;

                case 5:
                    CastleKeepCoreAuthoring.AuthorFacade(_authoring, in _plan);
                    break;

                case 6:
                    CastleKeepRooflineAuthoring.Author(_authoring, in _plan);
                    CastleGreatHallWingAuthoring.Author(_authoring, in _plan);
                    CastleChapelAuthoring.Author(_authoring, in _plan);
                    break;

                default:
                    return true;
            }

            _keepStage++;
            return _keepStage > 6;
        }

        private void RequireBudget(string stageName)
        {
            if (!_authoring.BudgetExceeded) return;

            throw new System.InvalidOperationException(
                $"Castle authoring exceeded its {_authoring.WriteBudget:N0}-write budget while " +
                $"building the {stageName}, after {_authoring.TotalVoxelsWritten:N0} changed " +
                "voxels. A partial castle is invalid.");
        }
    }
}
