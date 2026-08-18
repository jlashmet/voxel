using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>Internal deterministic implementation behind the stable CaveAuthoring facade.</summary>
    internal static class CaveNetworkAuthoringCore
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
            public int TraversalDistance;
            public ulong Seed;
        }

        public static CaveAuthoringResult Author(IStructureAuthoringSession authoring,
            in CaveGenerationRequest request, in CaveConfig config, in CaveMaterialPalette palette)
        {
            int3 entrance = request.EntranceWorldPosition;
            int2 entranceDirection = Direction(request.Entrance.Facing);
            CarveEntrance(authoring, entrance, entranceDirection, request.Entrance.Width,
                request.Entrance.Height, request.Entrance.ClearanceLength, palette.Opening);
            int3 start = entrance + new int3(entranceDirection.x * request.Entrance.ClearanceLength,
                0, entranceDirection.y * request.Entrance.ClearanceLength);

            var queue = new FixedList4096Bytes<PathState>();
            var branchOrigins = new FixedList512Bytes<int3>();
            var mainKey = new FixedString64Bytes("cave.main");
            queue.Add(new PathState
            {
                Position = start,
                Direction = entranceDirection,
                SegmentCount = config.MainSegmentCount,
                Depth = 0,
                TraversalDistance = 0,
                Seed = StructureSeed.Child(request.Seed, in mainKey),
            });

            var result = new CaveAuthoringResult { MainPathEnd = start };
            int branchesCreated = 0;
            for (int pathIndex = 0; pathIndex < queue.Length; pathIndex++)
            {
                PathState path = queue[pathIndex];
                int3 current = path.Position;
                int2 direction = path.Direction;
                int traversalDistance = path.TraversalDistance;
                int pathSegmentsAuthored = 0;
                for (int segmentIndex = 0; segmentIndex < path.SegmentCount; segmentIndex++)
                {
                    ulong segmentSeed = FeatureHash.Mix(path.Seed ^ ((ulong)(uint)(segmentIndex + 1) * SegmentSalt));
                    if (segmentIndex > 0 && ChancePercent(segmentSeed ^ TurnSalt, config.TurnChancePercent))
                    {
                        ulong turnState = FeatureHash.Mix(segmentSeed ^ TurnSalt ^ 0x71ul);
                        direction = Rotate(direction, FeatureHash.Range(ref turnState, 0, 1) == 0 ? -1 : 1);
                    }

                    int targetY = math.clamp(current.y + ResolveVerticalDelta(in request, in config,
                        path.Depth, segmentIndex, segmentSeed, current.y),
                        request.Origin.y + config.MinVerticalOffset, request.Origin.y + config.MaxVerticalOffset);
                    int3 candidate = new int3(current.x + direction.x * config.SegmentLength,
                        targetY, current.z + direction.y * config.SegmentLength);
                    if (request.Entrance.Mode == CaveEntranceMode.Surface &&
                        (path.Depth > 0 || segmentIndex >= config.SurfaceDescentSegments))
                    {
                        candidate.y = math.min(candidate.y,
                            ResolveCoveredTargetY(in request, in config, current, candidate, direction));
                        candidate.y = math.max(candidate.y, request.Origin.y + config.MinVerticalOffset);
                    }
                    if (!FitsBounds(in request, in config, candidate)) break;

                    CarveSegment(authoring, in request, in config, in palette, current, candidate,
                        direction, segmentSeed, request.Entrance.Mode == CaveEntranceMode.Surface &&
                        (path.Depth > 0 || segmentIndex >= config.SurfaceDescentSegments));
                    current = candidate;
                    traversalDistance += config.SegmentLength;
                    pathSegmentsAuthored++;
                    result.SegmentsAuthored++;
                    if (pathIndex == 0)
                    {
                        result.MainPathEnd = current;
                        result.MainPathTraversalDistance = traversalDistance;
                    }

                    if (ChancePercent(segmentSeed ^ ChamberSalt, config.ChamberChancePercent))
                    {
                        AuthorChamber(authoring, in request, in config, in palette, current, segmentSeed);
                        result.ChambersAuthored++;
                    }
                    if (branchesCreated < config.MaxBranches && path.Depth < config.MaxBranchDepth &&
                        ChancePercent(segmentSeed ^ BranchSalt, config.BranchChancePercent) &&
                        IsSeparated(current, in branchOrigins, config.MinBranchSeparation))
                    {
                        ulong branchState = FeatureHash.Mix(segmentSeed ^ BranchSalt ^ 0xB1ul);
                        queue.Add(new PathState
                        {
                            Position = current,
                            Direction = Rotate(direction, FeatureHash.Range(ref branchState, 0, 1) == 0 ? -1 : 1),
                            SegmentCount = config.BranchSegmentCount,
                            Depth = path.Depth + 1,
                            TraversalDistance = traversalDistance,
                            Seed = FeatureHash.Mix(segmentSeed ^ BranchSalt ^ (ulong)(uint)branchesCreated),
                        });
                        branchOrigins.Add(current);
                        branchesCreated++;
                        result.BranchesAuthored++;
                    }
                }

                if (pathSegmentsAuthored > 0)
                {
                    result.TraversalCandidates.Items.Add(new CaveTraversalCandidate
                    {
                        Position = current,
                        TraversalDistance = traversalDistance,
                        BranchDepth = (byte)path.Depth,
                        Flags = CaveTraversalFlags.ReachableFromEntrance |
                                CaveTraversalFlags.Terminal |
                                (path.Depth == 0 ? CaveTraversalFlags.MainPath : CaveTraversalFlags.Branch),
                        ExitFacing = FacingFor(direction),
                    });
                }
            }
            return result;
        }

        private static void CarveEntrance(IStructureAuthoringSession a, int3 entrance, int2 direction,
            int width, int height, int length, byte opening)
        {
            for (int step = 0; step <= length; step++)
                CarveCrossSection(a, new int3(entrance.x + direction.x * step, entrance.y,
                    entrance.z + direction.y * step), direction, width, height, 0, 0, 0, opening);
        }

        private static void CarveSegment(IStructureAuthoringSession a, in CaveGenerationRequest request,
            in CaveConfig config, in CaveMaterialPalette palette, int3 start, int3 end, int2 direction,
            ulong segmentSeed, bool enforceSurfaceCover)
        {
            for (int step = 1; step <= config.SegmentLength; step++)
            {
                int floorY = start.y + (end.y - start.y) * step / config.SegmentLength;
                int x = start.x + direction.x * step;
                int z = start.z + direction.y * step;
                if (enforceSurfaceCover)
                {
                    int surface = TerrainQuery.HeightAt(x, z, request.TerrainSeed);
                    floorY = math.min(floorY,
                        surface - config.MinimumSurfaceCover - config.TunnelHeight - config.CeilingRoughness);
                    floorY = math.max(floorY, request.Origin.y + config.MinVerticalOffset);
                }
                ulong roughness = FeatureHash.Mix(segmentSeed ^ RoughnessSalt ^ ((ulong)(uint)step * SegmentSalt));
                int floorExtra = config.FloorRoughness == 0 ? 0 : FeatureHash.Range(ref roughness, 0, config.FloorRoughness);
                int ceilingExtra = config.CeilingRoughness == 0 ? 0 : FeatureHash.Range(ref roughness, 0, config.CeilingRoughness);
                int wallExtra = config.WallRoughness == 0 ? 0 : FeatureHash.Range(ref roughness, 0, config.WallRoughness);
                CarveCrossSection(a, new int3(x, floorY, z), direction, config.TunnelWidth,
                    config.TunnelHeight, floorExtra, ceilingExtra, wallExtra, palette.Opening);
            }
        }

        private static void CarveCrossSection(IStructureAuthoringSession a, int3 floorCentre,
            int2 direction, int width, int height, int floorExtra, int ceilingExtra, int wallExtra,
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
                a.FillColumnBulk(x, minY, maxY, z, opening);
            }
        }

        private static void AuthorChamber(IStructureAuthoringSession a, in CaveGenerationRequest request,
            in CaveConfig config, in CaveMaterialPalette palette, int3 centre, ulong segmentSeed)
        {
            ulong state = FeatureHash.Mix(segmentSeed ^ ChamberSalt ^ 0xC4ul);
            int radius = FeatureHash.Range(ref state, config.MinChamberRadius, config.MaxChamberRadius);
            int height = FeatureHash.Range(ref state, config.MinChamberHeight, config.MaxChamberHeight);
            radius = math.min(radius, math.min(
                config.BoundsHalfExtents.x - math.abs(centre.x - request.Origin.x) - 1,
                config.BoundsHalfExtents.z - math.abs(centre.z - request.Origin.z) - 1));
            if (radius < 2) return;
            height = math.min(height, request.Origin.y + config.BoundsHalfExtents.y - centre.y);
            if (height < 3) return;

            if (config.ChamberShape == CaveChamberShape.Box)
            {
                int diameter = radius * 2 + 1;
                a.Box(new int3(centre.x - radius, centre.y, centre.z - radius),
                    new int3(diameter, height, diameter), palette.Opening);
                a.Box(new int3(centre.x - radius, centre.y - 1, centre.z - radius),
                    new int3(diameter, 1, diameter), palette.Rock);
            }
            else
            {
                a.Cylinder(centre.x, centre.y, centre.z, radius, height, palette.Opening);
                a.Disc(centre.x, centre.y - 1, centre.z, radius, palette.Rock);
            }
        }

        private static int ResolveVerticalDelta(in CaveGenerationRequest request, in CaveConfig config,
            int pathDepth, int segmentIndex, ulong segmentSeed, int currentY)
        {
            if (request.Entrance.Mode == CaveEntranceMode.Surface && pathDepth == 0 &&
                segmentIndex < config.SurfaceDescentSegments)
                return -config.SurfaceDescentPerSegment;
            if (config.MaxVerticalStepPerSegment == 0 ||
                !ChancePercent(segmentSeed ^ VerticalSalt, config.VerticalChancePercent)) return 0;
            ulong state = FeatureHash.Mix(segmentSeed ^ VerticalSalt ^ 0x93ul);
            int delta = FeatureHash.Range(ref state, -config.MaxVerticalStepPerSegment,
                config.MaxVerticalStepPerSegment);
            if (delta == 0) delta = (FeatureHash.Next(ref state) & 1ul) == 0ul ? -1 : 1;
            int minY = request.Origin.y + config.MinVerticalOffset;
            int maxY = request.Origin.y + config.MaxVerticalOffset;
            if (currentY + delta < minY) return minY - currentY;
            if (currentY + delta > maxY) return maxY - currentY;
            return delta;
        }

        private static int ResolveCoveredTargetY(in CaveGenerationRequest request, in CaveConfig config,
            int3 start, int3 candidate, int2 direction)
        {
            int coveredY = candidate.y;
            for (int step = 1; step <= config.SegmentLength; step++)
            {
                int surface = TerrainQuery.HeightAt(start.x + direction.x * step,
                    start.z + direction.y * step, request.TerrainSeed);
                coveredY = math.min(coveredY,
                    surface - config.MinimumSurfaceCover - config.TunnelHeight - config.CeilingRoughness);
            }
            return coveredY;
        }

        private static bool FitsBounds(in CaveGenerationRequest request, in CaveConfig config, int3 centre)
        {
            long x = (long)centre.x - request.Origin.x;
            long y = (long)centre.y - request.Origin.y;
            long z = (long)centre.z - request.Origin.z;
            int horizontal = config.TunnelWidth / 2 + config.WallRoughness + 1;
            return x >= -config.BoundsHalfExtents.x + horizontal && x <= config.BoundsHalfExtents.x - horizontal &&
                   z >= -config.BoundsHalfExtents.z + horizontal && z <= config.BoundsHalfExtents.z - horizontal &&
                   y >= -config.BoundsHalfExtents.y + config.FloorRoughness + 1 &&
                   y <= config.BoundsHalfExtents.y - config.TunnelHeight - config.CeilingRoughness - 1;
        }

        private static bool IsSeparated(int3 candidate, in FixedList512Bytes<int3> existing,
            int minimumSeparation)
        {
            if (minimumSeparation <= 0) return true;
            long minSquared = (long)minimumSeparation * minimumSeparation;
            for (int i = 0; i < existing.Length; i++)
            {
                int3 delta = candidate - existing[i];
                long squared = (long)delta.x * delta.x + (long)delta.y * delta.y + (long)delta.z * delta.z;
                if (squared < minSquared) return false;
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

        private static Facing FacingFor(int2 direction)
        {
            if (direction.x > 0) return Facing.East;
            if (direction.x < 0) return Facing.West;
            if (direction.y < 0) return Facing.South;
            return Facing.North;
        }

        private static int2 Rotate(int2 direction, int turn) => turn < 0
            ? new int2(-direction.y, direction.x)
            : new int2(direction.y, -direction.x);

        private static bool ChancePercent(ulong seed, int percent)
        {
            if (percent <= 0) return false;
            if (percent >= 100) return true;
            ulong state = FeatureHash.Mix(seed);
            return FeatureHash.Chance(ref state, percent * 65536 / 100);
        }
    }
}
