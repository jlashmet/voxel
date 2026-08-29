using System;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Reasserts the normal-player corridor after optional destination finish passes. This is kept
    /// separate from any one ruin style so additional cavern shells, facade/foundation detail, or
    /// formations cannot silently invalidate the authoritative route that selected the destination.
    /// </summary>
    public static class UndergroundCavernCirculationProtection
    {
        public static long Reassert(
            IStructureAuthoringSession authoring,
            in DecorationBounds cavernBounds,
            in DecorationBounds ruinBounds,
            Facing facing,
            int width = 20,
            int clearanceHeight = 32)
        {
            if (authoring == null) throw new ArgumentNullException(nameof(authoring));
            if (!cavernBounds.IsWellFormed)
                throw new ArgumentException("A valid cavern bound is required.", nameof(cavernBounds));
            if (!ruinBounds.IsWellFormed)
                throw new ArgumentException("A valid ruin bound is required.", nameof(ruinBounds));
            if (cavernBounds.Min.y != ruinBounds.Min.y)
                throw new ArgumentException("Cavern and ruin circulation must share one destination floor.", nameof(ruinBounds));
            if (width < 12) throw new ArgumentOutOfRangeException(nameof(width));
            if (clearanceHeight < 24) throw new ArgumentOutOfRangeException(nameof(clearanceHeight));

            long startWrites = authoring.TotalVoxelsWritten;
            int3 forward = FacingVector(facing);
            int3 cavernCentre = CentreOf(in cavernBounds);
            int3 ruinCentre = CentreOf(in ruinBounds);
            bool alongX = math.abs(forward.x) == 1;
            int cavernDepth = alongX ? cavernBounds.Size.x : cavernBounds.Size.z;
            int ruinDepth = alongX ? ruinBounds.Size.x : ruinBounds.Size.z;

            // Finish lobes are intentionally allowed to overlap the destination shell. Start the
            // final carve behind the cavern's rear bound with a width-derived overlap so a lobe
            // cannot refill the last primary-route span immediately before the cavern reveal.
            int rearOverlap = math.max(24, width + width / 2);
            int3 start = cavernCentre - forward * (cavernDepth / 2 + rearOverlap);
            int3 end = ruinCentre + forward * (ruinDepth / 3);
            int halfWidth = width / 2;
            int minX = math.min(start.x, end.x) - halfWidth;
            int minZ = math.min(start.z, end.z) - halfWidth;
            int sizeX = math.abs(end.x - start.x) + width;
            int sizeZ = math.abs(end.z - start.z) + width;

            authoring.Carve(
                new int3(minX, ruinBounds.Min.y, minZ),
                new int3(sizeX, clearanceHeight, sizeZ));

            return authoring.TotalVoxelsWritten - startWrites;
        }

        private static int3 CentreOf(in DecorationBounds bounds) => new int3(
            bounds.Min.x + bounds.Size.x / 2,
            bounds.Min.y,
            bounds.Min.z + bounds.Size.z / 2);

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
