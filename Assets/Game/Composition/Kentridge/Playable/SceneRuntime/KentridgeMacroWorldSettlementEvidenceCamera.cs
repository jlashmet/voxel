using System;
using System.IO;
using System.Reflection;
using Game.Composition.Kentridge.Playable;
using MountingForce.WorldGen;
using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Validation-only final camera correction for generic macro settlements.
    ///
    /// The macro evidence driver intentionally keeps CharacterMotor on the real ground focus so
    /// production streaming demand is authoritative. Rolling terrain can still hide one or more
    /// blockout plots from the driver's diagonal survey camera. This later execution-order pass
    /// leaves motor/world demand untouched and steepens only the four generic-settlement evidence
    /// views so all generated plots can be inspected in the built player.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    internal sealed class KentridgeMacroWorldSettlementEvidenceCamera : MonoBehaviour
    {
        private const string ValidationProfile = "kentridge-macro-world";
        private const float CameraHeightMetres = 70f;
        private static readonly Vector3 CameraHorizontalOffset = new(6f, 0f, 6f);

        private static readonly FieldInfo s_TargetsField = typeof(KentridgeMacroWorldEvidenceDriver).GetField(
            "_targets", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_TargetIndexField = typeof(KentridgeMacroWorldEvidenceDriver).GetField(
            "_targetIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_RoadArrivalPendingField = typeof(KentridgeMacroWorldEvidenceDriver).GetField(
            "_moordellRoadArrivalPending", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_MotorField = typeof(KentridgeMacroWorldEvidenceDriver).GetField(
            "_motor", BindingFlags.Instance | BindingFlags.NonPublic);

        private KentridgeMacroWorldEvidenceDriver _driver;
        private KentridgePlayableSlice _slice;
        private PropertyInfo _targetLabelProperty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForAssignedProfile()
        {
            if (!TryReadValidationProfile(out string profile)
                || !string.Equals(profile, ValidationProfile, StringComparison.Ordinal))
                return;

            var host = new GameObject("Kentridge Macro Settlement Evidence Camera");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<KentridgeMacroWorldSettlementEvidenceCamera>();
        }

        private void LateUpdate()
        {
            _driver ??= FindFirstObjectByType<KentridgeMacroWorldEvidenceDriver>();
            _slice ??= FindFirstObjectByType<KentridgePlayableSlice>();
            if (_driver == null || _slice == null) return;
            if (s_TargetsField == null || s_TargetIndexField == null
                || s_RoadArrivalPendingField == null || s_MotorField == null)
                throw new InvalidOperationException(
                    "Macro settlement evidence camera cannot resolve evidence-driver state.");

            if ((bool)s_RoadArrivalPendingField.GetValue(_driver)) return;
            int index = (int)s_TargetIndexField.GetValue(_driver);
            if (index < 0) return;

            if (s_TargetsField.GetValue(_driver) is not Array targets || index >= targets.Length)
                return;
            object target = targets.GetValue(index);
            if (target == null) return;

            _targetLabelProperty ??= target.GetType().GetProperty(
                "Label", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_targetLabelProperty == null)
                throw new InvalidOperationException(
                    "Macro settlement evidence camera cannot resolve target label state.");
            string label = _targetLabelProperty.GetValue(target) as string;
            if (!IsGenericSettlement(label)) return;

            if (s_MotorField.GetValue(_driver) is not KentridgeCharacterHost motor) return;
            Vector3 focus = motor.Position + Vector3.up * 5f;
            Vector3 camera = motor.Position + CameraHorizontalOffset + Vector3.up * CameraHeightMetres;
            _slice.transform.position = camera;
            Vector3 direction = focus - camera;
            if (direction.sqrMagnitude > 0.01f)
                _slice.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private static bool IsGenericSettlement(string label) =>
            string.Equals(label, MountingForceTopDownWorldDefinition.Moordell, StringComparison.Ordinal)
            || string.Equals(label, MountingForceTopDownWorldDefinition.Rossdam, StringComparison.Ordinal)
            || string.Equals(label, MountingForceTopDownWorldDefinition.FairyVillage, StringComparison.Ordinal)
            || string.Equals(label, MountingForceTopDownWorldDefinition.OrcVillage, StringComparison.Ordinal);

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
