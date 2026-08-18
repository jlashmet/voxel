using System;
using System.Collections.Generic;
using Game.Structures.Api;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Minimal gameplay-first Unity presentation backend for dynamic generated objects. It creates one owned proxy
    /// per object and applies runtime pose/collider/light/particle changes without mutating unrelated terrain voxels.
    /// Rich generated meshes can replace the primitive proxy later without changing the sink contract.
    /// </summary>
    public sealed class UnityWorldObjectPresentationSink : MonoBehaviour, IWorldObjectPresentationSink
    {
        [SerializeField] private float _worldUnitsPerVoxel = 0.1f;
        [SerializeField] private float _positionLerpSpeed = 12f;
        [SerializeField] private float _rotationLerpSpeed = 12f;

        private readonly Dictionary<WorldObjectId, Proxy> _proxies = new Dictionary<WorldObjectId, Proxy>();

        public int ProxyCount => _proxies.Count;

        public void CreateOrUpdate(in WorldObjectPresentationPlan plan)
        {
            if (!plan.IsWellFormed) throw new ArgumentException("Invalid world-object presentation plan.", nameof(plan));
            Proxy proxy = GetOrCreate(in plan);
            ApplyTarget(proxy, in plan);
        }

        public void Remove(WorldObjectId id)
        {
            if (!_proxies.TryGetValue(id, out Proxy proxy)) return;
            _proxies.Remove(id);
            if (proxy.Root != null) Destroy(proxy.Root);
        }

        public void Clear()
        {
            foreach (var pair in _proxies)
                if (pair.Value.Root != null) Destroy(pair.Value.Root);
            _proxies.Clear();
        }

        private void Update()
        {
            float positionT = 1f - Mathf.Exp(-Mathf.Max(0.01f, _positionLerpSpeed) * Time.deltaTime);
            float rotationT = 1f - Mathf.Exp(-Mathf.Max(0.01f, _rotationLerpSpeed) * Time.deltaTime);
            foreach (var pair in _proxies)
            {
                Proxy proxy = pair.Value;
                if (proxy.Root == null) continue;
                proxy.Root.transform.localPosition = Vector3.Lerp(
                    proxy.Root.transform.localPosition, proxy.TargetPosition, positionT);
                proxy.Root.transform.localRotation = Quaternion.Slerp(
                    proxy.Root.transform.localRotation, proxy.TargetRotation, rotationT);
            }
        }

        private void OnDestroy()
        {
            _proxies.Clear();
        }

        private Proxy GetOrCreate(in WorldObjectPresentationPlan plan)
        {
            if (_proxies.TryGetValue(plan.Id, out Proxy existing) && existing.Root != null)
                return existing;

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = $"WorldObject_{plan.Kind}_{plan.Id}";
            root.transform.SetParent(transform, false);

            int3 size = plan.BaselineBounds.Size;
            root.transform.localScale = new Vector3(
                math.max(1, size.x) * _worldUnitsPerVoxel,
                math.max(1, size.y) * _worldUnitsPerVoxel,
                math.max(1, size.z) * _worldUnitsPerVoxel);

            var proxy = new Proxy
            {
                Root = root,
                Collider = root.GetComponent<BoxCollider>(),
            };
            _proxies.Add(plan.Id, proxy);
            return proxy;
        }

        private void ApplyTarget(Proxy proxy, in WorldObjectPresentationPlan plan)
        {
            int3 min = plan.BaselineBounds.Min;
            int3 size = plan.BaselineBounds.Size;
            float3 centreVoxels = new float3(min) + new float3(size) * 0.5f + new float3(plan.TranslationVoxels);
            proxy.TargetPosition = new Vector3(
                centreVoxels.x * _worldUnitsPerVoxel,
                centreVoxels.y * _worldUnitsPerVoxel,
                centreVoxels.z * _worldUnitsPerVoxel);
            proxy.TargetRotation = Quaternion.Euler(
                plan.RotationDegrees.x,
                plan.RotationDegrees.y,
                plan.RotationDegrees.z);

            if (!proxy.Initialized)
            {
                proxy.Root.transform.localPosition = proxy.TargetPosition;
                proxy.Root.transform.localRotation = proxy.TargetRotation;
                proxy.Initialized = true;
            }

            if (proxy.Collider != null)
                proxy.Collider.enabled = plan.BlocksNavigation;

            SetLight(proxy, plan.LightActive);
            SetParticles(proxy, plan.ParticleActive);
        }

        private static void SetLight(Proxy proxy, bool active)
        {
            if (active && proxy.Light == null)
            {
                proxy.Light = proxy.Root.AddComponent<Light>();
                proxy.Light.type = LightType.Point;
                proxy.Light.range = 6f;
                proxy.Light.intensity = 1.5f;
            }
            if (proxy.Light != null) proxy.Light.enabled = active;
        }

        private static void SetParticles(Proxy proxy, bool active)
        {
            if (active && proxy.Particles == null)
            {
                proxy.Particles = proxy.Root.AddComponent<ParticleSystem>();
                var main = proxy.Particles.main;
                main.loop = true;
                main.startLifetime = 0.5f;
                main.startSpeed = 0.5f;
                main.startSize = 0.08f;
            }

            if (proxy.Particles == null) return;
            if (active && !proxy.Particles.isPlaying) proxy.Particles.Play();
            if (!active && proxy.Particles.isPlaying) proxy.Particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private sealed class Proxy
        {
            public GameObject Root;
            public BoxCollider Collider;
            public Light Light;
            public ParticleSystem Particles;
            public Vector3 TargetPosition;
            public Quaternion TargetRotation;
            public bool Initialized;
        }
    }
}
