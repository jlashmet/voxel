using System;
using System.Collections;
using System.Diagnostics;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelEngine.Composition;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Validation
{
    /// <summary>
    /// Module-owned visual acceptance probe for reusable macro physical realization. The fixture is
    /// intentionally independent of Kentridge composition: four generic settlements, a substantial
    /// water barrier, a ridge plus authored pass, and two hard routes all flow through the production
    /// physical planner/catalogue, ShowcaseWorld streaming/storage, and production renderer. The
    /// scene creates only camera/light presentation; no validation-only geometry stands in for the
    /// authored voxel output.
    /// </summary>
    [AddComponentMenu("WorldBuilder/Validation/Macro Physical World Probe")]
    [DisallowMultipleComponent]
    public sealed class MacroPhysicalWorldValidationProbe : MonoBehaviour
    {
        private const uint Seed = 0x4D414352u;
        private const int CellSizeDm = 800;
        private const int LoadRadiusRegions = 3;
        private const int UnloadRadiusRegions = 4;
        private const int BrickPoolCapacity = 196608;
        private const double StreamingBudgetMs = 18.0;
        private const float ViewTimeoutSeconds = 10f;
        private const int RequiredStableFrames = 4;
        private const float ViewDistanceMetres = 38f;
        private const float ViewHeightMetres = 26f;

        private ShowcaseWorld _world;
        private Camera _camera;
        private string _status = "building production macro plan";
        private string _metrics = string.Empty;

        private IEnumerator Start()
        {
            FeatureCatalogue catalogue = default;
            var total = Stopwatch.StartNew();
            try
            {
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

                Require(plan.Settlements.Count == 4,
                    "independent fixture must realize four settlements");
                Require(plan.BuildingCount >= 16,
                    "generic settlement production must emit at least four blockouts per settlement");
                Require(plan.Routes.Count == 2, "fixture must retain both hard routes");
                Require(plan.GeographyConstrainedRouteCount == 2,
                    "both routes must be solved against authored geography");
                Require(plan.RouteTileCount == replay.RouteTileCount
                        && plan.RouteSolveSteps == replay.RouteSolveSteps,
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

                catalogue = TopDownWorldPhysicalVoxelCatalogue.Build(
                    layout,
                    intent,
                    new Int2(0, 0),
                    CellSizeDm,
                    settings,
                    Allocator.Persistent);
                Require(catalogue.IsCreated,
                    "production physical voxel catalogue was not created");
                int definitions = catalogue.Definitions.Length;
                int rules = catalogue.Rules.Length;
                int placements = catalogue.ExplicitPlacements.Length;
                Require(definitions > 0 && rules > 0 && placements > 0,
                    "production catalogue must contain authored definitions, rules, and placements");

                _world = new ShowcaseWorld(
                    Seed,
                    BrickPoolCapacity,
                    LoadRadiusRegions,
                    UnloadRadiusRegions);
                _world.ConfigureGeneratedContentForGameplay(catalogue);
                catalogue = default;

                ConfigureProductionRenderer();
                EnsurePresentationCameraAndLight();

                Int2 settlementCentre = plan.Settlements[0].CentreDm;
                ValidationView[] views =
                {
                    new("settlement-road", settlementCentre, new Vector3(-0.75f, 0f, -1f)),
                    new("lake-detour", lake.CentreDm, new Vector3(-0.9f, 0f, -0.75f)),
                    new("ridge-pass", pass.CentreDm, new Vector3(-0.65f, 0f, -1f)),
                };

                for (var i = 0; i < views.Length; i++)
                    yield return ValidateRenderedView(views[i]);

                total.Stop();
                long managed = Profiler.GetTotalAllocatedMemoryLong();
                long reserved = Profiler.GetTotalReservedMemoryLong();
                RenderingComposition.GetVoxelSurfaceCounts(out int visible, out int missing);
                Debug.Log(
                    "MACRO_PHYSICAL_WORLD_COST " +
                    $"seconds={total.Elapsed.TotalSeconds:F2} regions={plan.Regions.Count} " +
                    $"settlements={plan.Settlements.Count} buildings={plan.BuildingCount} " +
                    $"routes={plan.Routes.Count} route_tiles={plan.RouteTileCount} " +
                    $"solve_steps={plan.RouteSolveSteps} constrained_routes={plan.GeographyConstrainedRouteCount} " +
                    $"definitions={definitions} rules={rules} placements={placements} " +
                    $"generated_regions={_world.RegionsGenerated} feature_voxels={_world.FeatureVoxelsBuilt} " +
                    $"visible={visible} missing={missing} managed_bytes={managed} reserved_bytes={reserved}");
                Debug.Log(
                    "MACRO_PHYSICAL_WORLD_VALIDATION ready: " +
                    $"independent_graph=true deterministic=true blocked_route_rejected=true " +
                    $"production_voxel_rendering=true lake={lake.HalfExtentXDm * 2}x{lake.HalfExtentZDm * 2}dm " +
                    $"ridge_height_delta={ridge.ElevationDeltaDm}dm pass={pass.Spec.Id}");
                _status = "ready — planner/catalogue streamed through production voxel rendering";
            }
            catch (Exception ex)
            {
                _status = "FAILED — " + ex.Message;
                Debug.LogError("MACRO_PHYSICAL_WORLD_VALIDATION FAILED: " + ex);
            }
            finally
            {
                if (catalogue.IsCreated) catalogue.Dispose();
            }
        }

        private IEnumerator ValidateRenderedView(ValidationView view)
        {
            Vector3 target = new(
                view.FocusDm.X * 0.1f,
                ShowcaseWorld.BaseHeightVoxels * ShowcaseWorld.VoxelSize,
                view.FocusDm.Y * 0.1f);
            Vector3 horizontal = view.ViewDirection.sqrMagnitude > 0.001f
                ? view.ViewDirection.normalized
                : new Vector3(0f, 0f, -1f);
            Vector3 cameraPosition = target
                                     + horizontal * ViewDistanceMetres
                                     + Vector3.up * ViewHeightMetres;
            _camera.transform.position = cameraPosition;
            _camera.transform.rotation = Quaternion.LookRotation(
                target + Vector3.up * 4f - cameraPosition,
                Vector3.up);

            _status = "streaming production view — " + view.Name;
            int stableFrames = 0;
            double started = Time.realtimeSinceStartupAsDouble;
            int visible = 0;
            int missing = int.MaxValue;
            while (Time.realtimeSinceStartupAsDouble - started < ViewTimeoutSeconds)
            {
                _world.StepStreaming((float3)_camera.transform.position, StreamingBudgetMs);
                yield return null;

                RenderingComposition.GetVoxelSurfaceCounts(out visible, out missing);
                bool storageSettled = _world.IsPresentationColumnContentSettled(
                    (float3)_camera.transform.position);
                bool published = RenderingComposition.HasCompletePublishedNearSurfaceCoverage();
                bool ready = storageSettled && published && visible > 0 && missing == 0;
                stableFrames = ready ? stableFrames + 1 : 0;
                _metrics =
                    $"view {view.Name}  visible {visible}  missing {missing}\n" +
                    $"pending {_world.PendingRegionLoads}  generated {_world.RegionsGenerated}  " +
                    $"feature voxels {_world.FeatureVoxelsBuilt}  stable {stableFrames}/{RequiredStableFrames}";
                if (stableFrames >= RequiredStableFrames)
                    break;
            }

            Require(stableFrames >= RequiredStableFrames,
                $"production rendered view '{view.Name}' did not converge; " +
                $"visible={visible}, missing={missing}, pending={_world.PendingRegionLoads}, " +
                $"generated={_world.RegionsGenerated}, featureVoxels={_world.FeatureVoxelsBuilt}");
            Debug.Log(
                "MACRO_PHYSICAL_WORLD_VIEW ready: " +
                $"target={view.Name} visible={visible} missing={missing} " +
                $"generated_regions={_world.RegionsGenerated} feature_voxels={_world.FeatureVoxelsBuilt}");
        }

        private void ConfigureProductionRenderer()
        {
            RenderingComposition.ResetSurfacePassDiagnostics("macro-physical-world-validation");
            RenderingComposition.SetSurfaceBuildEnabled(false);
            RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);
            RenderingComposition.SetVoxelRingRadiusMetres(
                (LoadRadiusRegions - 1) * ShowcaseWorld.RegionMetres);
            RenderingComposition.SetVoxelDetailBandScale(0.6f);
            var renderingWorld = new RenderingWorldBinding(
                _world.ReadStorage,
                _world.Palette,
                _world.SurfaceRules,
                _world.CoatingRules,
                _world.ProfileBlocks);
            RenderingComposition.ConfigureWorld(
                in renderingWorld,
                _world.Changes,
                _world.Seed,
                farFieldEnabled: false);
            RenderingComposition.SetSurfaceBuildEnabled(true);
        }

        private void EnsurePresentationCameraAndLight()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var cameraObject = new GameObject("Macro Physical Validation Camera");
                cameraObject.tag = "MainCamera";
                _camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 1200f;
            _camera.fieldOfView = 58f;

            if (FindFirstObjectByType<Light>() == null)
            {
                var lightObject = new GameObject("Macro Physical Validation Sun");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            }
        }

        private static TopDownWorldLayout BuildLayout()
        {
            var west = new TopDownWorldNodeSpec(
                "west", "Weststead", TopDownWorldNodeKind.Settlement, 600, "validation graph");
            var east = new TopDownWorldNodeSpec(
                "east", "Eaststead", TopDownWorldNodeKind.Settlement, 600, "validation graph");
            var south = new TopDownWorldNodeSpec(
                "south", "Southstead", TopDownWorldNodeKind.Settlement, 600, "validation graph");
            var north = new TopDownWorldNodeSpec(
                "north", "Northstead", TopDownWorldNodeKind.Settlement, 600, "validation graph");
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
                    new TopDownWorldRouteSpec(
                        "west", "east", new TopDownWorldGridPoint(2, 0),
                        TopDownWorldEvidenceKind.VerifiedTransition,
                        "validation east-west hard route", "module validation", 36),
                    new TopDownWorldRouteSpec(
                        "south", "north", new TopDownWorldGridPoint(0, 4),
                        TopDownWorldEvidenceKind.VerifiedTransition,
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
                new TopDownWorldSettlementPhysicalSpec(
                    "west", TopDownWorldSettlementRealizationKind.GenericBlockout, 4),
                new TopDownWorldSettlementPhysicalSpec(
                    "east", TopDownWorldSettlementRealizationKind.GenericBlockout, 4),
                new TopDownWorldSettlementPhysicalSpec(
                    "south", TopDownWorldSettlementRealizationKind.GenericBlockout, 4),
                new TopDownWorldSettlementPhysicalSpec(
                    "north", TopDownWorldSettlementRealizationKind.GenericBlockout, 4)
            };
            return new TopDownWorldPhysicalIntentSpec(regions, constraints, settlements);
        }

        private static void RequireBlockedRouteNeedsSemanticSolution(VoxelWorldGenSettings settings)
        {
            var a = new TopDownWorldNodeSpec(
                "a", "A", TopDownWorldNodeKind.Settlement, 600, "validation blocker");
            var b = new TopDownWorldNodeSpec(
                "b", "B", TopDownWorldNodeKind.Settlement, 600, "validation blocker");
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
                    new TopDownWorldRouteSpec(
                        "a", "b", new TopDownWorldGridPoint(2, 0),
                        TopDownWorldEvidenceKind.VerifiedTransition,
                        "blocked route discriminator", "module validation", 36)
                });
            var water = new TopDownWorldRegionSpec(
                "blocker", "Blocking Water", TopDownWorldRegionKind.WaterBody,
                TopDownWorldRegionRelationKind.Between, "a", "b",
                300, 300, -40, source: "module validation blocker");
            var intent = new TopDownWorldPhysicalIntentSpec(
                new[] { water },
                Array.Empty<TopDownWorldRouteRegionConstraintSpec>(),
                new[]
                {
                    new TopDownWorldSettlementPhysicalSpec(
                        "a", TopDownWorldSettlementRealizationKind.GenericBlockout),
                    new TopDownWorldSettlementPhysicalSpec(
                        "b", TopDownWorldSettlementRealizationKind.GenericBlockout)
                });
            bool planned = TopDownWorldPhysicalPlanner.TryPlan(
                layout,
                intent,
                new Int2(0, 0),
                CellSizeDm,
                settings.VoxelsPerDecimetre,
                out _,
                out string error);
            Require(!planned && !string.IsNullOrWhiteSpace(error),
                "a hard route crossing blocking geography must fail without an authored semantic solution");
        }

        private static VoxelWorldGenSettings Settings()
        {
            return new VoxelWorldGenSettings(
                1,
                new VoxelMaterialMap(
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
                    roadSurface: 12));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private void OnDestroy()
        {
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            _world?.Dispose();
            _world = null;
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(18, 18, 720, 110),
                "Macro Physical World · Production Voxel Validation");
            GUI.Label(new Rect(32, 48, 680, 24), _status);
            GUI.Label(new Rect(32, 72, 680, 50), _metrics);
        }

        private readonly struct ValidationView
        {
            public readonly string Name;
            public readonly Int2 FocusDm;
            public readonly Vector3 ViewDirection;

            public ValidationView(string name, Int2 focusDm, Vector3 viewDirection)
            {
                Name = name;
                FocusDm = focusDm;
                ViewDirection = viewDirection;
            }
        }
    }
}
