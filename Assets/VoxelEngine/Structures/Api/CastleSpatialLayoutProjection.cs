using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// API-level projection from semantic spatial placement into the legacy local-coordinate frame
    /// still used by keep-local realization, presentation, and interaction helpers. Centralizing
    /// this transform prevents Runtime and application code from carrying separate +60 Z offsets.
    /// </summary>
    public readonly struct CastleSpatialLayoutProjection
    {
        public const int LegacyKeepCentreZOffset = 60;

        public readonly CastlePlan PlacedKeepPlan;
        public readonly CastleGateGeometry PrimaryGate;

        private CastleSpatialLayoutProjection(
            in CastlePlan placedKeepPlan,
            in CastleGateGeometry primaryGate)
        {
            PlacedKeepPlan = placedKeepPlan;
            PrimaryGate = primaryGate;
        }

        /// <summary>
        /// Projects a fully resolved spatial plan into the compatibility CastlePlan consumed by
        /// legacy keep-local helpers plus authoritative world-space primary-gate geometry.
        /// </summary>
        public static CastleSpatialLayoutProjection Create(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
            {
                throw new InvalidOperationException(
                    "Castle spatial layout must resolve terrain-dependent keep placement before projection.");
            }

            CastlePlan placedKeepPlan = plan;
            placedKeepPlan.Centre = new int3(
                plan.Centre.x + spatial.KeepCentre.x,
                plan.Centre.y,
                plan.Centre.z + spatial.KeepCentre.y - LegacyKeepCentreZOffset);

            CastleGatePlacementSpec gatePlacement = spatial.PrimaryGate;
            CastleGateGeometry gateGeometry = CastleGateGeometryResolver.Resolve(
                in plan, in gatePlacement);
            return new CastleSpatialLayoutProjection(in placedKeepPlan, in gateGeometry);
        }

        /// <summary>The semantic keep centre in world X/Z recovered from the compatibility plan.</summary>
        public int2 KeepCentreWorld => new int2(
            PlacedKeepPlan.Centre.x,
            PlacedKeepPlan.Centre.z + LegacyKeepCentreZOffset);

        public int3 TrapdoorCentre => CastleLayout.TrapdoorCentre(in PlacedKeepPlan);

        public int3 ChapelBellTowerCentre =>
            CastleLayout.ChapelBellTowerCentre(in PlacedKeepPlan);
    }
}
