using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MountingForce.WorldGen
{
    [Flags]
    public enum ReservationCategory : ushort
    {
        None = 0,
        Building = 1 << 0,
        Plaza = 1 << 1,
        SettlementEnvelope = 1 << 2,
        Road = 1 << 3,
        PublicAccess = 1 << 4,
        StructuralChild = 1 << 5,
        Vegetation = 1 << 6,
        Underground = 1 << 7,
        Landmark = 1 << 8,
        Geographic = 1 << 9,
        All = Building | Plaza | SettlementEnvelope | Road | PublicAccess |
              StructuralChild | Vegetation | Underground | Landmark | Geographic,
    }

    [Flags]
    public enum ReservationSemantics : byte
    {
        None = 0,
        HardOccupancy = 1 << 0,
        Clearance = 1 << 1,
        ProtectedCorridor = 1 << 2,
        CompatibleHandoff = 1 << 3,
        SoftYield = 1 << 4,
    }

    [Flags]
    public enum ReservationConsumerKind : ushort
    {
        None = 0,
        SettlementBuilding = 1 << 0,
        Road = 1 << 1,
        StructuralChild = 1 << 2,
        Vegetation = 1 << 3,
        Underground = 1 << 4,
        Connector = 1 << 5,
        Landmark = 1 << 6,
        Inspection = 1 << 7,
        All = SettlementBuilding | Road | StructuralChild | Vegetation |
              Underground | Connector | Landmark | Inspection,
    }

    public enum ReservationShapeKind : byte
    {
        Box = 0,
        Corridor = 1,
    }

    public enum ReservationDecision : byte
    {
        Accepted = 0,
        Yield = 1,
        Rejected = 2,
    }

    public enum ReservationReasonCode : byte
    {
        None = 0,
        NoIntersection = 1,
        CompatibleHandoff = 2,
        SoftYield = 3,
        ClearanceConflict = 4,
        ProtectedCorridorConflict = 5,
        HardOccupancyConflict = 6,
        LowerPrecedence = 7,
        StableIdentityTieBreak = 8,
    }

    /// <summary>
    /// Stable reservation identity derived from semantic inputs rather than runtime insertion order.
    /// The hash is deliberately implemented here instead of using string.GetHashCode(), whose value is
    /// not a world-generation persistence contract.
    /// </summary>
    public readonly struct ReservationId : IEquatable<ReservationId>, IComparable<ReservationId>
    {
        public readonly ulong Value;

        public ReservationId(ulong value)
        {
            if (value == 0UL) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }

        public static ReservationId FromStableText(string ownerId, ReservationCategory category, int ordinal = 0)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                throw new ArgumentException("Reservation owner id is required.", nameof(ownerId));

            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < ownerId.Length; i++)
            {
                char c = ownerId[i];
                hash ^= (byte)c;
                hash *= 1099511628211UL;
                hash ^= (byte)(c >> 8);
                hash *= 1099511628211UL;
            }

            hash ^= (ushort)category;
            hash *= 1099511628211UL;
            unchecked
            {
                hash ^= (uint)ordinal;
                hash *= 1099511628211UL;
            }
            return new ReservationId(hash == 0UL ? 1UL : hash);
        }

        public bool Equals(ReservationId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ReservationId other && Equals(other);
        public override int GetHashCode() => unchecked((int)(Value ^ (Value >> 32)));
        public int CompareTo(ReservationId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString("X16");
    }

    /// <summary>Half-open integer-decimetre bounds: [Min, Max) on every axis.</summary>
    public readonly struct ReservationBoundsDm : IEquatable<ReservationBoundsDm>
    {
        public readonly int MinX;
        public readonly int MinY;
        public readonly int MinZ;
        public readonly int MaxX;
        public readonly int MaxY;
        public readonly int MaxZ;

        public ReservationBoundsDm(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
        {
            if (maxX <= minX) throw new ArgumentOutOfRangeException(nameof(maxX));
            if (maxY <= minY) throw new ArgumentOutOfRangeException(nameof(maxY));
            if (maxZ <= minZ) throw new ArgumentOutOfRangeException(nameof(maxZ));
            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            MaxX = maxX;
            MaxY = maxY;
            MaxZ = maxZ;
        }

        public static ReservationBoundsDm FromFootprint(Int2 positionDm, Int3 envelopeDm, int baseYDm = 0)
        {
            if (envelopeDm.X <= 0 || envelopeDm.Y <= 0 || envelopeDm.Z <= 0)
                throw new ArgumentOutOfRangeException(nameof(envelopeDm));
            return new ReservationBoundsDm(
                positionDm.X, baseYDm, positionDm.Y,
                checked(positionDm.X + envelopeDm.X), checked(baseYDm + envelopeDm.Y),
                checked(positionDm.Y + envelopeDm.Z));
        }

        public ReservationBoundsDm ExpandHorizontal(int amountDm)
        {
            if (amountDm < 0) throw new ArgumentOutOfRangeException(nameof(amountDm));
            return new ReservationBoundsDm(
                checked(MinX - amountDm), MinY, checked(MinZ - amountDm),
                checked(MaxX + amountDm), MaxY, checked(MaxZ + amountDm));
        }

        public bool Intersects(in ReservationBoundsDm other) =>
            MaxX > other.MinX && MinX < other.MaxX &&
            MaxY > other.MinY && MinY < other.MaxY &&
            MaxZ > other.MinZ && MinZ < other.MaxZ;

        public bool IntersectsXZ(in ReservationBoundsDm other) =>
            MaxX > other.MinX && MinX < other.MaxX &&
            MaxZ > other.MinZ && MinZ < other.MaxZ;

        public bool Equals(ReservationBoundsDm other) =>
            MinX == other.MinX && MinY == other.MinY && MinZ == other.MinZ &&
            MaxX == other.MaxX && MaxY == other.MaxY && MaxZ == other.MaxZ;
        public override bool Equals(object obj) => obj is ReservationBoundsDm other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int h = MinX;
                h = h * 397 ^ MinY;
                h = h * 397 ^ MinZ;
                h = h * 397 ^ MaxX;
                h = h * 397 ^ MaxY;
                h = h * 397 ^ MaxZ;
                return h;
            }
        }

        public override string ToString() =>
            "[" + MinX + "," + MinY + "," + MinZ + " -> " + MaxX + "," + MaxY + "," + MaxZ + ")";
    }

    /// <summary>
    /// One immutable semantic claim. Terrain suitability, biome scoring, water depth, quest state,
    /// aesthetics and route solving are deliberately not represented here; their owners may publish
    /// spatial claims after making those decisions.
    /// </summary>
    public readonly struct SpatialReservation
    {
        public readonly ReservationId Id;
        public readonly string OwnerId;
        public readonly string Provenance;
        public readonly ReservationCategory Category;
        public readonly ReservationSemantics Semantics;
        public readonly ReservationConsumerKind CompatibleConsumers;
        public readonly int Precedence;
        public readonly ReservationShapeKind ShapeKind;
        public readonly ReservationBoundsDm Bounds;
        public readonly Int2 CorridorStartDm;
        public readonly Int2 CorridorEndDm;
        public readonly int CorridorRadiusDm;

        private SpatialReservation(
            ReservationId id,
            string ownerId,
            string provenance,
            ReservationCategory category,
            ReservationSemantics semantics,
            ReservationConsumerKind compatibleConsumers,
            int precedence,
            ReservationShapeKind shapeKind,
            ReservationBoundsDm bounds,
            Int2 corridorStartDm,
            Int2 corridorEndDm,
            int corridorRadiusDm)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                throw new ArgumentException("Reservation owner id is required.", nameof(ownerId));
            if (category == ReservationCategory.None)
                throw new ArgumentOutOfRangeException(nameof(category));
            if (semantics == ReservationSemantics.None)
                throw new ArgumentOutOfRangeException(nameof(semantics));
            Id = id;
            OwnerId = ownerId;
            Provenance = provenance ?? string.Empty;
            Category = category;
            Semantics = semantics;
            CompatibleConsumers = compatibleConsumers;
            Precedence = precedence;
            ShapeKind = shapeKind;
            Bounds = bounds;
            CorridorStartDm = corridorStartDm;
            CorridorEndDm = corridorEndDm;
            CorridorRadiusDm = corridorRadiusDm;
        }

        public static SpatialReservation Box(
            string ownerId,
            ReservationCategory category,
            ReservationSemantics semantics,
            ReservationBoundsDm bounds,
            int precedence = 0,
            ReservationConsumerKind compatibleConsumers = ReservationConsumerKind.None,
            string provenance = "",
            int ordinal = 0)
        {
            return new SpatialReservation(
                ReservationId.FromStableText(ownerId, category, ordinal),
                ownerId, provenance, category, semantics, compatibleConsumers, precedence,
                ReservationShapeKind.Box, bounds, default(Int2), default(Int2), 0);
        }

        public static SpatialReservation Corridor(
            string ownerId,
            ReservationCategory category,
            ReservationSemantics semantics,
            Int2 startDm,
            Int2 endDm,
            int minYDm,
            int maxYDm,
            int radiusDm,
            int precedence = 0,
            ReservationConsumerKind compatibleConsumers = ReservationConsumerKind.None,
            string provenance = "",
            int ordinal = 0)
        {
            if (maxYDm <= minYDm) throw new ArgumentOutOfRangeException(nameof(maxYDm));
            if (radiusDm < 0) throw new ArgumentOutOfRangeException(nameof(radiusDm));
            int minX = Math.Min(startDm.X, endDm.X) - radiusDm;
            int maxX = Math.Max(startDm.X, endDm.X) + radiusDm + 1;
            int minZ = Math.Min(startDm.Y, endDm.Y) - radiusDm;
            int maxZ = Math.Max(startDm.Y, endDm.Y) + radiusDm + 1;
            return new SpatialReservation(
                ReservationId.FromStableText(ownerId, category, ordinal),
                ownerId, provenance, category, semantics, compatibleConsumers, precedence,
                ReservationShapeKind.Corridor,
                new ReservationBoundsDm(minX, minYDm, minZ, maxX, maxYDm, maxZ),
                startDm, endDm, radiusDm);
        }

        public bool Intersects(in SpatialReservation other)
        {
            if (!Bounds.Intersects(other.Bounds)) return false;
            if (ShapeKind == ReservationShapeKind.Box && other.ShapeKind == ReservationShapeKind.Box)
                return true;
            if (ShapeKind == ReservationShapeKind.Corridor && other.ShapeKind == ReservationShapeKind.Box)
                return CorridorIntersectsBox(this, other.Bounds);
            if (ShapeKind == ReservationShapeKind.Box && other.ShapeKind == ReservationShapeKind.Corridor)
                return CorridorIntersectsBox(other, Bounds);

            // Two corridor AABBs are a conservative narrow phase. They still preserve true vertical
            // separation, which is the critical underground stacking contract. Route solvers retain
            // ownership of exact path geometry and may publish tighter corridor segments when needed.
            return true;
        }

        private static bool CorridorIntersectsBox(in SpatialReservation corridor, in ReservationBoundsDm box)
        {
            if (corridor.Bounds.MaxY <= box.MinY || corridor.Bounds.MinY >= box.MaxY) return false;

            int minX = box.MinX - corridor.CorridorRadiusDm;
            int maxX = box.MaxX + corridor.CorridorRadiusDm;
            int minZ = box.MinZ - corridor.CorridorRadiusDm;
            int maxZ = box.MaxZ + corridor.CorridorRadiusDm;
            Int2 a = corridor.CorridorStartDm;
            Int2 b = corridor.CorridorEndDm;
            if (PointInside(a, minX, minZ, maxX, maxZ) || PointInside(b, minX, minZ, maxX, maxZ))
                return true;

            var r0 = new Int2(minX, minZ);
            var r1 = new Int2(maxX, minZ);
            var r2 = new Int2(maxX, maxZ);
            var r3 = new Int2(minX, maxZ);
            return SegmentsIntersect(a, b, r0, r1)
                || SegmentsIntersect(a, b, r1, r2)
                || SegmentsIntersect(a, b, r2, r3)
                || SegmentsIntersect(a, b, r3, r0);
        }

        private static bool PointInside(Int2 p, int minX, int minZ, int maxX, int maxZ) =>
            p.X >= minX && p.X <= maxX && p.Y >= minZ && p.Y <= maxZ;

        private static bool SegmentsIntersect(Int2 a, Int2 b, Int2 c, Int2 d)
        {
            long o1 = Cross(a, b, c);
            long o2 = Cross(a, b, d);
            long o3 = Cross(c, d, a);
            long o4 = Cross(c, d, b);
            if (((o1 > 0 && o2 < 0) || (o1 < 0 && o2 > 0))
                && ((o3 > 0 && o4 < 0) || (o3 < 0 && o4 > 0)))
                return true;
            if (o1 == 0 && OnSegment(a, b, c)) return true;
            if (o2 == 0 && OnSegment(a, b, d)) return true;
            if (o3 == 0 && OnSegment(c, d, a)) return true;
            return o4 == 0 && OnSegment(c, d, b);
        }

        private static long Cross(Int2 a, Int2 b, Int2 c) =>
            ((long)b.X - a.X) * ((long)c.Y - a.Y)
            - ((long)b.Y - a.Y) * ((long)c.X - a.X);

        private static bool OnSegment(Int2 a, Int2 b, Int2 p) =>
            p.X >= Math.Min(a.X, b.X) && p.X <= Math.Max(a.X, b.X)
            && p.Y >= Math.Min(a.Y, b.Y) && p.Y <= Math.Max(a.Y, b.Y);
    }

    public readonly struct ReservationConflict
    {
        public readonly SpatialReservation Existing;
        public readonly ReservationReasonCode Reason;
        public readonly bool CompatibilityApplied;

        public ReservationConflict(
            in SpatialReservation existing,
            ReservationReasonCode reason,
            bool compatibilityApplied)
        {
            Existing = existing;
            Reason = reason;
            CompatibilityApplied = compatibilityApplied;
        }
    }

    public readonly struct ReservationQueryMetrics
    {
        public readonly int BucketsVisited;
        public readonly int BroadPhaseCandidates;
        public readonly int NarrowPhaseTests;
        public readonly int Intersections;
        public readonly int RejectedConflicts;
        public readonly int YieldConflicts;
        public readonly long ElapsedStopwatchTicks;

        public ReservationQueryMetrics(
            int bucketsVisited,
            int broadPhaseCandidates,
            int narrowPhaseTests,
            int intersections,
            int rejectedConflicts,
            int yieldConflicts,
            long elapsedStopwatchTicks)
        {
            BucketsVisited = bucketsVisited;
            BroadPhaseCandidates = broadPhaseCandidates;
            NarrowPhaseTests = narrowPhaseTests;
            Intersections = intersections;
            RejectedConflicts = rejectedConflicts;
            YieldConflicts = yieldConflicts;
            ElapsedStopwatchTicks = elapsedStopwatchTicks;
        }
    }

    public sealed class ReservationQueryResult
    {
        private readonly ReservationConflict[] _conflicts;

        public ReservationQueryResult(
            ReservationDecision decision,
            ReservationReasonCode reason,
            ReservationConflict[] conflicts,
            ReservationQueryMetrics metrics)
        {
            Decision = decision;
            Reason = reason;
            _conflicts = conflicts ?? Array.Empty<ReservationConflict>();
            Metrics = metrics;
        }

        public ReservationDecision Decision { get; }
        public ReservationReasonCode Reason { get; }
        public IReadOnlyList<ReservationConflict> Conflicts => _conflicts;
        public ReservationQueryMetrics Metrics { get; }
        public bool IsAccepted => Decision == ReservationDecision.Accepted;
        public bool ShouldYield => Decision == ReservationDecision.Yield;

        public string Describe()
        {
            if (_conflicts.Length == 0)
                return "decision=" + Decision + ";reason=" + Reason + ";conflicts=none";

            ReservationConflict conflict = _conflicts[0];
            SpatialReservation existing = conflict.Existing;
            return "decision=" + Decision
                + ";reason=" + Reason
                + ";conflictId=" + existing.Id
                + ";owner=" + existing.OwnerId
                + ";category=" + existing.Category
                + ";semantics=" + existing.Semantics
                + ";bounds=" + existing.Bounds
                + ";precedence=" + existing.Precedence
                + ";compatibleConsumers=" + existing.CompatibleConsumers
                + ";provenance=" + existing.Provenance;
        }
    }

    /// <summary>
    /// Immutable bounded query view. Reservations are clipped to a caller-owned planning window before
    /// deterministic integer bucketing, so a long route or macro envelope does not materialize buckets
    /// outside that window.
    /// </summary>
    public sealed class SpatialReservationSnapshot
    {
        private readonly struct BucketKey : IEquatable<BucketKey>
        {
            public readonly int X;
            public readonly int Z;
            public BucketKey(int x, int z) { X = x; Z = z; }
            public bool Equals(BucketKey other) => X == other.X && Z == other.Z;
            public override bool Equals(object obj) => obj is BucketKey other && Equals(other);
            public override int GetHashCode() { unchecked { return X * 397 ^ Z; } }
        }

        private readonly SpatialReservation[] _reservations;
        private readonly Dictionary<BucketKey, int[]> _buckets;
        private readonly ReservationBoundsDm _window;
        private readonly int _bucketSizeDm;

        private SpatialReservationSnapshot(
            SpatialReservation[] reservations,
            Dictionary<BucketKey, int[]> buckets,
            ReservationBoundsDm window,
            int bucketSizeDm)
        {
            _reservations = reservations;
            _buckets = buckets;
            _window = window;
            _bucketSizeDm = bucketSizeDm;
        }

        public IReadOnlyList<SpatialReservation> Reservations => _reservations;
        public ReservationBoundsDm Window => _window;
        public int BucketSizeDm => _bucketSizeDm;

        public static SpatialReservationSnapshot Create(
            IEnumerable<SpatialReservation> source,
            ReservationBoundsDm window,
            int bucketSizeDm = 128)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (bucketSizeDm <= 0) throw new ArgumentOutOfRangeException(nameof(bucketSizeDm));

            var reservations = new List<SpatialReservation>();
            foreach (SpatialReservation reservation in source)
                if (reservation.Bounds.Intersects(window)) reservations.Add(reservation);
            reservations.Sort(CompareStable);

            var lists = new Dictionary<BucketKey, List<int>>();
            for (int i = 0; i < reservations.Count; i++)
            {
                ReservationBoundsDm b = reservations[i].Bounds;
                int minX = Math.Max(b.MinX, window.MinX);
                int maxX = Math.Min(b.MaxX, window.MaxX) - 1;
                int minZ = Math.Max(b.MinZ, window.MinZ);
                int maxZ = Math.Min(b.MaxZ, window.MaxZ) - 1;
                if (maxX < minX || maxZ < minZ) continue;

                int bx0 = FloorDiv(minX, bucketSizeDm);
                int bx1 = FloorDiv(maxX, bucketSizeDm);
                int bz0 = FloorDiv(minZ, bucketSizeDm);
                int bz1 = FloorDiv(maxZ, bucketSizeDm);
                for (int bz = bz0; bz <= bz1; bz++)
                for (int bx = bx0; bx <= bx1; bx++)
                {
                    var key = new BucketKey(bx, bz);
                    List<int> list;
                    if (!lists.TryGetValue(key, out list))
                    {
                        list = new List<int>();
                        lists.Add(key, list);
                    }
                    list.Add(i);
                }
            }

            var buckets = new Dictionary<BucketKey, int[]>(lists.Count);
            foreach (KeyValuePair<BucketKey, List<int>> pair in lists)
                buckets.Add(pair.Key, pair.Value.ToArray());
            return new SpatialReservationSnapshot(reservations.ToArray(), buckets, window, bucketSizeDm);
        }

        public ReservationQueryResult Query(
            in SpatialReservation candidate,
            ReservationConsumerKind consumer,
            ReservationCategory categoryMask = ReservationCategory.All)
        {
            long started = Stopwatch.GetTimestamp();
            if (!candidate.Bounds.Intersects(_window))
                return Result(ReservationDecision.Accepted, ReservationReasonCode.NoIntersection,
                    Array.Empty<ReservationConflict>(), 0, 0, 0, 0, 0, 0, started);

            int minX = Math.Max(candidate.Bounds.MinX, _window.MinX);
            int maxX = Math.Min(candidate.Bounds.MaxX, _window.MaxX) - 1;
            int minZ = Math.Max(candidate.Bounds.MinZ, _window.MinZ);
            int maxZ = Math.Min(candidate.Bounds.MaxZ, _window.MaxZ) - 1;
            int bx0 = FloorDiv(minX, _bucketSizeDm);
            int bx1 = FloorDiv(maxX, _bucketSizeDm);
            int bz0 = FloorDiv(minZ, _bucketSizeDm);
            int bz1 = FloorDiv(maxZ, _bucketSizeDm);

            var candidateIndices = new List<int>();
            int bucketsVisited = 0;
            for (int bz = bz0; bz <= bz1; bz++)
            for (int bx = bx0; bx <= bx1; bx++)
            {
                bucketsVisited++;
                int[] indices;
                if (!_buckets.TryGetValue(new BucketKey(bx, bz), out indices)) continue;
                for (int i = 0; i < indices.Length; i++) candidateIndices.Add(indices[i]);
            }

            candidateIndices.Sort();
            var conflicts = new List<ReservationConflict>();
            int broad = 0;
            int narrow = 0;
            int intersections = 0;
            int rejected = 0;
            int yielded = 0;
            int previous = -1;
            ReservationDecision decision = ReservationDecision.Accepted;
            ReservationReasonCode primaryReason = ReservationReasonCode.None;

            for (int i = 0; i < candidateIndices.Count; i++)
            {
                int index = candidateIndices[i];
                if (index == previous) continue;
                previous = index;
                broad++;
                SpatialReservation existing = _reservations[index];
                if ((existing.Category & categoryMask) == 0) continue;
                if (existing.Id.Equals(candidate.Id)) continue;
                narrow++;
                if (!candidate.Intersects(existing)) continue;
                intersections++;

                bool compatible = (existing.Semantics & ReservationSemantics.CompatibleHandoff) != 0
                    && (existing.CompatibleConsumers & consumer) != 0;
                if (compatible) continue;

                ReservationReasonCode reason;
                ReservationDecision conflictDecision;
                if ((existing.Semantics & ReservationSemantics.SoftYield) != 0)
                {
                    reason = ReservationReasonCode.SoftYield;
                    conflictDecision = ReservationDecision.Yield;
                }
                else if ((existing.Semantics & ReservationSemantics.Clearance) != 0)
                {
                    reason = ReservationReasonCode.ClearanceConflict;
                    conflictDecision = consumer == ReservationConsumerKind.Vegetation
                        ? ReservationDecision.Yield
                        : ReservationDecision.Rejected;
                }
                else if ((existing.Semantics & ReservationSemantics.ProtectedCorridor) != 0)
                {
                    reason = ReservationReasonCode.ProtectedCorridorConflict;
                    conflictDecision = ReservationDecision.Rejected;
                }
                else
                {
                    reason = ReservationReasonCode.HardOccupancyConflict;
                    conflictDecision = ReservationDecision.Rejected;
                }

                conflicts.Add(new ReservationConflict(existing, reason, false));
                if (conflictDecision == ReservationDecision.Rejected)
                {
                    rejected++;
                    if (decision != ReservationDecision.Rejected)
                    {
                        decision = ReservationDecision.Rejected;
                        primaryReason = reason;
                    }
                }
                else
                {
                    yielded++;
                    if (decision == ReservationDecision.Accepted)
                    {
                        decision = ReservationDecision.Yield;
                        primaryReason = reason;
                    }
                }
            }

            if (primaryReason == ReservationReasonCode.None)
                primaryReason = intersections == 0
                    ? ReservationReasonCode.NoIntersection
                    : ReservationReasonCode.CompatibleHandoff;
            return Result(decision, primaryReason, conflicts.ToArray(), bucketsVisited, broad, narrow,
                intersections, rejected, yielded, started);
        }

        private static ReservationQueryResult Result(
            ReservationDecision decision,
            ReservationReasonCode reason,
            ReservationConflict[] conflicts,
            int buckets,
            int broad,
            int narrow,
            int intersections,
            int rejected,
            int yielded,
            long started)
        {
            return new ReservationQueryResult(
                decision,
                reason,
                conflicts,
                new ReservationQueryMetrics(
                    buckets, broad, narrow, intersections, rejected, yielded,
                    Stopwatch.GetTimestamp() - started));
        }

        private static int CompareStable(SpatialReservation a, SpatialReservation b)
        {
            int id = a.Id.CompareTo(b.Id);
            if (id != 0) return id;
            int category = ((ushort)a.Category).CompareTo((ushort)b.Category);
            if (category != 0) return category;
            return a.Precedence.CompareTo(b.Precedence);
        }

        private static int FloorDiv(int value, int divisor)
        {
            int q = value / divisor;
            int r = value % divisor;
            return r < 0 ? q - 1 : q;
        }
    }

    /// <summary>Mutable state is scoped to one deterministic bounded planner, never global world authority.</summary>
    public sealed class PlannerLocalReservationSet
    {
        private readonly List<SpatialReservation> _reservations = new List<SpatialReservation>();
        private readonly ReservationBoundsDm _window;
        private readonly int _bucketSizeDm;

        public PlannerLocalReservationSet(ReservationBoundsDm window, int bucketSizeDm = 128)
        {
            _window = window;
            _bucketSizeDm = bucketSizeDm;
        }

        public IReadOnlyList<SpatialReservation> Reservations => _reservations;

        public ReservationQueryResult Query(
            in SpatialReservation candidate,
            ReservationConsumerKind consumer,
            ReservationCategory categoryMask = ReservationCategory.All) =>
            SpatialReservationSnapshot.Create(_reservations, _window, _bucketSizeDm)
                .Query(candidate, consumer, categoryMask);

        public void Add(in SpatialReservation reservation)
        {
            int index = IndexOf(reservation.Id);
            if (index >= 0)
                _reservations[index] = reservation;
            else
                _reservations.Add(reservation);
        }

        public ReservationQueryResult TryAdd(
            in SpatialReservation reservation,
            ReservationConsumerKind consumer,
            ReservationCategory categoryMask = ReservationCategory.All)
        {
            ReservationQueryResult result = Query(reservation, consumer, categoryMask);
            if (result.IsAccepted) Add(reservation);
            return result;
        }

        public bool Release(ReservationId reservationId)
        {
            int index = IndexOf(reservationId);
            if (index < 0) return false;
            _reservations.RemoveAt(index);
            return true;
        }

        public int ReleaseOwner(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                throw new ArgumentException("Reservation owner id is required.", nameof(ownerId));

            int removed = 0;
            for (int i = _reservations.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(_reservations[i].OwnerId, ownerId, StringComparison.Ordinal))
                    continue;
                _reservations.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        public SpatialReservationSnapshot Snapshot() =>
            SpatialReservationSnapshot.Create(_reservations, _window, _bucketSizeDm);

        private int IndexOf(ReservationId reservationId)
        {
            for (int i = 0; i < _reservations.Count; i++)
                if (_reservations[i].Id.Equals(reservationId)) return i;
            return -1;
        }
    }

    /// <summary>
    /// Resolves independently derived candidates with the R-002-style total order: higher precedence
    /// wins, then lower stable ReservationId. Because candidates are sorted before conflict checks,
    /// caller/insertion order cannot change the winners.
    /// </summary>
    public static class IndependentReservationResolver
    {
        public static SpatialReservation[] Resolve(
            IEnumerable<SpatialReservation> candidates,
            ReservationBoundsDm window,
            ReservationConsumerKind consumer,
            ReservationCategory categoryMask = ReservationCategory.All)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            var ordered = new List<SpatialReservation>(candidates);
            ordered.Sort((a, b) =>
            {
                int precedence = b.Precedence.CompareTo(a.Precedence);
                return precedence != 0 ? precedence : a.Id.CompareTo(b.Id);
            });

            var local = new PlannerLocalReservationSet(window);
            for (int i = 0; i < ordered.Count; i++)
            {
                SpatialReservation candidate = ordered[i];
                ReservationQueryResult result = local.Query(candidate, consumer, categoryMask);
                if (result.IsAccepted) local.Add(candidate);
            }

            var winners = new SpatialReservation[local.Reservations.Count];
            for (int i = 0; i < winners.Length; i++) winners[i] = local.Reservations[i];
            Array.Sort(winners, (a, b) => a.Id.CompareTo(b.Id));
            return winners;
        }
    }

    /// <summary>
    /// Production adapters shared by settlement, road, architecture, ecology and underground planners.
    /// They translate semantic decisions into claims; they do not take over those planners' policy.
    /// </summary>
    public static class WorldBuilderReservationFactory
    {
        public static SpatialReservation BuildingFootprint(
            string ownerId, Int2 positionDm, Int3 envelopeDm, int baseYDm = 0,
            int precedence = 0, string provenance = "settlement") =>
            SpatialReservation.Box(ownerId, ReservationCategory.Building,
                ReservationSemantics.HardOccupancy,
                ReservationBoundsDm.FromFootprint(positionDm, envelopeDm, baseYDm),
                precedence, ReservationConsumerKind.None, provenance, 0);

        public static SpatialReservation BuildingClearance(
            string ownerId, Int2 positionDm, Int3 envelopeDm, int clearanceDm, int baseYDm = 0,
            int precedence = 0, string provenance = "settlement") =>
            SpatialReservation.Box(ownerId, ReservationCategory.Building,
                ReservationSemantics.Clearance,
                ReservationBoundsDm.FromFootprint(positionDm, envelopeDm, baseYDm).ExpandHorizontal(clearanceDm),
                precedence, ReservationConsumerKind.None, provenance, 1);

        public static SpatialReservation PlazaKeepOpen(
            string ownerId, Int2 centreDm, Int2 sizeDm, int clearanceDm, int minYDm, int maxYDm,
            int precedence = 100, string provenance = "settlement-plaza")
        {
            var bounds = new ReservationBoundsDm(
                centreDm.X - sizeDm.X / 2, minYDm, centreDm.Y - sizeDm.Y / 2,
                centreDm.X + (sizeDm.X + 1) / 2, maxYDm, centreDm.Y + (sizeDm.Y + 1) / 2)
                .ExpandHorizontal(clearanceDm);
            return SpatialReservation.Box(ownerId, ReservationCategory.Plaza,
                ReservationSemantics.Clearance, bounds, precedence,
                ReservationConsumerKind.None, provenance);
        }

        public static SpatialReservation SettlementEnvelope(
            string ownerId, ReservationBoundsDm bounds, int precedence = 50,
            string provenance = "macro-settlement") =>
            SpatialReservation.Box(ownerId, ReservationCategory.SettlementEnvelope,
                ReservationSemantics.HardOccupancy | ReservationSemantics.CompatibleHandoff,
                bounds, precedence, ReservationConsumerKind.Road, provenance);

        public static SpatialReservation PublicAccessCorridor(
            string ownerId, Int2 startDm, Int2 endDm, int minYDm, int maxYDm, int radiusDm,
            int precedence = 80, string provenance = "settlement-access") =>
            SpatialReservation.Corridor(ownerId, ReservationCategory.PublicAccess,
                ReservationSemantics.ProtectedCorridor | ReservationSemantics.CompatibleHandoff,
                startDm, endDm, minYDm, maxYDm, radiusDm, precedence,
                ReservationConsumerKind.Road | ReservationConsumerKind.Connector,
                provenance);

        public static SpatialReservation RoadCorridor(
            string ownerId, Int2 startDm, Int2 endDm, int minYDm, int maxYDm, int radiusDm,
            int precedence = 70, string provenance = "road") =>
            SpatialReservation.Corridor(ownerId, ReservationCategory.Road,
                ReservationSemantics.ProtectedCorridor | ReservationSemantics.CompatibleHandoff,
                startDm, endDm, minYDm, maxYDm, radiusDm, precedence,
                ReservationConsumerKind.Road | ReservationConsumerKind.Connector,
                provenance);

        public static SpatialReservation StructuralChildClearance(
            string ownerId, ReservationBoundsDm bounds, int precedence = 40,
            ReservationConsumerKind compatibleConsumers = ReservationConsumerKind.Connector,
            string provenance = "architecture") =>
            SpatialReservation.Box(ownerId, ReservationCategory.StructuralChild,
                ReservationSemantics.Clearance | ReservationSemantics.CompatibleHandoff,
                bounds, precedence, compatibleConsumers, provenance);

        public static SpatialReservation VegetationSoftExclusion(
            string ownerId, ReservationBoundsDm bounds, int precedence = 5,
            string provenance = "ecology") =>
            SpatialReservation.Box(ownerId, ReservationCategory.Vegetation,
                ReservationSemantics.SoftYield, bounds, precedence,
                ReservationConsumerKind.None, provenance);

        public static SpatialReservation UndergroundVolume(
            string ownerId, ReservationBoundsDm bounds,
            ReservationSemantics semantics = ReservationSemantics.HardOccupancy,
            int precedence = 30,
            ReservationConsumerKind compatibleConsumers = ReservationConsumerKind.None,
            string provenance = "underground") =>
            SpatialReservation.Box(ownerId, ReservationCategory.Underground,
                semantics, bounds, precedence, compatibleConsumers, provenance);

        public static SpatialReservation HiddenSpaceVolume(
            SiteHiddenSpaceRealization hiddenSpace,
            Int3 siteOriginDm,
            int precedence = 30)
        {
            if (hiddenSpace == null) throw new ArgumentNullException(nameof(hiddenSpace));
            HiddenSpaceBoundsDm b = hiddenSpace.LocalBoundsDm;
            return UndergroundVolume(
                hiddenSpace.RequestId + ":" + hiddenSpace.CandidateId,
                new ReservationBoundsDm(
                    siteOriginDm.X + b.MinX,
                    siteOriginDm.Y + b.MinY,
                    siteOriginDm.Z + b.MinZ,
                    siteOriginDm.X + b.MinX + b.SizeX,
                    siteOriginDm.Y + b.MinY + b.SizeY,
                    siteOriginDm.Z + b.MinZ + b.SizeZ),
                ReservationSemantics.HardOccupancy,
                precedence,
                ReservationConsumerKind.None,
                "hidden-space");
        }
    }
}
