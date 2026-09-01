using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Composition.Kentridge.Playable;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using Unity.Mathematics;
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
    /// actual FOV/pose plus viewport, exact authored shell/roof storage, and published surface state
    /// for all generic blockouts. It never changes camera, streaming, world generation, rendering,
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
        private const float CentreCoverageWidthMetres = 4f;
        private const float CentreCoverageHeightMetres = 40f;
        private const int StableReadyFrames = 4;
        private const int BuildingFoundationInsetDm = 6;
        private const int BuildingTerrainSamplesPerAxis = 5;
        private const int TimberProbeHeightDm = 10;
        private const byte TimberMaterialId = 2;
        private const byte RoofTileMaterialId = 8;

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
                bool allAuthoredShellsStored = true;
                var buildings = new string[survey.Buildings.Length];
                for (var i = 0; i < survey.Buildings.Length; i++)
                {
                    TopDownWorldBuildingBlockoutPlan building = survey.Buildings[i];
                    Int2 centreDm = building.CentreDm;
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

                    int maximumGround = SampleMaximumGround(building);
                    var timberVoxel = new int3(
                        building.CentreDm.X,
                        maximumGround + TimberProbeHeightDm,
                        building.CentreDm.Y - building.HalfExtentZDm + 1);
                    var roofVoxel = new int3(
                        building.CentreDm.X,
                        maximumGround + building.HeightDm,
                        building.CentreDm.Y);
                    bool timberRead = TryReadCell(_world.ReadStorage, timberVoxel, out VoxelCell timberCell);
                    bool roofRead = TryReadCell(_world.ReadStorage, roofVoxel, out VoxelCell roofCell);
                    bool timberStored = timberRead && timberCell.BaseMaterialId == TimberMaterialId;
                    bool roofStored = roofRead && roofCell.BaseMaterialId == RoofTileMaterialId;
                    allAuthoredShellsStored &= timberStored && roofStored;

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
                    buildings[i] =
                        $"b{i}=({viewport.x:0.000},{viewport.y:0.000},{viewport.z:0.0}) inside={inside}" +
                        $" maxGround={maximumGround}" +
                        $" timberVoxel={Format(timberVoxel)} timberRead={timberRead} timberMaterial={(timberRead ? timberCell.BaseMaterialId.ToString() : "none")} timberStored={timberStored}" +
                        $" roofVoxel={Format(roofVoxel)} roofRead={roofRead} roofMaterial={(roofRead ? roofCell.BaseMaterialId.ToString() : "none")} roofStored={roofStored}" +
                        $" coverage={queried}/{coverage.ReadyChunkCount}/{coverage.ReadyIndexCount}" +
                        $" sourceStep={coverage.MinimumSourceStep}-{coverage.MaximumSourceStep}";
                }

                Vector3 euler = camera.transform.rotation.eulerAngles;
                Debug.Log(
                    $"MACROEVIDENCE end-frame-survey target={survey.Label} fov={camera.fieldOfView:0.0} " +
                    $"position=({position.x:0.0},{position.y:0.0},{position.z:0.0}) " +
                    $"euler=({euler.x:0.0},{euler.y:0.0},{euler.z:0.0}) allBuildingCentresInside={allCentresInside} " +
                    $"allAuthoredShellsStored={allAuthoredShellsStored} " +
                    string.Join(" ", buildings));
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

            for (var i = 0; i < survey.Buildings.Length; i++)
            {
                Int2 centreDm = survey.Buildings[i].CentreDm;
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

            var buildings = new TopDownWorldBuildingBlockoutPlan[settlement.Buildings.Count];
            TopDownWorldBuildingBlockoutPlan first = settlement.Buildings[0];
            buildings[0] = first;
            int minX = first.CentreDm.X - first.HalfExtentXDm;
            int maxX = first.CentreDm.X + first.HalfExtentXDm;
            int minZ = first.CentreDm.Y - first.HalfExtentZDm;
            int maxZ = first.CentreDm.Y + first.HalfExtentZDm;
            for (var i = 1; i < settlement.Buildings.Count; i++)
            {
                TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[i];
                buildings[i] = building;
                minX = Math.Min(minX, building.CentreDm.X - building.HalfExtentXDm);
                maxX = Math.Max(maxX, building.CentreDm.X + building.HalfExtentXDm);
                minZ = Math.Min(minZ, building.CentreDm.Y - building.HalfExtentZDm);
                maxZ = Math.Max(maxZ, building.CentreDm.Y + building.HalfExtentZDm);
            }

            var focusDm = new Int2((minX + maxX) / 2, (minZ + maxZ) / 2);
            return new Survey(
                nodeId,
                new Int2(focusDm.X + SurveyHorizontalOffsetDm, focusDm.Y + SurveyHorizontalOffsetDm),
                buildings);
        }

        private static int SampleMaximumGround(TopDownWorldBuildingBlockoutPlan building)
        {
            int leftDm = building.CentreDm.X - building.HalfExtentXDm - BuildingFoundationInsetDm;
            int rightDm = building.CentreDm.X + building.HalfExtentXDm + BuildingFoundationInsetDm;
            int backDm = building.CentreDm.Y - building.HalfExtentZDm - BuildingFoundationInsetDm;
            int frontDm = building.CentreDm.Y + building.HalfExtentZDm + BuildingFoundationInsetDm;
            int maximumGround = int.MinValue;
            for (var x = 0; x < BuildingTerrainSamplesPerAxis; x++)
            {
                int sampleX = leftDm + (rightDm - leftDm) * x / (BuildingTerrainSamplesPerAxis - 1);
                for (var z = 0; z < BuildingTerrainSamplesPerAxis; z++)
                {
                    int sampleZ = backDm + (frontDm - backDm) * z / (BuildingTerrainSamplesPerAxis - 1);
                    maximumGround = Math.Max(maximumGround, TerrainSampler.HeightAt(sampleX, sampleZ, Seed));
                }
            }
            return maximumGround;
        }

        private static bool TryReadCell(IRegionReadSource reads, int3 worldVoxel, out VoxelCell cell)
        {
            int edge = ShowcaseWorld.RegionVoxelEdge;
            var region = new int3(
                (int)math.floor((float)worldVoxel.x / edge),
                (int)math.floor((float)worldVoxel.y / edge),
                (int)math.floor((float)worldVoxel.z / edge));
            int3 local = worldVoxel - region * edge;
            if (!reads.TryAcquireRegion(region, out RegionReadView view))
            {
                cell = default;
                return false;
            }
            return view.TryReadCell(local, out cell);
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

        private static string Format(int3 value) => $"({value.x},{value.y},{value.z})";

        private readonly struct Survey
        {
            public string Label { get; }
            public Int2 CameraDm { get; }
            public TopDownWorldBuildingBlockoutPlan[] Buildings { get; }

            public Survey(string label, Int2 cameraDm, TopDownWorldBuildingBlockoutPlan[] buildings)
            {
                Label = label;
                CameraDm = cameraDm;
                Buildings = buildings;
            }
        }
    }
}
