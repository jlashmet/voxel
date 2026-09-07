using System;
using System.Collections.Generic;
using Game.Structures.Api;
using UnityEngine;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Shared Unity consumer for semantic decoration light/particle hooks. Geometry and authoritative
    /// interaction stay in their owning decoration systems; this component realizes only presentation
    /// effects derived from the existing production hook planner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DecorationEffectPresenter : MonoBehaviour
    {
        private readonly List<GameObject> _owned = new List<GameObject>();
        private static Material s_ParticleMaterial;

        public int ActiveCount => _owned.Count;
        public int ActiveLightCount { get; private set; }
        public int ActiveParticleCount { get; private set; }

        public bool TryPresent(
            DecorationPlacement[] placements,
            in DecorationContext context,
            float voxelWorldSize = DecorationProceduralMeshPresenter.DefaultWorldUnitsPerVoxel)
        {
            if (placements == null || !context.IsWellFormed || voxelWorldSize <= 0f)
                return false;

            Clear();
            DecorationEffectHook[] hooks = DecorationEffectHookPlanner.Collect(placements, in context);
            for (int i = 0; i < hooks.Length; i++)
            {
                DecorationEffectHook hook = hooks[i];
                var root = new GameObject($"DecorationEffect_{hook.Kind}_{hook.Id}");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = new Vector3(
                    hook.PositionVoxel.x * voxelWorldSize,
                    hook.PositionVoxel.y * voxelWorldSize,
                    hook.PositionVoxel.z * voxelWorldSize);
                _owned.Add(root);

                if (hook.Kind == DecorationEffectKind.Light)
                {
                    Light light = root.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.56f, 0.20f, 1f);
                    light.intensity = 2.2f;
                    light.range = 5.5f;
                    light.shadows = LightShadows.Soft;
                    ActiveLightCount++;
                }
                else if (hook.Kind == DecorationEffectKind.Particles)
                {
                    ParticleSystem particles = root.AddComponent<ParticleSystem>();
                    var main = particles.main;
                    main.loop = true;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.20f, 0.70f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(1f, 0.35f, 0.05f, 0.9f),
                        new Color(1f, 0.78f, 0.22f, 0.7f));
                    var emission = particles.emission;
                    emission.rateOverTime = 14f;
                    var shape = particles.shape;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 10f;
                    shape.radius = 0.12f;
                    ApplyParticleMaterial(particles);
                    particles.Play();
                    ActiveParticleCount++;
                }
                else
                {
                    Clear();
                    return false;
                }
            }
            return true;
        }

        public void Clear()
        {
            for (int i = 0; i < _owned.Count; i++)
                if (_owned[i] != null) Destroy(_owned[i]);
            _owned.Clear();
            ActiveLightCount = 0;
            ActiveParticleCount = 0;
        }

        private static void ApplyParticleMaterial(ParticleSystem particles)
        {
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;
            if (s_ParticleMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    renderer.enabled = false;
                    return;
                }
                s_ParticleMaterial = new Material(shader)
                {
                    name = "Decoration Effects (Shared Runtime)",
                    hideFlags = HideFlags.HideAndDontSave,
                    color = new Color(1f, 0.55f, 0.12f, 1f),
                };
            }
            renderer.sharedMaterial = s_ParticleMaterial;
        }

        private void OnDestroy() => Clear();
    }
}
