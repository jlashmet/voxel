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
        private readonly IStructureAuthoringSession _authoring;
        private readonly CastleConfig _config;
        private readonly CastlePlan _plan;
        private readonly uint _terrainSeed;
        private CastleSiteAuthoringState _siteState;
        private int _stage;
        private int _keepStage;

        public CastleAuthoringBuild(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            uint terrainSeed)
            : this(authoring, CastlePresets.Compatibility(in plan), terrainSeed)
        {
        }

        public CastleAuthoringBuild(
            IStructureAuthoringSession authoring,
            CastleConfig config,
            uint terrainSeed)
        {
            _authoring = authoring
                ?? throw new System.ArgumentNullException(nameof(authoring));
            if (!config.IsWellFormed)
                throw new System.ArgumentException(
                    "Castle authoring refused: castle configuration is invalid.", nameof(config));

            _config = config;
            _plan = config.ResolvePlan();
            _terrainSeed = terrainSeed;
            _stage = 1;
            _keepStage = 0;

            long estimate = CastlePlanner.EstimateWrites(in _plan);
            if (estimate > authoring.WriteBudget)
            {
                throw new System.InvalidOperationException(
                    $"Castle authoring refused: plan implies ~{estimate:N0} expensive-write " +
                    $"equivalents, budget is {authoring.WriteBudget:N0}. Reduce PlateauRadius " +
                    $"({_plan.PlateauRadius}) or the primary structure dimensions before retrying.");
            }
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
                        in _config.CurtainWallX,
                        in _config.CurtainWallZ,
                        in _config.CurtainBattlements);
                    break;

                case 3:
                    stageName = "corner towers";
                    CastleTowerAuthoring.AuthorCornerTowers(
                        _authoring,
                        in _plan,
                        in _config.CornerTowers);
                    break;

                case 4:
                    stageName = "gatehouse";
                    CastleGatehouseAuthoring.Author(
                        _authoring,
                        in _plan,
                        in _config.GateTowers,
                        in _config.MainGate,
                        in _config.GatehouseBattlements);
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
                        in _config.KeepFoundation,
                        _config.KeepFoundationTopOffset);
                    break;

                case 1:
                    CastleKeepCoreAuthoring.AuthorCornerTurrets(_authoring, in _plan);
                    break;

                case 2:
                    // Preserve the legacy write order exactly: each upper timber slab is written
                    // immediately before that floor's partitions/furnishing, rather than writing
                    // every slab first and then furnishing every floor.
                    for (int floor = 0; floor < _plan.Floors; floor++)
                    {
                        int y = baseY + floor * _plan.FloorHeight;
                        if (floor > 0)
                        {
                            _authoring.Box(
                                new int3(min.x + 8, y, min.z + 8),
                                new int3(size.x - 16, 3, size.z - 16),
                                Game.Materials.Api.GameMaterialIds.Wood);
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
