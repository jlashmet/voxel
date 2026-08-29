using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Reasserts the normal-player corridor after optional destination finish passes. This is kept
    /// separate from any one ruin style so additional facade/foundation detail cannot silently
    /// invalidate the authoritative route that selected the destination in the first place.
    /// </summary>
    public static class UndergroundCavernCirculationProtection
    {
        public static long Reassert(
            IStructureAuthoringSession authoring,
            in DecorationBounds ruinBounds,
            Facing facing,
            int width = 20,
            int clearanceHeight = 32)
        {
            if (authoring == null) throw new ArgumentNullException(nameof(authoring));
            if (!ruinBounds.IsWellFormed) throw new ArgumentException("A valid ruin bound is required.", nameof(ruinBounds));
            if (width < 12) throw new ArgumentOutOfRangeException(nameof(width));
            if (clearanceHeight < 24) throw new ArgumentOutOfRangeException(nameof(clearanceHeight));

            long startWrites = authoring.TotalVoxelsWritten;
            int3 forward = FacingVector(facing);
            int3 centre = new int3(
                (ruinBounds.Min.x + ruinBounds.MaxExclusive.x) / 2,
                ruinBounds.Min.y,
                (ruinBounds.Min.z + ruinBounds.MaxExclusive.z) / 2);
            bool alongX = math.abs(forward.x) == 1;
            int depth = alongX
                ? ruinBounds.MaxExclusive.x - ruinBounds.Min.x
                : ruinBounds.MaxExclusive.z - ruinBounds.Min.z;

            int3 start = centre - forward * (depth / 2 + 42);
            int3 end = centre + forward * (depth / 3);
            int3 midpoint = new int3(
                (start.x + end.x) / 2,
                ruinBounds.Min.y,
                (start.z + end.z) / 2);
            int length = math.abs(end.x - start.x) + math.abs(end.z - start.z) + 1;

            if (alongX)
                authoring.Carve(
                    new int3(midpoint.x - length / 2, ruinBounds.Min.y, midpoint.z - width / 2),
                    new int3(length, clearanceHeight, width));
            else
                authoring.Carve(
                    new int3(midpoint.x - width / 2, ruinBounds.Min.y, midpoint.z - length / 2),
                    new int3(width, clearanceHeight, length));

            return authoring.TotalVoxelsWritten - startWrites;
        }

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
