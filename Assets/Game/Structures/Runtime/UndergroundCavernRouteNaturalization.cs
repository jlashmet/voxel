using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Reusable finish pass for long guaranteed-clearance cave routes. The generic cave core keeps
    /// collision/traversal conservative; this pass overlaps rounded voids along that same grade so
    /// the presented walls and ceiling no longer read as one continuous rectangular service tunnel.
    /// Dogleg host windows are deliberately skipped because their curved bypass owns traversal there.
    /// </summary>
    public readonly struct UndergroundCavernRouteNaturalizationResult
    {
        public readonly int NodeCount;
        public readonly long VoxelsWritten;

        public UndergroundCavernRouteNaturalizationResult(int nodeCount, long voxelsWritten)
        {
            NodeCount = nodeCount;
            VoxelsWritten = voxelsWritten;
        }

        public bool IsWellFormed => NodeCount >= 24 && VoxelsWritten > 0;
    }

    public static class UndergroundCavernRouteNaturalization
    {
        private const ulong NaturalizationSalt = 0x4E41545552414Cul; // NATURAL

        public static UndergroundCavernRouteNaturalizationResult Author(
            IStructureAuthoringSession authoring,
            in CaveGenerationRequest request,
            in CaveConfig cave,
            in CaveMaterialPalette palette,
            in UndergroundCavernTraversalProfile profile)
        {
            if (authoring == null) throw new ArgumentNullException(nameof(authoring));
            if (!request.IsWellFormed || !cave.IsWellFormed || !profile.IsWellFormed)
                throw new ArgumentException("Route naturalization requires valid cave and traversal configuration.");

            int[] bendSegments = profile.ResolveBendSegments(in cave);
            int maxDoglegForward = 0;
            for (int i = 0; i < profile.BendForwardOffsets.Length; i++)
                maxDoglegForward = math.max(maxDoglegForward, math.abs(profile.BendForwardOffsets[i]));
            int doglegHalfWindow = maxDoglegForward + profile.BendRadius + 8;

            int spacing = profile.ResolvedNaturalizationSpacing;
            int baseRadius = math.max(
                profile.ResolvedNaturalizationRadius,
                cave.TunnelWidth / 2 + 3);
            int radiusVariation = profile.ResolvedNaturalizationRadiusVariation;
            int heightVariation = profile.ResolvedNaturalizationHeightVariation;
            int lateralJitter = profile.ResolvedNaturalizationLateralJitter;
            int totalDistance = cave.MainSegmentCount * cave.SegmentLength;
            int3 forward = FacingVector(request.Entrance.Facing);
            int3 side = new int3(-forward.z, 0, forward.x);

            long startWrites = authoring.TotalVoxelsWritten;
            int nodes = 0;
            for (int distance = spacing; distance < totalDistance - spacing; distance += spacing)
            {
                if (InsideDoglegWindow(distance, bendSegments, cave.SegmentLength, doglegHalfWindow))
                    continue;

                ulong state = FeatureHash.Mix(
                    request.Seed ^ NaturalizationSalt ^ ((ulong)(uint)distance * 0x9E3779B9ul));
                int radius = baseRadius;
                if (radiusVariation > 0)
                    radius += FeatureHash.Range(ref state, 0, radiusVariation * 2 + 1) - radiusVariation;
                radius = math.max(cave.TunnelWidth / 2 + 2, radius);

                int lateral = lateralJitter == 0
                    ? 0
                    : FeatureHash.Range(ref state, 0, lateralJitter * 2 + 1) - lateralJitter;
                int heightExtra = heightVariation == 0
                    ? 0
                    : FeatureHash.Range(ref state, 0, heightVariation + 1);
                int floorY = FloorAtPrimaryDistance(in request, in cave, distance);
                int3 centre = request.EntranceWorldPosition
                              + forward * (request.Entrance.ClearanceLength + distance)
                              + side * lateral;
                centre.y = floorY;

                int height = cave.TunnelHeight + cave.CeilingRoughness + 5 + heightExtra;
                authoring.Cylinder(centre.x, floorY, centre.z, radius, height, palette.Opening);
                authoring.Disc(centre.x, floorY - 1, centre.z, math.max(6, radius - 2), palette.Rock);
                nodes++;
            }

            return new UndergroundCavernRouteNaturalizationResult(
                nodes,
                authoring.TotalVoxelsWritten - startWrites);
        }

        private static bool InsideDoglegWindow(
            int distance,
            int[] bendSegments,
            int segmentLength,
            int halfWindow)
        {
            for (int i = 0; i < bendSegments.Length; i++)
            {
                int bendDistance = (bendSegments[i] + 1) * segmentLength;
                if (math.abs(distance - bendDistance) <= halfWindow)
                    return true;
            }
            return false;
        }

        private static int FloorAtPrimaryDistance(
            in CaveGenerationRequest request,
            in CaveConfig cave,
            int distance)
        {
            distance = math.clamp(distance, 0, cave.MainSegmentCount * cave.SegmentLength);
            int completedSegments = distance / cave.SegmentLength;
            int remainder = distance % cave.SegmentLength;
            int startY = FloorAfterCompletedSegments(in request, in cave, completedSegments);
            if (remainder == 0 || completedSegments >= cave.MainSegmentCount)
                return startY;

            int endY = FloorAfterCompletedSegments(in request, in cave, completedSegments + 1);
            return startY + (endY - startY) * remainder / cave.SegmentLength;
        }

        private static int FloorAfterCompletedSegments(
            in CaveGenerationRequest request,
            in CaveConfig cave,
            int completedSegments)
        {
            int descendedSegments = math.min(
                math.clamp(completedSegments, 0, cave.MainSegmentCount),
                cave.SurfaceDescentSegments);
            return math.max(
                request.EntranceWorldPosition.y - descendedSegments * cave.SurfaceDescentPerSegment,
                request.Origin.y + cave.MinVerticalOffset);
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