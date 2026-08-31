using Game.Structures.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// Returns the exact deterministic castle plan already owned by this world as soon as
        /// landmark planning has completed. This is a read-only semantic handoff for presentation;
        /// callers must not re-plan the castle or infer its existence from voxel residency.
        /// </summary>
        public bool TryGetPlannedCastle(out CastlePlan plan)
        {
            if (_castleTerrainQueued)
            {
                plan = _hasCastlePlan ? _castlePlan : _pendingCastlePlan;
                return true;
            }

            plan = default;
            return false;
        }
    }
}
