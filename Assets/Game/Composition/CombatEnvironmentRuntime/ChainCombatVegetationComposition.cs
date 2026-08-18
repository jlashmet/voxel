using MountingForce.CombatPrototype;
using UnityEngine;
using VoxelEngine.Vegetation.Runtime;

namespace Game.Composition.CombatEnvironment.Runtime
{
    /// <summary>
    /// Game-level composition root for the combat demo's vegetation capability. This assembly is
    /// intentionally allowed to reference both modules; CombatPrototype itself remains runtime-agnostic.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ChainCombatDemoGuide))]
    public sealed class ChainCombatVegetationComposition : MonoBehaviour
    {
        private ChainCombatVegetationBridge _bridge;

        private void Awake()
        {
            ChainCombatDemoGuide guide = GetComponent<ChainCombatDemoGuide>();
            _bridge = new ChainCombatVegetationBridge(new TreeDamageService());
            guide.SetEnvironmentBridge(_bridge);
        }
    }
}
