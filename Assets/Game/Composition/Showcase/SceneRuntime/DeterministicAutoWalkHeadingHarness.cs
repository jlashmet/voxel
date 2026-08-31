using System;
using System.Globalization;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Stabilizes the full VoxelShowcase command-line walking benchmark without changing normal
    /// player input or the production movement/streaming path.
    ///
    /// The real-player harness waits for the world to settle before enabling AutoWalk. During that
    /// wait the visible player can receive real mouse deltas, so nominally identical CI runs used
    /// to follow different circles and compare different chunk loads.
    ///
    /// This component is installed only when -voxel-autowalk-after is present and a VoxelShowcase
    /// exists. It arms shortly before AutoWalk, then derives the clockwise tangent from the public
    /// landmark/player geometry every frame and supplies that semantic heading to VoxelShowcase.
    /// VoxelShowcase retains ownership of its ordinary AutoWalk turn policy, CharacterMotor step,
    /// collision, and streaming.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    internal sealed class DeterministicAutoWalkHeadingHarness : MonoBehaviour
    {
        private const float ArmLeadSeconds = 0.25f;

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

            Vector3 landmark = _showcase.LandmarkPositionMetres;
            Vector3 radial = _showcase.transform.position - landmark;
            radial.y = 0f;
            if (radial.sqrMagnitude < 1e-4f) radial = Vector3.back;

            // With +Z as north and +X as east, up x radial is the clockwise tangent.
            Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;
            float tangentYaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
            _showcase.SetAutomatedHeading(tangentYaw);
        }

        private void OnDisable()
        {
            _showcase?.ClearAutomatedHeading();
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
