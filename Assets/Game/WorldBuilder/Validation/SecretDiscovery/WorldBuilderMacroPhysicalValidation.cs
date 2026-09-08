using System;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Validation
{
    /// <summary>
    /// Module-local built-player validation for the macro physical planning/catalogue path. The
    /// existing WorldBuilder validation scene still owns the rendered production consumer; this
    /// bootstrap adds source-backed macro planning and immutable catalogue construction so the new
    /// WorldBuilder runtime path is executed inside its owning module's player validation surface.
    /// </summary>
    internal static class WorldBuilderMacroPhysicalValidation
    {
        private const string ScenePath =
            "Assets/Game/WorldBuilder/Validation/SecretDiscovery/WorldBuilderSecretDiscoveryValidation.unity";
        private const uint Seed = 0x4B454E54u;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Validate()
        {
            if (!string.Equals(SceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal))
                return;

            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalIntentSpec intent = KentridgeTopDownWorldPhysicalIntent.Build();
            VoxelWorldGenSettings settings = Settings();
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalVoxelCatalogue.Plan(
                layout,
                intent,
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                settings);

            Require(physical.Settlements.Count == 6,
                "source-backed macro plan no longer realizes all six settlements");
            Require(physical.BuildingCount >= 16,
                "source-backed macro plan lost reusable settlement blockouts");
            Require(physical.GeographyConstrainedRouteCount >= 3,
                "source-backed macro plan lost geography-constrained routes");
            Require(
                physical.TryGetSettlement(
                    MountingForceTopDownWorldDefinition.Moordell,
                    out TopDownWorldSettlementPlan moordell)
                && moordell.Buildings.Count >= 4,
                "Moordell no longer has a reusable physical settlement realization");
            Require(
                physical.TryGetRegion(
                    KentridgeTopDownWorldPhysicalIntent.RossdamLake,
                    out TopDownWorldRegionPlan lake)
                && lake.Spec.Kind == TopDownWorldRegionKind.WaterBody,
                "Rossdam lake no longer reaches the production physical plan");

            FeatureCatalogue catalogue = default;
            try
            {
                catalogue = TopDownWorldPhysicalVoxelCatalogue.Build(
                    layout,
                    intent,
                    KentridgeDefinition.TownCentreDm,
                    MountingForceTopDownWorldDefinition.CellSizeDm,
                    settings,
                    Allocator.Temp);
                Require(catalogue.IsCreated,
                    "production macro physical catalogue was not created");
                Require(catalogue.Definitions.Length >= 46,
                    "production macro physical catalogue lost expected semantic definitions");
                Require(catalogue.ExplicitPlacements.Length > physical.BuildingCount,
                    "production macro physical catalogue did not publish route/building placements");

                Debug.Log(
                    "WorldBuilder macro physical validation ready: " +
                    $"settlements={physical.Settlements.Count} buildings={physical.BuildingCount} " +
                    $"regions={physical.Regions.Count} routes={physical.Routes.Count} " +
                    $"constrainedRoutes={physical.GeographyConstrainedRouteCount} " +
                    $"definitions={catalogue.Definitions.Length} placements={catalogue.ExplicitPlacements.Length}");
            }
            finally
            {
                if (catalogue.IsCreated) catalogue.Dispose();
            }
        }

        private static VoxelWorldGenSettings Settings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1,
                masonry: 2,
                darkMasonry: 3,
                timber: 4,
                glass: 5,
                warmWindow: 6,
                roofTile: 7,
                slate: 8,
                cloth: 9,
                moss: 10,
                water: 11,
                roadSurface: 12);
            return new VoxelWorldGenSettings(1, materials);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(
                    "WORLD_BUILDER_MACRO_PHYSICAL_VALIDATION FAILED: " + message);
        }
    }
}
