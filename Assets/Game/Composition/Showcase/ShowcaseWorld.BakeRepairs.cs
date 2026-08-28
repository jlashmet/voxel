using System;
using Game.Structures.Runtime;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;

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

            CastleLowerRiverWaterRepair.Repair(authoring, in _castlePlan);
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
