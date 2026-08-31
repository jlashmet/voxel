using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen.Architecture;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase composition boundary from renderer-neutral world facts to engine render instances.
    /// Visibility tiering and terrain elevation are injected policy; querying does not require voxel
    /// residency or physical structure realization.
    /// </summary>
    public sealed class ShowcaseFarStructureSource
    {
        private const float DmToMetres = 0.1f;

        private readonly IWorldVisibilitySource _source;
        private readonly Func<StructureFarPresentation, float2, FarStructureTier> _selectTier;
        private readonly Func<float2, float> _groundHeightMetres;
        private readonly List<FarStructureInstance> _instances = new List<FarStructureInstance>();

        public ShowcaseFarStructureSource(
            IWorldVisibilitySource source,
            Func<StructureFarPresentation, float2, FarStructureTier> selectTier,
            Func<float2, float> groundHeightMetres)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _selectTier = selectTier ?? throw new ArgumentNullException(nameof(selectTier));
            _groundHeightMetres = groundHeightMetres ?? throw new ArgumentNullException(nameof(groundHeightMetres));
        }

        public IReadOnlyList<FarStructureInstance> Query(float2 cameraXZMetres, float radiusMetres)
        {
            if (!(radiusMetres > 0f) || !math.isfinite(radiusMetres))
                throw new ArgumentOutOfRangeException(nameof(radiusMetres));

            int minX = MetresToDmFloor(cameraXZMetres.x - radiusMetres);
            int minY = MetresToDmFloor(cameraXZMetres.y - radiusMetres);
            int maxX = MetresToDmCeil(cameraXZMetres.x + radiusMetres);
            int maxY = MetresToDmCeil(cameraXZMetres.y + radiusMetres);
            IReadOnlyList<StructureFarPresentation> records = _source.Query(
                new WorldVisibilityBoundsDm(minX, minY, maxX, maxY));

            _instances.Clear();
            for (int i = 0; i < records.Count; i++)
            {
                StructureFarPresentation record = records[i];
                FarStructureTier tier = _selectTier(record, cameraXZMetres);
                if (tier == FarStructureTier.Culled)
                    continue;

                _instances.Add(ToInstance(record, tier));
            }

            return _instances;
        }

        private FarStructureInstance ToInstance(StructureFarPresentation record, FarStructureTier tier)
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

            FarStructureVisualFlags flags = ToFlags(record.VisibilityClass);
            return new FarStructureInstance(
                record.StructureKey,
                new float3(centerXZ.x, groundY, centerXZ.y),
                quaternion.RotateY(math.radians(90f * (byte)record.Facing)),
                new float3(width, height, depth),
                new float3(centerXZ.x, groundY + height * 0.5f, centerXZ.y),
                new float3(width * 0.5f, height * 0.5f, depth * 0.5f),
                record.Archetype.ToString(),
                record.MaterialFamilyKey.ToString("X16"),
                tier,
                flags);
        }

        private static FarStructureVisualFlags ToFlags(StructureVisibilityClass visibilityClass)
        {
            switch (visibilityClass)
            {
                case StructureVisibilityClass.SettlementAnchor:
                    return FarStructureVisualFlags.SettlementAnchor;
                case StructureVisibilityClass.Landmark:
                    return FarStructureVisualFlags.Landmark;
                case StructureVisibilityClass.HorizonLandmark:
                    return FarStructureVisualFlags.Landmark | FarStructureVisualFlags.HorizonLandmark;
                default:
                    return FarStructureVisualFlags.None;
            }
        }

        private static int MetresToDmFloor(float metres) =>
            checked((int)math.floor(metres / DmToMetres));

        private static int MetresToDmCeil(float metres) =>
            checked((int)math.ceil(metres / DmToMetres));
    }
}
