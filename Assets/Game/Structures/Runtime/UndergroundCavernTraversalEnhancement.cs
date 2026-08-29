using System;
using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Reusable traversal presentation layered over an authoritative cave network. The cave
    /// network remains the semantic reachability source; this pass gives a long cardinal descent
    /// a natural surface mouth, deterministic forced bends, and sparse supported route lights.
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
            DoglegCount >= 3 && TraversalCarveNodeCount >= 20 &&
            TraversalWaypoints != null && TraversalWaypoints.Length >= 20 &&
            RouteLights != null && RouteLights.Length == UndergroundCavernTraversalEnhancement.ExpectedRouteLightCount &&
            VoxelsWritten > 0;
    }

    public static class UndergroundCavernTraversalEnhancement
    {
        public const int ExpectedMouthOpeningCount = 5;
        public const int ExpectedDirectionChangeCount = 6;
        public const int ExpectedRouteLightCount = 6;

        private const int DoglegSideOffset = 32;
        private const int DoglegRadius = 16;
        private static readonly int[] DoglegSegments = { 17, 31, 43 };
        private static readonly int[] RouteLightSegments = { 8, 17, 26, 35, 44, 52 };
        private static readonly int[] DoglegForwardOffsets = { -30, -20, -10, 2, 14, 26, 32 };
        private static readonly int[] DoglegSideOffsets = { 0, 10, 22, 32, 30, 16, 2 };

        public static UndergroundCavernTraversalEnhancementResult Author(
            IStructureAuthoringSession authoring,
            in CaveGenerationRequest request,
            in CaveConfig cave,
            in CaveMaterialPalette palette)
        {
            if (authoring == null) throw new ArgumentNullException(nameof(authoring));
            if (!request.IsWellFormed || !cave.IsWellFormed)
                throw new ArgumentException("Traversal enhancement requires a valid cave request and configuration.");
            if (cave.MainSegmentCount <= RouteLightSegments[RouteLightSegments.Length - 1])
                throw new ArgumentException("Traversal enhancement requires enough primary segments for its deterministic bends and route lights.");

            long startWrites = authoring.TotalVoxelsWritten;
            int carveNodes = AuthorNaturalMouth(authoring, in request, in cave, in palette);

            for (int i = 0; i < DoglegSegments.Length; i++)
            {
                int sign = i == 1 ? -1 : 1;
                carveNodes += AuthorDogleg(
                    authoring, in request, in cave, DoglegSegments[i], sign, in palette);
            }

            var lights = new MineCaveLightRequest[RouteLightSegments.Length];
            for (int i = 0; i < RouteLightSegments.Length; i++)
            {
                int sign = (i & 1) == 0 ? 1 : -1;
                int3 floor = FloorAtSegment(in request, in cave, RouteLightSegments[i]);
                lights[i] = AuthorRouteLantern(
                    authoring, floor, request.Entrance.Facing, sign, i, in cave, in palette);
            }

            return new UndergroundCavernTraversalEnhancementResult
            {
                MouthOpeningCount = ExpectedMouthOpeningCount,
                DirectionChangeCount = ExpectedDirectionChangeCount,
                DoglegCount = DoglegSegments.Length,
                TraversalCarveNodeCount = carveNodes,
                TraversalWaypoints = BuildTraversalWaypoints(in request, in cave),
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
            if (!request.IsWellFormed || !cave.IsWellFormed)
                throw new ArgumentException("Traversal waypoints require a valid cave request and configuration.");

            int3 forward = FacingVector(request.Entrance.Facing);
            int finalDoglegSegment = DoglegSegments[DoglegSegments.Length - 1];
            int finalDoglegForwardOffset = DoglegForwardOffsets[DoglegForwardOffsets.Length - 1];
            int segmentsBeyondDogleg =
                (finalDoglegForwardOffset + cave.SegmentLength - 1) / cave.SegmentLength;
            int firstPrimarySegment = math.min(
                cave.MainSegmentCount,
                finalDoglegSegment + math.max(1, segmentsBeyondDogleg));
            int remainingPrimarySegments = math.max(0, cave.MainSegmentCount - firstPrimarySegment);
            var points = new int3[
                2 + DoglegSegments.Length * DoglegForwardOffsets.Length + remainingPrimarySegments];
            int output = 0;
            points[output++] = request.EntranceWorldPosition;
            points[output++] = request.EntranceWorldPosition + forward * request.Entrance.ClearanceLength;

            for (int dogleg = 0; dogleg < DoglegSegments.Length; dogleg++)
            {
                int sign = dogleg == 1 ? -1 : 1;
                for (int i = 0; i < DoglegForwardOffsets.Length; i++)
                    points[output++] = DoglegNode(
                        in request,
                        in cave,
                        DoglegSegments[dogleg],
                        DoglegForwardOffsets[i],
                        DoglegSideOffsets[i],
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
            in CaveMaterialPalette palette)
        {
            int3 floor = FloorAtSegment(in request, in cave, segmentIndex);
            int3 forward = FacingVector(request.Entrance.Facing);
            int3 side = new int3(-forward.z, 0, forward.x) * sign;

            // Re-establish a local host around the bend. This deliberately seals the original
            // straight centreline for a short span, then the rounded carve chain below provides
            // the only broad walkable bypass. The host follows the full vertical span of the
            // descending nodes so the bypass reconnects to the same grade as the primary tunnel.
            int hostForward = 34;
            int hostSide = DoglegSideOffset + DoglegRadius + 8;
            int minNodeY = int.MaxValue;
            int maxNodeY = int.MinValue;
            for (int i = 0; i < DoglegForwardOffsets.Length; i++)
            {
                int y = FloorAlongPrimaryRoute(
                    in request, in cave, segmentIndex, DoglegForwardOffsets[i]);
                minNodeY = math.min(minNodeY, y);
                maxNodeY = math.max(maxNodeY, y);
            }

            int hostMinY = minNodeY - cave.FloorRoughness - 3;
            int hostMaxY = maxNodeY + cave.TunnelHeight + cave.CeilingRoughness + 8;
            int hostHeight = hostMaxY - hostMinY + 1;
            int3 hostCentre = floor + side * (DoglegSideOffset / 2);
            int3 hostMin;
            int3 hostSize;
            if (math.abs(forward.x) == 1)
            {
                hostMin = new int3(hostCentre.x - hostForward, hostMinY,
                    math.min(floor.z, floor.z + side.z * hostSide) - DoglegRadius - 5);
                hostSize = new int3(hostForward * 2 + 1, hostHeight,
                    hostSide + DoglegRadius * 2 + 11);
            }
            else
            {
                hostMin = new int3(
                    math.min(floor.x, floor.x + side.x * hostSide) - DoglegRadius - 5,
                    hostMinY,
                    hostCentre.z - hostForward);
                hostSize = new int3(hostSide + DoglegRadius * 2 + 11, hostHeight,
                    hostForward * 2 + 1);
            }
            a.FillBulk(hostMin, hostSize, palette.Rock);

            for (int i = 0; i < DoglegForwardOffsets.Length; i++)
            {
                int3 centre = DoglegNode(
                    in request,
                    in cave,
                    segmentIndex,
                    DoglegForwardOffsets[i],
                    DoglegSideOffsets[i],
                    sign);
                int radius = DoglegRadius + ((i & 1) == 0 ? 1 : 0);
                int height = cave.TunnelHeight + 5 + (i % 3) * 2;
                a.Cylinder(centre.x, centre.y, centre.z, radius, height, palette.Opening);
                a.Disc(centre.x, centre.y - 1, centre.z, radius - 2, palette.Rock);
            }
            return DoglegForwardOffsets.Length;
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
            // request. The six route lights remain separated by long dark spans, while alternating
            // wall sides make the fixtures readable as navigation cues instead of a runway line.
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
