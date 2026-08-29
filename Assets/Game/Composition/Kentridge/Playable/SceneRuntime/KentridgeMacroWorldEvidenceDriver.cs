using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Composition.Kentridge.Playable;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using UnityEngine;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Built-player evidence automation for capture-less macro-world validations. It is dormant in
    /// normal gameplay. When an assigned SceneIssue selects the reusable kentridge-macro-world
    /// validation profile, it first lets the production CharacterMotor walk normally, then moves
    /// that same motor/camera to semantic physical-plan targets so ordinary streaming/rendering can
    /// produce durable close and survey frames for remote macro content.
    /// </summary>
    internal sealed class KentridgeMacroWorldEvidenceDriver : MonoBehaviour
    {
        private const string ValidationProfile = "kentridge-macro-world";
        private const float WalkEvidenceSeconds = 4f;
        private const float TargetSeconds = 4f;
        private const float CaptureAfterSeconds = 2.4f;
        private const float DmToMetres = 0.1f;

        private static readonly FieldInfo s_MotorField = typeof(KentridgePlayableSlice).GetField(
            "_motor",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private KentridgePlayableSlice _slice;
        private KentridgeCharacterHost _motor;
        private EvidenceTarget[] _targets;
        private string _screenshotDirectory;
        private float _gameplayStartedAt = -1f;
        private Vector3 _walkStartedAt;
        private bool _walkRecorded;
        private int _targetIndex = -1;
        private float _targetStartedAt;
        private bool _targetCaptured;

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

        private void Update()
        {
            if (_slice == null)
            {
                _slice = FindFirstObjectByType<KentridgePlayableSlice>();
                if (_slice == null) return;
                if (s_MotorField == null)
                    throw new InvalidOperationException("Macro evidence driver cannot resolve Kentridge CharacterMotor host.");
                _screenshotDirectory = ReadArgument("-voxel-screenshot-dir");
            }

            _motor ??= s_MotorField.GetValue(_slice) as KentridgeCharacterHost;
            if (_motor == null || !_slice.GameplayControlEnabled) return;

            if (_gameplayStartedAt < 0f)
            {
                _gameplayStartedAt = Time.realtimeSinceStartup;
                _walkStartedAt = _motor.Position;
                _slice.AutoSurvey = false;
                _slice.AutoRecede = false;
                _slice.AutoWalk = true;
                Debug.Log("MACROEVIDENCE phase=character-motor-walk start=" + Format(_walkStartedAt));
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
                Debug.Log($"MACROEVIDENCE traversal=CharacterMotor metres={delta.magnitude:0.00} " +
                          $"from={Format(_walkStartedAt)} to={Format(_motor.Position)}");
                BuildTargets();
            }

            _slice.AutoWalk = false;
            _slice.AutoSurvey = false;
            _slice.AutoRecede = false;

            if (_targets == null || _targets.Length == 0) return;
            int requested = Mathf.Min(
                _targets.Length - 1,
                Mathf.FloorToInt((elapsed - WalkEvidenceSeconds) / TargetSeconds));
            if (requested != _targetIndex)
            {
                _targetIndex = requested;
                _targetStartedAt = Time.realtimeSinceStartup;
                _targetCaptured = false;
                Debug.Log("MACROEVIDENCE target=" + _targets[_targetIndex].Label +
                          " focusDm=" + _targets[_targetIndex].FocusDm);
            }

            PinToTarget(_targets[_targetIndex]);
            if (!_targetCaptured
                && Time.realtimeSinceStartup - _targetStartedAt >= CaptureAfterSeconds)
            {
                _targetCaptured = true;
                CaptureTarget(_targets[_targetIndex]);
            }
        }

        private void LateUpdate()
        {
            if (_targetIndex < 0 || _targets == null || _targetIndex >= _targets.Length || _motor == null)
                return;
            ApplyCamera(_targets[_targetIndex]);
        }

        private void BuildTargets()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(0x4B454E54u);
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalPlanner.Plan(
                layout,
                KentridgeTopDownWorldPhysicalIntent.Build(),
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                voxelsPerDecimetre: 1);

            var targets = new List<EvidenceTarget>(7)
            {
                CloseSettlement(physical, MountingForceTopDownWorldDefinition.Moordell, new Int2(-320, -360)),
                CloseSettlement(physical, MountingForceTopDownWorldDefinition.Rossdam, new Int2(340, -340)),
                CloseSettlement(physical, MountingForceTopDownWorldDefinition.FairyVillage, new Int2(-320, 320)),
                CloseSettlement(physical, MountingForceTopDownWorldDefinition.OrcVillage, new Int2(320, 320))
            };

            if (!physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.RossdamLake, out TopDownWorldRegionPlan lake))
                throw new InvalidOperationException("Macro evidence plan has no Rossdam lake.");
            TopDownWorldPhysicalRoutePlan rossdamRoute = FindRoute(
                physical,
                MountingForceTopDownWorldDefinition.MoordellCorridor,
                MountingForceTopDownWorldDefinition.RossdamApproach);
            targets.Add(new EvidenceTarget(
                "rossdam-lake-detour",
                rossdamRoute.Tiles[rossdamRoute.Tiles.Count / 2],
                lake.CentreDm,
                cameraHeightMetres: 18f,
                elevated: true));

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
                new Int2(ridgeRoad.X - 240, ridgeRoad.Y - 220),
                ridgeRoad,
                cameraHeightMetres: 20f,
                elevated: true));

            targets.Add(new EvidenceTarget(
                "macro-network-overview",
                new Int2(KentridgeDefinition.TownCentreDm.X, KentridgeDefinition.TownCentreDm.Y + 1200),
                new Int2(KentridgeDefinition.TownCentreDm.X, KentridgeDefinition.TownCentreDm.Y + 800),
                cameraHeightMetres: 180f,
                elevated: true));
            _targets = targets.ToArray();
        }

        private static EvidenceTarget CloseSettlement(
            TopDownWorldPhysicalPlan physical,
            string nodeId,
            Int2 viewOffsetDm)
        {
            if (!physical.TryGetSettlement(nodeId, out TopDownWorldSettlementPlan settlement))
                throw new InvalidOperationException("Macro evidence plan has no settlement '" + nodeId + "'.");
            return new EvidenceTarget(
                nodeId,
                new Int2(
                    settlement.CentreDm.X + viewOffsetDm.X,
                    settlement.CentreDm.Y + viewOffsetDm.Y),
                settlement.CentreDm,
                cameraHeightMetres: 1.2f,
                elevated: false);
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
            int ground = TerrainSampler.HeightAt(x, z, 0x4B454E54u);
            float y = ground * DmToMetres + target.CameraHeightMetres;
            _motor.Position = new Vector3(x * DmToMetres, y, z * DmToMetres);
            _motor.Velocity = Vector3.zero;
            ApplyCamera(target);
        }

        private void ApplyCamera(EvidenceTarget target)
        {
            transform.position = _motor.EyePosition;
            int focusGround = TerrainSampler.HeightAt(target.FocusDm.X, target.FocusDm.Y, 0x4B454E54u);
            Vector3 focus = new Vector3(
                target.FocusDm.X * DmToMetres,
                focusGround * DmToMetres + (target.Elevated ? 8f : 5f),
                target.FocusDm.Y * DmToMetres);
            Vector3 direction = focus - transform.position;
            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void CaptureTarget(EvidenceTarget target)
        {
            if (string.IsNullOrWhiteSpace(_screenshotDirectory)) return;
            Directory.CreateDirectory(_screenshotDirectory);
            string path = Path.Combine(_screenshotDirectory, "macro-" + target.Label + ".png");
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
