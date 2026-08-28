using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Stabilizes the full VoxelShowcase command-line walking benchmark without changing normal
    /// player input or the production movement/streaming path.
    ///
    /// The real-player harness waits for the world to settle before enabling AutoWalk. During that
    /// wait the visible player can receive real mouse deltas, so VoxelShowcase's private yaw used
    /// to depend on incidental window focus/input and nominally identical CI runs followed
    /// different circles. That made the performance gate compare different chunk loads.
    ///
    /// This component is installed only when -voxel-autowalk-after is present and a VoxelShowcase
    /// exists. It arms shortly before AutoWalk, disables interactive mouse-look for the automated
    /// phase, and derives the clockwise tangent from player/landmark world geometry every frame.
    /// VoxelShowcase.StepAutoWalk still supplies its ordinary 24-degree/second turn and forward
    /// movement still goes through CharacterMotor and normal streaming; the small pre-compensation
    /// below makes the heading after StepAutoWalk land exactly on the deterministic tangent.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    internal sealed class DeterministicAutoWalkHeadingHarness : MonoBehaviour
    {
        private const float ExistingAutoWalkDegreesPerSecond = 24f;
        private const float ArmLeadSeconds = 0.25f;

        private static readonly FieldInfo YawField = typeof(VoxelShowcase).GetField(
            "_yaw", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MouseLookField = typeof(VoxelShowcase).GetField(
            "_mouseLook", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo LandmarkWorldPositionMethod = typeof(VoxelShowcase).GetMethod(
            "LandmarkWorldPosition", BindingFlags.Instance | BindingFlags.NonPublic);

        private VoxelShowcase _showcase;
        private float _armAfterSeconds;
        private float _elapsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!TryCommandLineValue("-voxel-autowalk-after", out float autoWalkAfter)
                || autoWalkAfter <= 0f)
                return;

            VoxelShowcase showcase = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>(
                FindObjectsInactive.Include);
            if (showcase == null) return;

            if (YawField == null || MouseLookField == null || LandmarkWorldPositionMethod == null)
            {
                Debug.LogError("HARNESS deterministic autowalk could not bind VoxelShowcase heading state");
                return;
            }

            var root = new GameObject("Deterministic AutoWalk Heading Harness")
            {
                hideFlags = HideFlags.DontSave
            };
            var harness = root.AddComponent<DeterministicAutoWalkHeadingHarness>();
            harness._showcase = showcase;
            harness._armAfterSeconds = Mathf.Max(0f, autoWalkAfter - ArmLeadSeconds);
            UnityEngine.Object.DontDestroyOnLoad(root);
            Debug.Log($"HARNESS deterministic autowalk heading arms at {harness._armAfterSeconds:0.00}s");
        }

        private void Update()
        {
            if (_showcase == null)
            {
                Destroy(gameObject);
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < _armAfterSeconds) return;

            Vector3 landmark = (Vector3)LandmarkWorldPositionMethod.Invoke(_showcase, null);
            Vector3 radial = _showcase.transform.position - landmark;
            radial.y = 0f;
            if (radial.sqrMagnitude < 1e-4f) radial = Vector3.back;

            // With +Z as north and +X as east, up x radial is the clockwise tangent.
            Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;
            float tangentYaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;

            // This Update runs before VoxelShowcase.Update. StepAutoWalk adds 24 deg/s, so
            // subtract that exact per-frame amount here to make the resulting movement tangent.
            YawField.SetValue(
                _showcase,
                tangentYaw - ExistingAutoWalkDegreesPerSecond * Time.deltaTime);
            MouseLookField.SetValue(_showcase, false);
        }

        private static bool TryCommandLineValue(string name, out float value)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], name, StringComparison.Ordinal)) continue;
                return float.TryParse(
                    args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            }

            value = 0f;
            return false;
        }
    }
}
