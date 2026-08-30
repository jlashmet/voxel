using System;
using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Describes the reusable rounded clearance sweep used to reconnect a finished destination to
    /// its authored approach. Keeping the plan explicit lets tests lock the visual-safe geometry
    /// contract without depending on showcase coordinates or renderer state.
    /// </summary>
    public readonly struct UndergroundCavernCirculationPlan
    {
        public readonly int3 Start;
        public readonly int3 End;
        public readonly int FloorY;
        public readonly int Radius;
        public readonly int ClearanceHeight;
        public readonly int Spacing;
        public readonly int NodeCount;

        public UndergroundCavernCirculationPlan(
            int3 start,
            int3 end,
            int floorY,
            int radius,
            int clearanceHeight,
            int spacing,
            int nodeCount)
        {
            Start = start;
            End = end;
            FloorY = floorY;
            Radius = radius;
            ClearanceHeight = clearanceHeight;
            Spacing = spacing;
            NodeCount = nodeCount;
        }

        public bool IsWellFormed =>
            Radius >= 7 && ClearanceHeight >= 24 && Spacing >= 4 && Spacing < Radius && NodeCount >= 2;
    }

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
            UndergroundCavernCirculationPlan plan = ResolvePlan(
                in cavernBounds,
                in ruinBounds,
                facing,
                width,
                clearanceHeight);

            long startWrites = authoring.TotalVoxelsWritten;
            AuthorRoundedSweep(authoring, in plan);
            return authoring.TotalVoxelsWritten - startWrites;
        }

        public static UndergroundCavernCirculationPlan ResolvePlan(
            in DecorationBounds cavernBounds,
            in DecorationBounds ruinBounds,
            Facing facing,
            int width = 20,
            int clearanceHeight = 32)
        {
            if (!cavernBounds.IsWellFormed)
                throw new ArgumentException("A valid cavern bound is required.", nameof(cavernBounds));
            if (!ruinBounds.IsWellFormed)
                throw new ArgumentException("A valid ruin bound is required.", nameof(ruinBounds));
            if (cavernBounds.Min.y != ruinBounds.Min.y)
                throw new ArgumentException("Cavern and ruin circulation must share one destination floor.", nameof(ruinBounds));
            if (width < 12) throw new ArgumentOutOfRangeException(nameof(width));
            if (clearanceHeight < 24) throw new ArgumentOutOfRangeException(nameof(clearanceHeight));

            int3 forward = FacingVector(facing);
            int3 cavernCentre = CentreOf(in cavernBounds);
            int3 ruinCentre = CentreOf(in ruinBounds);
            bool alongX = math.abs(forward.x) == 1;
            int cavernDepth = alongX ? cavernBounds.Size.x : cavernBounds.Size.z;
            int ruinDepth = alongX ? ruinBounds.Size.x : ruinBounds.Size.z;

            // Finish lobes are intentionally allowed to overlap the destination shell. Start the
            // final sweep behind the cavern's rear bound with a width-derived overlap so a lobe
            // cannot refill the last primary-route span immediately before the cavern reveal.
            int rearOverlap = math.max(24, width + width / 2);
            int3 start = cavernCentre - forward * (cavernDepth / 2 + rearOverlap);
            int3 end = ruinCentre + forward * (ruinDepth / 3);

            // Radius exceeds the old half-width slightly; overlapping nodes therefore retain at
            // least the previous guaranteed gameplay width between samples. The authoring pass now
            // uses rounded vault slices rather than vertical cylinders so this safety sweep cannot
            // restore the planar walls/caps removed by cavern visual finishing.
            int radius = math.max(7, width / 2 + 2);
            int spacing = math.max(4, radius - 3);
            int length = HorizontalCardinalDistance(start, end);
            int nodeCount = math.max(2, (length + spacing - 1) / spacing + 1);
            return new UndergroundCavernCirculationPlan(
                start,
                end,
                ruinBounds.Min.y,
                radius,
                clearanceHeight,
                spacing,
                nodeCount);
        }

        private static void AuthorRoundedSweep(
            IStructureAuthoringSession authoring,
            in UndergroundCavernCirculationPlan plan)
        {
            if (!plan.IsWellFormed)
                throw new ArgumentException("Rounded cavern circulation requires a valid plan.", nameof(plan));

            int3 delta = plan.End - plan.Start;
            if (delta.y != 0 || (delta.x != 0 && delta.z != 0))
                throw new ArgumentException("Rounded cavern circulation requires a horizontal cardinal sweep.", nameof(plan));

            int length = math.max(math.abs(delta.x), math.abs(delta.z));
            int3 direction = new int3(math.sign(delta.x), 0, math.sign(delta.z));
            for (int node = 0; node < plan.NodeCount; node++)
            {
                int distance = math.min(length, node * plan.Spacing);
                int3 centre = plan.Start + direction * distance;
                UndergroundCavernRouteNaturalization.AuthorRoundedVault(
                    authoring,
                    centre.x,
                    plan.FloorY,
                    centre.z,
                    plan.Radius,
                    plan.ClearanceHeight,
                    3 + (node & 1),
                    2 + (node % 3),
                    (node % 5) - 2,
                    GameMaterialIds.Empty);
            }
        }

        private static int HorizontalCardinalDistance(int3 start, int3 end)
        {
            int dx = math.abs(end.x - start.x);
            int dz = math.abs(end.z - start.z);
            if (dx != 0 && dz != 0)
                throw new ArgumentException("Cavern circulation endpoints must share a cardinal axis.");
            return math.max(dx, dz);
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
