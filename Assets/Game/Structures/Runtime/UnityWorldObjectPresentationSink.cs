using System;
using System.Collections.Generic;
using Game.Structures.Api;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Read-only identity/affordance metadata attached to Unity dynamic proxies so composition-level input adapters
    /// can route a picked proxy back through the shared WorldObjectSceneRuntime without parsing object names.
    /// </summary>
    public sealed class UnityWorldObjectProxyIdentity : MonoBehaviour
    {
        public WorldObjectId Id { get; private set; }
        public bool InteractionEnabled { get; internal set; }

        internal void Initialize(WorldObjectId id) => Id = id;
    }

    /// <summary>
    /// Gameplay-first Unity presentation backend for dynamic generated objects. It creates one owned proxy
    /// per object and applies runtime pose/collider/light/particle changes without mutating unrelated terrain voxels.
    /// Mechanism kinds with shared construction semantics use generated production proxy geometry; other kinds keep
    /// the bounded primitive fallback until their own reusable presentation is defined.
    /// </summary>
    public sealed class UnityWorldObjectPresentationSink : MonoBehaviour, IWorldObjectPresentationSink
    {
        public const float DefaultWorldUnitsPerVoxel = 0.1f;

        [SerializeField] private float _worldUnitsPerVoxel = DefaultWorldUnitsPerVoxel;
        [SerializeField] private float _positionLerpSpeed = 12f;
        [SerializeField] private float _rotationLerpSpeed = 12f;

        private readonly Dictionary<WorldObjectId, Proxy> _proxies = new Dictionary<WorldObjectId, Proxy>();

        public int ProxyCount => _proxies.Count;
        public int DetailedProxyCount
        {
            get
            {
                int count = 0;
                foreach (var pair in _proxies)
                    if (pair.Value.OwnedMesh != null) count++;
                return count;
            }
        }

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
            DestroyProxy(proxy, true);
        }

        public void Clear()
        {
            foreach (var pair in _proxies)
                DestroyProxy(pair.Value, true);
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
            foreach (var pair in _proxies)
                DestroyProxy(pair.Value, false);
            _proxies.Clear();
        }

        private Proxy GetOrCreate(in WorldObjectPresentationPlan plan)
        {
            if (_proxies.TryGetValue(plan.Id, out Proxy existing) && existing.Root != null)
                return existing;

            Mesh ownedMesh = null;
            GameObject root;
            MeshRenderer renderer;
            Collider collider;
            if (WorldObjectProxyGeometry.TryCreateMesh(plan.Kind, out ownedMesh))
            {
                root = new GameObject($"WorldObject_{plan.Kind}_{plan.Id}");
                var filter = root.AddComponent<MeshFilter>();
                filter.sharedMesh = ownedMesh;
                renderer = root.AddComponent<MeshRenderer>();
                collider = root.AddComponent<BoxCollider>();
            }
            else
            {
                root = GameObject.CreatePrimitive(PrimitiveFor(plan.Kind));
                root.name = $"WorldObject_{plan.Kind}_{plan.Id}";
                renderer = root.GetComponent<MeshRenderer>();
                collider = root.GetComponent<Collider>();
            }

            root.transform.SetParent(transform, false);
            bool canRender = ApplyProxyMaterial(renderer, plan.Kind);

            int3 size = plan.BaselineBounds.Size;
            root.transform.localScale = new Vector3(
                math.max(1, size.x) * _worldUnitsPerVoxel,
                math.max(1, size.y) * _worldUnitsPerVoxel,
                math.max(1, size.z) * _worldUnitsPerVoxel);

            var identity = root.AddComponent<UnityWorldObjectProxyIdentity>();
            identity.Initialize(plan.Id);

            var proxy = new Proxy
            {
                Root = root,
                Renderer = renderer,
                CanRender = canRender,
                Collider = collider,
                Identity = identity,
                OwnedMesh = ownedMesh,
            };
            _proxies.Add(plan.Id, proxy);
            return proxy;
        }

        private static PrimitiveType PrimitiveFor(WorldObjectKind kind)
        {
            switch (kind)
            {
                case WorldObjectKind.Lever:
                case WorldObjectKind.Button:
                case WorldObjectKind.Torch:
                    return PrimitiveType.Cylinder;
                case WorldObjectKind.Chest:
                    return PrimitiveType.Capsule;
                default:
                    return PrimitiveType.Cube;
            }
        }

        /// <summary>
        /// Applies a render-pipeline-safe semantic material. The compact kind palette is presentation only,
        /// keeping generated mechanisms distinguishable without leaking scene policy into gameplay code.
        /// </summary>
        private static bool ApplyProxyMaterial(MeshRenderer renderer, WorldObjectKind kind)
        {
            if (renderer == null) return false;

            if (!s_ProxyMaterials.TryGetValue(kind, out Material material) || material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    renderer.enabled = false;
                    return false;
                }

                material = new Material(shader)
                {
                    name = $"World Object Proxy ({kind})",
                    hideFlags = HideFlags.HideAndDontSave,
                    color = ColorForKind(kind),
                };
                if (material.HasProperty("_Smoothness"))
                    material.SetFloat("_Smoothness", 0.22f);
                s_ProxyMaterials[kind] = material;
            }

            renderer.sharedMaterial = material;
            return true;
        }

        private static Color ColorForKind(WorldObjectKind kind)
        {
            switch (kind)
            {
                case WorldObjectKind.Lever:
                case WorldObjectKind.Button:
                case WorldObjectKind.PressurePlate:
                    return new Color(0.76f, 0.52f, 0.20f, 1f);
                case WorldObjectKind.Door:
                case WorldObjectKind.Trapdoor:
                case WorldObjectKind.Gate:
                case WorldObjectKind.Portcullis:
                    return new Color(0.34f, 0.22f, 0.12f, 1f);
                case WorldObjectKind.SecretDoor:
                    return new Color(0.34f, 0.31f, 0.27f, 1f);
                case WorldObjectKind.Elevator:
                case WorldObjectKind.Drawbridge:
                    return new Color(0.30f, 0.55f, 0.42f, 1f);
                case WorldObjectKind.RotatingWall:
                    return new Color(0.48f, 0.34f, 0.56f, 1f);
                case WorldObjectKind.Chest:
                case WorldObjectKind.Torch:
                    return new Color(0.72f, 0.58f, 0.24f, 1f);
                default:
                    return new Color(0.40f, 0.40f, 0.38f, 1f);
            }
        }

        private static void ApplyParticleMaterial(ParticleSystem particles)
        {
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
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
                    name = "World Object Particles (Shared Runtime)",
                    hideFlags = HideFlags.HideAndDontSave,
                    color = new Color(1f, 0.72f, 0.35f, 1f),
                };
            }

            renderer.sharedMaterial = s_ParticleMaterial;
        }

        private static readonly Dictionary<WorldObjectKind, Material> s_ProxyMaterials =
            new Dictionary<WorldObjectKind, Material>();
        private static Material s_ParticleMaterial;

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

            if (proxy.Renderer != null)
                proxy.Renderer.enabled = proxy.CanRender && plan.Visible;
            if (proxy.Identity != null)
                proxy.Identity.InteractionEnabled = plan.InteractionEnabled;
            if (proxy.Collider != null)
                proxy.Collider.enabled = plan.BlocksNavigation || plan.InteractionEnabled;

            SetLight(proxy, plan.Visible && plan.LightActive);
            SetParticles(proxy, plan.Visible && plan.ParticleActive);
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
                ApplyParticleMaterial(proxy.Particles);
            }

            if (proxy.Particles == null) return;
            if (active && !proxy.Particles.isPlaying) proxy.Particles.Play();
            if (!active && proxy.Particles.isPlaying) proxy.Particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private static void DestroyProxy(Proxy proxy, bool destroyRoot)
        {
            if (proxy == null) return;
            if (proxy.OwnedMesh != null) Destroy(proxy.OwnedMesh);
            proxy.OwnedMesh = null;
            if (destroyRoot && proxy.Root != null) Destroy(proxy.Root);
        }

        private sealed class Proxy
        {
            public GameObject Root;
            public MeshRenderer Renderer;
            public bool CanRender;
            public Collider Collider;
            public UnityWorldObjectProxyIdentity Identity;
            public Light Light;
            public ParticleSystem Particles;
            public Mesh OwnedMesh;
            public Vector3 TargetPosition;
            public Quaternion TargetRotation;
            public bool Initialized;
        }
    }
}
