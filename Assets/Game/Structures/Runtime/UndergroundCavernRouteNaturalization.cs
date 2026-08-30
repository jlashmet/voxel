using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Reusable finish pass for long guaranteed-clearance cave routes. The generic cave core keeps
    /// collision/traversal conservative; this pass overlaps deterministic rounded vaults along that
    /// same grade so the rectangular gameplay core never remains the visible wall or ceiling.
    /// Dogleg host windows remain under the traversal enhancement's ownership, but use the same
    /// rounded-vault brush.
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
            int coreHeight = cave.TunnelHeight + cave.CeilingRoughness + 1;

            for (int i = 0; i < plan.Length; i++)
            {
                UndergroundCavernNaturalizationNode node = plan[i];
                int floorY = FloorAtPrimaryDistance(in request, in cave, node.Distance);
                int3 centre = request.EntranceWorldPosition
                              + forward * (request.Entrance.ClearanceLength + node.Distance);
                centre.y = floorY;

                // The primary vault owns guaranteed route clearance. Its minimum wall radius is
                // conservative enough to cover the generic rectangular tunnel even halfway between
                // maximum-spaced nodes. Radius then bulges through the wall height and tapers into a
                // crown above clearance, so neither the box core nor a flat cylinder top can become
                // the rendered boundary.
                int primaryCrown = math.max(5, node.PrimaryHeight - coreHeight);
                int primaryBulge = 3 + (node.SideOffset % 3);
                int primaryBias = node.DominantSide * math.min(3, math.max(1, node.UpperOffset / 2));
                AuthorRoundedVault(
                    authoring,
                    centre.x,
                    floorY,
                    centre.z,
                    node.PrimaryRadius,
                    coreHeight,
                    primaryCrown,
                    primaryBulge,
                    primaryBias,
                    palette.Opening);
                authoring.Disc(
                    centre.x, floorY - 1, centre.z,
                    math.max(6, node.PrimaryRadius - 2), palette.Rock);

                // Smaller offset vaults start above the floor and vary independently. Their bases
                // remain embedded in the guaranteed primary opening, while their rounded sides and
                // crowns break symmetry without changing the authoritative walkable floor.
                int3 sideCentre = centre + side * (node.DominantSide * node.SideOffset);
                int sideCrown = math.max(4, math.min(7, node.SideHeight / 4));
                int sideClearance = math.max(6, node.SideHeight - sideCrown);
                AuthorRoundedVault(
                    authoring,
                    sideCentre.x,
                    floorY + node.SideBaseOffset,
                    sideCentre.z,
                    math.max(5, node.SideRadius - 4),
                    sideClearance,
                    sideCrown,
                    3 + (i % 3),
                    (i % 5) - 2,
                    palette.Opening);

                int3 upperCentre = centre - side * (node.DominantSide * node.UpperOffset);
                int upperCrown = math.max(4, math.min(8, node.UpperHeight / 3));
                int upperClearance = math.max(6, node.UpperHeight - upperCrown);
                AuthorRoundedVault(
                    authoring,
                    upperCentre.x,
                    floorY + node.UpperBaseOffset,
                    upperCentre.z,
                    math.max(5, node.UpperRadius - 4),
                    upperClearance,
                    upperCrown,
                    2 + ((i + 1) % 4),
                    ((i * 3) % 5) - 2,
                    palette.Opening);

                if (previousSide != 0 && previousSide != node.DominantSide)
                    sideSwitches++;
                previousSide = node.DominantSide;
                spacingSeen[math.clamp(node.StepToNext, 0, spacingSeen.Length - 1)] = true;
                int ceilingKey = math.clamp(
                    primaryCrown + node.UpperBaseOffset + node.UpperHeight - cave.TunnelHeight,
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

        /// <summary>
        /// Resolves the vertical radii for one deterministic rounded vault. The first
        /// <paramref name="clearanceHeight"/> slices never shrink below
        /// <paramref name="guaranteedRadius"/>; they bulge outward around a biased midpoint.
        /// Remaining crown slices taper from the guaranteed radius to a one-voxel apex.
        /// </summary>
        public static int[] ResolveRoundedVaultRadii(
            int guaranteedRadius,
            int clearanceHeight,
            int crownHeight,
            int bulge,
            int verticalBias)
        {
            if (guaranteedRadius < 4) throw new ArgumentOutOfRangeException(nameof(guaranteedRadius));
            if (clearanceHeight < 6) throw new ArgumentOutOfRangeException(nameof(clearanceHeight));
            if (crownHeight < 2) throw new ArgumentOutOfRangeException(nameof(crownHeight));
            if (bulge < 1 || bulge > guaranteedRadius) throw new ArgumentOutOfRangeException(nameof(bulge));

            int midpoint = math.clamp(
                clearanceHeight / 2 + verticalBias,
                math.max(1, clearanceHeight / 4),
                math.max(2, clearanceHeight - 1 - clearanceHeight / 4));
            int leftSpan = math.max(1, midpoint);
            int rightSpan = math.max(1, clearanceHeight - 1 - midpoint);
            var radii = new int[clearanceHeight + crownHeight];

            for (int y = 0; y < clearanceHeight; y++)
            {
                int extra = y <= midpoint
                    ? bulge * y / leftSpan
                    : bulge * (clearanceHeight - 1 - y) / rightSpan;
                radii[y] = guaranteedRadius + math.max(0, extra);
            }

            if (crownHeight == 2)
            {
                radii[clearanceHeight] = guaranteedRadius;
                radii[clearanceHeight + 1] = 1;
                return radii;
            }

            for (int y = 0; y < crownHeight; y++)
            {
                int remaining = crownHeight - 1 - y;
                radii[clearanceHeight + y] =
                    1 + (guaranteedRadius - 1) * remaining / (crownHeight - 1);
            }
            return radii;
        }

        /// <summary>
        /// Authors one reusable rounded cave vault using only the stable authoring API. The shape
        /// is defined as stacked horizontal discs, but is emitted as contiguous vertical bulk spans
        /// for each radial column. This exactly preserves the rounded profile while retaining the
        /// engine's cheap batched-column write path instead of paying one collapse scan per voxel.
        /// </summary>
        public static int AuthorRoundedVault(
            IStructureAuthoringSession authoring,
            int centreX,
            int baseY,
            int centreZ,
            int guaranteedRadius,
            int clearanceHeight,
            int crownHeight,
            int bulge,
            int verticalBias,
            byte material)
        {
            if (authoring == null) throw new ArgumentNullException(nameof(authoring));
            int[] radii = ResolveRoundedVaultRadii(
                guaranteedRadius,
                clearanceHeight,
                crownHeight,
                bulge,
                verticalBias);

            int maxRadius = 0;
            for (int y = 0; y < radii.Length; y++)
                maxRadius = math.max(maxRadius, radii[y]);

            for (int z = -maxRadius; z <= maxRadius; z++)
            for (int x = -maxRadius; x <= maxRadius; x++)
            {
                int distanceSquared = x * x + z * z;
                int runStart = -1;
                for (int y = 0; y <= radii.Length; y++)
                {
                    bool inside = y < radii.Length &&
                                  distanceSquared <= radii[y] * radii[y];
                    if (inside)
                    {
                        if (runStart < 0) runStart = y;
                        continue;
                    }

                    if (runStart < 0) continue;
                    authoring.FillColumnBulk(
                        centreX + x,
                        baseY + runStart,
                        baseY + y,
                        centreZ + z,
                        material);
                    runStart = -1;
                }
            }
            return radii.Length;
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
            int maximumStep = math.clamp(spacing + spacingVariation, 8, 32);
            int rectangularHalfWidth = cave.TunnelWidth / 2 + cave.WallRoughness + 1;
            int halfStep = (maximumStep + 1) / 2;
            int requiredCoverRadius =
                CeilSqrt(rectangularHalfWidth * rectangularHalfWidth + halfStep * halfStep) + 1;
            int baseRadius = math.max(
                math.max(profile.ResolvedNaturalizationRadius, cave.TunnelWidth / 2 + 3),
                requiredCoverRadius);
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
                    int primaryRadius = math.max(requiredCoverRadius, baseRadius + radiusDelta);
                    int heightExtra = heightVariation == 0
                        ? 0
                        : FeatureHash.Range(ref state, 0, heightVariation + 1);
                    int primaryHeight = cave.TunnelHeight + cave.CeilingRoughness + 6 + heightExtra;

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

        private static int CeilSqrt(int value)
        {
            if (value <= 0) return 0;
            int root = 1;
            while (root * root < value)
                root++;
            return root;
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
