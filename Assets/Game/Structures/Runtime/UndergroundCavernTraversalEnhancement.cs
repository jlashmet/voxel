using System;
using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Reusable traversal presentation layered over an authoritative cave network. The cave
    /// network remains the semantic reachability source; this pass gives a long cardinal descent
    /// a natural surface mouth, configurable deterministic bends, and sparse supported route lights.
    /// </summary>
    public struct UndergroundCavernTraversalEnhancementResult
    {
        public int MouthOpeningCount;
        public int DirectionChangeCount;
        public int DoglegCount;
        public int TraversalCarveNodeCount;
        public int3[] TraversalWaypoints;
        public MineCaveLightRequest[] RouteLights;
        public long VoxelsWritten;

        public bool IsWellFormed =>
            MouthOpeningCount >= 4 && DirectionChangeCount >= 4 &&
            DoglegCount >= 2 && TraversalCarveNodeCount >= MouthOpeningCount + DoglegCount * 5 &&
            TraversalWaypoints != null && TraversalWaypoints.Length >= 12 &&
            RouteLights != null && RouteLights.Length >= 2 &&
            VoxelsWritten > 0;
    }

    public static class UndergroundCavernTraversalEnhancement
    {
        public const int ExpectedMouthOpeningCount = 5;

        public static UndergroundCavernTraversalEnhancementResult Author(
            IStructureAuthoringSession authoring,
            in CaveGenerationRequest request,
            in CaveConfig cave,
            in CaveMaterialPalette palette)
        {
            UndergroundCavernTraversalProfile profile = UndergroundCavernTraversalProfile.LongDescent;
            return Author(authoring, in request, in cave, in palette, in profile);
        }

        public static UndergroundCavernTraversalEnhancementResult Author(
            IStructureAuthoringSession authoring,
            in CaveGenerationRequest request,
            in CaveConfig cave,
            in CaveMaterialPalette palette,
            in UndergroundCavernTraversalProfile profile)
        {
            if (authoring == null) throw new ArgumentNullException(nameof(authoring));
            if (!request.IsWellFormed || !cave.IsWellFormed || !profile.IsWellFormed)
                throw new ArgumentException("Traversal enhancement requires a valid cave request, configuration, and traversal profile.");

            int[] bendSegments = profile.ResolveBendSegments(in cave);
            int[] lightSegments = profile.ResolveRouteLightSegments(in cave);
            if (!ResolvedSegmentsAreStrictlyIncreasing(bendSegments) ||
                !ResolvedSegmentsAreStrictlyIncreasing(lightSegments))
                throw new ArgumentException(
                    "Traversal profile positions collapse at this cave length; use fewer positions or more primary segments.",
                    nameof(profile));

            long startWrites = authoring.TotalVoxelsWritten;
            int carveNodes = AuthorNaturalMouth(authoring, in request, in cave, in palette);

            for (int i = 0; i < bendSegments.Length; i++)
            {
                int sign = (i & 1) == 0 ? 1 : -1;
                carveNodes += AuthorDogleg(
                    authoring, in request, in cave, bendSegments[i], sign, in palette, in profile);
            }

            var lights = new MineCaveLightRequest[lightSegments.Length];
            for (int i = 0; i < lightSegments.Length; i++)
            {
                int sign = (i & 1) == 0 ? 1 : -1;
                int3 floor = FloorAtSegment(in request, in cave, lightSegments[i]);
                lights[i] = AuthorRouteLantern(
                    authoring, floor, request.Entrance.Facing, sign, i, in cave, in palette);
            }

            return new UndergroundCavernTraversalEnhancementResult
            {
                MouthOpeningCount = ExpectedMouthOpeningCount,
                DirectionChangeCount = bendSegments.Length * 2,
                DoglegCount = bendSegments.Length,
                TraversalCarveNodeCount = carveNodes,
                TraversalWaypoints = BuildTraversalWaypoints(in request, in cave, in profile),
                RouteLights = lights,
                VoxelsWritten = authoring.TotalVoxelsWritten - startWrites,
            };
        }

        /// <summary>
        /// Returns the centreline points of the deliberately forced walkable route. Consumers can
        /// use these semantic waypoints for gameplay navigation/validation without reconstructing
        /// private dog-leg geometry or reducing the route to a straight entrance-to-terminal ray.
        /// </summary>
        public static int3[] BuildTraversalWaypoints(
            in CaveGenerationRequest request,
            in CaveConfig cave)
        {
            UndergroundCavernTraversalProfile profile = UndergroundCavernTraversalProfile.LongDescent;
            return BuildTraversalWaypoints(in request, in cave, in profile);
        }

        public static int3[] BuildTraversalWaypoints(
            in CaveGenerationRequest request,
            in CaveConfig cave,
            in UndergroundCavernTraversalProfile profile)
        {
            if (!request.IsWellFormed || !cave.IsWellFormed || !profile.IsWellFormed)
                throw new ArgumentException("Traversal waypoints require a valid cave request, configuration, and traversal profile.");

            int[] bendSegments = profile.ResolveBendSegments(in cave);
            if (!ResolvedSegmentsAreStrictlyIncreasing(bendSegments))
                throw new ArgumentException("Traversal bend positions collapse at this cave length.", nameof(profile));

            int3 forward = FacingVector(request.Entrance.Facing);
            int finalDoglegSegment = bendSegments[bendSegments.Length - 1];
            int finalDoglegForwardOffset = profile.BendForwardOffsets[profile.BendForwardOffsets.Length - 1];
            int segmentsBeyondDogleg =
                (finalDoglegForwardOffset + cave.SegmentLength - 1) / cave.SegmentLength;
            int firstPrimarySegment = math.min(
                cave.MainSegmentCount,
                finalDoglegSegment + math.max(1, segmentsBeyondDogleg));
            int remainingPrimarySegments = math.max(0, cave.MainSegmentCount - firstPrimarySegment);
            var points = new int3[
                2 + bendSegments.Length * profile.BendForwardOffsets.Length + remainingPrimarySegments];
            int output = 0;
            points[output++] = request.EntranceWorldPosition;
            points[output++] = request.EntranceWorldPosition + forward * request.Entrance.ClearanceLength;

            for (int dogleg = 0; dogleg < bendSegments.Length; dogleg++)
            {
                int sign = (dogleg & 1) == 0 ? 1 : -1;
                for (int i = 0; i < profile.BendForwardOffsets.Length; i++)
                    points[output++] = DoglegNode(
                        in request,
                        in cave,
                        bendSegments[dogleg],
                        profile.BendForwardOffsets[i],
                        profile.BendSideOffsets[i],
                        sign);
            }

            // The final dogleg ends partway into the following primary-route span. Continue with
            // every primary segment endpoint ahead of that carve rather than jumping straight to
            // the terminal segment through uncarved rock. Deriving the first endpoint from the
            // configured segment length also avoids introducing a backwards waypoint when a
            // dogleg reaches beyond the immediately following segment boundary.
            for (int segmentIndex = firstPrimarySegment;
                 segmentIndex < cave.MainSegmentCount;
                 segmentIndex++)
                points[output++] = FloorAtSegment(in request, in cave, segmentIndex);

            return points;
        }

        private static int AuthorNaturalMouth(
            IStructureAuthoringSession a,
            in CaveGenerationRequest request,
            in CaveConfig cave,
            in CaveMaterialPalette palette)
        {
            int3 forward = FacingVector(request.Entrance.Facing);
            int3 side = new int3(-forward.z, 0, forward.x);
            int[] lateral = { -3, 2, -1, 4, 0 };
            int[] radii = { 15, 14, 16, 13, 12 };
            int[] baseOffsets = { -4, -3, -5, -2, -3 };

            for (int i = 0; i < ExpectedMouthOpeningCount; i++)
            {
                int step = i * math.max(2, request.Entrance.ClearanceLength / 4);
                int3 centre = request.EntranceWorldPosition + forward * step + side * lateral[i];
                int baseY = centre.y + baseOffsets[i];
                int height = math.max(24, cave.TunnelHeight - 2 + (i % 3) * 3);
                a.Cylinder(centre.x, baseY, centre.z, radii[i], height, palette.Opening);
                a.Disc(centre.x, baseY - 1, centre.z, math.max(8, radii[i] - 2), palette.Rock);
            }

            // Unequal shoulders break the silhouette around the daylight opening without reducing
            // the walkable cross-section carved above.
            int3 left = request.EntranceWorldPosition + side * (cave.TunnelWidth / 2 + 8) + forward * 5;
            int3 right = request.EntranceWorldPosition - side * (cave.TunnelWidth / 2 + 6) + forward * 9;
            a.Cone(left.x, left.y - 5, left.z, 7, 20, palette.Rock);
            a.Cone(right.x, right.y - 4, right.z, 5, 15, palette.Rock);
            return ExpectedMouthOpeningCount;
        }

        private static int AuthorDogleg(
            IStructureAuthoringSession a,
            in CaveGenerationRequest request,
            in CaveConfig cave,
            int segmentIndex,
            int sign,
            in CaveMaterialPalette palette,
            in UndergroundCavernTraversalProfile profile)
        {
            int3 floor = FloorAtSegment(in request, in cave, segmentIndex);
            int3 forward = FacingVector(request.Entrance.Facing);
            int3 side = new int3(-forward.z, 0, forward.x) * sign;

            // Re-establish a local host around the bend. This deliberately seals the original
            // straight centreline for a short span, then the rounded carve chain below provides
            // the only broad walkable bypass. The host follows the full vertical span of the
            // descending nodes so the bypass reconnects to the same grade as the primary tunnel.
            int maxForwardOffset = 0;
            for (int i = 0; i < profile.BendForwardOffsets.Length; i++)
                maxForwardOffset = math.max(maxForwardOffset, math.abs(profile.BendForwardOffsets[i]));
            int hostForward = maxForwardOffset + 2;
            int hostSide = profile.BendSideReach + profile.BendRadius + 8;
            int minNodeY = int.MaxValue;
            int maxNodeY = int.MinValue;
            for (int i = 0; i < profile.BendForwardOffsets.Length; i++)
            {
                int y = FloorAlongPrimaryRoute(
                    in request, in cave, segmentIndex, profile.BendForwardOffsets[i]);
                minNodeY = math.min(minNodeY, y);
                maxNodeY = math.max(maxNodeY, y);
            }

            int hostMinY = minNodeY - cave.FloorRoughness - 3;
            int hostMaxY = maxNodeY + cave.TunnelHeight + cave.CeilingRoughness + 8;
            int hostHeight = hostMaxY - hostMinY + 1;
            int3 hostCentre = floor + side * (profile.BendSideReach / 2);
            int3 hostMin;
            int3 hostSize;
            if (math.abs(forward.x) == 1)
            {
                hostMin = new int3(hostCentre.x - hostForward, hostMinY,
                    math.min(floor.z, floor.z + side.z * hostSide) - profile.BendRadius - 5);
                hostSize = new int3(hostForward * 2 + 1, hostHeight,
                    hostSide + profile.BendRadius * 2 + 11);
            }
            else
            {
                hostMin = new int3(
                    math.min(floor.x, floor.x + side.x * hostSide) - profile.BendRadius - 5,
                    hostMinY,
                    hostCentre.z - hostForward);
                hostSize = new int3(hostSide + profile.BendRadius * 2 + 11, hostHeight,
                    hostForward * 2 + 1);
            }
            a.FillBulk(hostMin, hostSize, palette.Rock);

            for (int i = 0; i < profile.BendForwardOffsets.Length; i++)
            {
                int3 centre = DoglegNode(
                    in request,
                    in cave,
                    segmentIndex,
                    profile.BendForwardOffsets[i],
                    profile.BendSideOffsets[i],
                    sign);
                int radius = profile.BendRadius + ((i & 1) == 0 ? 1 : 0);
                int height = cave.TunnelHeight + 5 + (i % 3) * 2;
                a.Cylinder(centre.x, centre.y, centre.z, radius, height, palette.Opening);
                a.Disc(centre.x, centre.y - 1, centre.z, radius - 2, palette.Rock);
            }
            return profile.BendForwardOffsets.Length;
        }

        private static int3 DoglegNode(
            in CaveGenerationRequest request,
            in CaveConfig cave,
            int segmentIndex,
            int forwardOffset,
            int sideOffset,
            int sign)
        {
            int3 floor = FloorAtSegment(in request, in cave, segmentIndex);
            int3 forward = FacingVector(request.Entrance.Facing);
            int3 side = new int3(-forward.z, 0, forward.x) * sign;
            int y = FloorAlongPrimaryRoute(in request, in cave, segmentIndex, forwardOffset);
            return new int3(
                floor.x + forward.x * forwardOffset + side.x * sideOffset,
                y,
                floor.z + forward.z * forwardOffset + side.z * sideOffset);
        }

        /// <summary>
        /// Samples the same piecewise-linear grade used by the generic cave core during the
        /// configured surface-descent portion of the primary route. Dogleg nodes straddle a
        /// segment endpoint, so keeping them at the endpoint Y creates a rock step at both joins.
        /// Sharing this sample between geometry and semantic waypoints keeps normal motor
        /// traversal on the authored floor rather than asking it to cross a hidden vertical gap.
        /// </summary>
        private static int FloorAlongPrimaryRoute(
            in CaveGenerationRequest request,
            in CaveConfig cave,
            int segmentIndex,
            int forwardOffset)
        {
            int distance = (segmentIndex + 1) * cave.SegmentLength + forwardOffset;
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

        private static MineCaveLightRequest AuthorRouteLantern(
            IStructureAuthoringSession a,
            int3 floor,
            Facing facing,
            int sign,
            int ordinal,
            in CaveConfig cave,
            in CaveMaterialPalette palette)
        {
            int3 forward = FacingVector(facing);
            int3 side = new int3(-forward.z, 0, forward.x) * sign;
            int wallOffset = math.max(5, cave.TunnelWidth / 2 - 3);
            int3 basePosition = floor + forward * 4 + side * wallOffset;

            // A grounded metal stand and glowing lantern body accompany every real point-light
            // request. Sparse profile positions retain long dark spans; alternating wall sides
            // make fixtures readable as navigation cues instead of a runway line.
            a.Box(basePosition, new int3(2, 16, 2), GameMaterialIds.Gold);
            a.Box(basePosition + new int3(-2, 14, -2), new int3(5, 5, 5), GameMaterialIds.Gold);
            a.Box(basePosition + new int3(-1, 15, -1), new int3(3, 3, 3), palette.Accent);

            return new MineCaveLightRequest
            {
                PositionVoxels = (float3)(basePosition + new int3(0, 17, 0)),
                Variant = (uint)(0xC4170000u + ordinal),
            };
        }

        private static int3 FloorAtSegment(
            in CaveGenerationRequest request,
            in CaveConfig cave,
            int segmentIndex)
        {
            int completedSegments = math.clamp(segmentIndex + 1, 1, cave.MainSegmentCount);
            int3 forward = FacingVector(request.Entrance.Facing);
            return request.EntranceWorldPosition
                   + forward * (request.Entrance.ClearanceLength + completedSegments * cave.SegmentLength)
                   + new int3(
                       0,
                       FloorAfterCompletedSegments(in request, in cave, completedSegments)
                           - request.EntranceWorldPosition.y,
                       0);
        }

        private static bool ResolvedSegmentsAreStrictlyIncreasing(int[] segments)
        {
            if (segments == null || segments.Length == 0) return false;
            for (int i = 1; i < segments.Length; i++)
                if (segments[i] <= segments[i - 1]) return false;
            return true;
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
