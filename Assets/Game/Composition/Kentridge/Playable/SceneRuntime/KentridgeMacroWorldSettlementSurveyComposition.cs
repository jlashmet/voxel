using System;
using System.IO;
using System.Reflection;
using Game.Composition.Kentridge.Playable;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
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
    /// survey pose: close enough for readable authored shells/streets, oblique enough to show massing,
    /// and with CharacterMotor streaming authority pinned to the same presentation point.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    internal sealed class KentridgeMacroWorldSettlementSurveyComposition : MonoBehaviour
    {
        private const string ValidationProfile = "kentridge-macro-world";
        private const float CloseSurveyHeightMetres = 31f;
        private const float CloseSurveyHorizontalOffsetMetres = 22f;
        private const float CloseSurveyFocusHeightMetres = 5f;
        private const float MaximumSurveyFieldOfView = 60f;
        private const uint Seed = 0x4B454E54u;
        private const float DmToMetres = 0.1f;

        private static readonly FieldInfo s_WorldField = typeof(KentridgePlayableSlice).GetField(
            "_world",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_MotorField = typeof(KentridgePlayableSlice).GetField(
            "_motor",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_TargetsField = typeof(KentridgeMacroWorldEvidenceDriver).GetField(
            "_targets",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_TargetIndexField = typeof(KentridgeMacroWorldEvidenceDriver).GetField(
            "_targetIndex",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_TargetContentReadyLoggedField = typeof(KentridgeMacroWorldEvidenceDriver).GetField(
            "_targetContentReadyLogged",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private KentridgePlayableSlice _slice;
        private KentridgeMacroWorldEvidenceDriver _driver;
        private KentridgeCharacterHost _motor;
        private Camera _camera;
        private Type _targetType;
        private FieldInfo _targetLabelField;
        private FieldInfo _targetFocusDmField;
        private int _lastTargetIndex = -1;
        private bool _lastCloseSettlement;
        private Vector3 _closeSurveyPosition;
        private Vector3 _closeSurveyFocus;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForAssignedProfile()
        {
            if (!TryReadValidationProfile(out string profile)
                || !string.Equals(profile, ValidationProfile, StringComparison.Ordinal))
                return;

            var host = new GameObject("Kentridge Close Settlement Survey Composition");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<KentridgeMacroWorldSettlementSurveyComposition>();
        }

        private void LateUpdate()
        {
            _slice ??= FindFirstObjectByType<KentridgePlayableSlice>();
            _driver ??= FindFirstObjectByType<KentridgeMacroWorldEvidenceDriver>();
            if (_slice == null || _driver == null) return;

            if (s_MotorField == null || s_TargetsField == null || s_TargetIndexField == null
                || s_TargetContentReadyLoggedField == null)
                throw new InvalidOperationException(
                    "Close settlement survey composition cannot resolve macro evidence driver state.");

            _motor ??= s_MotorField.GetValue(_slice) as KentridgeCharacterHost;
            if (_motor == null || s_WorldField?.GetValue(_slice) == null) return;

            int targetIndex = (int)s_TargetIndexField.GetValue(_driver);
            Array targets = s_TargetsField.GetValue(_driver) as Array;
            if (targets == null || targetIndex < 0 || targetIndex >= targets.Length)
            {
                _lastTargetIndex = -1;
                _lastCloseSettlement = false;
                return;
            }

            object target = targets.GetValue(targetIndex);
            EnsureTargetFields(target);
            string label = _targetLabelField.GetValue(target) as string;
            bool closeSettlement = IsCloseSettlement(label);
            if (targetIndex != _lastTargetIndex)
            {
                _lastTargetIndex = targetIndex;
                _lastCloseSettlement = closeSettlement;
                if (closeSettlement)
                {
                    Int2 focusDm = (Int2)_targetFocusDmField.GetValue(target);
                    BuildCloseSurveyPose(focusDm, out _closeSurveyPosition, out _closeSurveyFocus);
                    Debug.Log(
                        $"MACROEVIDENCE close-survey target={label} position={Format(_closeSurveyPosition)} " +
                        $"focus={Format(_closeSurveyFocus)} heightM={CloseSurveyHeightMetres:0.0}");
                }
            }

            if (!_lastCloseSettlement) return;

            // Keep streaming authority and rendered presentation at one point. This preserves the
            // strict production coverage gate rather than widening it or treating missing chunks as
            // optional evidence.
            _motor.Position = _closeSurveyPosition;
            _motor.Velocity = Vector3.zero;
            _slice.transform.position = _closeSurveyPosition;
            _slice.transform.rotation = Quaternion.LookRotation(
                (_closeSurveyFocus - _closeSurveyPosition).normalized,
                Vector3.up);

            _camera ??= Camera.main;
            if (_camera != null && _camera.fieldOfView > MaximumSurveyFieldOfView)
                _camera.fieldOfView = MaximumSurveyFieldOfView;

            // The evidence driver's content-ready flag remains authoritative. Log only when its
            // production readiness gate has actually turned green at this close pose.
            if ((bool)s_TargetContentReadyLoggedField.GetValue(_driver))
                Debug.LogOncePerFrame(
                    $"MACROEVIDENCE close-survey-content-ready target={label} demand={Format(_motor.EyePosition)}");
        }

        private void EnsureTargetFields(object target)
        {
            if (target == null) throw new InvalidOperationException("Macro evidence target is null.");
            Type targetType = target.GetType();
            if (_targetType == targetType) return;
            _targetType = targetType;
            _targetLabelField = targetType.GetField(
                "Label",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _targetFocusDmField = targetType.GetField(
                "FocusDm",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_targetLabelField == null || _targetFocusDmField == null)
                throw new InvalidOperationException(
                    "Close settlement survey composition cannot resolve evidence target label/focus state.");
        }

        private static bool IsCloseSettlement(string label) =>
            string.Equals(label, MountingForceTopDownWorldDefinition.Moordell, StringComparison.Ordinal)
            || string.Equals(label, MountingForceTopDownWorldDefinition.Rossdam, StringComparison.Ordinal)
            || string.Equals(label, MountingForceTopDownWorldDefinition.FairyVillage, StringComparison.Ordinal)
            || string.Equals(label, MountingForceTopDownWorldDefinition.OrcVillage, StringComparison.Ordinal);

        private static void BuildCloseSurveyPose(Int2 focusDm, out Vector3 position, out Vector3 focus)
        {
            float focusX = focusDm.X * DmToMetres;
            float focusZ = focusDm.Y * DmToMetres;
            float groundY = TerrainSampler.HeightAt(focusDm.X, focusDm.Y, Seed) * DmToMetres;
            focus = new Vector3(focusX, groundY + CloseSurveyFocusHeightMetres, focusZ);

            // The diagonal offset keeps streets and front/side wall planes legible while avoiding
            // the near-nadir evidence that previously reduced settlements to indistinct roof pixels.
            float diagonal = CloseSurveyHorizontalOffsetMetres * 0.70710678f;
            position = new Vector3(
                focusX - diagonal,
                groundY + CloseSurveyHeightMetres,
                focusZ - diagonal);
        }

        private static bool TryReadValidationProfile(out string profile)
        {
            profile = ReadArgument("-voxel-validation-profile");
            return !string.IsNullOrWhiteSpace(profile);
        }

        private static string ReadArgument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], key, StringComparison.Ordinal)) return args[i + 1];
            return string.Empty;
        }

        private static string Format(Vector3 value) =>
            $"({value.x:0.0},{value.y:0.0},{value.z:0.0})";
    }

    internal static class KentridgeMacroWorldSettlementSurveyCompositionLogExtensions
    {
        private static int s_LastFrame = -1;
        private static string s_LastMessage;

        internal static void LogOncePerFrame(this object _, string message)
        {
            int frame = Time.frameCount;
            if (frame == s_LastFrame && string.Equals(message, s_LastMessage, StringComparison.Ordinal)) return;
            s_LastFrame = frame;
            s_LastMessage = message;
            Debug.Log(message);
        }
    }
}