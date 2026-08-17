using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>Summary of one bounded generic cave authoring pass.</summary>
    public struct CaveAuthoringResult
    {
        public int SegmentsAuthored;
        public int BranchesAuthored;
        public int ChambersAuthored;
        public int3 MainPathEnd;
    }

    /// <summary>
    /// Shared integer-only cave authorer. Standalone, structure-attached, and underground requests
    /// all enter this same path. Geometry is emitted through the existing structure authoring session;
    /// caves never become a second authoritative world representation.
    /// </summary>
    public static class CaveAuthoring
    {
        private const ulong SegmentSalt = 0x9E3779B97F4A7C15ul;
        private const ulong TurnSalt = 0x44A91B1D6E2F301Bul;
        private const ulong VerticalSalt = 0x82C3D4E5A6172839ul;
        private const ulong ChamberSalt = 0x19D2E3F405162738ul;
        private const ulong BranchSalt = 0xA57B9C1D2E3F4061ul;
        private const ulong RoughnessSalt = 0x63B4C5D6E7F8091Aul;

        private struct PathState
        {
            public int3 Position;
            public int2 Direction;
            public int SegmentCount;
            public int Depth;
            public ulong Seed;
        }

        public static CaveAuthoringResult Author(
            IStructureAuthoringSession authoring,
            in CaveGenerationRequest request,
            in CaveConfig config,
            in CaveMaterialPalette palette)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!request.IsWellFormed)
                throw new System.ArgumentException("Cave generation request is invalid.", nameof(request));
            if (!config.IsWellFormed)
                throw new System.ArgumentException("Cave configuration is invalid.", nameof(config));
            if (!request.TryGetWorldBounds(in config, out _))
                throw new System.ArgumentException(
                    "Cave bounds overflow world coordinates.", nameof(request));

            int3 entrance = request.EntranceWorldPosition;
            int2 entranceDirection = Direction(request.Entrance.Facing);
            CarveEntrance(
                authoring,
                entrance,
                entranceDirection,
                request.Entrance.Width,
                request.Entrance.Height,
                request.Entrance.ClearanceLength,
                palette.Opening);

            int3 start = entrance + new int3(
                entranceDirection.x * request.Entrance.ClearanceLength,
                0,
                entranceDirection.y * request.Entrance.ClearanceLength);

            var queue = new FixedList4096Bytes<PathState>();
            var branchOrigins = new FixedList512Bytes<int3>();
            var mainKey = new FixedString64Bytes("cave.main");
            queue.Add(new PathState
            {
                Position = start,
                Direction = entranceDirection,
                SegmentCount = config.MainSegmentCount,
                Depth = 0,
                Seed = StructureSeed.Child(request.Seed, in mainKey),
            });

            var result = new CaveAuthoringResult { MainPathEnd = start };
            int branchesCreated = 0;

            for (int pathIndex = 0; pathIndex < queue.Length; pathIndex++)
            {
                PathState path = queue[pathIndex];
                int3 current = path.Position;
                int2 direction = path.Direction;

                for (int segmentIndex = 0; segmentIndex < path.SegmentCount; segmentIndex++)
                {
                    ulong segmentSeed = FeatureHash.Mix(
                        path.Seed ^ ((ulong)(uint)(segmentIndex + 1) * SegmentSalt));

                    if (segmentIndex > 0 && ChancePercent(segmentSeed ^ TurnSalt, config.TurnChancePercent))
                    {
                        ulong turnState = FeatureHash.Mix(segmentSeed ^ TurnSalt ^ 0x71ul);
                        direction = Rotate(direction, FeatureHash.Range(ref turnState, 0, 1) == 0 ? -1 : 1);
                    }

                    int verticalDelta = ResolveVerticalDelta(
                        in request,
                        in config,
                        path.Depth,
                        segmentIndex,
                        segmentSeed,
                        current.y);
                    int targetY = math.clamp(
                        current.y + verticalDelta,
                        request.Origin.y + config.MinVerticalOffset,
                        request.Origin.y + config.MaxVerticalOffset);

                    int3 candidate = new int3(
                        current.x + direction.x * config.SegmentLength,
                        targetY,
                        current.z + direction.y * config.SegmentLength);

                    if (request.Entrance.Mode == CaveEntranceMode.Surface &&
                        (path.Depth > 0 || segmentIndex >= config.SurfaceDescentSegments))
                    {
                        int coveredY = ResolveCoveredTargetY(
                            in request,
                            in config,
                            current,
                            candidate,
                            direction);
                        candidate.y = math.min(candidate.y, coveredY);
                        candidate.y = math.max(
                            candidate.y,
                            request.Origin.y + config.MinVerticalOffset);
                    }

                    if (!FitsBounds(in request, in config, candidate))
                        break;

                    CarveSegment(
                        authoring,
                        in request,
                        in config,
                        in palette,
                        current,
                        candidate,
                        direction,
                        segmentSeed,
                        request.Entrance.Mode == CaveEntranceMode.Surface &&
                        (path.Depth > 0 || segmentIndex >= config.SurfaceDescentSegments));
                    current = candidate;
                    result.SegmentsAuthored++;
                    if (pathIndex == 0)
                        result.MainPathEnd = current;

                    if (ChancePercent(segmentSeed ^ ChamberSalt, config.ChamberChancePercent))
                    {
                        AuthorChamber(
                            authoring,
                            in request,
                            in config,
                            in palette,
                            current,
                            segmentSeed);
                        result.ChambersAuthored++;
                    }

                    if (branchesCreated < config.MaxBranches &&
                        path.Depth < config.MaxBranchDepth &&
                        ChancePercent(segmentSeed ^ BranchSalt, config.BranchChancePercent) &&
                        IsSeparated(current, in branchOrigins, config.MinBranchSeparation))
                    {
                        ulong branchState = FeatureHash.Mix(segmentSeed ^ BranchSalt ^ 0xB1ul);
                        int branchTurn = FeatureHash.Range(ref branchState, 0, 1) == 0 ? -1 : 1;
                        int2 branchDirection = Rotate(direction, branchTurn);
                        ulong branchSeed = FeatureHash.Mix(segmentSeed ^ BranchSalt ^ (ulong)(uint)branchesCreated);
                        queue.Add(new PathState
                        {
                            Position = current,
                            Direction = branchDirection,
                            SegmentCount = config.BranchSegmentCount,
                            Depth = path.Depth + 1,
                            Seed = branchSeed,
                        });
                        branchOrigins.Add(current);
                        branchesCreated++;
                        result.BranchesAuthored++;
                    }
                }
            }

            return result;
        }

        private static void CarveEntrance(
            IStructureAuthoringSession authoring,
            int3 entrance,
            int2 direction,
            int width,
            int height,
            int length,
            byte opening)
        {
            for (int step = 0; step <= length; step++)
            {
                int3 centre = new int3(
                    entrance.x + direction.x * step,
                    entrance.y,
                    entrance.z + direction.y * step);
                CarveCrossSection(authoring, centre, direction, width, height, 0, 0, 0, opening);
            }
        }

        private static void CarveSegment(
            IStructureAuthoringSession authoring,
            in CaveGenerationRequest request,
            in CaveConfig config,
            in CaveMaterialPalette palette,
            int3 start,
            int3 end,
            int2 direction,
            ulong segmentSeed,
            bool enforceSurfaceCover)
        {
            for (int step = 1; step <= config.SegmentLength; step++)
            {
                int floorY = start.y + (end.y - start.y) * step / config.SegmentLength;
                int x = start.x + direction.x * step;
                int z = start.z + direction.y * step;

                if (enforceSurfaceCover)
                {
                    int surface = TerrainQuery.HeightAt(x, z, request.TerrainSeed);
                    int maxFloor = surface
                                 - config.MinimumSurfaceCover
                                 - config.TunnelHeight
                                 - config.CeilingRoughness;
                    floorY = math.min(floorY, maxFloor);
                    floorY = math.max(
                        floorY,
                        request.Origin.y + config.MinVerticalOffset);
                }

                ulong roughnessState = FeatureHash.Mix(
                    segmentSeed ^ RoughnessSalt ^ ((ulong)(uint)step * SegmentSalt));
                int floorExtra = config.FloorRoughness == 0
                    ? 0
                    : FeatureHash.Range(ref roughnessState, 0, config.FloorRoughness);
                int ceilingExtra = config.CeilingRoughness == 0
                    ? 0
                    : FeatureHash.Range(ref roughnessState, 0, config.CeilingRoughness);
                int wallExtra = config.WallRoughness == 0
                    ? 0
                    : FeatureHash.Range(ref roughnessState, 0, config.WallRoughness);

                CarveCrossSection(
                    authoring,
                    new int3(x, floorY, z),
                    direction,
                    config.TunnelWidth,
                    config.TunnelHeight,
                    floorExtra,
                    ceilingExtra,
                    wallExtra,
                    palette.Opening);
            }
        }

        private static void CarveCrossSection(
            IStructureAuthoringSession authoring,
            int3 floorCentre,
            int2 direction,
            int width,
            int height,
            int floorExtra,
            int ceilingExtra,
            int wallExtra,
            byte opening)
        {
            int first = -(width / 2) - wallExtra;
            int count = width + wallExtra * 2;
            int minY = floorCentre.y - floorExtra;
            int maxY = floorCentre.y + height + ceilingExtra;

            for (int i = 0; i < count; i++)
            {
                int offset = first + i;
                int x = direction.x == 0 ? floorCentre.x + offset : floorCentre.x;
                int z = direction.y == 0 ? floorCentre.z + offset : floorCentre.z;
                authoring.FillColumnBulk(x, minY, maxY, z, opening);
            }
        }

        private static void AuthorChamber(
            IStructureAuthoringSession authoring,
            in CaveGenerationRequest request,
            in CaveConfig config,
            in CaveMaterialPalette palette,
            int3 centre,
            ulong segmentSeed)
        {
            ulong state = FeatureHash.Mix(segmentSeed ^ ChamberSalt ^ 0xC4ul);
            int radius = FeatureHash.Range(ref state, config.MinChamberRadius, config.MaxChamberRadius);
            int height = FeatureHash.Range(ref state, config.MinChamberHeight, config.MaxChamberHeight);

            int maxRadiusX = config.BoundsHalfExtents.x - math.abs(centre.x - request.Origin.x) - 1;
            int maxRadiusZ = config.BoundsHalfExtents.z - math.abs(centre.z - request.Origin.z) - 1;
            radius = math.min(radius, math.min(maxRadiusX, maxRadiusZ));
            if (radius < 2) return;

            int maxHeight = request.Origin.y + config.BoundsHalfExtents.y - centre.y;
            height = math.min(height, maxHeight);
            if (height < 3) return;

            authoring.Cylinder(
                centre.x,
                centre.y,
                centre.z,
                radius,
                height,
                palette.Opening);
            authoring.Disc(
                centre.x,
                centre.y - 1,
                centre.z,
                radius,
                palette.Rock);
        }

        private static int ResolveVerticalDelta(
            in CaveGenerationRequest request,
            in CaveConfig config,
            int pathDepth,
            int segmentIndex,
            ulong segmentSeed,
            int currentY)
        {
            if (request.Entrance.Mode == CaveEntranceMode.Surface &&
                pathDepth == 0 && segmentIndex < config.SurfaceDescentSegments)
                return -config.SurfaceDescentPerSegment;

            if (config.MaxVerticalStepPerSegment == 0 ||
                !ChancePercent(segmentSeed ^ VerticalSalt, config.VerticalChancePercent))
                return 0;

            ulong state = FeatureHash.Mix(segmentSeed ^ VerticalSalt ^ 0x93ul);
            int delta = FeatureHash.Range(
                ref state,
                -config.MaxVerticalStepPerSegment,
                config.MaxVerticalStepPerSegment);
            if (delta == 0)
                delta = (FeatureHash.Next(ref state) & 1ul) == 0ul ? -1 : 1;

            int minY = request.Origin.y + config.MinVerticalOffset;
            int maxY = request.Origin.y + config.MaxVerticalOffset;
            if (currentY + delta < minY) return minY - currentY;
            if (currentY + delta > maxY) return maxY - currentY;
            return delta;
        }

        private static int ResolveCoveredTargetY(
            in CaveGenerationRequest request,
            in CaveConfig config,
            int3 start,
            int3 candidate,
            int2 direction)
        {
            int coveredY = candidate.y;
            for (int step = 1; step <= config.SegmentLength; step++)
            {
                int x = start.x + direction.x * step;
                int z = start.z + direction.y * step;
                int surface = TerrainQuery.HeightAt(x, z, request.TerrainSeed);
                int maxFloor = surface
                             - config.MinimumSurfaceCover
                             - config.TunnelHeight
                             - config.CeilingRoughness;
                coveredY = math.min(coveredY, maxFloor);
            }
            return coveredY;
        }

        private static bool FitsBounds(
            in CaveGenerationRequest request,
            in CaveConfig config,
            int3 centre)
        {
            long localX = (long)centre.x - request.Origin.x;
            long localY = (long)centre.y - request.Origin.y;
            long localZ = (long)centre.z - request.Origin.z;
            int horizontalMargin = config.TunnelWidth / 2 + config.WallRoughness + 1;
            int upperMargin = config.TunnelHeight + config.CeilingRoughness + 1;
            int lowerMargin = config.FloorRoughness + 1;

            return localX >= -config.BoundsHalfExtents.x + horizontalMargin &&
                   localX <= config.BoundsHalfExtents.x - horizontalMargin &&
                   localZ >= -config.BoundsHalfExtents.z + horizontalMargin &&
                   localZ <= config.BoundsHalfExtents.z - horizontalMargin &&
                   localY >= -config.BoundsHalfExtents.y + lowerMargin &&
                   localY <= config.BoundsHalfExtents.y - upperMargin;
        }

        private static bool IsSeparated(
            int3 candidate,
            in FixedList512Bytes<int3> existing,
            int minimumSeparation)
        {
            if (minimumSeparation <= 0) return true;
            long minSquared = (long)minimumSeparation * minimumSeparation;
            for (int i = 0; i < existing.Length; i++)
            {
                int3 delta = candidate - existing[i];
                long squared = (long)delta.x * delta.x
                             + (long)delta.y * delta.y
                             + (long)delta.z * delta.z;
                if (squared < minSquared)
                    return false;
            }
            return true;
        }

        private static int2 Direction(Facing facing)
        {
            switch (facing)
            {
                case Facing.North: return new int2(0, 1);
                case Facing.East: return new int2(1, 0);
                case Facing.South: return new int2(0, -1);
                case Facing.West: return new int2(-1, 0);
                default: return new int2(0, 1);
            }
        }

        private static int2 Rotate(int2 direction, int turn)
        {
            return turn < 0
                ? new int2(-direction.y, direction.x)
                : new int2(direction.y, -direction.x);
        }

        private static bool ChancePercent(ulong seed, int percent)
        {
            if (percent <= 0) return false;
            if (percent >= 100) return true;
            ulong state = FeatureHash.Mix(seed);
            int threshold = percent * 65536 / 100;
            return FeatureHash.Chance(ref state, threshold);
        }
    }
}
