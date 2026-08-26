using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.Vegetation
{
    /// <summary>
    /// Game-facing registration seam for transforms that should displace nearby grass. Gameplay
    /// components register character roots; the rendering layer samples those transforms once per
    /// frame and forwards a bounded position/radius list to the shared foliage material bridge.
    /// </summary>
    public static class GrassInteractorRegistry
    {
        private readonly struct Binding
        {
            public readonly Transform Transform;
            public readonly float Radius;

            public Binding(Transform transform, float radius)
            {
                Transform = transform;
                Radius = radius;
            }
        }

        private static readonly List<Binding> s_Bindings = new List<Binding>();
        private static readonly List<Vector4> s_Published =
            new List<Vector4>(ProceduralVegetationMaterials.MaxGrassInteractors);
        private static int s_LastPublishedFrame = -1;

        public static void Register(Transform target, float radius)
        {
            if (target == null) return;

            float boundedRadius = Mathf.Max(0.05f, radius);
            for (int i = 0; i < s_Bindings.Count; i++)
            {
                if (s_Bindings[i].Transform != target) continue;
                s_Bindings[i] = new Binding(target, boundedRadius);
                s_LastPublishedFrame = -1;
                return;
            }

            s_Bindings.Add(new Binding(target, boundedRadius));
            s_LastPublishedFrame = -1;
        }

        public static void Unregister(Transform target)
        {
            if (target == null) return;

            for (int i = s_Bindings.Count - 1; i >= 0; i--)
            {
                if (s_Bindings[i].Transform == target)
                    s_Bindings.RemoveAt(i);
            }

            // Publish again even if another character already published this frame so disabled or
            // destroyed characters stop influencing grass immediately.
            s_LastPublishedFrame = -1;
            Publish();
        }

        public static void Publish()
        {
            int frame = Time.frameCount;
            if (s_LastPublishedFrame == frame) return;
            s_LastPublishedFrame = frame;

            for (int i = s_Bindings.Count - 1; i >= 0; i--)
            {
                if (s_Bindings[i].Transform == null)
                    s_Bindings.RemoveAt(i);
            }

            s_Published.Clear();
            int count = Mathf.Min(s_Bindings.Count, ProceduralVegetationMaterials.MaxGrassInteractors);
            for (int i = 0; i < count; i++)
            {
                Binding binding = s_Bindings[i];
                Vector3 position = binding.Transform.position;
                s_Published.Add(new Vector4(position.x, position.y, position.z, binding.Radius));
            }

            ProceduralVegetationMaterials.SetGrassInteractors(s_Published);
        }
    }
}
