using UnityEngine;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace Game.WorldBuilder.Validation
{
    /// <summary>
    /// Validation-only camera evidence for the natural SecretDiscovery approach. The production
    /// validation component still owns app/session lifecycle, world authoring, clue realization,
    /// interaction, destruction, and later evidence poses. This late camera pass only keeps the
    /// first two capture intervals on loaded surface terrain and looks outward along the authored
    /// vegetation discontinuity so the clue can be judged without exposing the cave cutaway.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    internal sealed class SecretDiscoveryEvidenceCamera : MonoBehaviour
    {
        private const float VoxelMetres = 0.1f;
        private const uint Seed = 0x53454352u;
        private const int CaveAnchorX = -1024;
        private const int CaveAnchorZ = 512;
        private const float ExteriorEvidenceSeconds = 8.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            WorldBuilderSecretDiscoveryValidation owner =
                Object.FindObjectOfType<WorldBuilderSecretDiscoveryValidation>();
            if (owner == null || owner.GetComponent<SecretDiscoveryEvidenceCamera>() != null)
                return;

            owner.gameObject.AddComponent<SecretDiscoveryEvidenceCamera>();
        }

        private void LateUpdate()
        {
            if (Time.timeSinceLevelLoad >= ExteriorEvidenceSeconds)
            {
                enabled = false;
                return;
            }

            // Stay immediately outside the authored mouth, where the validation world is already
            // preloaded, and look away from the opening through the natural negative-space corridor.
            // This is a gameplay-height observation pose; it does not change world or clue state.
            int eyeX = CaveAnchorX;
            int eyeZ = CaveAnchorZ - 5;
            int eyeY = TerrainSampler.HeightAt(eyeX, eyeZ, Seed) + 20;

            int targetX = CaveAnchorX;
            int targetZ = CaveAnchorZ - 70;
            int targetY = TerrainSampler.HeightAt(targetX, targetZ, Seed) + 18;

            Vector3 eye = new Vector3(eyeX, eyeY, eyeZ) * VoxelMetres;
            Vector3 target = new Vector3(targetX, targetY, targetZ) * VoxelMetres;
            Vector3 direction = target - eye;
            if (direction.sqrMagnitude <= 0.001f)
                return;

            transform.SetPositionAndRotation(
                eye,
                Quaternion.LookRotation(direction.normalized, Vector3.up));
        }
    }
}
