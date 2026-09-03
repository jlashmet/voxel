using UnityEngine;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Rendering.Runtime.Vegetation
{
    /// <summary>
    /// Repairs the presentation/subscription boundary when a persistent Unity process resets
    /// vegetation statics while the DontDestroyOnLoad tree renderer survives. Normal event-driven
    /// damage stays unchanged; this only intervenes when semantic damage advanced but the affected
    /// tree is still incorrectly batch-only on the following frame.
    /// </summary>
    internal sealed class ProceduralTreeRendererLifecycleRecovery : MonoBehaviour
    {
        private int _observedVersion = int.MinValue;
        private int _observedDamageVersion = int.MinValue;
        private int _verifyAfterFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<ProceduralTreeRendererLifecycleRecovery>(FindObjectsInactive.Include) != null)
                return;

            var go = new GameObject("Procedural Tree Renderer Lifecycle Recovery")
            {
                hideFlags = HideFlags.DontSave,
            };
            DontDestroyOnLoad(go);
            go.AddComponent<ProceduralTreeRendererLifecycleRecovery>();
        }

        private void Update()
        {
            ITreeWorldReadSource source = TreeWorldReadRegistry.Current;
            int version = source.Version;
            int damageVersion = source.DamageVersion;

            if (version != _observedVersion)
            {
                _observedVersion = version;
                _observedDamageVersion = damageVersion;
                _verifyAfterFrame = -1;
                return;
            }

            if (damageVersion != _observedDamageVersion)
            {
                _observedDamageVersion = damageVersion;
                _verifyAfterFrame = Time.frameCount + 1;
                return;
            }

            if (_verifyAfterFrame < 0 || Time.frameCount < _verifyAfterFrame)
                return;

            _verifyAfterFrame = -1;
            ProceduralTreeRenderer renderer =
                FindFirstObjectByType<ProceduralTreeRenderer>(FindObjectsInactive.Include);
            if (renderer == null || !HasStaleBatchOnlyDamage(source, renderer))
                return;

            // TreeWorldState.SubsystemRegistration intentionally clears static event delegates.
            // Re-enabling rebinds the surviving DontDestroyOnLoad renderer and marks its snapshot
            // dirty, so it rebuilds once from authoritative semantic state on the next Update.
            renderer.enabled = false;
            renderer.enabled = true;
        }

        private static bool HasStaleBatchOnlyDamage(
            ITreeWorldReadSource source, ProceduralTreeRenderer renderer)
        {
            var damage = source.Damage;
            var instances = source.Instances;
            for (int i = 0; i < instances.Count; i++)
            {
                if (renderer.TryGetDynamicPresentationRoot(i, out _))
                    continue;

                bool hasRemovedBranch = source.RemovedBranches(i).Count > 0;
                bool hasNonTerminalDamage = i < damage.Count
                    && !damage[i].Severed
                    && damage[i].FoliageHealth < 0.9999f;
                if (hasRemovedBranch || hasNonTerminalDamage)
                    return true;
            }
            return false;
        }
    }
}
