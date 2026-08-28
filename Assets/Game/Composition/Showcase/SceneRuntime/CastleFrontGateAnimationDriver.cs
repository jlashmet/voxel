using System.Reflection;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Presentation driver for the castle front gate. The world keeps gate state and voxel
    /// mutation authoritative; this scene-level component only supplies frame time around the
    /// existing player interaction loop.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    internal sealed class CastleFrontGateAnimationDriver : MonoBehaviour
    {
        private static readonly FieldInfo s_WorldField = typeof(VoxelShowcase).GetField(
            "_world", BindingFlags.Instance | BindingFlags.NonPublic);

        private VoxelShowcase _showcase;
        private ShowcaseWorld _world;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Object.FindFirstObjectByType<CastleFrontGateAnimationDriver>() != null)
                return;

            var root = new GameObject("Castle front gate animation driver");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<CastleFrontGateAnimationDriver>();
        }

        private void Update()
        {
            ResolveWorld();
            if (_world != null && !_world.CastleFrontGateOpen)
                _world.PrepareCastleFrontGateAnimation();
        }

        private void LateUpdate()
        {
            ResolveWorld();
            _world?.StepCastleFrontGateAnimation(Time.deltaTime);
        }

        private void ResolveWorld()
        {
            if (_showcase == null)
            {
                _showcase = Object.FindFirstObjectByType<VoxelShowcase>();
                _world = null;
            }

            if (_showcase != null && _world == null && s_WorldField != null)
                _world = s_WorldField.GetValue(_showcase) as ShowcaseWorld;
        }
    }
}
