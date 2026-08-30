using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    [Flags]
    public enum WorldRoadTerrainFlags : byte
    {
        None = 0,
        Blocked = 1 << 0,
        Water = 1 << 1,
        Reserved = 1 << 2,
        Pass = 1 << 3,
        Crossing = 1 << 4,
    }

    [Flags]
    public enum WorldRoadCrossingPolicy : byte
    {
        None = 0,
        AllowPass = 1 << 0,
        AllowWaterCrossing = 1 << 1,
        AllowReserved = 1 << 2,
    }

    public sealed class WorldRoadProfile
    {
        public string Id { get; }
        public string SurfaceId { get; }
        public int CarriagewayWidthDm { get; }
        public int TransitionWidthDm { get; }
        public int MaximumGradePermille { get; }
        public int MaximumCutFillDm { get; }
        public int EdgeVariationDm { get; }
        public int VegetationSuppressionPermille { get; }
        public int TraversalCostPermille { get; }
        public WorldRoadCrossingPolicy CrossingPolicy { get; }

        public int CoreRadiusDm => (CarriagewayWidthDm + 1) / 2;
        public int InfluenceRadiusDm => CoreRadiusDm + TransitionWidthDm + EdgeVariationDm;

        public WorldRoadProfile(
            string id,
            string surfaceId,
            int carriagewayWidthDm,
            int transitionWidthDm,
            int maximumGradePermille,
            int maximumCutFillDm,
            int edgeVariationDm = 0,
            int vegetationSuppressionPermille = 1000,
            int traversalCostPermille = 1000,
            WorldRoadCrossingPolicy crossingPolicy = WorldRoadCrossingPolicy.AllowPass)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Road profile id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(surfaceId)) throw new ArgumentException("Road surface id is required.", nameof(surfaceId));
            if (carriagewayWidthDm < 1) throw new ArgumentOutOfRangeException(nameof(carriagewayWidthDm));
            if (transitionWidthDm < 0) throw new ArgumentOutOfRangeException(nameof(transitionWidthDm));
            if (maximumGradePermille < 1 || maximumGradePermille > 1000) throw new ArgumentOutOfRangeException(nameof(maximumGradePermille));
            if (maximumCutFillDm < 0) throw new ArgumentOutOfRangeException(nameof(maximumCutFillDm));
            if (edgeVariationDm < 0) throw new ArgumentOutOfRangeException(nameof(edgeVariationDm));
            if (vegetationSuppressionPermille < 0 || vegetationSuppressionPermille > 1000) throw new ArgumentOutOfRangeException(nameof(vegetationSuppressionPermille));
            if (traversalCostPermille < 1) throw new ArgumentOutOfRangeException(nameof(traversalCostPermille));

            Id = id;
            SurfaceId = surfaceId;
            CarriagewayWidthDm = carriagewayWidthDm;
            TransitionWidthDm = transitionWidthDm;
            MaximumGradePermille = maximumGradePermille;
            MaximumCutFillDm = maximumCutFillDm;
            EdgeVariationDm = edgeVariationDm;
            VegetationSuppressionPermille = vegetationSuppressionPermille;
            TraversalCostPermille = traversalCostPermille;
            CrossingPolicy = crossingPolicy;
        }
    }

    public static class WorldRoadProfiles
    {
        public static readonly WorldRoadProfile DirtRoad = new WorldRoadProfile(
            "dirt-road", "road-surface", 36, 30, 140, 30, 4, 1000, 820,
            WorldRoadCrossingPolicy.AllowPass | WorldRoadCrossingPolicy.AllowWaterCrossing);

        public static readonly WorldRoadProfile Trail = new WorldRoadProfile(
            "trail", "road-surface", 18, 18, 220, 18, 3, 850, 950,
            WorldRoadCrossingPolicy.AllowPass);
    }

    public readonly struct WorldRoadPlanPoint : IEquatable<WorldRoadPlanPoint>
    {
        public int Xdm { get; }
        public int Zdm { get; }

        public WorldRoadPlanPoint(int xdm, int zdm) { Xdm = xdm; Zdm = zdm; }
        public bool Equals(WorldRoadPlanPoint other) => Xdm == other.Xdm && Zdm == other.Zdm;
        public override bool Equals(object obj) => obj is WorldRoadPlanPoint other && Equals(other);
        public override int GetHashCode() => unchecked((Xdm * 397) ^ Zdm);
        public override string ToString() => $"({Xdm},{Zdm})dm";
    }

    public readonly struct ResolvedWorldRoadPoint : IEquatable<ResolvedWorldRoadPoint>
    {
        public int Xdm { get; }
        public int Ydm { get; }
        public int Zdm { get; }

        public ResolvedWorldRoadPoint(int xdm, int ydm, int zdm) { Xdm = xdm; Ydm = ydm; Zdm = zdm; }
        public bool Equals(ResolvedWorldRoadPoint other) => Xdm == other.Xdm && Ydm == other.Ydm && Zdm == other.Zdm;
        public override bool Equals(object obj) => obj is ResolvedWorldRoadPoint other && Equals(other);
        public override int GetHashCode() => unchecked(((Xdm * 397) ^ Ydm) * 397 ^ Zdm);
        public override string ToString() => $"({Xdm},{Ydm},{Zdm})dm";
    }

    public sealed class WorldRoadIntent
    {
        private readonly WorldRoadPlanPoint[] _controlPoints;

        public string Id { get; }
        public string FromId { get; }
        public string ToId { get; }
        public uint Seed { get; }
        public WorldRoadProfile Profile { get; }
        public string Provenance { get; }
        public IReadOnlyList<WorldRoadPlanPoint> ControlPoints => _controlPoints;

        public WorldRoadIntent(string id, string fromId, string toId, uint seed,
            WorldRoadProfile profile, string provenance, IReadOnlyList<WorldRoadPlanPoint> controlPoints)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Road id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(fromId)) throw new ArgumentException("Road source id is required.", nameof(fromId));
            if (string.IsNullOrWhiteSpace(toId)) throw new ArgumentException("Road destination id is required.", nameof(toId));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(provenance)) throw new ArgumentException("Road provenance is required.", nameof(provenance));
            if (controlPoints == null || controlPoints.Count < 2) throw new ArgumentException("A road requires at least two control points.", nameof(controlPoints));

            Id = id; FromId = fromId; ToId = toId; Seed = seed; Profile = profile; Provenance = provenance;
            _controlPoints = new WorldRoadPlanPoint[controlPoints.Count];
            for (var i = 0; i < _controlPoints.Length; i++) _controlPoints[i] = controlPoints[i];
        }
    }

    public interface IWorldRoadTerrain
    {
        int HeightAtDm(int xdm, int zdm);
        WorldRoadTerrainFlags FlagsAtDm(int xdm, int zdm);
    }

    public enum WorldRoadResolutionStatus : byte
    {
        Resolved = 0,
        Blocked = 1,
        GradeExceeded = 2,
        CutFillExceeded = 3,
        InvalidInput = 4,
    }

    public sealed class ResolvedWorldRoad
    {
        private readonly ResolvedWorldRoadPoint[] _points;
        public WorldRoadIntent Intent { get; }
        public WorldRoadResolutionStatus Status { get; }
        public string FailureReason { get; }
        public IReadOnlyList<ResolvedWorldRoadPoint> Points => _points;
        public bool IsResolved => Status == WorldRoadResolutionStatus.Resolved;

        internal ResolvedWorldRoad(WorldRoadIntent intent, WorldRoadResolutionStatus status,
            string failureReason, IReadOnlyList<ResolvedWorldRoadPoint> points)
        {
            Intent = intent ?? throw new ArgumentNullException(nameof(intent));
            Status = status; FailureReason = failureReason ?? string.Empty;
            if (points == null) { _points = Array.Empty<ResolvedWorldRoadPoint>(); return; }
            _points = new ResolvedWorldRoadPoint[points.Count];
            for (var i = 0; i < _points.Length; i++) _points[i] = points[i];
        }
    }

    public readonly struct WorldRoadInfluenceSample
    {
        public int DistanceDm { get; }
        public int TargetHeightDm { get; }
        public byte Coverage31 { get; }
        public byte VegetationSuppression31 { get; }
        public bool InCore { get; }

        public WorldRoadInfluenceSample(int distanceDm, int targetHeightDm, byte coverage31,
            byte vegetationSuppression31, bool inCore)
        {
            DistanceDm = distanceDm; TargetHeightDm = targetHeightDm; Coverage31 = coverage31;
            VegetationSuppression31 = vegetationSuppression31; InCore = inCore;
        }
    }

    public sealed class WorldRoadInfluence
    {
        private const int EdgeNoiseCellDm = 64;

        public ResolvedWorldRoad Road { get; }

        public WorldRoadInfluence(ResolvedWorldRoad road)
        {
            Road = road ?? throw new ArgumentNullException(nameof(road));
            if (!road.IsResolved || road.Points.Count < 2)
                throw new ArgumentException("Road influence requires resolved geometry.", nameof(road));
        }

        public bool TrySample(int xdm, int zdm, out WorldRoadInfluenceSample sample)
        {
            WorldRoadProfile profile = Road.Intent.Profile;
            IReadOnlyList<ResolvedWorldRoadPoint> presentation = WorldRoadPresentationPath.Build(Road);
            bool found = false;
            WorldRoadInfluenceSample best = default;

            // Physical roads lower the deterministic presentation polyline into bounded corridor
            // pieces. Evaluate exactly the same presentation path here so ecology, placement,
            // material coverage and physical grading remain one shared influence authority while
            // the resolver's original route points remain unchanged.
            for (var i = 0; i + 1 < presentation.Count; i++)
            {
                ClosestPoint(
                    presentation[i], presentation[i + 1], xdm, zdm,
                    out long distanceSquared, out int height,
                    out int centreX, out int centreZ);

                int distance = IntegerSqrt(distanceSquared);
                int edge = DeterministicEdgeOffset(
                    Road.Intent.Seed, centreX, centreZ, profile.EdgeVariationDm);
                int core = Math.Max(0, profile.CoreRadiusDm + edge);
                int outer = Math.Max(core, profile.CoreRadiusDm + profile.TransitionWidthDm + edge);
                if (distance > outer) continue;

                int coverage = distance <= core || outer == core
                    ? 31
                    : ((outer - distance) * 31 + (outer - core) / 2) / (outer - core);
                coverage = Clamp(coverage, 0, 31);
                if (coverage <= 0) continue;

                int vegetation = Clamp(
                    coverage * profile.VegetationSuppressionPermille / 1000, 0, 31);
                int targetHeight = height
                    + WorldRoadPresentationPath.CrossSectionOffsetDm(distance, core, outer);
                var candidate = new WorldRoadInfluenceSample(
                    distance, targetHeight, (byte)coverage, (byte)vegetation, distance <= core);
                if (!found
                    || candidate.Coverage31 > best.Coverage31
                    || candidate.Coverage31 == best.Coverage31
                       && candidate.DistanceDm < best.DistanceDm)
                {
                    best = candidate;
                    found = true;
                }
            }

            sample = best;
            return found;
        }

        private static void ClosestPoint(
            ResolvedWorldRoadPoint a,
            ResolvedWorldRoadPoint b,
            int x,
            int z,
            out long distanceSquared,
            out int height,
            out int centreX,
            out int centreZ)
        {
            long dx = (long)b.Xdm - a.Xdm, dz = (long)b.Zdm - a.Zdm;
            long lengthSquared = dx * dx + dz * dz;
            if (lengthSquared <= 0)
            {
                long px = (long)x - a.Xdm, pz = (long)z - a.Zdm;
                distanceSquared = px * px + pz * pz;
                height = a.Ydm;
                centreX = a.Xdm;
                centreZ = a.Zdm;
                return;
            }

            long dot = ((long)x - a.Xdm) * dx + ((long)z - a.Zdm) * dz;
            dot = Math.Max(0L, Math.Min(lengthSquared, dot));
            long qx = (long)a.Xdm + DivideRounded(dx * dot, lengthSquared);
            long qz = (long)a.Zdm + DivideRounded(dz * dot, lengthSquared);
            long ex = (long)x - qx, ez = (long)z - qz;
            distanceSquared = ex * ex + ez * ez;
            height = a.Ydm + (int)DivideRounded(((long)b.Ydm - a.Ydm) * dot, lengthSquared);
            centreX = (int)qx;
            centreZ = (int)qz;
        }

        private static int DeterministicEdgeOffset(uint seed, int x, int z, int amplitude)
        {
            if (amplitude <= 0) return 0;

            int cellX = FloorDiv(x, EdgeNoiseCellDm);
            int cellZ = FloorDiv(z, EdgeNoiseCellDm);
            int localX = x - cellX * EdgeNoiseCellDm;
            int localZ = z - cellZ * EdgeNoiseCellDm;
            int v00 = EdgeNoiseValue(seed, cellX, cellZ, amplitude);
            int v10 = EdgeNoiseValue(seed, cellX + 1, cellZ, amplitude);
            int v01 = EdgeNoiseValue(seed, cellX, cellZ + 1, amplitude);
            int v11 = EdgeNoiseValue(seed, cellX + 1, cellZ + 1, amplitude);
            int x0 = LerpRounded(v00, v10, localX, EdgeNoiseCellDm);
            int x1 = LerpRounded(v01, v11, localX, EdgeNoiseCellDm);
            return LerpRounded(x0, x1, localZ, EdgeNoiseCellDm);
        }

        private static int EdgeNoiseValue(uint seed, int x, int z, int amplitude)
        {
            unchecked
            {
                uint h = seed ^ 0x9E3779B9u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)z) * 16777619u;
                return (int)(h % (uint)(amplitude * 2 + 1)) - amplitude;
            }
        }

        private static int LerpRounded(int a, int b, int numerator, int denominator)
            => a + (int)DivideRounded((long)(b - a) * numerator, denominator);

        private static int FloorDiv(int value, int divisor)
        {
            int q = value / divisor;
            int r = value % divisor;
            return r != 0 && value < 0 ? q - 1 : q;
        }

        private static long DivideRounded(long numerator, long denominator)
        {
            if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
            if (numerator >= 0) return (numerator + denominator / 2) / denominator;
            return -((-numerator + denominator / 2) / denominator);
        }

        private static int IntegerSqrt(long value)
        {
            if (value <= 0) return 0;
            long low = 1;
            long high = Math.Min(value, 3037000499L);
            while (low <= high)
            {
                long middle = low + ((high - low) >> 1);
                if (middle <= value / middle) low = middle + 1;
                else high = middle - 1;
            }

            long root = high;
            long lowerError = value - root * root;
            long next = root + 1;
            if (next <= 3037000499L)
            {
                long upperError = next * next - value;
                if (upperError <= lowerError) root = next;
            }
            return root > int.MaxValue ? int.MaxValue : (int)root;
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }

    public static class WorldRoadResolver
    {
        private static readonly int[] NeighborX = { 1, 0, -1, 0, 1, -1, -1, 1 };
        private static readonly int[] NeighborZ = { 0, 1, 0, -1, 1, 1, -1, -1 };

        public static ResolvedWorldRoad Resolve(WorldRoadIntent intent, IWorldRoadTerrain terrain,
            int sampleSpacingDm = 40, int searchMarginCells = 8)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (terrain == null) throw new ArgumentNullException(nameof(terrain));
            if (sampleSpacingDm < 1) throw new ArgumentOutOfRangeException(nameof(sampleSpacingDm));
            if (searchMarginCells < 0) throw new ArgumentOutOfRangeException(nameof(searchMarginCells));

            var routed = new List<WorldRoadPlanPoint>();
            for (var control = 0; control + 1 < intent.ControlPoints.Count; control++)
            {
                if (!TryRouteLeg(intent.ControlPoints[control], intent.ControlPoints[control + 1], intent.Profile,
                    terrain, sampleSpacingDm, searchMarginCells, out List<WorldRoadPlanPoint> leg))
                    return new ResolvedWorldRoad(intent, WorldRoadResolutionStatus.Blocked,
                        "No terrain-valid corridor could be resolved between control points.", null);
                if (routed.Count > 0 && leg.Count > 0) leg.RemoveAt(0);
                routed.AddRange(leg);
            }

            if (routed.Count < 2)
                return new ResolvedWorldRoad(intent, WorldRoadResolutionStatus.InvalidInput,
                    "Resolved road has fewer than two points.", null);

            var resolved = new List<ResolvedWorldRoadPoint>(routed.Count);
            for (var i = 0; i < routed.Count; i++)
            {
                var p = routed[i];
                resolved.Add(new ResolvedWorldRoadPoint(p.Xdm, terrain.HeightAtDm(p.Xdm, p.Zdm), p.Zdm));
            }
            WorldRoadResolutionStatus status = Grade(resolved, intent.Profile, terrain, out string failure);
            return new ResolvedWorldRoad(intent, status, failure,
                status == WorldRoadResolutionStatus.Resolved ? resolved : null);
        }

        private static bool TryRouteLeg(WorldRoadPlanPoint from, WorldRoadPlanPoint to, WorldRoadProfile profile,
            IWorldRoadTerrain terrain, int spacing, int margin, out List<WorldRoadPlanPoint> result)
        {
            result = null;
            if (!Allowed(terrain.FlagsAtDm(from.Xdm, from.Zdm), profile.CrossingPolicy) ||
                !Allowed(terrain.FlagsAtDm(to.Xdm, to.Zdm), profile.CrossingPolicy))
                return false;

            int fx = FloorDiv(from.Xdm, spacing), fz = FloorDiv(from.Zdm, spacing);
            int tx = FloorDiv(to.Xdm, spacing), tz = FloorDiv(to.Zdm, spacing);
            int minX = Math.Min(fx, tx) - margin, maxX = Math.Max(fx, tx) + margin;
            int minZ = Math.Min(fz, tz) - margin, maxZ = Math.Max(fz, tz) + margin;
            int width = maxX - minX + 1, count = width * (maxZ - minZ + 1);
            int start = Index(fx, fz, minX, minZ, width), goal = Index(tx, tz, minX, minZ, width);
            if (!Allowed(terrain.FlagsAtDm(fx * spacing, fz * spacing), profile.CrossingPolicy) ||
                !Allowed(terrain.FlagsAtDm(tx * spacing, tz * spacing), profile.CrossingPolicy))
                return false;

            var open = new bool[count]; var closed = new bool[count];
            var cost = new long[count]; var previous = new int[count];
            for (var i = 0; i < count; i++) { cost[i] = long.MaxValue; previous[i] = -1; }
            cost[start] = 0; open[start] = true;

            while (true)
            {
                int current = -1; long bestScore = long.MaxValue;
                for (var i = 0; i < count; i++)
                {
                    if (!open[i]) continue;
                    Cell(i, minX, minZ, width, out int cx, out int cz);
                    long score = cost[i] + Heuristic(cx, cz, tx, tz, spacing, profile.TraversalCostPermille);
                    if (score < bestScore || score == bestScore && (current < 0 || i < current)) { bestScore = score; current = i; }
                }
                if (current < 0 || current == goal) break;
                open[current] = false; closed[current] = true;
                Cell(current, minX, minZ, width, out int x, out int z);
                int currentHeight = terrain.HeightAtDm(x * spacing, z * spacing);
                for (var n = 0; n < NeighborX.Length; n++)
                {
                    int nx = x + NeighborX[n], nz = z + NeighborZ[n];
                    if (nx < minX || nx > maxX || nz < minZ || nz > maxZ) continue;
                    int next = Index(nx, nz, minX, minZ, width);
                    if (closed[next]) continue;
                    int nextXdm = nx * spacing, nextZdm = nz * spacing;
                    if (!Allowed(terrain.FlagsAtDm(nextXdm, nextZdm), profile.CrossingPolicy)) continue;
                    int nextHeight = terrain.HeightAtDm(nextXdm, nextZdm);
                    int planar = n < 4 ? spacing : DiagonalDistance(spacing);
                    int rise = Math.Abs(nextHeight - currentHeight);
                    int maximumGradedRise = Math.Max(1, planar * profile.MaximumGradePermille / 1000);
                    int maximumRecoverableRise = maximumGradedRise + profile.MaximumCutFillDm * 2;
                    if (rise > maximumRecoverableRise) continue;

                    long edgeCost = planar * 1000L + rise * 250L;
                    long tentative = cost[current] + ScaleTraversalCost(edgeCost, profile.TraversalCostPermille);
                    if (tentative >= cost[next]) continue;
                    cost[next] = tentative; previous[next] = current; open[next] = true;
                }
            }
            if (cost[goal] == long.MaxValue) return false;

            var cells = new List<int>();
            for (int cursor = goal; cursor >= 0; cursor = previous[cursor])
            { cells.Add(cursor); if (cursor == start) break; }
            if (cells.Count == 0 || cells[cells.Count - 1] != start) return false;
            cells.Reverse();
            result = new List<WorldRoadPlanPoint>(cells.Count + 2) { from };
            for (var i = 1; i + 1 < cells.Count; i++)
            {
                Cell(cells[i], minX, minZ, width, out int x, out int z);
                result.Add(new WorldRoadPlanPoint(x * spacing, z * spacing));
            }
            result.Add(to); RemoveCollinear(result); return true;
        }

        private static WorldRoadResolutionStatus Grade(List<ResolvedWorldRoadPoint> points,
            WorldRoadProfile profile, IWorldRoadTerrain terrain, out string failure)
        {
            var heights = new int[points.Count];
            for (var i = 0; i < points.Count; i++) heights[i] = points[i].Ydm;
            for (var pass = 0; pass < 3; pass++)
            {
                for (var i = 1; i < points.Count; i++)
                    heights[i] = ClampGrade(points[i - 1], points[i], heights[i - 1], heights[i], profile.MaximumGradePermille);
                for (var i = points.Count - 2; i >= 0; i--)
                    heights[i] = ClampGrade(points[i + 1], points[i], heights[i + 1], heights[i], profile.MaximumGradePermille);
            }
            for (var i = 0; i < points.Count; i++)
            {
                int terrainHeight = terrain.HeightAtDm(points[i].Xdm, points[i].Zdm);
                int delta = Math.Abs(heights[i] - terrainHeight);
                if (delta > profile.MaximumCutFillDm)
                {
                    failure = $"Road '{profile.Id}' requires {delta}dm cut/fill at point {i}, exceeding {profile.MaximumCutFillDm}dm.";
                    return WorldRoadResolutionStatus.CutFillExceeded;
                }
                points[i] = new ResolvedWorldRoadPoint(points[i].Xdm, heights[i], points[i].Zdm);
            }
            for (var i = 1; i < points.Count; i++)
            {
                int run = Math.Max(1, Distance(points[i - 1], points[i]));
                int rise = Math.Abs(points[i].Ydm - points[i - 1].Ydm);
                if ((long)rise * 1000L > (long)profile.MaximumGradePermille * run)
                { failure = $"Road '{profile.Id}' exceeds maximum grade between points {i - 1} and {i}."; return WorldRoadResolutionStatus.GradeExceeded; }
            }
            failure = string.Empty; return WorldRoadResolutionStatus.Resolved;
        }

        private static int ClampGrade(ResolvedWorldRoadPoint from, ResolvedWorldRoadPoint to,
            int fromHeight, int desiredHeight, int maximumGradePermille)
        {
            int run = Math.Max(1, Distance(from, to));
            int maximumRise = Math.Max(1, run * maximumGradePermille / 1000);
            if (desiredHeight > fromHeight + maximumRise) return fromHeight + maximumRise;
            if (desiredHeight < fromHeight - maximumRise) return fromHeight - maximumRise;
            return desiredHeight;
        }

        private static bool Allowed(WorldRoadTerrainFlags flags, WorldRoadCrossingPolicy policy)
        {
            if ((flags & WorldRoadTerrainFlags.Blocked) != 0) return false;
            if ((flags & WorldRoadTerrainFlags.Water) != 0 && (policy & WorldRoadCrossingPolicy.AllowWaterCrossing) == 0) return false;
            if ((flags & WorldRoadTerrainFlags.Reserved) != 0 && (policy & WorldRoadCrossingPolicy.AllowReserved) == 0) return false;
            if ((flags & WorldRoadTerrainFlags.Pass) != 0 && (policy & WorldRoadCrossingPolicy.AllowPass) == 0) return false;
            return true;
        }

        private static long Heuristic(int x, int z, int goalX, int goalZ, int spacing, int traversalCostPermille)
        {
            int dx = Math.Abs(x - goalX), dz = Math.Abs(z - goalZ);
            int diagonalCells = Math.Min(dx, dz);
            int straightCells = Math.Max(dx, dz) - diagonalCells;
            long planarDm = (long)diagonalCells * DiagonalDistance(spacing) + (long)straightCells * spacing;
            return ScaleTraversalCost(planarDm * 1000L, traversalCostPermille);
        }

        private static long ScaleTraversalCost(long cost, int traversalCostPermille)
            => (cost * traversalCostPermille + 999L) / 1000L;

        private static int DiagonalDistance(int spacing)
            => IntegerSqrt((long)spacing * spacing * 2L);

        private static int Distance(ResolvedWorldRoadPoint a, ResolvedWorldRoadPoint b)
        {
            long dx = (long)b.Xdm - a.Xdm, dz = (long)b.Zdm - a.Zdm;
            return Math.Max(1, IntegerSqrt(dx * dx + dz * dz));
        }

        private static int IntegerSqrt(long value)
        {
            if (value <= 0) return 0;
            long low = 1;
            long high = Math.Min(value, 3037000499L);
            while (low <= high)
            {
                long middle = low + ((high - low) >> 1);
                if (middle <= value / middle) low = middle + 1;
                else high = middle - 1;
            }

            long root = high;
            long lowerError = value - root * root;
            long next = root + 1;
            if (next <= 3037000499L)
            {
                long upperError = next * next - value;
                if (upperError <= lowerError) root = next;
            }
            return root > int.MaxValue ? int.MaxValue : (int)root;
        }

        private static void RemoveCollinear(List<WorldRoadPlanPoint> points)
        {
            for (var i = points.Count - 2; i > 0; i--)
            {
                long ax = (long)points[i].Xdm - points[i - 1].Xdm, az = (long)points[i].Zdm - points[i - 1].Zdm;
                long bx = (long)points[i + 1].Xdm - points[i].Xdm, bz = (long)points[i + 1].Zdm - points[i].Zdm;
                if (ax * bz == az * bx) points.RemoveAt(i);
            }
        }

        private static int Index(int x, int z, int minX, int minZ, int width) => (x - minX) + (z - minZ) * width;
        private static void Cell(int index, int minX, int minZ, int width, out int x, out int z) { x = minX + index % width; z = minZ + index / width; }
        private static int FloorDiv(int value, int divisor) { int q = value / divisor, r = value % divisor; return r != 0 && value < 0 ? q - 1 : q; }
    }
}
