using System;
using System.Collections.Generic;
using System.Diagnostics;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Module-local built-player validation surface for the spatial reservation system.
    /// It renders a deterministic read-only projection of production Kentridge reservation claims,
    /// one production hidden-space claim, and one deliberately rejected candidate. It owns no
    /// placement authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpatialReservationValidationShowcase : MonoBehaviour
    {
        private const uint EvidenceSeed = 0x4B454E54u;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Material> _materials = new List<Material>();
        private SpatialReservationSnapshot _snapshot;
        private SpatialReservation _rejected;
        private ReservationQueryResult _rejection;
        private GUIStyle _headingStyle;
        private GUIStyle _bodyStyle;

        private void Awake()
        {
            long started = Stopwatch.GetTimestamp();
            SettlementPlan plan = KentridgeTownPlanner.Build(EvidenceSeed);
            _snapshot = KentridgeTownPlanner.BuildReservationSnapshot(EvidenceSeed);
            SpatialReservation hard = FindFirst(
                claim => claim.Category == ReservationCategory.Building
                    && (claim.Semantics & ReservationSemantics.HardOccupancy) != 0,
                "hard building");
            SpatialReservation clearance = FindFirst(
                claim => (claim.Semantics & ReservationSemantics.Clearance) != 0,
                "clearance");
            SpatialReservation road = FindFirst(
                claim => claim.Category == ReservationCategory.Road,
                "road");
            SpatialReservation access = FindFirst(
                claim => claim.Category == ReservationCategory.PublicAccess,
                "public access");
            SpatialReservation underground = BuildProductionUndergroundClaim(plan);

            _rejected = SpatialReservation.Box(
                "validation:deliberate-rejected-candidate",
                ReservationCategory.Landmark,
                ReservationSemantics.HardOccupancy,
                hard.Bounds,
                precedence: hard.Precedence,
                provenance: "SpatialReservationValidationShowcase deliberate conflict");
            _rejection = _snapshot.Query(
                _rejected,
                ReservationConsumerKind.Landmark,
                ReservationCategory.Building | ReservationCategory.Plaza
                | ReservationCategory.Road | ReservationCategory.PublicAccess);
            if (_rejection.Decision != ReservationDecision.Rejected)
                throw new InvalidOperationException(
                    "Spatial reservation validation expected the deliberate overlap to be rejected.");

            BuildEnvironment();

            // Presentation-only evidence layout. The geometry and decisions above remain production-derived;
            // fixed display positions prevent unrelated Kentridge world extents from making one claim occlude
            // another in the module-local validation capture.
            AddEvidenceClaim(hard, new Color(0.92f, 0.92f, 0.92f, 1f),
                "Hard occupancy", new Vector3(-3.9f, 0.72f, 2.7f), 1.25f);
            AddEvidenceClaim(clearance, new Color(0.12f, 0.78f, 0.95f, 1f),
                "Clearance", new Vector3(-2.35f, 0.72f, 2.7f), 1.25f);
            AddEvidenceClaim(road, new Color(0.95f, 0.72f, 0.12f, 1f),
                "Road", new Vector3(-0.55f, 0.62f, 2.7f), 1.75f);
            AddEvidenceClaim(access, new Color(0.25f, 0.88f, 0.32f, 1f),
                "Public access", new Vector3(1.25f, 0.62f, 2.7f), 1.75f);
            AddEvidenceClaim(_rejected, new Color(0.95f, 0.12f, 0.10f, 1f),
                "Rejected candidate", new Vector3(2.85f, 0.72f, 2.7f), 1.25f);

            AddSurfaceSlice(new Vector3(4.25f, 0.52f, 2.7f));
            AddEvidenceClaim(underground, new Color(0.82f, 0.22f, 0.95f, 1f),
                "Underground", new Vector3(4.25f, -0.72f, 2.7f), 1.30f);

            ReservationQueryMetrics metrics = _rejection.Metrics;
            Debug.Log(
                "SPATIAL_RESERVATION_COST build_ticks=" + (Stopwatch.GetTimestamp() - started)
                + " claims=" + _snapshot.Reservations.Count
                + " query_buckets=" + metrics.BucketsVisited
                + " query_candidates=" + metrics.BroadPhaseCandidates
                + " query_tests=" + metrics.NarrowPhaseTests
                + " allocated_bytes=" + Profiler.GetTotalAllocatedMemoryLong()
                + " reserved_bytes=" + Profiler.GetTotalReservedMemoryLong()
                + " unused_reserved_bytes=" + Profiler.GetTotalUnusedReservedMemoryLong());
            Debug.Log(
                "SPATIAL_RESERVATION_VALIDATION ready: claims=" + _snapshot.Reservations.Count
                + " underground=" + underground.OwnerId
                + " decision=" + _rejection.Decision
                + " reason=" + _rejection.Reason);
        }

        private SpatialReservation FindFirst(Predicate<SpatialReservation> predicate, string description)
        {
            for (int i = 0; i < _snapshot.Reservations.Count; i++)
            {
                SpatialReservation claim = _snapshot.Reservations[i];
                if (predicate(claim)) return claim;
            }
            throw new InvalidOperationException(
                "Spatial reservation validation found no production " + description + " claim.");
        }

        private static SpatialReservation BuildProductionUndergroundClaim(SettlementPlan plan)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                var request = new SiteHiddenSpaceRequest(
                    "spatial-reservation-validation:" + plot.RoleId,
                    plot.RoleId,
                    minimumCount: 0,
                    targetCount: 1,
                    entrance: HiddenSpaceEntranceKind.BreakableMatchingWall);
                IReadOnlyList<KentridgeHiddenSpaceGeometry> candidates =
                    KentridgeHiddenSpacePlanner.Resolve(plot, plan.Seed, request);
                if (candidates.Count == 0) continue;
                return WorldBuilderReservationFactory.HiddenSpaceVolume(
                    candidates[0].Realization,
                    new Int3(plot.PositionDm.X, 0, plot.PositionDm.Y));
            }

            throw new InvalidOperationException(
                "Spatial reservation validation could not realize a production hidden-space claim.");
        }

        private void BuildEnvironment()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Validation Camera");
                _spawned.Add(cameraObject);
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }
            camera.transform.position = new Vector3(0.15f, 5.2f, -10.6f);
            camera.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            camera.fieldOfView = 48f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.11f, 0.16f, 1f);

            GameObject lightObject = new GameObject("Validation Sun");
            _spawned.Add(lightObject);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Validation Ground";
            _spawned.Add(floor);
            floor.transform.position = new Vector3(0.1f, -1.48f, 2.7f);
            floor.transform.localScale = new Vector3(10.5f, 0.10f, 3.0f);
            RemoveCollider(floor);
            floor.GetComponent<Renderer>().sharedMaterial =
                CreateMaterial(new Color(0.16f, 0.18f, 0.20f, 1f));
        }

        private void AddEvidenceClaim(
            SpatialReservation claim,
            Color colour,
            string label,
            Vector3 displayCentre,
            float maxDisplaySize)
        {
            ReservationBoundsDm bounds = claim.Bounds;
            float width = Math.Max(1, bounds.MaxX - bounds.MinX);
            float height = Math.Max(1, bounds.MaxY - bounds.MinY);
            float depth = Math.Max(1, bounds.MaxZ - bounds.MinZ);
            float largest = Math.Max(width, Math.Max(height, depth));
            float scale = maxDisplaySize / largest;
            Vector3 size = new Vector3(
                Math.Max(0.24f, width * scale),
                Math.Max(0.24f, height * scale),
                Math.Max(0.24f, depth * scale));

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = label + " — " + claim.OwnerId;
            _spawned.Add(box);
            box.transform.position = displayCentre;
            box.transform.localScale = size;
            RemoveCollider(box);
            box.GetComponent<Renderer>().sharedMaterial = CreateMaterial(colour);
        }

        private void AddSurfaceSlice(Vector3 centre)
        {
            GameObject slice = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slice.name = "Surface reference above underground claim";
            _spawned.Add(slice);
            slice.transform.position = centre;
            slice.transform.localScale = new Vector3(1.55f, 0.10f, 1.25f);
            RemoveCollider(slice);
            slice.GetComponent<Renderer>().sharedMaterial =
                CreateMaterial(new Color(0.48f, 0.50f, 0.54f, 1f));
        }

        private Material CreateMaterial(Color colour)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                throw new InvalidOperationException(
                    "Spatial reservation validation requires a runtime colour shader.");
            Material material = new Material(shader)
            {
                color = colour,
                hideFlags = HideFlags.DontSave
            };
            _materials.Add(material);
            return material;
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUI.Box(new Rect(18f, 18f, 760f, 182f), GUIContent.none);
            GUI.Label(new Rect(32f, 28f, 730f, 28f),
                "Spatial Reservations — module validation", _headingStyle);
            GUI.Label(new Rect(32f, 60f, 730f, 48f),
                "LEFT→RIGHT: WHITE hard   CYAN clearance   YELLOW road   GREEN access   RED rejected   MAGENTA underground",
                _bodyStyle);
            GUI.Label(new Rect(32f, 108f, 730f, 76f),
                "Production Kentridge reservation + hidden-space computations; fixed positions are presentation-only.\n"
                + "Red is the exact hard-overlap candidate, separated only for readability; magenta is shown below a surface slice.\n"
                + "Rejected candidate: " + _rejection.Describe(),
                _bodyStyle);
        }

        private void EnsureStyles()
        {
            if (_headingStyle != null) return;
            _headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true
            };
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Destroy(_spawned[i]);
            for (int i = 0; i < _materials.Count; i++)
                if (_materials[i] != null) Destroy(_materials[i]);
        }
    }
}
