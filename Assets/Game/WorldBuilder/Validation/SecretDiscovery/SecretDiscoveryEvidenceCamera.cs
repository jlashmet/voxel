using UnityEngine;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace Game.WorldBuilder.Validation
{
    /// <summary>
    /// Validation-only evidence orchestration for the natural SecretDiscovery approach. The production
    /// validation component still owns app/session lifecycle, world authoring, clue realization,
    /// interaction, destruction, and later evidence poses. This late pass keeps the first two capture
    /// intervals on loaded surface terrain, then stops validation commands only after the final evidence
    /// capture so the standalone replay cannot stream indefinitely after acceptance has been proven.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    internal sealed class SecretDiscoveryEvidenceCamera : MonoBehaviour
    {
        private const float VoxelMetres = 0.1f;
        private const uint Seed = 0x53454352u;
        private const int CaveAnchorX = -1024;
        private const int CaveAnchorZ = 512;
        private const float ExteriorEvidenceSeconds = 8.5f;
        private const float EvidenceCompleteSeconds = 22.5f;

        private WorldBuilderSecretDiscoveryValidation _owner;
        private float _elapsedSeconds;
        private bool _commandsStopped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            WorldBuilderSecretDiscoveryValidation owner =
                Object.FindObjectOfType<WorldBuilderSecretDiscoveryValidation>();
            if (owner == null || owner.GetComponent<SecretDiscoveryEvidenceCamera>() != null)
                return;

            SecretDiscoveryEvidenceCamera evidence =
                owner.gameObject.AddComponent<SecretDiscoveryEvidenceCamera>();
            evidence._owner = owner;
        }

        private void Awake()
        {
            if (_owner == null)
                _owner = GetComponent<WorldBuilderSecretDiscoveryValidation>();
        }

        private void LateUpdate()
        {
            _elapsedSeconds += Mathf.Max(0f, Time.deltaTime);

            if (_elapsedSeconds < ExteriorEvidenceSeconds)
                PlaceNaturalApproachEvidence();

            if (_commandsStopped || _elapsedSeconds < EvidenceCompleteSeconds || _owner == null)
                return;

            // The final required module-local frame is captured at 21 seconds. Stop only the
            // validation command/update loop after that point, leaving the realized world visible
            // for any longer SceneIssue harness replay and preserving normal shutdown ownership.
            _owner.StopCommands();
            _commandsStopped = true;
            Debug.Log("WorldBuilder secret validation evidence complete: commands stopped after final capture.");
        }

        private void PlaceNaturalApproachEvidence()
        {
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
