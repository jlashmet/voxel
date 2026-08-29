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
            RouteLights != null && RouteLights.Length >= 3 && RouteLights.Length <= 4 &&
            VoxelsWritten > 0;
    }

    public static class UndergroundCavernTraversalEnhancement
    {
        public const int ExpectedMouthOpeningCount = 5;
        public const int ExpectedDirectionChangeCount = 6;
        public const int ExpectedRouteLightCount = 3;

        private const int DoglegSideOffset = 32;
        private const int DoglegRadius = 16;
        private static readonly int[] DoglegSegments = { 17, 31, 43 };
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
            if (cave.MainSegmentCount <= DoglegSegments[DoglegSegments.Length - 1])
                throw new ArgumentException("Traversal enhancement requires enough primary segments for its deterministic bends.");

            long startWrites = authoring.TotalVoxelsWritten;
            int carveNodes = AuthorNaturalMouth(authoring, in request, in cave, in palette);

            var lights = new MineCaveLightRequest[DoglegSegments.Length];
            for (int i = 0; i < DoglegSegments.Length; i++)
            {
                int sign = i == 1 ? -1 : 1;
                int3 floor = FloorAtSegment(in request, in cave, DoglegSegments[i]);
                carveNodes += AuthorDogleg(authoring, floor, request.Entrance.Facing, sign, in cave, in palette);
                lights[i] = AuthorRouteLantern(
                    authoring, floor, request.Entrance.Facing, sign, i, in palette);
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
            var points = new int3[2 + DoglegSegments.Length * DoglegForwardOffsets.Length + 1];
            int output = 0;
            points[output++] = request.EntranceWorldPosition;
            points[output++] = request.EntranceWorldPosition + forward * request.Entrance.ClearanceLength;

            for (int dogleg = 0; dogleg < DoglegSegments.Length; dogleg++)
            {
                int sign = dogleg == 1 ? -1 : 1;
                int3 floor = FloorAtSegment(in request, in cave, DoglegSegments[dogleg]);
                int3 side = new int3(-forward.z, 0, forward.x) * sign;
                for (int i = 0; i < DoglegForwardOffsets.Length; i++)
                    points[output++] = floor
                                       + forward * DoglegForwardOffsets[i]
                                       + side * DoglegSideOffsets[i];
            }

            points[output] = FloorAtSegment(in request, in cave, cave.MainSegmentCount - 1);
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
            int3 floor,
            Facing facing,
            int sign,
            in CaveConfig cave,
            in CaveMaterialPalette palette)
        {
            int3 forward = FacingVector(facing);
            int3 side = new int3(-forward.z, 0, forward.x) * sign;

            // Re-establish a local host around the bend. This deliberately seals the original
            // straight centreline for a short span, then the rounded carve chain below provides
            // the only broad walkable bypass. Sparse deep storage therefore behaves identically
            // to an already-solid subterranean region.
            int hostForward = 34;
            int hostSide = DoglegSideOffset + DoglegRadius + 8;
            int hostHeight = cave.TunnelHeight + cave.CeilingRoughness + cave.FloorRoughness + 12;
            int3 hostCentre = floor + side * (DoglegSideOffset / 2);
            int3 hostMin;
            int3 hostSize;
            if (math.abs(forward.x) == 1)
            {
                hostMin = new int3(hostCentre.x - hostForward, floor.y - cave.FloorRoughness - 3,
                    math.min(floor.z, floor.z + side.z * hostSide) - DoglegRadius - 5);
                hostSize = new int3(hostForward * 2 + 1, hostHeight,
                    hostSide + DoglegRadius * 2 + 11);
            }
            else
            {
                hostMin = new int3(
                    math.min(floor.x, floor.x + side.x * hostSide) - DoglegRadius - 5,
                    floor.y - cave.FloorRoughness - 3,
                    hostCentre.z - hostForward);
                hostSize = new int3(hostSide + DoglegRadius * 2 + 11, hostHeight,
                    hostForward * 2 + 1);
            }
            a.FillBulk(hostMin, hostSize, palette.Rock);

            for (int i = 0; i < DoglegForwardOffsets.Length; i++)
            {
                int3 centre = floor
                              + forward * DoglegForwardOffsets[i]
                              + side * DoglegSideOffsets[i];
                int baseY = floor.y - (i == 2 || i == 5 ? 1 : 0);
                int radius = DoglegRadius + ((i & 1) == 0 ? 1 : 0);
                int height = cave.TunnelHeight + 5 + (i % 3) * 2;
                a.Cylinder(centre.x, baseY, centre.z, radius, height, palette.Opening);
                a.Disc(centre.x, baseY - 1, centre.z, radius - 2, palette.Rock);
            }
            return DoglegForwardOffsets.Length;
        }

        private static MineCaveLightRequest AuthorRouteLantern(
            IStructureAuthoringSession a,
            int3 floor,
            Facing facing,
            int sign,
            int ordinal,
            in CaveMaterialPalette palette)
        {
            int3 forward = FacingVector(facing);
            int3 side = new int3(-forward.z, 0, forward.x) * sign;
            int3 basePosition = floor + forward * 5 + side * (DoglegSideOffset - 5);

            // A real authored stand and glowing lantern body accompany every point-light request.
            // The light is not simulated by an emissive voxel alone.
            a.Box(basePosition, new int3(1, 10, 1), GameMaterialIds.Gold);
            a.Box(basePosition + new int3(-2, 9, -2), new int3(5, 5, 5), GameMaterialIds.Gold);
            a.Box(basePosition + new int3(-1, 10, -1), new int3(3, 3, 3), palette.Accent);

            return new MineCaveLightRequest
            {
                PositionVoxels = (float3)(basePosition + new int3(0, 11, 0)),
                Variant = (uint)(0xC4170000u + ordinal),
            };
        }

        private static int3 FloorAtSegment(
            in CaveGenerationRequest request,
            in CaveConfig cave,
            int segmentIndex)
        {
            int completedSegments = math.clamp(segmentIndex + 1, 1, cave.MainSegmentCount);
            int descendedSegments = math.min(completedSegments, cave.SurfaceDescentSegments);
            int y = math.max(
                request.EntranceWorldPosition.y - descendedSegments * cave.SurfaceDescentPerSegment,
                request.Origin.y + cave.MinVerticalOffset);
            int3 forward = FacingVector(request.Entrance.Facing);
            return request.EntranceWorldPosition
                   + forward * (request.Entrance.ClearanceLength + completedSegments * cave.SegmentLength)
                   + new int3(0, y - request.EntranceWorldPosition.y, 0);
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
