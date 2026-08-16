using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// World-space projection of a validated spatial castle plan for consumers that still use the
    /// historical keep/dungeon authoring recipe. This is the single compatibility seam for the
    /// recipe's legacy +60 Z keep anchor; semantic planning continues to expose the actual keep
    /// centre through <see cref="CastleSpatialPlan.KeepCentre"/>.
    /// </summary>
    public readonly struct CastleSpatialLayoutProjection
    {
        private const int LegacyKeepCentreZOffset = 60;

        /// <summary>
        /// Compatibility plan consumed by the existing keep/dungeon realization recipe. Its
        /// Centre is an authoring anchor, not the semantic castle centre.
        /// </summary>
        public CastlePlan KeepRecipePlan { get; }

        /// <summary>Authoritative world-space geometry for the primary gate.</summary>
        public CastleGateGeometry PrimaryGateGeometry { get; }

        /// <summary>Actual semantic keep centre in world X/Z voxels.</summary>
        public int2 KeepCentreWorld { get; }

        private CastleSpatialLayoutProjection(
            in CastlePlan keepRecipePlan,
            in CastleGateGeometry primaryGateGeometry,
            int2 keepCentreWorld)
        {
            KeepRecipePlan = keepRecipePlan;
            PrimaryGateGeometry = primaryGateGeometry;
            KeepCentreWorld = keepCentreWorld;
        }

        /// <summary>
        /// Projects one resolved, validated spatial plan into world-space interaction/realization
        /// geometry. Terrain-dependent keep placement must already be resolved by Composition.
        /// </summary>
        public static CastleSpatialLayoutProjection Create(
            in CastlePlan plan,
            CastleSpatialPlan spatialPlan)
        {
            if (spatialPlan == null) throw new ArgumentNullException(nameof(spatialPlan));
            if (spatialPlan.KeepRequiresTerrainResolution)
            {
                throw new InvalidOperationException(
                    "Castle spatial layout cannot be projected before HighestGround keep resolution.");
            }

            if (!CastleSpatialPlanValidator.TryValidate(
                    in plan, spatialPlan, out CastleSpatialPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle spatial layout cannot project an invalid plan: {issue}.");
            }

            int2 keepCentreWorld = new int2(
                plan.Centre.x + spatialPlan.KeepCentre.x,
                plan.Centre.z + spatialPlan.KeepCentre.y);

            CastlePlan keepRecipePlan = plan;
            keepRecipePlan.Centre = new int3(
                keepCentreWorld.x,
                plan.Centre.y,
                keepCentreWorld.y - LegacyKeepCentreZOffset);

            CastleGatePlacementSpec primaryGate = spatialPlan.PrimaryGate;
            CastleGateGeometry primaryGateGeometry = CastleGateGeometryResolver.Resolve(
                in plan, in primaryGate);

            return new CastleSpatialLayoutProjection(
                in keepRecipePlan,
                in primaryGateGeometry,
                keepCentreWorld);
        }

        /// <summary>World-space trapdoor centre matching the projected keep/dungeon recipe.</summary>
        public int3 TrapdoorCentre
        {
            get
            {
                CastlePlan keepPlan = KeepRecipePlan;
                return CastleLayout.TrapdoorCentre(in keepPlan);
            }
        }

        /// <summary>World-space chapel bell-tower centre matching the projected keep recipe.</summary>
        public int3 ChapelBellTowerCentre
        {
            get
            {
                CastlePlan keepPlan = KeepRecipePlan;
                return CastleLayout.ChapelBellTowerCentre(in keepPlan);
            }
        }
    }
}
