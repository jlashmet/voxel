using System;
using System.Diagnostics;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Validation
{
    /// <summary>
    /// Module-owned visual acceptance probe for reusable macro physical realization. The fixture is
    /// intentionally independent of Kentridge composition: four generic settlements, a substantial
    /// water barrier, a ridge plus authored pass, and two hard routes all flow through the production
    /// physical planner and voxel catalogue before being projected into this read-only tableau.
    /// </summary>
    [AddComponentMenu("WorldBuilder/Validation/Macro Physical World Probe")]
    [DisallowMultipleComponent]
    public sealed class MacroPhysicalWorldValidationProbe : MonoBehaviour
    {
        private const uint Seed = 0x4D414352u;
        private const int CellSizeDm = 800;
        private const float DisplayScale = 0.018f;

        private string _status = "building production macro plan";

        private void Start()
        {
            var timer = Stopwatch.StartNew();
            VoxelWorldGenSettings settings = Settings();
            TopDownWorldLayout layout = BuildLayout();
            TopDownWorldPhysicalIntentSpec intent = BuildIntent();

            TopDownWorldPhysicalPlan plan = TopDownWorldPhysicalPlanner.Plan(
                layout,
                intent,
                new Int2(0, 0),
                CellSizeDm,
                settings.VoxelsPerDecimetre);
            TopDownWorldPhysicalPlan replay = TopDownWorldPhysicalPlanner.Plan(
                layout,
                intent,
                new Int2(0, 0),
                CellSizeDm,
                settings.VoxelsPerDecimetre);

            Require(plan.Settlements.Count == 4, "independent fixture must realize four settlements");
            Require(plan.BuildingCount >= 16, "generic settlement production must emit at least four blockouts per settlement");
            Require(plan.Routes.Count == 2, "fixture must retain both hard routes");
            Require(plan.GeographyConstrainedRouteCount == 2, "both routes must be solved against authored geography");
            Require(plan.RouteTileCount == replay.RouteTileCount && plan.RouteSolveSteps == replay.RouteSolveSteps,
                "production planning must replay deterministically for the same seed and semantic inputs");
            Require(plan.TryGetRegion("validation-lake", out TopDownWorldRegionPlan lake)
                    && lake.Spec.Kind == TopDownWorldRegionKind.WaterBody,
                "water barrier must survive semantic planning");
            Require(plan.TryGetRegion("validation-ridge", out TopDownWorldRegionPlan ridge)
                    && ridge.Spec.Kind == TopDownWorldRegionKind.MountainRidge,
                "ridge barrier must survive semantic planning");
            Require(plan.TryGetRegion("validation-pass", out TopDownWorldRegionPlan pass)
                    && pass.Spec.Kind == TopDownWorldRegionKind.ValleyPass,
                "designated pass must survive semantic planning");

            RequireBlockedRouteNeedsSemanticSolution(settings);

            FeatureCatalogue catalogue = default;
            int definitions = 0;
            int rules = 0;
            int placements = 0;
            try
            {
                catalogue = TopDownWorldPhysicalVoxelCatalogue.Build(
                    layout,
                    intent,
                    new Int2(0, 0),
                    CellSizeDm,
                    settings,
                    Allocator.Persistent);
                Require(catalogue.IsCreated, "production physical voxel catalogue was not created");
                definitions = catalogue.Definitions.Length;
                rules = catalogue.Rules.Length;
                placements = catalogue.ExplicitPlacements.Length;
                Require(definitions > 0 && rules > 0 && placements > 0,
                    "production catalogue must contain authored definitions, rules, and placements");
            }
            finally
            {
                if (catalogue.IsCreated) catalogue.Dispose();
            }

            BuildPresentation(plan);
            timer.Stop();
            long managed = Profiler.GetTotalAllocatedMemoryLong();
            long reserved = Profiler.GetTotalReservedMemoryLong();
            Debug.Log(
                "MACRO_PHYSICAL_WORLD_COST " +
                $"build_ms={timer.Elapsed.TotalMilliseconds:F2} regions={plan.Regions.Count} settlements={plan.Settlements.Count} " +
                $"buildings={plan.BuildingCount} routes={plan.Routes.Count} route_tiles={plan.RouteTileCount} " +
                $"solve_steps={plan.RouteSolveSteps} constrained_routes={plan.GeographyConstrainedRouteCount} " +
                $"definitions={definitions} rules={rules} placements={placements} managed_bytes={managed} reserved_bytes={reserved}");
            Debug.Log(
                "MACRO_PHYSICAL_WORLD_VALIDATION ready: " +
                $"independent_graph=true deterministic=true blocked_route_rejected=true lake={lake.HalfExtentXDm * 2}x{lake.HalfExtentZDm * 2}dm " +
                $"ridge_height_delta={ridge.ElevationDeltaDm}dm pass={pass.Spec.Id}");
            _status = "ready — production planner/catalogue visualized from semantic output";
        }

        private static TopDownWorldLayout BuildLayout()
        {
            var west = new TopDownWorldNodeSpec("west", "Weststead", TopDownWorldNodeKind.Settlement, 600, "validation graph");
            var east = new TopDownWorldNodeSpec("east", "Eaststead", TopDownWorldNodeKind.Settlement, 600, "validation graph");
            var south = new TopDownWorldNodeSpec("south", "Southstead", TopDownWorldNodeKind.Settlement, 600, "validation graph");
            var north = new TopDownWorldNodeSpec("north", "Northstead", TopDownWorldNodeKind.Settlement, 600, "validation graph");
            return new TopDownWorldLayout(
                "west",
                Seed,
                new[]
                {
                    new TopDownWorldNodePlacement(west, new TopDownWorldGridPoint(0, 0)),
                    new TopDownWorldNodePlacement(east, new TopDownWorldGridPoint(2, 0)),
                    new TopDownWorldNodePlacement(south, new TopDownWorldGridPoint(0, -2)),
                    new TopDownWorldNodePlacement(north, new TopDownWorldGridPoint(0, 2))
                },
                new[]
                {
                    new TopDownWorldRouteSpec("west", "east", new TopDownWorldGridPoint(2, 0), TopDownWorldEvidenceKind.VerifiedTransition,
                        "validation east-west hard route", "module validation", 36),
                    new TopDownWorldRouteSpec("south", "north", new TopDownWorldGridPoint(0, 4), TopDownWorldEvidenceKind.VerifiedTransition,
                        "validation north-south hard route", "module validation", 36)
                });
        }

        private static TopDownWorldPhysicalIntentSpec BuildIntent()
        {
            var regions = new[]
            {
                new TopDownWorldRegionSpec(
                    "validation-lake", "Validation Lake", TopDownWorldRegionKind.WaterBody,
                    TopDownWorldRegionRelationKind.Between, "west", "east",
                    halfExtentXDm: 300, halfExtentZDm: 260, elevationDeltaDm: -45, variationDm: 0,
                    source: "module validation substantial water blocker"),
                new TopDownWorldRegionSpec(
                    "validation-ridge", "Validation Ridge", TopDownWorldRegionKind.MountainRidge,
                    TopDownWorldRegionRelationKind.Separates, "south", "north",
                    halfExtentXDm: 360, halfExtentZDm: 150, elevationDeltaDm: 120, variationDm: 0,
                    source: "module validation ridge blocker"),
                new TopDownWorldRegionSpec(
                    "validation-pass", "Validation Pass", TopDownWorldRegionKind.ValleyPass,
                    TopDownWorldRegionRelationKind.Between, "south", "north",
                    halfExtentXDm: 70, halfExtentZDm: 260, elevationDeltaDm: 24, variationDm: 0,
                    source: "module validation designated ridge crossing")
            };
            var constraints = new[]
            {
                new TopDownWorldRouteRegionConstraintSpec(
                    "west", "east", "validation-lake", TopDownWorldRouteRegionSolutionKind.GoAround,
                    clearanceDm: 60, source: "validation dry-ground lake detour"),
                new TopDownWorldRouteRegionConstraintSpec(
                    "south", "north", "validation-ridge", TopDownWorldRouteRegionSolutionKind.DesignatedCrossing,
                    "validation-pass", clearanceDm: 35, source: "validation ridge pass")
            };
            var settlements = new[]
            {
                new TopDownWorldSettlementPhysicalSpec("west", TopDownWorldSettlementRealizationKind.GenericBlockout, 4),
                new TopDownWorldSettlementPhysicalSpec("east", TopDownWorldSettlementRealizationKind.GenericBlockout, 4),
                new TopDownWorldSettlementPhysicalSpec("south", TopDownWorldSettlementRealizationKind.GenericBlockout, 4),
                new TopDownWorldSettlementPhysicalSpec("north", TopDownWorldSettlementRealizationKind.GenericBlockout, 4)
            };
            return new TopDownWorldPhysicalIntentSpec(regions, constraints, settlements);
        }

        private static void RequireBlockedRouteNeedsSemanticSolution(VoxelWorldGenSettings settings)
        {
            var a = new TopDownWorldNodeSpec("a", "A", TopDownWorldNodeKind.Settlement, 600, "validation blocker");
            var b = new TopDownWorldNodeSpec("b", "B", TopDownWorldNodeKind.Settlement, 600, "validation blocker");
            var layout = new TopDownWorldLayout(
                "a",
                Seed,
                new[]
                {
                    new TopDownWorldNodePlacement(a, new TopDownWorldGridPoint(0, 0)),
                    new TopDownWorldNodePlacement(b, new TopDownWorldGridPoint(2, 0))
                },
                new[]
                {
                    new TopDownWorldRouteSpec("a", "b", new TopDownWorldGridPoint(2, 0), TopDownWorldEvidenceKind.VerifiedTransition,
                        "blocked route discriminator", "module validation", 36)
                });
            var water = new TopDownWorldRegionSpec(
                "blocker", "Blocking Water", TopDownWorldRegionKind.WaterBody,
                TopDownWorldRegionRelationKind.Between, "a", "b", 300, 300, -40, source: "module validation blocker");
            var intent = new TopDownWorldPhysicalIntentSpec(
                new[] { water },
                Array.Empty<TopDownWorldRouteRegionConstraintSpec>(),
                new[]
                {
                    new TopDownWorldSettlementPhysicalSpec("a", TopDownWorldSettlementRealizationKind.GenericBlockout),
                    new TopDownWorldSettlementPhysicalSpec("b", TopDownWorldSettlementRealizationKind.GenericBlockout)
                });
            bool planned = TopDownWorldPhysicalPlanner.TryPlan(
                layout, intent, new Int2(0, 0), CellSizeDm, settings.VoxelsPerDecimetre,
                out _, out string error);
            Require(!planned && !string.IsNullOrWhiteSpace(error),
                "a hard route crossing blocking geography must fail without an authored semantic solution");
        }

        private static VoxelWorldGenSettings Settings()
        {
            return new VoxelWorldGenSettings(
                1,
                new VoxelMaterialMap(
                    foundationStone: 1, masonry: 2, darkMasonry: 3, timber: 4,
                    glass: 5, warmWindow: 6, roofTile: 7, slate: 8, cloth: 9,
                    moss: 10, water: 11, roadSurface: 12));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("MACRO_PHYSICAL_WORLD_VALIDATION FAILED: " + message);
        }

        private static Vector3 Display(Int2 point, float y = 0f) =>
            new Vector3(point.X * DisplayScale, y, point.Y * DisplayScale);

        private static void BuildPresentation(TopDownWorldPhysicalPlan plan)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Validation Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
                camera.transform.position = new Vector3(22f, 36f, -38f);
                camera.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
                camera.farClipPlane = 500f;
            }
            var lightObject = new GameObject("Validation Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            for (var i = 0; i < plan.Regions.Count; i++)
            {
                TopDownWorldRegionPlan region = plan.Regions[i];
                float height = region.Spec.Kind == TopDownWorldRegionKind.MountainRidge ? 2.8f : 0.35f;
                float y = region.Spec.Kind == TopDownWorldRegionKind.WaterBody ? -0.4f : height * 0.5f;
                GameObject marker = GameObject.CreatePrimitive(CubeOrCylinder(region.Spec.Kind));
                marker.name = "Region · " + region.Spec.Name;
                marker.transform.position = Display(region.CentreDm, y);
                marker.transform.localScale = new Vector3(
                    Math.Max(0.8f, region.HalfExtentXDm * 2 * DisplayScale),
                    height,
                    Math.Max(0.8f, region.HalfExtentZDm * 2 * DisplayScale));
            }

            for (var i = 0; i < plan.Settlements.Count; i++)
            {
                TopDownWorldSettlementPlan settlement = plan.Settlements[i];
                for (var buildingIndex = 0; buildingIndex < settlement.Buildings.Count; buildingIndex++)
                {
                    TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[buildingIndex];
                    GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    marker.name = settlement.Node.Name + " · Building " + buildingIndex;
                    float height = Math.Max(0.6f, building.HeightDm * DisplayScale);
                    marker.transform.position = Display(building.CentreDm, height * 0.5f + 0.15f);
                    marker.transform.localScale = new Vector3(
                        Math.Max(0.4f, building.HalfExtentXDm * 2 * DisplayScale),
                        height,
                        Math.Max(0.4f, building.HalfExtentZDm * 2 * DisplayScale));
                }
            }

            for (var routeIndex = 0; routeIndex < plan.Routes.Count; routeIndex++)
            {
                TopDownWorldPhysicalRoutePlan route = plan.Routes[routeIndex];
                var routeObject = new GameObject("Production Hard Route " + routeIndex);
                LineRenderer line = routeObject.AddComponent<LineRenderer>();
                line.positionCount = route.Tiles.Count;
                line.widthMultiplier = 0.16f;
                line.useWorldSpace = true;
                line.material = new Material(Shader.Find("Sprites/Default"));
                for (var tileIndex = 0; tileIndex < route.Tiles.Count; tileIndex++)
                    line.SetPosition(tileIndex, Display(route.Tiles[tileIndex], 0.45f));
            }
        }

        private static PrimitiveType CubeOrCylinder(TopDownWorldRegionKind kind) =>
            kind == TopDownWorldRegionKind.ValleyPass ? PrimitiveType.Cylinder : PrimitiveType.Cube;

        private void OnGUI()
        {
            GUI.Box(new Rect(18, 18, 660, 78), "Macro Physical World · Reusable Production Validation");
            GUI.Label(new Rect(32, 50, 630, 24), _status);
        }
    }
}
