using System;
using System.IO;
using System.Reflection;
using Game.Composition.Kentridge.Playable;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using UnityEngine;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Validation-only composition for readable close settlement evidence. The production evidence
    /// driver still owns semantic target selection, content-settlement checks, strict renderer
    /// coverage, capture timing, and road traversal. This component only composes the settlement
    /// survey target into a genuinely close oblique view and keeps the CharacterMotor streaming
    /// authority at that same camera point. Production world generation, residency radius, LOD
    /// bands, renderer budgets, and normal gameplay cameras are unchanged.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    internal sealed class KentridgeMacroWorldSettlementSurveyComposition : MonoBehaviour
    {
        private const string ValidationProfile = "kentridge-macro-world";
        private const uint Seed = 0x4B454E54u;
        private const float DmToMetres = 0.1f;
        private const int CloseSurveyHorizontalOffsetDm = 260;
        private const float CloseSurveyHeightMetres = 31f;
        private const float CloseSurveyFocusHeightMetres = 8f;
        private const float MaximumReadableSettlementFieldOfView = 60f;

        private static readonly FieldInfo s_TargetIndexField =
            typeof(KentridgeMacroWorldEvidenceDriver).GetField(
                "_targetIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_TargetsField =
            typeof(KentridgeMacroWorldEvidenceDriver).GetField(
                "_targets", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_TargetCapturedField =
            typeof(KentridgeMacroWorldEvidenceDriver).GetField(
                "_targetCaptured", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_MotorField = typeof(KentridgePlayableSlice).GetField(
            "_motor", BindingFlags.Instance | BindingFlags.NonPublic);

        private KentridgeMacroWorldEvidenceDriver _driver;
        private KentridgePlayableSlice _slice;
        private Camera _camera;
        private float _normalFieldOfView;
        private bool _fieldOfViewOverridden;
        private bool _closeSurveyActive;
        private Int2 _focusDm;
        private Int2 _cameraDm;
        private int _activeTargetIndex = -1;
        private int _loggedTargetIndex = -1;
        private PropertyInfo _labelProperty;
        private PropertyInfo _focusProperty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForAssignedProfile()
        {
            if (!TryReadValidationProfile(out string profile)
                || !string.Equals(profile, ValidationProfile, StringComparison.Ordinal))
                return;

            var host = new GameObject("Kentridge Macro Settlement Survey Composition");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<KentridgeMacroWorldSettlementSurveyComposition>();
        }

        private void OnDisable() => RestoreNormalFieldOfView();

        private void OnDestroy() => RestoreNormalFieldOfView();

        private void Update()
        {
            _driver ??= FindFirstObjectByType<KentridgeMacroWorldEvidenceDriver>();
            _slice ??= FindFirstObjectByType<KentridgePlayableSlice>();
            if (_driver == null || _slice == null || !TryResolveCloseSettlementTarget(out int targetIndex))
            {
                _closeSurveyActive = false;
                _activeTargetIndex = -1;
                return;
            }

            _closeSurveyActive = true;
            _activeTargetIndex = targetIndex;
            _cameraDm = new Int2(
                _focusDm.X + CloseSurveyHorizontalOffsetDm,
                _focusDm.Y + CloseSurveyHorizontalOffsetDm);

            KentridgeCharacterHost motor = s_MotorField?.GetValue(_slice) as KentridgeCharacterHost;
            if (motor != null)
            {
                motor.Position = ResolveSurveyCameraPosition(_cameraDm)
                               - Vector3.up * motor.EyeHeight;
                motor.Velocity = Vector3.zero;
            }
        }

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (!_closeSurveyActive || camera == null)
            {
                RestoreNormalFieldOfView();
                return;
            }

            if (_camera != camera)
            {
                RestoreNormalFieldOfView();
                _camera = camera;
                _normalFieldOfView = camera.fieldOfView;
            }

            if (!_fieldOfViewOverridden)
            {
                _normalFieldOfView = camera.fieldOfView;
                _fieldOfViewOverridden = true;
            }
            camera.fieldOfView = ResolveReadableSurveyFieldOfView(_normalFieldOfView);

            Vector3 position = ResolveSurveyCameraPosition(_cameraDm);
            int focusGround = TerrainSampler.HeightAt(_focusDm.X, _focusDm.Y, Seed);
            Vector3 focus = new Vector3(
                _focusDm.X * DmToMetres,
                focusGround * DmToMetres + CloseSurveyFocusHeightMetres,
                _focusDm.Y * DmToMetres);
            Vector3 direction = focus - position;
            camera.transform.position = position;
            if (direction.sqrMagnitude > 0.01f)
                camera.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            if (_loggedTargetIndex != _activeTargetIndex)
            {
                _loggedTargetIndex = _activeTargetIndex;
                Debug.Log(
                    $"MACROEVIDENCE close-survey-composition targetIndex={_activeTargetIndex} " +
                    $"cameraHeightM={CloseSurveyHeightMetres:0.0} " +
                    $"horizontalOffsetDm={CloseSurveyHorizontalOffsetDm} " +
                    $"fov={camera.fieldOfView:0.0}");
            }
        }

        private bool TryResolveCloseSettlementTarget(out int targetIndex)
        {
            targetIndex = -1;
            if (s_TargetIndexField == null || s_TargetsField == null || s_TargetCapturedField == null)
                return false;
            if ((bool)s_TargetCapturedField.GetValue(_driver)) return false;

            targetIndex = (int)s_TargetIndexField.GetValue(_driver);
            if (targetIndex < 0) return false;
            Array targets = s_TargetsField.GetValue(_driver) as Array;
            if (targets == null || targetIndex >= targets.Length) return false;

            object target = targets.GetValue(targetIndex);
            if (target == null) return false;
            Type targetType = target.GetType();
            _labelProperty ??= targetType.GetProperty("Label", BindingFlags.Instance | BindingFlags.Public);
            _focusProperty ??= targetType.GetProperty("FocusDm", BindingFlags.Instance | BindingFlags.Public);
            if (_labelProperty == null || _focusProperty == null) return false;

            string label = _labelProperty.GetValue(target) as string;
            if (!IsCloseSettlement(label)) return false;
            _focusDm = (Int2)_focusProperty.GetValue(target);
            return true;
        }

        private static bool IsCloseSettlement(string label) =>
            string.Equals(label, MountingForceTopDownWorldDefinition.Moordell, StringComparison.Ordinal)
            || string.Equals(label, MountingForceTopDownWorldDefinition.Rossdam, StringComparison.Ordinal)
            || string.Equals(label, MountingForceTopDownWorldDefinition.FairyVillage, StringComparison.Ordinal)
            || string.Equals(label, MountingForceTopDownWorldDefinition.OrcVillage, StringComparison.Ordinal);

        private static Vector3 ResolveSurveyCameraPosition(Int2 cameraDm)
        {
            int cameraGround = TerrainSampler.HeightAt(cameraDm.X, cameraDm.Y, Seed);
            return new Vector3(
                cameraDm.X * DmToMetres,
                cameraGround * DmToMetres + CloseSurveyHeightMetres,
                cameraDm.Y * DmToMetres);
        }

        private void RestoreNormalFieldOfView()
        {
            if (!_fieldOfViewOverridden || _camera == null) return;
            _camera.fieldOfView = _normalFieldOfView;
            _fieldOfViewOverridden = false;
        }

        private static float ResolveReadableSurveyFieldOfView(float normalFieldOfView) =>
            Mathf.Min(normalFieldOfView, MaximumReadableSettlementFieldOfView);

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
    }
}
