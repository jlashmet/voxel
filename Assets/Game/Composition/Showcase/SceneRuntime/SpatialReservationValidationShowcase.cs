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
            AddClaim(hard, new Color(0.92f, 0.92f, 0.92f, 0.72f), "Hard occupancy");
            AddClaim(clearance, new Color(0.12f, 0.78f, 0.95f, 0.55f), "Clearance");
            AddClaim(road, new Color(0.95f, 0.72f, 0.12f, 0.68f), "Road");
            AddClaim(access, new Color(0.25f, 0.88f, 0.32f, 0.68f), "Public access");
            AddClaim(underground, new Color(0.82f, 0.22f, 0.95f, 0.70f), "Underground");
            AddClaim(_rejected, new Color(0.95f, 0.12f, 0.10f, 0.62f), "Rejected candidate");

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
            camera.transform.position = new Vector3(0f, 4.4f, -10.5f);
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
            floor.transform.position = new Vector3(0f, -1.55f, 2.6f);
            floor.transform.localScale = new Vector3(10.5f, 0.15f, 7.2f);
            RemoveCollider(floor);
            floor.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.16f, 0.18f, 0.20f, 1f));
        }

        private void AddClaim(SpatialReservation claim, Color colour, string label)
        {
            ReservationBoundsDm window = _snapshot.Window;
            float centreX = (window.MinX + window.MaxX) * 0.5f;
            float centreY = (window.MinY + window.MaxY) * 0.5f;
            float centreZ = (window.MinZ + window.MaxZ) * 0.5f;
            float xScale = 8.0f / Math.Max(1, window.MaxX - window.MinX);
            float yScale = 3.2f / Math.Max(1, window.MaxY - window.MinY);
            float zScale = 5.6f / Math.Max(1, window.MaxZ - window.MinZ);

            ReservationBoundsDm bounds = claim.Bounds;
            Vector3 centre = new Vector3(
                ((bounds.MinX + bounds.MaxX) * 0.5f - centreX) * xScale,
                ((bounds.MinY + bounds.MaxY) * 0.5f - centreY) * yScale,
                ((bounds.MinZ + bounds.MaxZ) * 0.5f - centreZ) * zScale + 2.6f);
            Vector3 size = new Vector3(
                Math.Max(0.18f, (bounds.MaxX - bounds.MinX) * xScale),
                Math.Max(0.18f, (bounds.MaxY - bounds.MinY) * yScale),
                Math.Max(0.18f, (bounds.MaxZ - bounds.MinZ) * zScale));

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = label + " — " + claim.OwnerId;
            _spawned.Add(box);
            box.transform.position = centre;
            box.transform.localScale = size;
            RemoveCollider(box);
            box.GetComponent<Renderer>().sharedMaterial = CreateMaterial(colour);
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
            GUI.Box(new Rect(18f, 18f, 690f, 164f), GUIContent.none);
            GUI.Label(new Rect(32f, 28f, 660f, 28f),
                "Spatial Reservations — module validation", _headingStyle);
            GUI.Label(new Rect(32f, 60f, 660f, 44f),
                "WHITE hard   CYAN clearance   YELLOW road   GREEN access   MAGENTA underground   RED rejected",
                _bodyStyle);
            GUI.Label(new Rect(32f, 108f, 660f, 56f),
                "Production Kentridge reservation + hidden-space computations; local scene is presentation-only.\n"
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
