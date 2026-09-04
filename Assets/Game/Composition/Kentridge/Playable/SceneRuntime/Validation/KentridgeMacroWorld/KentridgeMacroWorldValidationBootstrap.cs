using System;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Module-local validation bootstrap for the shipped Kentridge SceneRuntime composition. The
    /// scene owns no alternate world graph or renderer: it hosts the real KentridgePlayableSlice,
    /// checks a few source-backed macro relationships, then attaches the same production evidence
    /// driver used by the assignment's full built-player replay so streaming, readiness, rendering,
    /// and CharacterMotor traversal all execute through the shipped composition path.
    /// </summary>
    internal static class KentridgeMacroWorldValidationBootstrap
    {
        private const string ScenePath =
            "Assets/Game/Composition/Kentridge/Playable/SceneRuntime/Validation/" +
            "KentridgeMacroWorld/KentridgeMacroWorldValidation.unity";
        private const uint Seed = 0x4B454E54u;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!string.Equals(SceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal))
                return;

            KentridgePlayableSlice slice = UnityEngine.Object.FindFirstObjectByType<KentridgePlayableSlice>();
            Require(slice != null, "focused scene is missing the production KentridgePlayableSlice");

            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalPlanner.Plan(
                layout,
                KentridgeTopDownWorldPhysicalIntent.Build(),
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                voxelsPerDecimetre: 1);

            Require(
                physical.TryGetSettlement(
                    MountingForceTopDownWorldDefinition.Moordell,
                    out TopDownWorldSettlementPlan moordell)
                && moordell.Buildings.Count >= 4,
                "source-backed Moordell physical settlement is missing its reusable blockouts");

            TopDownWorldPhysicalRoutePlan moordellArrival = FindRoute(
                physical,
                MountingForceTopDownWorldDefinition.MoordellCorridor,
                MountingForceTopDownWorldDefinition.Moordell);
            Require(moordellArrival.Tiles.Count > 1,
                "source-backed Moordell road does not reach the settlement");

            Require(
                physical.TryGetRegion(
                    KentridgeTopDownWorldPhysicalIntent.RossdamLake,
                    out TopDownWorldRegionPlan lake)
                && lake.Spec.Kind == TopDownWorldRegionKind.WaterBody,
                "source-backed Rossdam lake is missing from physical intent");
            TopDownWorldPhysicalRoutePlan lakeRoute = FindRoute(
                physical,
                MountingForceTopDownWorldDefinition.MoordellCorridor,
                MountingForceTopDownWorldDefinition.RossdamApproach);
            Require(lakeRoute.GeographyConstrained,
                "Rossdam hard route is no longer solved against authored geography");

            if (UnityEngine.Object.FindFirstObjectByType<KentridgeMacroWorldEvidenceDriver>() == null)
                slice.gameObject.AddComponent<KentridgeMacroWorldEvidenceDriver>();

            Debug.Log(
                "KENTRIDGE_MACRO_MODULE_VALIDATION ready: " +
                $"source_backed=true moordell_buildings={moordell.Buildings.Count} " +
                $"moordell_road_tiles={moordellArrival.Tiles.Count} " +
                $"lake_route_constrained={lakeRoute.GeographyConstrained} " +
                "streaming=KentridgePlayableSlice evidence=production");
        }

        private static TopDownWorldPhysicalRoutePlan FindRoute(
            TopDownWorldPhysicalPlan physical,
            string fromId,
            string toId)
        {
            for (var i = 0; i < physical.Routes.Count; i++)
            {
                TopDownWorldPhysicalRoutePlan route = physical.Routes[i];
                if (string.Equals(route.Route.FromId, fromId, StringComparison.Ordinal)
                    && string.Equals(route.Route.ToId, toId, StringComparison.Ordinal))
                    return route;
            }

            throw new InvalidOperationException(
                $"KENTRIDGE_MACRO_MODULE_VALIDATION FAILED: missing route {fromId}->{toId}");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(
                    "KENTRIDGE_MACRO_MODULE_VALIDATION FAILED: " + message);
        }
    }
}
