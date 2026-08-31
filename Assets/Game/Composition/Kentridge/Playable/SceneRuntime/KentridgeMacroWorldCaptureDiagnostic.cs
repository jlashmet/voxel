using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Composition.Kentridge.Playable;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Validation-only end-of-frame discriminator for macro settlement captures. It observes the
    /// already-authored survey camera after the settlement-lens composition has run and reports the
    /// actual FOV/pose plus viewport and authoritative/published surface state at all authored
    /// generic blockout centres. It never changes camera, streaming, world generation, rendering,
    /// residency, or replay state.
    /// </summary>
    [DefaultExecutionOrder(20000)]
    internal sealed class KentridgeMacroWorldCaptureDiagnostic : MonoBehaviour
    {
        private const string ValidationProfile = "kentridge-macro-world";
        private const uint Seed = 0x4B454E54u;
        private const float DmToMetres = 0.1f;
        private const float SurveyHeightMetres = 70f;
        private const float HeightToleranceMetres = 1.5f;
        private const int SurveyHorizontalOffsetDm = 60;
        private const float CameraMatchToleranceMetres = 2f;
        private const int TopProbeBelowTerrainVoxels = 16;
        private const int TopProbeAboveTerrainVoxels = 400;
        private const float CentreCoverageWidthMetres = 4f;
        private const float CentreCoverageHeightMetres = 40f;
        private const int StableReadyFrames = 4;

        private static readonly FieldInfo s_WorldField = typeof(KentridgePlayableSlice).GetField(
            "_world",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);
        private Survey[] _surveys;
        private KentridgePlayableSlice _slice;
        private ShowcaseWorld _world;
        private string _readySurvey;
        private int _stableReadyFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForAssignedProfile()
        {
            if (!TryReadValidationProfile(out string profile)
                || !string.Equals(profile, ValidationProfile, StringComparison.Ordinal))
                return;

            var host = new GameObject("Kentridge Macro Capture Diagnostic");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<KentridgeMacroWorldCaptureDiagnostic>();
        }

        private void Awake()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalPlanner.Plan(
                layout,
                KentridgeTopDownWorldPhysicalIntent.Build(),
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                voxelsPerDecimetre: 1);

            _surveys = new[]
            {
                BuildSurvey(physical, MountingForceTopDownWorldDefinition.Moordell),
                BuildSurvey(physical, MountingForceTopDownWorldDefinition.Rossdam),
                BuildSurvey(physical, MountingForceTopDownWorldDefinition.FairyVillage),
                BuildSurvey(physical, MountingForceTopDownWorldDefinition.OrcVillage)
            };
        }

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null || _surveys == null) return;

            Vector3 position = camera.transform.position;
            int xDm = Mathf.RoundToInt(position.x / DmToMetres);
            int zDm = Mathf.RoundToInt(position.z / DmToMetres);
            float terrainMetres = TerrainSampler.HeightAt(xDm, zDm, Seed) * DmToMetres;
            if (Mathf.Abs(position.y - terrainMetres - SurveyHeightMetres) > HeightToleranceMetres)
            {
                ResetReadyFrames();
                return;
            }

            for (var surveyIndex = 0; surveyIndex < _surveys.Length; surveyIndex++)
            {
                Survey survey = _surveys[surveyIndex];
                float dx = position.x - survey.CameraDm.X * DmToMetres;
                float dz = position.z - survey.CameraDm.Y * DmToMetres;
                if (dx * dx + dz * dz > CameraMatchToleranceMetres * CameraMatchToleranceMetres) continue;
                if (_reported.Contains(survey.Label)) return;
                if (!IsCaptureReady(survey))
                {
                    ResetReadyFrames(survey.Label);
                    return;
                }

                if (!string.Equals(_readySurvey, survey.Label, StringComparison.Ordinal))
                {
                    _readySurvey = survey.Label;
                    _stableReadyFrames = 0;
                }
                _stableReadyFrames++;
                if (_stableReadyFrames < StableReadyFrames) return;
                _reported.Add(survey.Label);

                bool allCentresInside = true;
                var centres = new string[survey.BuildingCentresDm.Length];
                for (var i = 0; i < survey.BuildingCentresDm.Length; i++)
                {
                    Int2 centreDm = survey.BuildingCentresDm[i];
                    int groundVoxel = TerrainSampler.HeightAt(centreDm.X, centreDm.Y, Seed);
                    float ground = groundVoxel * DmToMetres;
                    Vector3 viewport = camera.WorldToViewportPoint(new Vector3(
                        centreDm.X * DmToMetres,
                        ground + 8f,
                        centreDm.Y * DmToMetres));
                    bool inside = viewport.z > 0f
                                  && viewport.x >= 0.04f && viewport.x <= 0.96f
                                  && viewport.y >= 0.04f && viewport.y <= 0.96f;
                    allCentresInside &= inside;

                    bool hasTop = _world.SurfaceQuery.TryFindTopSolid(
                        centreDm.X,
                        centreDm.Y,
                        groundVoxel - TopProbeBelowTerrainVoxels,
                        groundVoxel + TopProbeAboveTerrainVoxels,
                        out int topY,
                        out VoxelCell topCell);
                    int topDelta = hasTop ? topY - groundVoxel : int.MinValue;
                    var coverageBounds = new Bounds(
                        new Vector3(
                            centreDm.X * DmToMetres,
                            ground + CentreCoverageHeightMetres * 0.5f,
                            centreDm.Y * DmToMetres),
                        new Vector3(
                            CentreCoverageWidthMetres,
                            CentreCoverageHeightMetres,
                            CentreCoverageWidthMetres));
                    bool queried = RenderingSurfaceCoverageDiagnostics.TryQueryVisibleSolidBounds(
                        coverageBounds,
                        DmToMetres,
                        out SurfaceBoundsCoverage coverage);
                    centres[i] =
                        $"b{i}=({viewport.x:0.000},{viewport.y:0.000},{viewport.z:0.0}) inside={inside}" +
                        $" top={(hasTop ? topY.ToString() : "none")}" +
                        $" delta={(hasTop ? topDelta.ToString() : "none")}" +
                        $" material={(hasTop ? topCell.BaseMaterialId.ToString() : "none")}" +
                        $" surfaceStyle={(hasTop ? topCell.Surface.ReconstructionStyleId.ToString() : "none")}" +
                        $" coverage={queried}/{coverage.ReadyChunkCount}/{coverage.ReadyIndexCount}" +
                        $" sourceStep={coverage.MinimumSourceStep}-{coverage.MaximumSourceStep}";
                }

                Vector3 euler = camera.transform.rotation.eulerAngles;
                Debug.Log(
                    $"MACROEVIDENCE end-frame-survey target={survey.Label} fov={camera.fieldOfView:0.0} " +
                    $"position=({position.x:0.0},{position.y:0.0},{position.z:0.0}) " +
                    $"euler=({euler.x:0.0},{euler.y:0.0},{euler.z:0.0}) allBuildingCentresInside={allCentresInside} " +
                    string.Join(" ", centres));
                ResetReadyFrames();
                return;
            }

            ResetReadyFrames();
        }

        private bool IsCaptureReady(Survey survey)
        {
            _slice ??= FindFirstObjectByType<KentridgePlayableSlice>();
            if (_slice == null || s_WorldField == null) return false;
            _world ??= s_WorldField.GetValue(_slice) as ShowcaseWorld;
            if (_world == null || !RenderingComposition.HasCompletePublishedNearSurfaceCoverage()) return false;

            for (var i = 0; i < survey.BuildingCentresDm.Length; i++)
            {
                Int2 centreDm = survey.BuildingCentresDm[i];
                int ground = TerrainSampler.HeightAt(centreDm.X, centreDm.Y, Seed);
                var worldPoint = new Vector3(
                    centreDm.X * DmToMetres,
                    ground * DmToMetres,
                    centreDm.Y * DmToMetres);
                if (!_world.IsPresentationColumnContentSettled(worldPoint)) return false;
            }
            return true;
        }

        private void ResetReadyFrames(string survey = null)
        {
            _readySurvey = survey;
            _stableReadyFrames = 0;
        }

        private static Survey BuildSurvey(TopDownWorldPhysicalPlan physical, string nodeId)
        {
            if (!physical.TryGetSettlement(nodeId, out TopDownWorldSettlementPlan settlement))
                throw new InvalidOperationException("Capture diagnostic has no settlement '" + nodeId + "'.");

            var centres = new Int2[settlement.Buildings.Count];
            TopDownWorldBuildingBlockoutPlan first = settlement.Buildings[0];
            centres[0] = first.CentreDm;
            int minX = first.CentreDm.X - first.HalfExtentXDm;
            int maxX = first.CentreDm.X + first.HalfExtentXDm;
            int minZ = first.CentreDm.Y - first.HalfExtentZDm;
            int maxZ = first.CentreDm.Y + first.HalfExtentZDm;
            for (var i = 1; i < settlement.Buildings.Count; i++)
            {
                TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[i];
                centres[i] = building.CentreDm;
                minX = Math.Min(minX, building.CentreDm.X - building.HalfExtentXDm);
                maxX = Math.Max(maxX, building.CentreDm.X + building.HalfExtentXDm);
                minZ = Math.Min(minZ, building.CentreDm.Y - building.HalfExtentZDm);
                maxZ = Math.Max(maxZ, building.CentreDm.Y + building.HalfExtentZDm);
            }

            var focusDm = new Int2((minX + maxX) / 2, (minZ + maxZ) / 2);
            return new Survey(
                nodeId,
                new Int2(focusDm.X + SurveyHorizontalOffsetDm, focusDm.Y + SurveyHorizontalOffsetDm),
                centres);
        }

        private static bool TryReadValidationProfile(out string profile)
        {
            profile = null;
            string path = ReadArgument("-voxel-scene-issue");
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return false;
            string json = System.IO.File.ReadAllText(path);
            const string key = "\"validationProfile\"";
            int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex < 0) return false;
            int colon = json.IndexOf(':', keyIndex + key.Length);
            int firstQuote = colon >= 0 ? json.IndexOf('"', colon + 1) : -1;
            int secondQuote = firstQuote >= 0 ? json.IndexOf('"', firstQuote + 1) : -1;
            if (firstQuote < 0 || secondQuote <= firstQuote) return false;
            profile = json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
            return true;
        }

        private static string ReadArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
            return null;
        }

        private readonly struct Survey
        {
            public string Label { get; }
            public Int2 CameraDm { get; }
            public Int2[] BuildingCentresDm { get; }

            public Survey(string label, Int2 cameraDm, Int2[] buildingCentresDm)
            {
                Label = label;
                CameraDm = cameraDm;
                BuildingCentresDm = buildingCentresDm;
            }
        }
    }
}
