using System;
using System.IO;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using UnityEngine;
using VoxelEngine.Composition;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Experiment-020 discriminator for the assigned macro-world validation profile. It observes
    /// the real Rossdam survey camera and production draw set without driving streaming/rendering.
    /// Rossdam policy stays here; the renderer diagnostic accepts only caller-provided bounds.
    /// </summary>
    internal sealed class KentridgeRossdamPublicationEvidence : MonoBehaviour
    {
        private const string ValidationProfile = "kentridge-macro-world";
        private const uint Seed = 0x4B454E54u;
        private const float DmToMetres = 0.1f;
        private const int SurveyOffsetDm = 60;
        private const float SurveyHeightMetres = 70f;
        private const float CameraPositionToleranceMetres = 2f;
        private const float DiagnosticVerticalExtentMetres = 40f;

        private TopDownWorldSettlementPlan _rossdam;
        private Vector3 _expectedCameraPosition;
        private bool _initialized;
        private bool _recorded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForAssignedProfile()
        {
            if (!TryReadValidationProfile(out string profile)
                || !string.Equals(profile, ValidationProfile, StringComparison.Ordinal))
                return;

            var host = new GameObject("Kentridge Rossdam Publication Evidence");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<KentridgeRossdamPublicationEvidence>();
        }

        private void Start()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalPlanner.Plan(
                layout,
                KentridgeTopDownWorldPhysicalIntent.Build(),
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                voxelsPerDecimetre: 1);
            if (!physical.TryGetSettlement(
                    MountingForceTopDownWorldDefinition.Rossdam,
                    out _rossdam)
                || _rossdam.Buildings.Count < 4)
            {
                Debug.LogError("MACROEVIDENCE rossdam-publication unavailable-settlement");
                enabled = false;
                return;
            }

            TopDownWorldBuildingBlockoutPlan first = _rossdam.Buildings[0];
            int minX = first.CentreDm.X - first.HalfExtentXDm;
            int maxX = first.CentreDm.X + first.HalfExtentXDm;
            int minZ = first.CentreDm.Y - first.HalfExtentZDm;
            int maxZ = first.CentreDm.Y + first.HalfExtentZDm;
            for (var i = 1; i < _rossdam.Buildings.Count; i++)
            {
                TopDownWorldBuildingBlockoutPlan building = _rossdam.Buildings[i];
                minX = Math.Min(minX, building.CentreDm.X - building.HalfExtentXDm);
                maxX = Math.Max(maxX, building.CentreDm.X + building.HalfExtentXDm);
                minZ = Math.Min(minZ, building.CentreDm.Y - building.HalfExtentZDm);
                maxZ = Math.Max(maxZ, building.CentreDm.Y + building.HalfExtentZDm);
            }

            int focusX = (minX + maxX) / 2;
            int focusZ = (minZ + maxZ) / 2;
            int cameraX = focusX + SurveyOffsetDm;
            int cameraZ = focusZ + SurveyOffsetDm;
            int cameraGroundDm = TerrainSampler.HeightAt(cameraX, cameraZ, Seed);
            _expectedCameraPosition = new Vector3(
                cameraX * DmToMetres,
                cameraGroundDm * DmToMetres + SurveyHeightMetres,
                cameraZ * DmToMetres);
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized || _recorded) return;
            Camera camera = Camera.main;
            if (camera == null) return;
            if ((camera.transform.position - _expectedCameraPosition).sqrMagnitude
                > CameraPositionToleranceMetres * CameraPositionToleranceMetres)
                return;
            if (!RenderingComposition.HasCompletePublishedNearSurfaceCoverage()) return;

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            for (var i = 0; i < _rossdam.Buildings.Count; i++)
            {
                TopDownWorldBuildingBlockoutPlan building = _rossdam.Buildings[i];
                int groundDm = TerrainSampler.HeightAt(building.CentreDm.X, building.CentreDm.Y, Seed);
                float width = (building.HalfExtentXDm * 2 + 2) * DmToMetres;
                float depth = (building.HalfExtentZDm * 2 + 2) * DmToMetres;
                var bounds = new Bounds(
                    new Vector3(
                        building.CentreDm.X * DmToMetres,
                        groundDm * DmToMetres + DiagnosticVerticalExtentMetres * 0.5f,
                        building.CentreDm.Y * DmToMetres),
                    new Vector3(width, DiagnosticVerticalExtentMetres, depth));
                bool inFrustum = GeometryUtility.TestPlanesAABB(planes, bounds);
                bool queried = RenderingSurfaceCoverageDiagnostics.TryQueryVisibleSolidBounds(
                    bounds,
                    DmToMetres,
                    out SurfaceBoundsCoverage coverage);
                Debug.Log(
                    $"MACROEVIDENCE rossdam-publication building={i}" +
                    $" centreDm=({building.CentreDm.X},{building.CentreDm.Y})" +
                    $" frustum={inFrustum}" +
                    $" queried={queried}" +
                    $" visibleChunks={coverage.VisibleChunkCount}" +
                    $" readyChunks={coverage.ReadyChunkCount}" +
                    $" readyIndices={coverage.ReadyIndexCount}" +
                    $" sourceStep={coverage.MinimumSourceStep}-{coverage.MaximumSourceStep}");
            }

            _recorded = true;
        }

        private static bool TryReadValidationProfile(out string profile)
        {
            profile = null;
            string path = ReadArgument("-voxel-scene-issue");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            string json = File.ReadAllText(path);
            const string key = "\"validationProfile\"";
            int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex < 0) return false;
            int colon = json.IndexOf(':', keyIndex + key.Length);
            if (colon < 0) return false;
            int firstQuote = json.IndexOf('"', colon + 1);
            if (firstQuote < 0) return false;
            int secondQuote = json.IndexOf('"', firstQuote + 1);
            if (secondQuote < 0) return false;
            profile = json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
            return true;
        }

        private static string ReadArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
            return null;
        }
    }
}
