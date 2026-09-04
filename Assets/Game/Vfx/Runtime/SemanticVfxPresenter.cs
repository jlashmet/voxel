using System;
using System.Collections.Generic;
using Game.Vfx.Api;
using UnityEngine;

namespace Game.Vfx.Runtime
{
    /// <summary>
    /// Client-only Unity realization of semantic VFX. It owns pooled ParticleSystems and never owns
    /// gameplay colliders, damage callbacks, or authoritative world mutation.
    /// </summary>
    public sealed class SemanticVfxPresenter : MonoBehaviour, IVfxEffectBackend, IVfxDiagnosticsSink
    {
        private sealed class OneShot
        {
            public GameObject Root;
            public ParticleSystem System;
            public float ExpiresAt;
        }

        private sealed class Persistent
        {
            public GameObject Root;
            public ParticleSystem System;
        }

        private readonly List<OneShot> _pool = new List<OneShot>();
        private readonly Dictionary<VfxTreatmentId, Persistent> _persistent = new Dictionary<VfxTreatmentId, Persistent>();
        private readonly List<VfxDiagnostic> _diagnostics = new List<VfxDiagnostic>();
        private Material _particleMaterial;
        private int _playCount;

        public int OneShotPlayCount => _playCount;
        public int PersistentCount => _persistent.Count;
        public IReadOnlyList<VfxDiagnostic> Diagnostics => _diagnostics;

        private void Awake() => EnsureMaterial();

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _pool.Count; i++)
            {
                OneShot entry = _pool[i];
                if (entry.Root.activeSelf && now >= entry.ExpiresAt)
                {
                    entry.System.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    entry.Root.SetActive(false);
                }
            }
        }

        public void PlayOneShot(VfxEffectProfile profile, VfxWorldPoint point)
        {
            OneShot entry = AcquireOneShot();
            entry.Root.transform.position = ToVector3(point);
            Configure(entry.System, profile, false);
            entry.Root.SetActive(true);
            entry.System.Clear(true);
            entry.System.Emit(profile.ParticleCount);
            entry.ExpiresAt = Time.unscaledTime + profile.LifetimeSeconds + 0.25f;
            _playCount++;
        }

        public void ApplyPersistent(VfxTreatmentId treatmentId, VfxEffectProfile profile, VfxWorldPoint point)
        {
            if (!_persistent.TryGetValue(treatmentId, out Persistent entry))
            {
                GameObject root = new GameObject("PersistentVfx_" + treatmentId.Value);
                root.transform.SetParent(transform, false);
                ParticleSystem system = root.AddComponent<ParticleSystem>();
                entry = new Persistent { Root = root, System = system };
                _persistent.Add(treatmentId, entry);
            }
            entry.Root.transform.position = ToVector3(point);
            Configure(entry.System, profile, true);
            if (!entry.System.isPlaying) entry.System.Play(true);
        }

        public void RemovePersistent(VfxTreatmentId treatmentId)
        {
            if (!_persistent.TryGetValue(treatmentId, out Persistent entry)) return;
            if (entry.Root != null) Destroy(entry.Root);
            _persistent.Remove(treatmentId);
        }

        public void Report(VfxDiagnostic diagnostic)
        {
            _diagnostics.Add(diagnostic);
            Debug.LogWarning("VFX_DIAGNOSTIC " + diagnostic.Code + " cue=" + diagnostic.Cue.Value + " event=" + diagnostic.EventId.Value + " " + diagnostic.Message);
        }

        public int CountGameplayPhysicsComponents()
        {
            int count = GetComponentsInChildren<Collider>(true).Length;
            count += GetComponentsInChildren<Rigidbody>(true).Length;
            return count;
        }

        private OneShot AcquireOneShot()
        {
            for (int i = 0; i < _pool.Count; i++) if (!_pool[i].Root.activeSelf) return _pool[i];
            GameObject root = new GameObject("SemanticVfxOneShot_" + _pool.Count);
            root.transform.SetParent(transform, false);
            ParticleSystem system = root.AddComponent<ParticleSystem>();
            var entry = new OneShot { Root = root, System = system };
            root.SetActive(false);
            _pool.Add(entry);
            return entry;
        }

        private void Configure(ParticleSystem system, VfxEffectProfile profile, bool persistent)
        {
            EnsureMaterial();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = persistent;
            main.duration = Mathf.Max(0.25f, profile.LifetimeSeconds);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = persistent ? Mathf.Max(profile.ParticleCount * 4, 64) : Mathf.Max(profile.ParticleCount * 2, 64);
            main.startLifetime = persistent ? new ParticleSystem.MinMaxCurve(0.9f, 1.5f) : Lifetime(profile.Style, profile.LifetimeSeconds);
            main.startSpeed = Speed(profile.Style, profile.Scale);
            main.startSize = Size(profile.Style, profile.Scale);
            main.startColor = ColorFor(profile.Style);
            main.gravityModifier = profile.Style == VfxEffectStyle.Debris ? 1.4f : 0f;

            var emission = system.emission;
            emission.enabled = persistent;
            emission.rateOverTime = persistent ? Mathf.Max(8f, profile.ParticleCount) : 0f;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = profile.Style == VfxEffectStyle.Impact || profile.Style == VfxEffectStyle.Debris
                ? ParticleSystemShapeType.Cone
                : ParticleSystemShapeType.Sphere;
            shape.radius = profile.Style == VfxEffectStyle.DefeatedAura ? 0.85f * profile.Scale : 0.2f * profile.Scale;
            shape.angle = profile.Style == VfxEffectStyle.Debris ? 55f : 25f;

            var color = system.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            Color start = ColorFor(profile.Style);
            Color end = new Color(start.r, start.g, start.b, 0f);
            gradient.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(Color.white, 0.35f), new GradientColorKey(start, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(start.a, 0.55f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = system.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, profile.Style == VfxEffectStyle.DefeatBurst ? 0.2f : 0.65f),
                new Keyframe(0.35f, 1f),
                new Keyframe(1f, profile.Style == VfxEffectStyle.DefeatedAura ? 0.7f : 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var noise = system.noise;
            noise.enabled = profile.Style == VfxEffectStyle.InteractionPulse || profile.Style == VfxEffectStyle.DefeatedAura;
            noise.strength = profile.Style == VfxEffectStyle.DefeatedAura ? 0.45f : 0.25f;
            noise.frequency = 0.55f;
            noise.scrollSpeed = 0.25f;

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 50;
            if (_particleMaterial != null) renderer.sharedMaterial = _particleMaterial;
        }

        private void EnsureMaterial()
        {
            if (_particleMaterial != null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                _particleMaterial = new Material(shader) { name = "SemanticVfxRuntimeMaterial" };
                if (_particleMaterial.HasProperty("_BaseColor")) _particleMaterial.SetColor("_BaseColor", Color.white);
                if (_particleMaterial.HasProperty("_Color")) _particleMaterial.SetColor("_Color", Color.white);
            }
        }

        private static ParticleSystem.MinMaxCurve Lifetime(VfxEffectStyle style, float fallback)
        {
            switch (style)
            {
                case VfxEffectStyle.Impact: return new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
                case VfxEffectStyle.Debris: return new ParticleSystem.MinMaxCurve(0.7f, Mathf.Max(1.2f, fallback));
                case VfxEffectStyle.DefeatBurst: return new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
                default: return new ParticleSystem.MinMaxCurve(0.55f, Mathf.Max(0.8f, fallback));
            }
        }

        private static ParticleSystem.MinMaxCurve Speed(VfxEffectStyle style, float scale)
        {
            switch (style)
            {
                case VfxEffectStyle.Impact: return new ParticleSystem.MinMaxCurve(2.5f * scale, 5.5f * scale);
                case VfxEffectStyle.DefeatBurst: return new ParticleSystem.MinMaxCurve(1.8f * scale, 4.2f * scale);
                case VfxEffectStyle.InteractionPulse: return new ParticleSystem.MinMaxCurve(0.4f * scale, 1.4f * scale);
                case VfxEffectStyle.ResolutionBurst: return new ParticleSystem.MinMaxCurve(1.8f * scale, 4.8f * scale);
                case VfxEffectStyle.Debris: return new ParticleSystem.MinMaxCurve(2f * scale, 6f * scale);
                default: return new ParticleSystem.MinMaxCurve(0.1f, 0.45f * scale);
            }
        }

        private static ParticleSystem.MinMaxCurve Size(VfxEffectStyle style, float scale)
        {
            switch (style)
            {
                case VfxEffectStyle.Impact: return new ParticleSystem.MinMaxCurve(0.08f * scale, 0.24f * scale);
                case VfxEffectStyle.DefeatBurst: return new ParticleSystem.MinMaxCurve(0.12f * scale, 0.38f * scale);
                case VfxEffectStyle.Debris: return new ParticleSystem.MinMaxCurve(0.07f * scale, 0.22f * scale);
                default: return new ParticleSystem.MinMaxCurve(0.09f * scale, 0.28f * scale);
            }
        }

        private static Color ColorFor(VfxEffectStyle style)
        {
            switch (style)
            {
                case VfxEffectStyle.Impact: return new Color(1f, 0.78f, 0.22f, 0.95f);
                case VfxEffectStyle.DefeatBurst: return new Color(0.9f, 0.2f, 0.28f, 0.92f);
                case VfxEffectStyle.DefeatedAura: return new Color(0.6f, 0.12f, 0.2f, 0.55f);
                case VfxEffectStyle.InteractionPulse: return new Color(0.25f, 0.9f, 1f, 0.9f);
                case VfxEffectStyle.ResolutionBurst: return new Color(0.65f, 0.42f, 1f, 0.92f);
                case VfxEffectStyle.Debris: return new Color(0.78f, 0.58f, 0.34f, 0.9f);
                default: return Color.white;
            }
        }

        private static Vector3 ToVector3(VfxWorldPoint point) => new Vector3(point.X, point.Y, point.Z);

        private void OnDestroy()
        {
            if (_particleMaterial != null) Destroy(_particleMaterial);
        }
    }

    public sealed class SceneVfxBindingResolver : IVfxPresentationBindingResolver
    {
        private readonly Dictionary<string, Transform> _characters = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Transform> _worldObjects = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly Transform _fallback;

        public SceneVfxBindingResolver(Transform fallback = null) { _fallback = fallback; }
        public SceneVfxBindingResolver BindCharacter(string id, Transform transform) { if (!string.IsNullOrWhiteSpace(id) && transform != null) _characters[id] = transform; return this; }
        public SceneVfxBindingResolver BindWorldObject(string id, Transform transform) { if (!string.IsNullOrWhiteSpace(id) && transform != null) _worldObjects[id] = transform; return this; }

        public bool TryResolve(VfxSemanticOrigin origin, out VfxWorldPoint point)
        {
            switch (origin.Kind)
            {
                case VfxOriginKind.WorldPoint:
                    point = origin.Point;
                    return true;
                case VfxOriginKind.Character:
                    if (origin.CharacterId.IsValid && _characters.TryGetValue(origin.CharacterId.Value, out Transform character))
                    { point = FromVector3(character.position); return true; }
                    break;
                case VfxOriginKind.WorldObject:
                    if (origin.WorldObjectId.IsValid && _worldObjects.TryGetValue(origin.WorldObjectId.Value, out Transform worldObject))
                    { point = FromVector3(worldObject.position); return true; }
                    break;
                case VfxOriginKind.None:
                    if (_fallback != null) { point = FromVector3(_fallback.position); return true; }
                    point = default;
                    return true;
            }
            point = default;
            return false;
        }

        private static VfxWorldPoint FromVector3(Vector3 value) => new VfxWorldPoint(value.x, value.y, value.z);
    }
}
