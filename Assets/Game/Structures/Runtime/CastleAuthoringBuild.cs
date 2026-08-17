using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public sealed class CastleAuthoringBuild
    {
        private IStructureAuthoringSession _authoring;
        private CastlePlan _plan;
        private CastleComponentConfig _components;
        private CastleCurtainConfig _curtain;
        private CastleBuildStageConfig _stages;
        private uint _terrainSeed;
        private CastleSiteAuthoringState _siteState;
        private int _stage;
        private int _keepStage;

        public CastleAuthoringBuild(IStructureAuthoringSession authoring, in CastlePlan plan,
            uint terrainSeed)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CastlePresetConfig preset = CastlePresets.Compatibility(in plan, in palette);
            Initialize(authoring, in plan, in preset.Components, in preset.Curtain,
                in preset.Stages, terrainSeed);
        }

        public CastleAuthoringBuild(IStructureAuthoringSession authoring, in CastlePlan plan,
            CastleComponentConfig components, uint terrainSeed)
        {
            CastleCurtainConfig curtain = CastleCurtainPresets.Compatibility(in components);
            CastleBuildStageConfig stages = CastleBuildStageConfig.Full;
            Initialize(authoring, in plan, in components, in curtain, in stages, terrainSeed);
        }

        public CastleAuthoringBuild(IStructureAuthoringSession authoring, in CastlePlan plan,
            CastleComponentConfig components, CastleCurtainConfig curtain, uint terrainSeed)
        {
            CastleBuildStageConfig stages = CastleBuildStageConfig.Full;
            Initialize(authoring, in plan, in components, in curtain, in stages, terrainSeed);
        }

        public CastleAuthoringBuild(IStructureAuthoringSession authoring, in CastlePlan plan,
            CastlePresetConfig preset, uint terrainSeed)
        {
            if (!preset.IsWellFormed)
                throw new System.ArgumentException("Castle preset configuration is invalid.", nameof(preset));
            Initialize(authoring, in plan, in preset.Components, in preset.Curtain,
                in preset.Stages, terrainSeed);
        }

        public bool IsComplete => _stage > 8;
        public int StageNumber => _stage;
        public long TotalVoxelsWritten => _authoring.TotalVoxelsWritten;

        public bool Step()
        {
            if (IsComplete) return true;
            string stageName;
            switch (_stage)
            {
                case 1:
                    stageName = "site";
                    if (_stages.Site && !CastleSiteAuthoring.Step(_authoring, in _plan,
                            in _components, _terrainSeed, ref _siteState))
                    {
                        RequireBudget(stageName);
                        return false;
                    }
                    break;
                case 2:
                    stageName = "curtain walls";
                    if (_stages.CurtainWalls)
                        CastleCurtainAuthoring.Author(_authoring, in _plan, in _curtain);
                    break;
                case 3:
                    stageName = "corner towers";
                    if (_stages.CornerTowers)
                        CastleTowerAuthoring.AuthorCornerTowers(_authoring, in _plan,
                            in _components.CornerTowers);
                    break;
                case 4:
                    stageName = "gatehouse";
                    if (_stages.Gatehouse)
                        CastleGatehouseAuthoring.Author(_authoring, in _plan,
                            in _components.Gatehouse);
                    break;
                case 5:
                    stageName = "courtyard";
                    if (_stages.Courtyard)
                        CastleCourtyardAuthoring.Author(_authoring, in _plan,
                            in _components.Courtyard, in _components.Palette);
                    break;
                case 6:
                    stageName = $"keep {_keepStage + 1}";
                    if (_stages.Keep && !StepKeep())
                    {
                        RequireBudget(stageName);
                        return false;
                    }
                    break;
                case 7:
                    stageName = "dungeon";
                    if (_stages.Dungeon)
                        CastleDungeonAuthoring.Author(_authoring, in _plan);
                    break;
                case 8:
                    stageName = "landscape details";
                    if (_stages.Landscape)
                        CastleLandscapeAuthoring.Author(_authoring, in _plan, _terrainSeed);
                    break;
                default:
                    return true;
            }

            RequireBudget(stageName);
            _stage++;
            return IsComplete;
        }

        private void Initialize(IStructureAuthoringSession authoring, in CastlePlan plan,
            in CastleComponentConfig components, in CastleCurtainConfig curtain,
            in CastleBuildStageConfig stages, uint terrainSeed)
        {
            _authoring = authoring ?? throw new System.ArgumentNullException(nameof(authoring));
            if (!components.IsWellFormed)
                throw new System.ArgumentException(
                    "Castle authoring refused: shared castle component configuration is invalid.",
                    nameof(components));
            if (!curtain.IsWellFormed)
                throw new System.ArgumentException(
                    "Castle authoring refused: curtain configuration is invalid.", nameof(curtain));

            _plan = plan;
            _plan.KeepHalfX = components.KeepWidth / 2;
            _plan.KeepHalfZ = components.KeepDepth / 2;
            _plan.KeepHeight = components.KeepHeight;
            _plan.Floors = components.KeepFloors.FloorCount;
            _plan.FloorHeight = components.KeepFloors.LevelHeight;
            if (curtain.Layout == CastleCurtainLayoutKind.Rectangular)
            {
                _plan.BaileyHalfX = curtain.RectangularHalfExtents.x;
                _plan.BaileyHalfZ = curtain.RectangularHalfExtents.y;
                _plan.WallHeight = curtain.Height;
                _plan.WallThickness = curtain.Thickness;
            }

            _components = components;
            _curtain = curtain;
            _stages = stages;
            _terrainSeed = terrainSeed;
            _stage = 1;
            _keepStage = 0;

            long estimate = CastlePlanner.EstimateWrites(in _plan);
            if (estimate > authoring.WriteBudget)
                throw new System.InvalidOperationException(
                    $"Castle authoring refused: plan implies ~{estimate:N0} expensive-write " +
                    $"equivalents, budget is {authoring.WriteBudget:N0}. Reduce PlateauRadius " +
                    $"({plan.PlateauRadius}) or the primary structure dimensions before retrying.");
        }

        private bool StepKeep()
        {
            int baseY = _plan.Centre.y + _plan.PlateauHeight;
            int3 min = CastleKeepCoreAuthoring.Minimum(in _plan);
            int3 size = CastleKeepCoreAuthoring.Size(in _plan);
            switch (_keepStage)
            {
                case 0:
                    CastleKeepCoreAuthoring.AuthorShell(_authoring, in _plan,
                        in _components.KeepFoundation, _components.KeepFoundationTopOffset,
                        in _components.KeepWalls, in _components.Palette);
                    break;
                case 1:
                    CastleKeepCoreAuthoring.AuthorCornerTurrets(_authoring, in _plan);
                    break;
                case 2:
                    for (int floor = 0; floor < _components.KeepFloors.FloorCount; floor++)
                    {
                        int y = baseY + floor * _components.KeepFloors.LevelHeight;
                        if (floor > 0)
                            _authoring.Box(
                                new int3(min.x + _components.KeepWalls.Thickness, y,
                                    min.z + _components.KeepWalls.Thickness),
                                new int3(size.x - 2 * _components.KeepWalls.Thickness,
                                    _components.KeepFloors.SlabThickness,
                                    size.z - 2 * _components.KeepWalls.Thickness),
                                _components.Palette.Resolve(_components.KeepFloors.SlabMaterialRole));

                        if (floor == 1)
                            CastleProceduralBedroomAuthoring.Author(_authoring, in _plan, min, size, y);
                        else
                            CastleKeepRoomAuthoring.AuthorFloor(_authoring, in _plan, min, size, y, floor);
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
