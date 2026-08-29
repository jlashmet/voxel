using System;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Shared spatial semantics for presenting an underground landmark from its cavern approach.
    /// Callers supply authored bounds and facing; no showcase coordinates are encoded here.
    /// </summary>
    public static class UndergroundCavernDestinationLayout
    {
        public static int3 ResolveRuinApproach(
            in DecorationBounds cavern,
            in DecorationBounds ruin,
            Facing facing)
        {
            if (!cavern.IsWellFormed || !ruin.IsWellFormed)
                throw new ArgumentException("Ruin approach requires valid cavern and ruin bounds.");

            int3 forward = FacingVector(facing);
            int3 cavernCentre = CentreOf(in cavern);
            int3 ruinCentre = CentreOf(in ruin);
            bool alongX = math.abs(forward.x) == 1;
            int forwardSize = alongX ? ruin.Size.x : ruin.Size.z;
            int sideSize = alongX ? ruin.Size.z : ruin.Size.x;

            int3 front = ruinCentre - forward * (forwardSize / 2);
            // Keep enough setback to read the full facade/statue pair in ordinary gameplay while
            // ensuring the final waypoint does not retreat behind the destination cavern centre.
            int viewingClearance = math.max(48, sideSize * 2 / 3);
            int3 approach = front - forward * viewingClearance;
            int forwardFromCavernCentre = math.dot(approach - cavernCentre, forward);
            if (forwardFromCavernCentre < 0)
                approach = cavernCentre;
            approach.y = ruin.Min.y;
            return approach;
        }

        public static bool IsRuinAtFarEnd(
            in DecorationBounds cavern,
            in DecorationBounds ruin,
            Facing facing)
        {
            if (!cavern.IsWellFormed || !ruin.IsWellFormed) return false;
            int3 forward = FacingVector(facing);
            int3 delta = CentreOf(in ruin) - CentreOf(in cavern);
            bool alongX = math.abs(forward.x) == 1;
            int cavernHalfExtent = (alongX ? cavern.Size.x : cavern.Size.z) / 2;
            int forwardDistance = math.dot(delta, forward);
            return forwardDistance >= cavernHalfExtent * 2 / 3;
        }

        private static int3 CentreOf(in DecorationBounds bounds) =>
            new int3(
                (bounds.Min.x + bounds.MaxExclusive.x) / 2,
                (bounds.Min.y + bounds.MaxExclusive.y) / 2,
                (bounds.Min.z + bounds.MaxExclusive.z) / 2);

        private static int3 FacingVector(Facing facing)
        {
            switch (facing)
            {
                case Facing.East: return new int3(1, 0, 0);
                case Facing.South: return new int3(0, 0, -1);
                case Facing.West: return new int3(-1, 0, 0);
                default: return new int3(0, 0, 1);
            }
        }
    }
}
