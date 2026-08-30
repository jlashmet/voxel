using System;
using System.IO;
using System.Reflection;
using Game.Composition.Kentridge.Playable;
using MountingForce.WorldGen;
using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Validation-only final scheduling/camera correction for macro evidence.
    ///
    /// The macro evidence driver intentionally keeps CharacterMotor on the real ground focus so
    /// production streaming demand is authoritative. This later execution-order pass makes two
    /// evidence-only corrections without changing runtime world budgets: it visits Rossdam Lake
    /// before Rossdam settlement so shared lake content is already published when the settlement
    /// becomes current demand, and it steepens the four generic-settlement survey views so rolling
    /// terrain cannot hide generated blockout plots. Every target still uses the driver's real
    /// content-readiness and renderer-coverage gates before capture.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    internal sealed class KentridgeMacroWorldSettlementEvidenceCamera : MonoBehaviour
    {
        private const string ValidationProfile = "kentridge-macro-world";
        private const string RossdamLakeEvidenceLabel = "rossdam-lake-detour";
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
        private bool _targetOrderPrepared;

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
                    "Macro settlement evidence helper cannot resolve evidence-driver state.");

            int index = (int)s_TargetIndexField.GetValue(_driver);
            if (s_TargetsField.GetValue(_driver) is not Array targets || targets.Length == 0)
                return;

            PrepareTargetOrder(targets, index);

            if ((bool)s_RoadArrivalPendingField.GetValue(_driver)) return;
            if (index < 0 || index >= targets.Length) return;
            object target = targets.GetValue(index);
            if (target == null) return;

            string label = TargetLabel(target);
            if (!IsGenericSettlement(label)) return;

            if (s_MotorField.GetValue(_driver) is not KentridgeCharacterHost motor) return;
            Vector3 focus = motor.Position + Vector3.up * 5f;
            Vector3 camera = motor.Position + CameraHorizontalOffset + Vector3.up * CameraHeightMetres;
            _slice.transform.position = camera;
            Vector3 direction = focus - camera;
            if (direction.sqrMagnitude > 0.01f)
                _slice.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void PrepareTargetOrder(Array targets, int currentIndex)
        {
            if (_targetOrderPrepared || currentIndex > 0) return;

            int rossdamIndex = -1;
            int lakeIndex = -1;
            for (var i = 0; i < targets.Length; i++)
            {
                object target = targets.GetValue(i);
                if (target == null) continue;
                string label = TargetLabel(target);
                if (string.Equals(label, MountingForceTopDownWorldDefinition.Rossdam, StringComparison.Ordinal))
                    rossdamIndex = i;
                else if (string.Equals(label, RossdamLakeEvidenceLabel, StringComparison.Ordinal))
                    lakeIndex = i;
            }

            if (rossdamIndex < 0 || lakeIndex < 0)
                throw new InvalidOperationException(
                    "Macro evidence ordering requires both Rossdam settlement and Rossdam lake targets.");

            if (lakeIndex > rossdamIndex)
            {
                object rossdam = targets.GetValue(rossdamIndex);
                object lake = targets.GetValue(lakeIndex);
                targets.SetValue(lake, rossdamIndex);
                targets.SetValue(rossdam, lakeIndex);
                Debug.Log(
                    $"MACROEVIDENCE target-order=lake-before-rossdam lakeIndex={rossdamIndex} rossdamIndex={lakeIndex}");
            }

            _targetOrderPrepared = true;
        }

        private string TargetLabel(object target)
        {
            _targetLabelProperty ??= target.GetType().GetProperty(
                "Label", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_targetLabelProperty == null)
                throw new InvalidOperationException(
                    "Macro settlement evidence helper cannot resolve target label state.");
            return _targetLabelProperty.GetValue(target) as string;
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
