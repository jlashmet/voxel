using System;
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

        public CastleBuildPipeline(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            in CastlePlan plan,
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

            _plan = plan;
            _terrainSeed = terrainSeed;
            _stage = 1;
            _keepStage = 0;
            _site = default;
        }

        public bool IsComplete => _stage > 8;
        public int StageNumber => _stage;
        public long TotalVoxelsWritten => _brush.TotalVoxelsWritten;

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
                    CastleFortificationRealizer.CurtainWalls(ref _brush, in _plan);
                    return CompleteStage("curtain walls");

                case 3:
                    CastleFortificationRealizer.CornerTowers(ref _brush, in _plan);
                    return CompleteStage("corner towers");

                case 4:
                    CastleFortificationRealizer.Gatehouse(ref _brush, in _plan);
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
