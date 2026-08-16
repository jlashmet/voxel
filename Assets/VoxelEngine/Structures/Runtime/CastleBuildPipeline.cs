using System;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Incremental realization pipeline for a planned castle.
    ///
    /// New stages move here one at a time. Until each legacy stage has been extracted, stages
    /// that have not migrated are delegated to CastleBuilder so the refactor remains output-stable.
    /// </summary>
    public sealed class CastleBuildPipeline
    {
        private CastleBuilder.IncrementalBuild _legacy;
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

            var brush = new VoxelBrush(reads, mutations, materials);
            CastleBuildPreflightResult preflight = CastleBuildPreflight.Evaluate(
                in plan, brush.WriteBudget);

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

            _legacy = new CastleBuilder.IncrementalBuild
            {
                Brush = brush,
                Plan = plan,
                TerrainSeed = terrainSeed,
                Stage = 1,
            };
            _site = default;
        }

        public bool IsComplete => _legacy.IsComplete;
        public int StageNumber => _legacy.StageNumber;
        public long TotalVoxelsWritten => _legacy.TotalVoxelsWritten;

        /// <summary>Executes one bounded unit of the current semantic stage.</summary>
        public bool Step()
        {
            if (IsComplete) return true;

            switch (_legacy.Stage)
            {
                case 1:
                    if (!CastleSiteRealizer.Step(
                            ref _legacy.Brush,
                            in _legacy.Plan,
                            _legacy.TerrainSeed,
                            ref _site))
                    {
                        RequireBudget("site");
                        return false;
                    }
                    return CompleteStage("site");

                case 2:
                    CastleFortificationRealizer.CurtainWalls(
                        ref _legacy.Brush, in _legacy.Plan);
                    return CompleteStage("curtain walls");

                case 3:
                    CastleFortificationRealizer.CornerTowers(
                        ref _legacy.Brush, in _legacy.Plan);
                    return CompleteStage("corner towers");

                case 4:
                    CastleFortificationRealizer.Gatehouse(
                        ref _legacy.Brush, in _legacy.Plan);
                    return CompleteStage("gatehouse");

                case 5:
                    CastleCourtyardRealizer.Build(
                        ref _legacy.Brush, in _legacy.Plan);
                    return CompleteStage("courtyard");

                default:
                    // Keep/interiors, dungeon, and landscape dressing still use the legacy
                    // implementation. Each extraction removes another case from here.
                    return CastleBuilder.StepBuild(ref _legacy);
            }
        }

        private bool CompleteStage(string stage)
        {
            RequireBudget(stage);
            _legacy.Stage++;
            return IsComplete;
        }

        private void RequireBudget(string stage)
        {
            if (!_legacy.Brush.BudgetExceeded) return;

            throw new InvalidOperationException(
                $"Castle build exceeded its {_legacy.Brush.WriteBudget:N0}-write budget while " +
                $"building the {stage}, after {_legacy.Brush.TotalVoxelsWritten:N0} changed voxels. " +
                "A partial castle is invalid.");
        }
    }
}
