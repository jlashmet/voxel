using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Composition.Kentridge.Playable;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using UnityEngine;
using VoxelEngine.Composition;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Built-player evidence automation for capture-less macro-world validations. It is dormant in
    /// normal gameplay. When an assigned SceneIssue selects the reusable kentridge-macro-world
    /// validation profile, it exercises the production CharacterMotor first in ordinary gameplay
    /// and then on a generated macro-road segment before moving that same motor/camera through
    /// semantic physical-plan targets. Normal world streaming/rendering remains authoritative.
    /// </summary>
    internal sealed class KentridgeMacroWorldEvidenceDriver : MonoBehaviour
    {
        private const string ValidationProfile = "kentridge-macro-world";
        private const float OpeningEvidenceTimeScale = 12f;
        private const float WalkEvidenceSeconds = 0.75f;
        private const float RoadPrestreamSeconds = 0.75f;
        private const float RoadWalkSeconds = 1f;
        private const float TargetMinimumDwellSeconds = 1.25f;
        private const float TargetPostCaptureSeconds = 0.10f;
        private const float DmToMetres = 0.1f;
        private const uint Seed = 0x4B454E54u;
        private const int SettlementSurveyOffsetDm = 500;
        private const float SettlementSurveyHeightMetres = 36f;

        private static readonly FieldInfo s_MotorField = typeof(KentridgePlayableSlice).GetField(
            "_motor",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_YawField = typeof(KentridgePlayableSlice).GetField(
            "_yaw",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private KentridgePlayableSlice _slice;
        private KentridgeCharacterHost _motor;
        private EvidenceTarget[] _targets;
        private string _screenshotDirectory;
        private float _gameplayStartedAt = -1f;
        private Vector3 _walkStartedAt;
        private bool _walkRecorded;
        private Int2 _roadStartDm;
        private Int2 _roadNextDm;
        private bool _roadPrepared;
        private bool _roadWalkStarted;
        private Vector3 _roadWalkStartedAt;
        private bool _roadWalkRecorded;
        private int _targetIndex = -1;
        private float _targetStartedAt;
        private bool _targetCaptured;
        private float _targetCapturedAt;
        private float _originalTimeScale = 1f;
        private bool _timeScaleBoosted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForAssignedProfile()
        {
            if (!TryReadValidationProfile(out string profile)
                || !string.Equals(profile, ValidationProfile, StringComparison.Ordinal))
                return;

            var host = new GameObject("Kentridge Macro World Evidence");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<KentridgeMacroWorldEvidenceDriver>();
        }

        private void OnEnable()
        {
            _originalTimeScale = Time.timeScale;
            Time.timeScale = OpeningEvidenceTimeScale;
            _timeScaleBoosted = true;
            Debug.Log($"MACROEVIDENCE opening-time-scale={OpeningEvidenceTimeScale:0.##}");
        }

        private void OnDisable() => RestoreTimeScale();

        private void OnDestroy() => RestoreTimeScale();

        private void Update()
        {
            if (_slice == null)
            {
                _slice = FindFirstObjectByType<KentridgePlayableSlice>();
                if (_slice == null) return;
                if (s_MotorField == null || s_YawField == null)
                    throw new InvalidOperationException(
                        "Macro evidence driver cannot resolve Kentridge CharacterMotor/yaw host state.");
                _screenshotDirectory = ReadArgument("-voxel-screenshot-dir");
            }

            _motor ??= s_MotorField.GetValue(_slice) as KentridgeCharacterHost;
            if (_motor == null || !_slice.GameplayControlEnabled) return;

            RestoreTimeScale();

            if (_gameplayStartedAt < 0f)
            {
                _gameplayStartedAt = Time.realtimeSinceStartup;
                _walkStartedAt = _motor.Position;
                _slice.AutoSurvey = false;
                _slice.AutoRecede = false;
                _slice.AutoWalk = true;
                Debug.Log("MACROEVIDENCE phase=local-character-motor-walk start=" + Format(_walkStartedAt));
                return;
            }

            float elapsed = Time.realtimeSinceStartup - _gameplayStartedAt;
            if (elapsed < WalkEvidenceSeconds)
            {
                _slice.AutoSurvey = false;
                _slice.AutoRecede = false;
                _slice.AutoWalk = true;
                return;
            }

            if (!_walkRecorded)
            {
                _walkRecorded = true;
                Vector3 delta = _motor.Position - _walkStartedAt;
                delta.y = 0f;
                Debug.Log($"MACROEVIDENCE traversal=CharacterMotor-local metres={delta.magnitude:0.00} " +
                          $"from={Format(_walkStartedAt)} to={Format(_motor.Position)}");
                BuildTargetsAndRoadTraversal();
            }

            _slice.AutoSurvey = false;
            _slice.AutoRecede = false;

            float roadPrestreamEnd = WalkEvidenceSeconds + RoadPrestreamSeconds;
            float roadWalkEnd = roadPrestreamEnd + RoadWalkSeconds;
            if (elapsed < roadPrestreamEnd)
            {
                _slice.AutoWalk = false;
                PinToRoadStart();
                return;
            }

            if (elapsed < roadWalkEnd)
            {
                if (!_roadWalkStarted)
                {
                    _roadWalkStarted = true;
                    _roadWalkStartedAt = _motor.Position;
                    Debug.Log(
                        "MACROEVIDENCE phase=macro-road-character-motor start=" + Format(_roadWalkStartedAt) +
                        $" startDm=({_roadStartDm.X},{_roadStartDm.Y}) nextDm=({_roadNextDm.X},{_roadNextDm.Y})");
                }

                HoldRoadHeading();
                _slice.AutoWalk = true;
                return;
            }

            _slice.AutoWalk = false;
            if (!_roadWalkRecorded)
            {
                _roadWalkRecorded = true;
                Vector3 delta = _motor.Position - _roadWalkStartedAt;
                delta.y = 0f;
                Debug.Log($"MACROEVIDENCE traversal=CharacterMotor-macro-road metres={delta.magnitude:0.00} " +
                          $"from={Format(_roadWalkStartedAt)} to={Format(_motor.Position)}");
                CaptureNamed("macro-road-character-motor");
            }

            if (_targets == null || _targets.Length == 0) return;
            if (_targetIndex < 0)
                BeginTarget(0);

            EvidenceTarget target = _targets[_targetIndex];
            PinToTarget(target);
            float now = Time.realtimeSinceStartup;
            float targetElapsed = now - _targetStartedAt;
            if (!_targetCaptured
                && targetElapsed >= TargetMinimumDwellSeconds
                && RenderingComposition.HasCompletePublishedNearSurfaceCoverage())
            {
                _targetCaptured = true;
                _targetCapturedAt = now;
                Debug.Log("MACROEVIDENCE capture-ready target=" + target.Label + " coverage=True");
                CaptureTarget(target);
            }

            if (_targetCaptured
                && now - _targetCapturedAt >= TargetPostCaptureSeconds
                && _targetIndex + 1 < _targets.Length)
                BeginTarget(_targetIndex + 1);
        }

        private void LateUpdate()
        {
            if (_targetIndex < 0 || _targets == null || _targetIndex >= _targets.Length || _motor == null)
                return;
            ApplyCamera(_targets[_targetIndex]);
        }

        private void RestoreTimeScale()
        {
            if (!_timeScaleBoosted) return;
            Time.timeScale = _originalTimeScale;
            _timeScaleBoosted = false;
            Debug.Log($"MACROEVIDENCE restored-time-scale={Time.timeScale:0.##}");
        }

        private void BeginTarget(int index)
        {
            _targetIndex = index;
            _targetStartedAt = Time.realtimeSinceStartup;
            _targetCaptured = false;
            _targetCapturedAt = 0f;
            EvidenceTarget target = _targets[index];
            Debug.Log(
                "MACROEVIDENCE target=" + target.Label +
                $" cameraDm=({target.CameraDm.X},{target.CameraDm.Y})" +
                $" focusDm=({target.FocusDm.X},{target.FocusDm.Y})" +
                $" cameraHeightM={target.CameraHeightMetres:0.0}");
        }

        private void BuildTargetsAndRoadTraversal()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalPlanner.Plan(
                layout,
                KentridgeTopDownWorldPhysicalIntent.Build(),
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                voxelsPerDecimetre: 1);

            TopDownWorldPhysicalRoutePlan moordellArrival = FindRoute(
                physical,
                MountingForceTopDownWorldDefinition.MoordellCorridor,
                MountingForceTopDownWorldDefinition.Moordell);
            if (moordellArrival.Tiles.Count < 2)
                throw new InvalidOperationException("Moordell macro road has no traversable segment for evidence.");
            int roadIndex = Math.Max(0, moordellArrival.Tiles.Count - 10);
            roadIndex = Math.Min(roadIndex, moordellArrival.Tiles.Count - 2);
            _roadStartDm = moordellArrival.Tiles[roadIndex];
            _roadNextDm = moordellArrival.Tiles[roadIndex + 1];
            _roadPrepared = true;

            var targets = new List<EvidenceTarget>(7)
            {
                SettlementSurvey(physical, MountingForceTopDownWorldDefinition.Moordell),
                SettlementSurvey(physical, MountingForceTopDownWorldDefinition.Rossdam)
            };

            if (!physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.RossdamLake, out TopDownWorldRegionPlan lake))
                throw new InvalidOperationException("Macro evidence plan has no Rossdam lake.");
            TopDownWorldPhysicalRoutePlan rossdamRoute = FindRoute(
                physical,
                MountingForceTopDownWorldDefinition.MoordellCorridor,
                MountingForceTopDownWorldDefinition.RossdamApproach);
            Int2 lakeRoutePoint = rossdamRoute.Tiles[rossdamRoute.Tiles.Count / 2];
            targets.Add(new EvidenceTarget(
                "rossdam-lake-detour",
                lakeRoutePoint,
                lake.CentreDm,
                cameraHeightMetres: 72f,
                elevated: true));

            targets.Add(SettlementSurvey(physical, MountingForceTopDownWorldDefinition.FairyVillage));
            targets.Add(SettlementSurvey(physical, MountingForceTopDownWorldDefinition.OrcVillage));

            if (!physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.SouthernRidge, out TopDownWorldRegionPlan ridge)
                || !physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.SouthernPass, out TopDownWorldRegionPlan pass))
                throw new InvalidOperationException("Macro evidence plan has no southern ridge/pass.");
            TopDownWorldPhysicalRoutePlan loganRoute = FindRoute(
                physical,
                MountingForceTopDownWorldDefinition.SouthFightingArea,
                MountingForceTopDownWorldDefinition.LoganApproach);
            Int2 ridgeRoad = pass.CentreDm;
            for (var i = 0; i < loganRoute.Tiles.Count; i++)
            {
                if (!ridge.Contains(loganRoute.Tiles[i])) continue;
                ridgeRoad = loganRoute.Tiles[i];
                break;
            }
            targets.Add(new EvidenceTarget(
                "southern-ridge-pass",
                new Int2(ridgeRoad.X - 320, ridgeRoad.Y - 300),
                ridgeRoad,
                cameraHeightMetres: 55f,
                elevated: true));

            targets.Add(new EvidenceTarget(
                "macro-network-overview",
                new Int2(KentridgeDefinition.TownCentreDm.X, KentridgeDefinition.TownCentreDm.Y + 900),
                new Int2(KentridgeDefinition.TownCentreDm.X, KentridgeDefinition.TownCentreDm.Y + 250),
                cameraHeightMetres: 105f,
                elevated: true));
            _targets = targets.ToArray();
        }

        private static EvidenceTarget SettlementSurvey(
            TopDownWorldPhysicalPlan physical,
            string nodeId)
        {
            if (!physical.TryGetSettlement(nodeId, out TopDownWorldSettlementPlan settlement))
                throw new InvalidOperationException("Macro evidence plan has no settlement '" + nodeId + "'.");
            if (settlement.Buildings.Count < 4)
                throw new InvalidOperationException(
                    "Macro evidence settlement '" + nodeId + "' does not expose the expected four blockout plots.");

            var offsets = new[]
            {
                new Int2(-SettlementSurveyOffsetDm, -SettlementSurveyOffsetDm),
                new Int2(SettlementSurveyOffsetDm, -SettlementSurveyOffsetDm),
                new Int2(-SettlementSurveyOffsetDm, SettlementSurveyOffsetDm),
                new Int2(SettlementSurveyOffsetDm, SettlementSurveyOffsetDm)
            };

            int focusGround = TerrainSampler.HeightAt(settlement.CentreDm.X, settlement.CentreDm.Y, Seed);
            Int2 bestCamera = new Int2(
                settlement.CentreDm.X + offsets[0].X,
                settlement.CentreDm.Y + offsets[0].Y);
            int bestCameraGround = TerrainSampler.HeightAt(bestCamera.X, bestCamera.Y, Seed);
            int bestAdvantage = bestCameraGround - focusGround;
            for (var i = 1; i < offsets.Length; i++)
            {
                var candidate = new Int2(
                    settlement.CentreDm.X + offsets[i].X,
                    settlement.CentreDm.Y + offsets[i].Y);
                int candidateGround = TerrainSampler.HeightAt(candidate.X, candidate.Y, Seed);
                int advantage = candidateGround - focusGround;
                if (advantage <= bestAdvantage) continue;
                bestCamera = candidate;
                bestCameraGround = candidateGround;
                bestAdvantage = advantage;
            }

            return new EvidenceTarget(
                nodeId,
                bestCamera,
                settlement.CentreDm,
                cameraHeightMetres: SettlementSurveyHeightMetres,
                elevated: true);
        }

        private void PinToRoadStart()
        {
            if (!_roadPrepared) return;
            int ground = TerrainSampler.HeightAt(_roadStartDm.X, _roadStartDm.Y, Seed);
            _motor.Position = new Vector3(
                _roadStartDm.X * DmToMetres,
                ground * DmToMetres + 0.1f,
                _roadStartDm.Y * DmToMetres);
            _motor.Velocity = Vector3.zero;
            HoldRoadHeading();
            _slice.transform.position = _motor.EyePosition;
        }

        private void HoldRoadHeading()
        {
            Vector3 direction = new Vector3(
                _roadNextDm.X - _roadStartDm.X,
                0f,
                _roadNextDm.Y - _roadStartDm.Y);
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            s_YawField.SetValue(_slice, yaw);
            _slice.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static TopDownWorldPhysicalRoutePlan FindRoute(
            TopDownWorldPhysicalPlan physical,
            string fromId,
            string toId)
        {
            if (!physical.TryGetRoute(fromId, toId, out TopDownWorldPhysicalRoutePlan route))
                throw new InvalidOperationException("Macro evidence plan has no route '" + fromId + "->" + toId + "'.");
            return route;
        }

        private void PinToTarget(EvidenceTarget target)
        {
            int x = target.CameraDm.X;
            int z = target.CameraDm.Y;
            int ground = TerrainSampler.HeightAt(x, z, Seed);
            float y = ground * DmToMetres + target.CameraHeightMetres;
            _motor.Position = new Vector3(x * DmToMetres, y, z * DmToMetres);
            _motor.Velocity = Vector3.zero;
            ApplyCamera(target);
        }

        private void ApplyCamera(EvidenceTarget target)
        {
            if (_slice == null || _motor == null) return;
            _slice.transform.position = _motor.EyePosition;
            int focusGround = TerrainSampler.HeightAt(target.FocusDm.X, target.FocusDm.Y, Seed);
            Vector3 focus = new Vector3(
                target.FocusDm.X * DmToMetres,
                focusGround * DmToMetres + (target.Elevated ? 8f : 5f),
                target.FocusDm.Y * DmToMetres);
            Vector3 direction = focus - _slice.transform.position;
            if (direction.sqrMagnitude > 0.01f)
                _slice.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void CaptureTarget(EvidenceTarget target) => CaptureNamed("macro-" + target.Label);

        private void CaptureNamed(string name)
        {
            if (string.IsNullOrWhiteSpace(_screenshotDirectory)) return;
            Directory.CreateDirectory(_screenshotDirectory);
            string path = Path.Combine(_screenshotDirectory, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("MACROEVIDENCE screenshot=" + path);
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

        private static string Format(Vector3 value) =>
            $"({value.x:0.0},{value.y:0.0},{value.z:0.0})";

        private readonly struct EvidenceTarget
        {
            public string Label { get; }
            public Int2 CameraDm { get; }
            public Int2 FocusDm { get; }
            public float CameraHeightMetres { get; }
            public bool Elevated { get; }

            public EvidenceTarget(
                string label,
                Int2 cameraDm,
                Int2 focusDm,
                float cameraHeightMetres,
                bool elevated)
            {
                Label = label;
                CameraDm = cameraDm;
                FocusDm = focusDm;
                CameraHeightMetres = cameraHeightMetres;
                Elevated = elevated;
            }
        }
    }
}
