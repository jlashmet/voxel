using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Reusable finish pass for long guaranteed-clearance cave routes. The generic cave core keeps
    /// collision/traversal conservative; this pass overlaps a deterministic irregular lobe sweep
    /// along that same grade so the presented walls and ceiling do not repeat one vertical-cylinder
    /// cadence. Dogleg host windows remain under the traversal enhancement's ownership.
    /// </summary>
    public readonly struct UndergroundCavernRouteNaturalizationResult
    {
        public readonly int NodeCount;
        public readonly int LobeCount;
        public readonly int SpacingVariantCount;
        public readonly int SideSwitchCount;
        public readonly int CeilingVariantCount;
        public readonly long VoxelsWritten;

        public UndergroundCavernRouteNaturalizationResult(
            int nodeCount,
            int lobeCount,
            int spacingVariantCount,
            int sideSwitchCount,
            int ceilingVariantCount,
            long voxelsWritten)
        {
            NodeCount = nodeCount;
            LobeCount = lobeCount;
            SpacingVariantCount = spacingVariantCount;
            SideSwitchCount = sideSwitchCount;
            CeilingVariantCount = ceilingVariantCount;
            VoxelsWritten = voxelsWritten;
        }

        public bool IsWellFormed =>
            NodeCount >= 24 &&
            LobeCount >= NodeCount * 3 &&
            SpacingVariantCount >= 3 &&
            SideSwitchCount >= 4 &&
            CeilingVariantCount >= 4 &&
            VoxelsWritten > 0;
    }

    public readonly struct UndergroundCavernNaturalizationNode
    {
        public readonly int Distance;
        public readonly int StepToNext;
        public readonly int PrimaryRadius;
        public readonly int PrimaryHeight;
        public readonly int DominantSide;
        public readonly int SideOffset;
        public readonly int SideBaseOffset;
        public readonly int SideRadius;
        public readonly int SideHeight;
        public readonly int UpperOffset;
        public readonly int UpperBaseOffset;
        public readonly int UpperRadius;
        public readonly int UpperHeight;

        public UndergroundCavernNaturalizationNode(
            int distance,
            int stepToNext,
            int primaryRadius,
            int primaryHeight,
            int dominantSide,
            int sideOffset,
            int sideBaseOffset,
            int sideRadius,
            int sideHeight,
            int upperOffset,
            int upperBaseOffset,
            int upperRadius,
            int upperHeight)
        {
            Distance = distance;
            StepToNext = stepToNext;
            PrimaryRadius = primaryRadius;
            PrimaryHeight = primaryHeight;
            DominantSide = dominantSide;
            SideOffset = sideOffset;
            SideBaseOffset = sideBaseOffset;
            SideRadius = sideRadius;
            SideHeight = sideHeight;
            UpperOffset = upperOffset;
            UpperBaseOffset = upperBaseOffset;
            UpperRadius = upperRadius;
            UpperHeight = upperHeight;
        }

        public bool IsWellFormed =>
            Distance > 0 && StepToNext >= 8 && PrimaryRadius >= 8 && PrimaryHeight >= 12 &&
            (DominantSide == -1 || DominantSide == 1) && SideOffset >= 3 && SideBaseOffset >= 1 &&
            SideRadius >= 8 && SideHeight >= 10 && UpperOffset >= 2 && UpperBaseOffset >= 4 &&
            UpperRadius >= 8 && UpperHeight >= 10;
    }

    public static class UndergroundCavernRouteNaturalization
    {
        private const ulong NaturalizationSalt = 0x4E41545552414Cul; // NATURAL
        private const ulong StepSalt = 0x5354455056415259ul; // STEPVARY
        private const ulong ShapeSalt = 0x5348415045564152ul; // SHAPEVAR

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

            UndergroundCavernNaturalizationNode[] plan = ResolvePlan(
                in request, in cave, in profile);
            int3 forward = FacingVector(request.Entrance.Facing);
            int3 side = new int3(-forward.z, 0, forward.x);

            long startWrites = authoring.TotalVoxelsWritten;
            int previousSide = 0;
            int sideSwitches = 0;
            var spacingSeen = new bool[65];
            var ceilingSeen = new bool[65];

            for (int i = 0; i < plan.Length; i++)
            {
                UndergroundCavernNaturalizationNode node = plan[i];
                int floorY = FloorAtPrimaryDistance(in request, in cave, node.Distance);
                int3 centre = request.EntranceWorldPosition
                              + forward * (request.Entrance.ClearanceLength + node.Distance);
                centre.y = floorY;

                // The centred node owns guaranteed route clearance. The two smaller offset lobes
                // deliberately begin above the floor so they can dominate the visible walls/ceiling
                // without eroding the stable walkable floor produced by the generic cave core.
                authoring.Cylinder(
                    centre.x, floorY, centre.z,
                    node.PrimaryRadius, node.PrimaryHeight, palette.Opening);
                authoring.Disc(
                    centre.x, floorY - 1, centre.z,
                    math.max(6, node.PrimaryRadius - 2), palette.Rock);

                int3 sideCentre = centre + side * (node.DominantSide * node.SideOffset);
                authoring.Cylinder(
                    sideCentre.x,
                    floorY + node.SideBaseOffset,
                    sideCentre.z,
                    node.SideRadius,
                    node.SideHeight,
                    palette.Opening);

                int3 upperCentre = centre - side * (node.DominantSide * node.UpperOffset);
                authoring.Cylinder(
                    upperCentre.x,
                    floorY + node.UpperBaseOffset,
                    upperCentre.z,
                    node.UpperRadius,
                    node.UpperHeight,
                    palette.Opening);

                if (previousSide != 0 && previousSide != node.DominantSide)
                    sideSwitches++;
                previousSide = node.DominantSide;
                spacingSeen[math.clamp(node.StepToNext, 0, spacingSeen.Length - 1)] = true;
                int ceilingKey = math.clamp(
                    node.UpperBaseOffset + node.UpperHeight - cave.TunnelHeight,
                    0,
                    ceilingSeen.Length - 1);
                ceilingSeen[ceilingKey] = true;
            }

            return new UndergroundCavernRouteNaturalizationResult(
                plan.Length,
                plan.Length * 3,
                CountTrue(spacingSeen),
                sideSwitches,
                CountTrue(ceilingSeen),
                authoring.TotalVoxelsWritten - startWrites);
        }

        public static UndergroundCavernNaturalizationNode[] ResolvePlan(
            in CaveGenerationRequest request,
            in CaveConfig cave,
            in UndergroundCavernTraversalProfile profile)
        {
            if (!request.IsWellFormed || !cave.IsWellFormed || !profile.IsWellFormed)
                throw new ArgumentException("Route naturalization requires valid cave and traversal configuration.");

            int[] bendSegments = profile.ResolveBendSegments(in cave);
            int maxDoglegForward = 0;
            for (int i = 0; i < profile.BendForwardOffsets.Length; i++)
                maxDoglegForward = math.max(maxDoglegForward, math.abs(profile.BendForwardOffsets[i]));
            int doglegHalfWindow = maxDoglegForward + profile.BendRadius + 8;

            int spacing = profile.ResolvedNaturalizationSpacing;
            int spacingVariation = math.max(2, math.min(6, spacing / 3));
            int baseRadius = math.max(
                profile.ResolvedNaturalizationRadius,
                cave.TunnelWidth / 2 + 3);
            int radiusVariation = profile.ResolvedNaturalizationRadiusVariation;
            int heightVariation = profile.ResolvedNaturalizationHeightVariation;
            int lateralJitter = profile.ResolvedNaturalizationLateralJitter;
            int totalDistance = cave.MainSegmentCount * cave.SegmentLength;

            // Maximum possible samples at the minimum supported 8-voxel step, with enough room for
            // legacy profiles. The final returned array is trimmed to authored nodes only.
            var nodes = new UndergroundCavernNaturalizationNode[math.max(1, totalDistance / 8 + 1)];
            int count = 0;
            int distance = spacing;
            int ordinal = 0;
            while (distance < totalDistance - spacing)
            {
                ulong stepState = FeatureHash.Mix(
                    request.Seed ^ NaturalizationSalt ^ StepSalt ^
                    ((ulong)(uint)(ordinal + 1) * 0x9E3779B97F4A7C15ul));
                int step = spacing + FeatureHash.Range(
                    ref stepState, -spacingVariation, spacingVariation + 1);
                step = math.clamp(step, 8, 32);

                if (!InsideDoglegWindow(distance, bendSegments, cave.SegmentLength, doglegHalfWindow))
                {
                    ulong state = FeatureHash.Mix(
                        request.Seed ^ NaturalizationSalt ^ ShapeSalt ^
                        ((ulong)(uint)(distance + 1) * 0xD6E8FEB86659FD93ul));
                    int radiusDelta = radiusVariation == 0
                        ? 0
                        : FeatureHash.Range(ref state, -radiusVariation, radiusVariation + 1);
                    int primaryRadius = math.max(cave.TunnelWidth / 2 + 2, baseRadius + radiusDelta);
                    int heightExtra = heightVariation == 0
                        ? 0
                        : FeatureHash.Range(ref state, 0, heightVariation + 1);
                    int primaryHeight = cave.TunnelHeight + cave.CeilingRoughness + 5 + heightExtra;

                    int dominantSide = (FeatureHash.Next(ref state) & 1ul) == 0ul ? -1 : 1;
                    int sideOffsetBase = math.max(4, lateralJitter / 2 + 2);
                    int sideOffsetExtra = lateralJitter <= 1
                        ? 0
                        : FeatureHash.Range(ref state, 0, lateralJitter);
                    int sideOffset = sideOffsetBase + sideOffsetExtra;
                    int sideBaseOffset = 1 + FeatureHash.Range(ref state, 0, 5);
                    int sideRadius = math.max(
                        8,
                        primaryRadius - 3 + FeatureHash.Range(ref state, -2, 3));
                    int sideHeightExtra = heightVariation == 0
                        ? 0
                        : FeatureHash.Range(ref state, 0, math.max(2, heightVariation / 2 + 1));
                    int sideHeight = math.max(10, primaryHeight - sideBaseOffset + sideHeightExtra);

                    int upperOffset = math.max(2, sideOffset / 2);
                    int upperBaseOffset = math.max(
                        4,
                        cave.TunnelHeight / 3 + FeatureHash.Range(ref state, 0, 6));
                    int upperRadius = math.max(
                        8,
                        primaryRadius - 4 + FeatureHash.Range(ref state, -2, 3));
                    int upperHeightExtra = heightVariation == 0
                        ? 0
                        : FeatureHash.Range(ref state, 0, heightVariation + 1);
                    int upperHeight = math.max(
                        10,
                        primaryHeight - upperBaseOffset + upperHeightExtra);

                    nodes[count++] = new UndergroundCavernNaturalizationNode(
                        distance,
                        step,
                        primaryRadius,
                        primaryHeight,
                        dominantSide,
                        sideOffset,
                        sideBaseOffset,
                        sideRadius,
                        sideHeight,
                        upperOffset,
                        upperBaseOffset,
                        upperRadius,
                        upperHeight);
                }

                distance += step;
                ordinal++;
            }

            var result = new UndergroundCavernNaturalizationNode[count];
            Array.Copy(nodes, result, count);
            return result;
        }

        private static int CountTrue(bool[] values)
        {
            int count = 0;
            for (int i = 0; i < values.Length; i++)
                if (values[i]) count++;
            return count;
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
