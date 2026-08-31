using System;
using Game.Structures.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// Published synchronously from the existing landmark planning flow, immediately after
        /// CastlePlan is created and before any castle voxel region is required to be resident.
        /// Scene presentation may subscribe to build lightweight far-world metadata; gameplay and
        /// physical world ownership remain here.
        /// </summary>
        public static event Action<ShowcaseWorld, CastlePlan> CastlePlanned;

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

        private void PublishCastlePlanned(in CastlePlan plan) => CastlePlanned?.Invoke(this, plan);
    }
}
