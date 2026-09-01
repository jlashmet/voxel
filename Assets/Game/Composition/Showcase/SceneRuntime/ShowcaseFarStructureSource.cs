using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen.Architecture;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase composition boundary from renderer-neutral world facts to engine render instances.
    /// Visibility tiering and terrain elevation are injected policy; querying does not require voxel
    /// residency or physical structure realization. Optional settlement HLOD uses fixed semantic
    /// sectors so cluster identity does not depend on camera position.
    /// </summary>
    public sealed class ShowcaseFarStructureSource
    {
        private const float DmToMetres = 0.1f;

        public sealed class ClusterConfiguration
        {
            public ClusterConfiguration(
                int sectorSizeDm,
                Func<WorldVisibilityClusterBuilder.Cluster, float2, FarStructureTier> selectTier)
            {
                if (sectorSizeDm <= 0) throw new ArgumentOutOfRangeException(nameof(sectorSizeDm));
                SectorSizeDm = sectorSizeDm;
                SelectTier = selectTier ?? throw new ArgumentNullException(nameof(selectTier));
            }

            public int SectorSizeDm { get; }
            public Func<WorldVisibilityClusterBuilder.Cluster, float2, FarStructureTier> SelectTier { get; }
        }

        private readonly IWorldVisibilitySource _source;
        private readonly Func<StructureFarPresentation, float2, FarStructureTier> _selectTier;
        private readonly Func<float2, float> _groundHeightMetres;
        private readonly ClusterConfiguration _clusters;
        private readonly IStructureVisualStateSource _visualState;
        private readonly List<FarFeatureInstance> _instances = new List<FarFeatureInstance>();
        private readonly List<StructureFarPresentation> _activeRecords = new List<StructureFarPresentation>();
        private readonly HashSet<ulong> _clusteredMembers = new HashSet<ulong>();

        public ShowcaseFarStructureSource(
            IWorldVisibilitySource source,
            Func<StructureFarPresentation, float2, FarStructureTier> selectTier,
            Func<float2, float> groundHeightMetres)
            : this(source, selectTier, groundHeightMetres, null, null)
        {
        }

        public ShowcaseFarStructureSource(
            IWorldVisibilitySource source,
            Func<StructureFarPresentation, float2, FarStructureTier> selectTier,
            Func<float2, float> groundHeightMetres,
            ClusterConfiguration clusters)
            : this(source, selectTier, groundHeightMetres, clusters, null)
        {
        }

        public ShowcaseFarStructureSource(
            IWorldVisibilitySource source,
            Func<StructureFarPresentation, float2, FarStructureTier> selectTier,
            Func<float2, float> groundHeightMetres,
            ClusterConfiguration clusters,
            IStructureVisualStateSource visualState)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _selectTier = selectTier ?? throw new ArgumentNullException(nameof(selectTier));
            _groundHeightMetres = groundHeightMetres ?? throw new ArgumentNullException(nameof(groundHeightMetres));
            _clusters = clusters;
            _visualState = visualState;
        }

        public IReadOnlyList<FarFeatureInstance> Query(float2 cameraXZMetres, float radiusMetres)
        {
            if (!(radiusMetres > 0f) || !math.isfinite(radiusMetres))
                throw new ArgumentOutOfRangeException(nameof(radiusMetres));

            var requestedBounds = new WorldVisibilityBoundsDm(
                MetresToDmFloor(cameraXZMetres.x - radiusMetres),
                MetresToDmFloor(cameraXZMetres.y - radiusMetres),
                MetresToDmCeil(cameraXZMetres.x + radiusMetres),
                MetresToDmCeil(cameraXZMetres.y + radiusMetres));
            WorldVisibilityBoundsDm sourceBounds = _clusters == null
                ? requestedBounds
                : AlignToClusterSectors(requestedBounds, _clusters.SectorSizeDm);
            IReadOnlyList<StructureFarPresentation> queriedRecords = _source.Query(sourceBounds);
            IReadOnlyList<StructureFarPresentation> records = FilterRemoved(queriedRecords);

            _instances.Clear();
            _clusteredMembers.Clear();
            if (_clusters != null)
                AddActiveClusters(records, requestedBounds, cameraXZMetres);

            for (int i = 0; i < records.Count; i++)
            {
                StructureFarPresentation record = records[i];
                if (!requestedBounds.Intersects(record))
                    continue;
                if (_clusteredMembers.Contains(record.StructureKey)
                    && record.VisibilityClass == StructureVisibilityClass.OrdinaryStructure)
                    continue;

                FarStructureTier tier = _selectTier(record, cameraXZMetres);
                if (tier == FarStructureTier.Culled)
                    continue;

                _instances.Add(ToInstance(record, tier));
            }

            return _instances;
        }

        private IReadOnlyList<StructureFarPresentation> FilterRemoved(
            IReadOnlyList<StructureFarPresentation> records)
        {
            if (_visualState == null) return records;

            _activeRecords.Clear();
            for (int i = 0; i < records.Count; i++)
            {
                StructureFarPresentation record = records[i];
                if (_visualState.Get(record.StructureKey) == StructureVisualState.Removed)
                    continue;
                _activeRecords.Add(record);
            }
            return _activeRecords;
        }

        private void AddActiveClusters(
            IReadOnlyList<StructureFarPresentation> records,
            WorldVisibilityBoundsDm requestedBounds,
            float2 cameraXZMetres)
        {
            IReadOnlyList<WorldVisibilityClusterBuilder.Cluster> clusters =
                WorldVisibilityClusterBuilder.Build(records, _clusters.SectorSizeDm);
            for (int i = 0; i < clusters.Count; i++)
            {
                WorldVisibilityClusterBuilder.Cluster cluster = clusters[i];
                if (!Intersects(requestedBounds, cluster))
                    continue;

                FarStructureTier tier = _clusters.SelectTier(cluster, cameraXZMetres);
                if (tier != FarStructureTier.Far && tier != FarStructureTier.Horizon)
                    continue;

                for (int member = 0; member < cluster.MemberStructureKeys.Count; member++)
                    _clusteredMembers.Add(cluster.MemberStructureKeys[member]);
                _instances.Add(ToInstance(cluster, tier));
            }
        }

        private FarFeatureInstance ToInstance(StructureFarPresentation record, FarStructureTier tier)
        {
            float minX = record.FootprintMinDm.X * DmToMetres;
            float minZ = record.FootprintMinDm.Y * DmToMetres;
            float maxX = record.FootprintMaxDm.X * DmToMetres;
            float maxZ = record.FootprintMaxDm.Y * DmToMetres;
            float width = maxX - minX;
            float depth = maxZ - minZ;
            float height = record.HeightDm * DmToMetres;
            float2 centerXZ = new float2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            float groundY = _groundHeightMetres(centerXZ);

            FarFeatureVisualFlags flags = ToFlags(record.VisibilityClass);
            return new FarFeatureInstance(
                record.StructureKey,
                new float3(centerXZ.x, groundY, centerXZ.y),
                quaternion.RotateY(math.radians(90f * (byte)record.Facing)),
                new float3(width, height, depth),
                new float3(centerXZ.x, groundY + height * 0.5f, centerXZ.y),
                new float3(width * 0.5f, height * 0.5f, depth * 0.5f),
                record.Archetype.ToString(),
                record.MaterialFamilyKey.ToString("X16"),
                ToRenderTier(tier),
                flags);
        }

        private FarFeatureInstance ToInstance(
            WorldVisibilityClusterBuilder.Cluster cluster,
            FarStructureTier tier)
        {
            float minX = cluster.FootprintMinDm.X * DmToMetres;
            float minZ = cluster.FootprintMinDm.Y * DmToMetres;
            float maxX = cluster.FootprintMaxDm.X * DmToMetres;
            float maxZ = cluster.FootprintMaxDm.Y * DmToMetres;
            float width = maxX - minX;
            float depth = maxZ - minZ;
            float height = cluster.MaxHeightDm * DmToMetres;
            float2 centerXZ = new float2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            float groundY = _groundHeightMetres(centerXZ);

            return new FarFeatureInstance(
                cluster.ClusterKey,
                new float3(centerXZ.x, groundY, centerXZ.y),
                quaternion.identity,
                new float3(width, height, depth),
                new float3(centerXZ.x, groundY + height * 0.5f, centerXZ.y),
                new float3(width * 0.5f, height * 0.5f, depth * 0.5f),
                "settlement-cluster",
                cluster.DominantMaterialFamilyKey.ToString("X16"),
                ToRenderTier(tier),
                FarFeatureVisualFlags.SettlementAnchor);
        }

        private static FarFeatureVisualFlags ToFlags(StructureVisibilityClass visibilityClass)
        {
            switch (visibilityClass)
            {
                case StructureVisibilityClass.SettlementAnchor:
                    return FarFeatureVisualFlags.SettlementAnchor;
                case StructureVisibilityClass.Landmark:
                    return FarFeatureVisualFlags.Landmark;
                case StructureVisibilityClass.HorizonLandmark:
                    return FarFeatureVisualFlags.Landmark | FarFeatureVisualFlags.HorizonLandmark;
                default:
                    return FarFeatureVisualFlags.None;
            }
        }

        private static FarFeatureTier ToRenderTier(FarStructureTier tier)
        {
            switch (tier)
            {
                case FarStructureTier.Mid:
                    return FarFeatureTier.Mid;
                case FarStructureTier.Far:
                    return FarFeatureTier.Far;
                case FarStructureTier.Horizon:
                    return FarFeatureTier.Horizon;
                default:
                    return FarFeatureTier.Culled;
            }
        }

        private static WorldVisibilityBoundsDm AlignToClusterSectors(
            WorldVisibilityBoundsDm bounds,
            int sectorSizeDm)
        {
            int minX = checked(FloorDiv(bounds.MinX, sectorSizeDm) * sectorSizeDm);
            int minY = checked(FloorDiv(bounds.MinY, sectorSizeDm) * sectorSizeDm);
            int maxX = checked((FloorDiv(bounds.MaxX - 1, sectorSizeDm) + 1) * sectorSizeDm);
            int maxY = checked((FloorDiv(bounds.MaxY - 1, sectorSizeDm) + 1) * sectorSizeDm);
            return new WorldVisibilityBoundsDm(minX, minY, maxX, maxY);
        }

        private static bool Intersects(
            WorldVisibilityBoundsDm bounds,
            WorldVisibilityClusterBuilder.Cluster cluster) =>
            cluster.FootprintMaxDm.X > bounds.MinX
            && cluster.FootprintMinDm.X < bounds.MaxX
            && cluster.FootprintMaxDm.Y > bounds.MinY
            && cluster.FootprintMinDm.Y < bounds.MaxY;

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            if (value % divisor != 0 && value < 0) quotient--;
            return quotient;
        }

        private static int MetresToDmFloor(float metres) =>
            checked((int)math.floor(metres / DmToMetres));

        private static int MetresToDmCeil(float metres) =>
            checked((int)math.ceil(metres / DmToMetres));
    }
}
