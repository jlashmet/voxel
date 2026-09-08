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
        private PropertyInfo _targetLabelProperty;
        private PropertyInfo _targetFocusDmProperty;
        private int _lastTargetIndex = -1;
        private bool _lastCloseSettlement;
        private string _activeLabel;
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

        private void Update()
        {
            if (!ResolveActiveCloseSettlement()) return;

            // EvidenceDriver runs at -100 and pins its generic survey demand first. This component
            // runs at -90, still before the production KentridgePlayableSlice Update, so the real
            // streaming step consumes the same close position that will be rendered in LateUpdate.
            // Without this Update-stage override, the prior LateUpdate-only implementation rendered
            // a close camera while streaming the old 70 m near-nadir point one frame after another.
            PinStreamingAuthority();
        }

        private void LateUpdate()
        {
            if (!ResolveActiveCloseSettlement()) return;

            PinStreamingAuthority();
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
                this.LogOncePerFrame(
                    $"MACROEVIDENCE close-survey-content-ready target={_activeLabel} demand={Format(_motor.EyePosition)}");
        }

        private bool ResolveActiveCloseSettlement()
        {
            _slice ??= FindFirstObjectByType<KentridgePlayableSlice>();
            _driver ??= FindActiveEvidenceDriverForValidation();
            if (_slice == null || _driver == null) return false;

            if (s_MotorField == null || s_TargetsField == null || s_TargetIndexField == null
                || s_TargetContentReadyLoggedField == null)
                throw new InvalidOperationException(
                    "Close settlement survey composition cannot resolve macro evidence driver state.");

            _motor ??= s_MotorField.GetValue(_slice) as KentridgeCharacterHost;
            if (_motor == null || s_WorldField?.GetValue(_slice) == null) return false;

            int targetIndex = (int)s_TargetIndexField.GetValue(_driver);
            Array targets = s_TargetsField.GetValue(_driver) as Array;
            if (targets == null || targetIndex < 0 || targetIndex >= targets.Length)
            {
                _lastTargetIndex = -1;
                _lastCloseSettlement = false;
                _activeLabel = null;
                return false;
            }

            object target = targets.GetValue(targetIndex);
            EnsureTargetProperties(target);
            string label = _targetLabelProperty.GetValue(target) as string;
            bool closeSettlement = IsCloseSettlement(label);
            if (targetIndex != _lastTargetIndex)
            {
                _lastTargetIndex = targetIndex;
                _lastCloseSettlement = closeSettlement;
                _activeLabel = closeSettlement ? label : null;
                if (closeSettlement)
                {
                    Int2 focusDm = (Int2)_targetFocusDmProperty.GetValue(target);
                    BuildCloseSurveyPose(focusDm, out _closeSurveyPosition, out _closeSurveyFocus);
                    Debug.Log(
                        $"MACROEVIDENCE close-survey target={label} position={Format(_closeSurveyPosition)} " +
                        $"focus={Format(_closeSurveyFocus)} heightM={CloseSurveyHeightMetres:0.0}");
                }
            }

            return _lastCloseSettlement;
        }

        private void PinStreamingAuthority()
        {
            _motor.Position = _closeSurveyPosition;
            _motor.Velocity = Vector3.zero;
        }

        private void EnsureTargetProperties(object target)
        {
            if (target == null) throw new InvalidOperationException("Macro evidence target is null.");
            Type targetType = target.GetType();
            if (_targetType == targetType) return;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo labelProperty = targetType.GetProperty("Label", flags);
            PropertyInfo focusDmProperty = targetType.GetProperty("FocusDm", flags);
            if (labelProperty == null
                || labelProperty.PropertyType != typeof(string)
                || focusDmProperty == null
                || focusDmProperty.PropertyType != typeof(Int2))
                throw new InvalidOperationException(
                    "Close settlement survey composition cannot resolve evidence target Label/FocusDm properties.");

            // Commit the cache only after both members have been validated. This prevents a failed
            // metadata probe from caching the type alongside null accessors and turning the next
            // frame into an unrelated NullReferenceException.
            _targetLabelProperty = labelProperty;
            _targetFocusDmProperty = focusDmProperty;
            _targetType = targetType;
        }

        internal static KentridgeMacroWorldEvidenceDriver FindActiveEvidenceDriverForValidation()
        {
            KentridgeMacroWorldEvidenceDriver[] drivers =
                Resources.FindObjectsOfTypeAll<KentridgeMacroWorldEvidenceDriver>();
            for (var i = 0; i < drivers.Length; i++)
                if (drivers[i] != null && drivers[i].isActiveAndEnabled)
                    return drivers[i];
            return null;
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
            if (!string.IsNullOrWhiteSpace(profile)) return true;

            string sceneIssuePath = ReadArgument("-voxel-scene-issue");
            if (string.IsNullOrWhiteSpace(sceneIssuePath) || !File.Exists(sceneIssuePath))
                return false;

            string json = File.ReadAllText(sceneIssuePath);
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
