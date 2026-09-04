using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Composition
{
    public readonly struct FarFeatureAggregate
    {
        private readonly ulong[] _memberIds;

        internal FarFeatureAggregate(
            ulong stableId,
            ulong revision,
            ulong groupKey,
            int3 boundsMin,
            int3 boundsMax,
            ulong[] memberIds)
        {
            StableId = stableId;
            Revision = revision;
            GroupKey = groupKey;
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
            _memberIds = memberIds ?? Array.Empty<ulong>();
        }

        public ulong StableId { get; }
        public ulong Revision { get; }
        public ulong GroupKey { get; }
        public int3 BoundsMin { get; }
        public int3 BoundsMax { get; }
        public IReadOnlyList<ulong> MemberIds => _memberIds;
        public int MemberCount => _memberIds.Length;
    }

    /// <summary>
    /// Builds deterministic disposable HLOD groups from generic baked-feature truth. Grouping policy
    /// is injected by composition. Important/landmark members may be excluded independently without
    /// changing the bake contract or introducing structure-specific presentation ownership.
    /// </summary>
    public static class FarFeatureAggregateBuilder
    {
        public static IReadOnlyList<FarFeatureAggregate> Build(
            IReadOnlyList<FeaturePresentationBake> bakes,
            Func<FeaturePresentationBake, ulong> groupKey,
            Func<FeaturePresentationBake, bool> isIndependent = null,
            int minimumMembers = 2)
        {
            if (bakes == null) throw new ArgumentNullException(nameof(bakes));
            if (groupKey == null) throw new ArgumentNullException(nameof(groupKey));
            if (minimumMembers < 2) throw new ArgumentOutOfRangeException(nameof(minimumMembers));

            var groups = new Dictionary<ulong, List<FeaturePresentationBake>>();
            for (int i = 0; i < bakes.Count; i++)
            {
                FeaturePresentationBake bake = bakes[i];
                if (bake == null || (isIndependent != null && isIndependent(bake))) continue;
                ulong key = groupKey(bake);
                if (key == 0UL) continue;
                if (!groups.TryGetValue(key, out List<FeaturePresentationBake> members))
                {
                    members = new List<FeaturePresentationBake>();
                    groups.Add(key, members);
                }
                members.Add(bake);
            }

            var keys = new List<ulong>(groups.Keys);
            keys.Sort();
            var result = new List<FarFeatureAggregate>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                List<FeaturePresentationBake> members = groups[keys[i]];
                if (members.Count < minimumMembers) continue;
                result.Add(BuildAggregate(keys[i], members));
            }
            return result;
        }

        private static FarFeatureAggregate BuildAggregate(ulong groupKey, List<FeaturePresentationBake> members)
        {
            members.Sort((left, right) => left.SourceId.CompareTo(right.SourceId));
            int3 min = members[0].BoundsMin;
            int3 max = members[0].BoundsMax;
            var ids = new ulong[members.Count];
            ulong revision = HashUlong(FnvOffset, groupKey);
            for (int i = 0; i < members.Count; i++)
            {
                FeaturePresentationBake member = members[i];
                ids[i] = member.SourceId;
                min = math.min(min, member.BoundsMin);
                max = math.max(max, member.BoundsMax);
                revision = HashUlong(revision, member.SourceId);
                revision = HashUlong(revision, member.Revision);
            }

            ulong stableId = HashUlong(HashUlong(FnvOffset, AggregateNamespace), groupKey);
            return new FarFeatureAggregate(stableId, revision, groupKey, min, max, ids);
        }

        private const ulong AggregateNamespace = 0x4147475245474154UL;
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private static ulong HashUlong(ulong hash, ulong value)
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash *= FnvPrime;
            }
            return hash;
        }
    }

    /// <summary>
    /// Optional aggregate wrapper over the normal far-feature adapter. At far/horizon significance,
    /// dense groups replace their ordinary members with one cached render instance. Mid-tier members
    /// remain independent. Important members are always excluded from aggregate membership.
    /// </summary>
    public sealed class AggregatingFarFeaturePresentationAdapter
    {
        private readonly IFeaturePresentationSource _source;
        private readonly FarFeatureSelectionPolicy _selection;
        private readonly FarFeaturePresentationAdapter _memberAdapter;
        private readonly float _voxelSizeMetres;
        private readonly Func<FeaturePresentationBake, ulong> _groupKey;
        private readonly Func<FeaturePresentationBake, FarFeatureImportance> _importance;
        private readonly int _minimumMembers;
        private readonly FilteredSource _filteredSource = new FilteredSource();
        private readonly List<FarFeatureInstance> _instances = new List<FarFeatureInstance>();
        private readonly HashSet<ulong> _suppressedMembers = new HashSet<ulong>();

        public AggregatingFarFeaturePresentationAdapter(
            IFeaturePresentationSource source,
            FarFeatureSelectionPolicy selection,
            float voxelSizeMetres,
            Func<FeaturePresentationBake, ulong> groupKey,
            Func<FeaturePresentationBake, FarFeatureImportance> importance = null,
            int minimumMembers = 2)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            if (!(voxelSizeMetres > 0f) || !math.isfinite(voxelSizeMetres))
                throw new ArgumentOutOfRangeException(nameof(voxelSizeMetres));
            _groupKey = groupKey ?? throw new ArgumentNullException(nameof(groupKey));
            if (minimumMembers < 2) throw new ArgumentOutOfRangeException(nameof(minimumMembers));
            _voxelSizeMetres = voxelSizeMetres;
            _importance = importance;
            _minimumMembers = minimumMembers;
            _memberAdapter = new FarFeaturePresentationAdapter(_filteredSource, selection, voxelSizeMetres, importance);
        }

        public IReadOnlyList<FarFeatureInstance> Query(float3 cameraPosition, float radiusMetres)
        {
            if (!(radiusMetres > 0f) || !math.isfinite(radiusMetres))
                throw new ArgumentOutOfRangeException(nameof(radiusMetres));

            IReadOnlyList<FeaturePresentationBake> bakes = _source.Query(BuildQueryBounds(cameraPosition, radiusMetres));
            IReadOnlyList<FarFeatureAggregate> aggregates = FarFeatureAggregateBuilder.Build(
                bakes,
                _groupKey,
                bake => (_importance?.Invoke(bake) ?? FarFeatureImportance.Default) != FarFeatureImportance.Default,
                _minimumMembers);

            _instances.Clear();
            _suppressedMembers.Clear();
            for (int i = 0; i < aggregates.Count; i++)
            {
                FarFeatureAggregate aggregate = aggregates[i];
                BoundsFor(aggregate.BoundsMin, aggregate.BoundsMax, out float3 position, out float3 center, out float3 extents, out float3 scale);
                FarFeatureTier tier = _selection.Select(
                    aggregate.StableId,
                    center,
                    extents,
                    cameraPosition,
                    FarFeatureImportance.Default);
                if (tier != FarFeatureTier.Far && tier != FarFeatureTier.Horizon) continue;

                for (int memberIndex = 0; memberIndex < aggregate.MemberCount; memberIndex++)
                    _suppressedMembers.Add(aggregate.MemberIds[memberIndex]);

                _instances.Add(new FarFeatureInstance(
                    aggregate.StableId,
                    position,
                    quaternion.identity,
                    scale,
                    center,
                    extents,
                    $"aggregate-{aggregate.Revision:X16}",
                    "aggregate",
                    tier,
                    FarFeatureVisualFlags.None,
                    AggregateGeometry(aggregate, bakes)));
            }

            _filteredSource.Reset(bakes, _suppressedMembers);
            IReadOnlyList<FarFeatureInstance> members = _memberAdapter.Query(cameraPosition, radiusMetres);
            for (int i = 0; i < members.Count; i++) _instances.Add(members[i]);
            _instances.Sort((left, right) => left.StableId.CompareTo(right.StableId));
            return _instances;
        }

        private FeaturePresentationBounds BuildQueryBounds(float3 cameraPosition, float radiusMetres)
        {
            float inverseVoxelSize = 1f / _voxelSizeMetres;
            float3 radius = new float3(radiusMetres);
            int3 min = (int3)math.floor((cameraPosition - radius) * inverseVoxelSize);
            int3 max = (int3)math.ceil((cameraPosition + radius) * inverseVoxelSize) + new int3(1);
            return new FeaturePresentationBounds(min, max);
        }

        private void BoundsFor(
            int3 minVoxel,
            int3 maxVoxel,
            out float3 position,
            out float3 center,
            out float3 extents,
            out float3 scale)
        {
            int3 maxExclusiveVoxel = maxVoxel + new int3(1);
            float3 min = new float3(minVoxel.x, minVoxel.y, minVoxel.z) * _voxelSizeMetres;
            float3 maxExclusive = new float3(maxExclusiveVoxel.x, maxExclusiveVoxel.y, maxExclusiveVoxel.z) * _voxelSizeMetres;
            scale = math.max(maxExclusive - min, new float3(_voxelSizeMetres));
            extents = scale * 0.5f;
            center = min + extents;
            position = new float3(center.x, min.y, center.z);
        }

        private static FarFeatureGeometry AggregateGeometry(FarFeatureAggregate aggregate, IReadOnlyList<FeaturePresentationBake> bakes)
        {
            int3 maxExclusive = aggregate.BoundsMax + new int3(1);
            float3 aggregateMin = new float3(aggregate.BoundsMin.x, aggregate.BoundsMin.y, aggregate.BoundsMin.z);
            float3 aggregateSize = math.max(
                new float3(
                    maxExclusive.x - aggregate.BoundsMin.x,
                    maxExclusive.y - aggregate.BoundsMin.y,
                    maxExclusive.z - aggregate.BoundsMin.z),
                new float3(1f));
            var originOffset = new float3(0.5f, 0f, 0.5f);
            var memberSet = new HashSet<ulong>();
            for (int i = 0; i < aggregate.MemberCount; i++) memberSet.Add(aggregate.MemberIds[i]);
            var primitives = new List<FarFeatureGeometryPrimitive>(aggregate.MemberCount);
            for (int i = 0; i < bakes.Count; i++)
            {
                FeaturePresentationBake bake = bakes[i];
                if (!memberSet.Contains(bake.SourceId)) continue;
                int3 memberMaxExclusive = bake.BoundsMax + new int3(1);
                float3 memberMin = new float3(bake.BoundsMin.x, bake.BoundsMin.y, bake.BoundsMin.z);
                float3 memberMax = new float3(memberMaxExclusive.x, memberMaxExclusive.y, memberMaxExclusive.z);
                primitives.Add(new FarFeatureGeometryPrimitive(
                    FarFeatureGeometryShape.Box,
                    (memberMin - aggregateMin) / aggregateSize - originOffset,
                    (memberMax - aggregateMin) / aggregateSize - originOffset));
            }
            return primitives.Count == 0 ? null : new FarFeatureGeometry(primitives.ToArray());
        }

        private sealed class FilteredSource : IFeaturePresentationSource
        {
            private readonly List<FeaturePresentationBake> _bakes = new List<FeaturePresentationBake>();

            public void Reset(IReadOnlyList<FeaturePresentationBake> bakes, HashSet<ulong> suppressed)
            {
                _bakes.Clear();
                for (int i = 0; i < bakes.Count; i++)
                {
                    FeaturePresentationBake bake = bakes[i];
                    if (!suppressed.Contains(bake.SourceId)) _bakes.Add(bake);
                }
            }

            public bool TryGet(ulong sourceId, out FeaturePresentationBake bake)
            {
                for (int i = 0; i < _bakes.Count; i++)
                {
                    if (_bakes[i].SourceId != sourceId) continue;
                    bake = _bakes[i];
                    return true;
                }
                bake = null;
                return false;
            }

            public IReadOnlyList<FeaturePresentationBake> Query(FeaturePresentationBounds bounds)
            {
                var result = new List<FeaturePresentationBake>();
                for (int i = 0; i < _bakes.Count; i++)
                {
                    if (bounds.Intersects(_bakes[i])) result.Add(_bakes[i]);
                }
                return result;
            }
        }
    }
}
