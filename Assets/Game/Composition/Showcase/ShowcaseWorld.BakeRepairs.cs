using System;
using Game.Structures.Runtime;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;
using GameCastlePlan = Game.Structures.Api.CastlePlan;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        // Bounded compatibility work for startup images created before the lower-river receiving
        // bank correction. This remains far below the cost of procedural castle generation and is
        // paid once when the baked world is installed, never per frame.
        private const int BakedCastleSemanticRepairWriteBudget = 1_500_000;

        private void ApplyBakedCastleSemanticRepairs()
        {
            IStructureAuthoringSession authoring = StructuresComposition.CreateAuthoringSession(
                ReadStorage,
                MutationStorage,
                _storage.MaterialAuthoring,
                BakedCastleSemanticRepairWriteBudget);

            // The showcase retains a compatibility wrapper around the game-owned plan. Convert
            // through a local value so the reusable game authoring helper receives its canonical
            // plan type by readonly reference rather than leaking the wrapper across the boundary.
            GameCastlePlan gamePlan = _castlePlan.Value;
            CastleLowerRiverWaterRepair.Repair(authoring, in gamePlan);
            if (authoring.BudgetExceeded)
                throw new InvalidOperationException(
                    $"Baked showcase lower-river repair exceeded its " +
                    $"{BakedCastleSemanticRepairWriteBudget:N0}-write budget after " +
                    $"{authoring.TotalVoxelsWritten:N0} changed voxels. Re-bake the showcase world.");

            // The bake restore published its snapshots before this compatibility mutation. Publish
            // the finished resident state once more so rendering/collision observe the repaired
            // water immediately rather than waiting for an unrelated edit to dirty those regions.
            _storage.PublishAllResidentRegions();
        }
    }
}
