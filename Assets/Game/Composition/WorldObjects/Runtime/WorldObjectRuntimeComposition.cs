using System;
using System.Collections.Generic;
using Game.Structures.Api;
using Game.Structures.Runtime;
using UnityEngine;
using VoxelEngine.Structures.Api;

namespace Game.Composition.WorldObjects.Runtime
{
    /// <summary>
    /// Unity-facing WorldObject presentation/composition owner. It can own a registry for simple callers, but it
    /// also observes any authoritative WorldObjectSceneRegistry in the process (for example ShowcaseWorld) and
    /// binds presentation to those scenes without duplicating their persistent gameplay state.
    ///
    /// Active registries are advanced once per Unity fixed step while they have at least one presented scene.
    /// Registry reference counts prevent multiple loaded scenes from making one registry tick more than once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldObjectRuntimeComposition : MonoBehaviour
    {
        [SerializeField] private UnityWorldObjectPresentationSink _presentationSink;

        private readonly WorldObjectSceneRegistry _registry = new WorldObjectSceneRegistry();
        private readonly Dictionary<SceneBindingKey, WorldObjectPresentationRuntime> _presentations =
            new Dictionary<SceneBindingKey, WorldObjectPresentationRuntime>();
        private readonly Dictionary<WorldObjectSceneRegistry, int> _activeRegistries =
            new Dictionary<WorldObjectSceneRegistry, int>();
        private readonly List<WorldObjectSceneRegistry> _tickRegistries =
            new List<WorldObjectSceneRegistry>();

        public WorldObjectSceneRegistry Registry => _registry;
        public int LoadedSceneCount => _registry.LoadedSceneCount;
        public int PresentedSceneCount => _presentations.Count;
        public int ActiveRegistryCount => _activeRegistries.Count;

        private void Awake()
        {
            if (_presentationSink == null)
                _presentationSink = GetComponent<UnityWorldObjectPresentationSink>();
            if (_presentationSink == null)
                _presentationSink = gameObject.AddComponent<UnityWorldObjectPresentationSink>();

            WorldObjectSceneLifecycle.Loaded += OnSceneLoaded;
            WorldObjectSceneLifecycle.Unloaded += OnSceneUnloaded;
        }

        public WorldObjectGeneratedScene LoadCastle(IStructureAuthoringSession geometry,
            uint worldSeed, uint parentId, in CastlePlan plan) =>
            _registry.LoadCastleForUnityDynamicPresentation(geometry, worldSeed, parentId, in plan);

        public WorldObjectGeneratedScene LoadMineCave(IStructureAuthoringSession geometry,
            uint worldSeed, uint parentId, DecorationBounds chamber) =>
            _registry.LoadMineCaveForUnityDynamicPresentation(geometry, worldSeed, parentId, chamber);

        public WorldObjectGeneratedScene LoadDecorations(uint parentId, DecorationPlacement[] placements) =>
            _registry.LoadDecorations(parentId, placements);

        public bool Unload(uint parentId) => _registry.Unload(parentId);

        public bool Forget(uint parentId) => _registry.RemovePersistentState(parentId);

        public WorldObjectStateDelta[] Snapshot(uint parentId) => _registry.Snapshot(parentId);

        public void Restore(uint parentId, WorldObjectStateDelta[] deltas) =>
            _registry.Restore(parentId, deltas);

        /// <summary>Advances each active authoritative registry once, regardless of its loaded scene count.</summary>
        public int Tick(int ticks = 1)
        {
            if (ticks <= 0 || _activeRegistries.Count == 0) return 0;

            // Tick callbacks can change scene lifecycle. Iterate a stable snapshot so registry load/unload events
            // cannot invalidate the dictionary enumerator or cause a registry to run twice in one fixed step.
            _tickRegistries.Clear();
            foreach (var pair in _activeRegistries)
                _tickRegistries.Add(pair.Key);

            int changed = 0;
            for (int i = 0; i < _tickRegistries.Count; i++)
                changed += _tickRegistries[i].TickLoaded(ticks);
            _tickRegistries.Clear();
            return changed;
        }

        private void FixedUpdate()
        {
            Tick(1);
        }

        private void OnSceneLoaded(WorldObjectSceneRegistry registry, uint parentId,
            WorldObjectGeneratedScene scene)
        {
            SceneBindingKey key = new SceneBindingKey(registry, parentId);
            bool replacing = _presentations.ContainsKey(key);
            UnbindPresentation(key, releaseRegistry: false);
            _presentations[key] = new WorldObjectPresentationRuntime(scene, _presentationSink);
            if (!replacing) RetainRegistry(registry);
        }

        private void OnSceneUnloaded(WorldObjectSceneRegistry registry, uint parentId)
        {
            UnbindPresentation(new SceneBindingKey(registry, parentId), releaseRegistry: true);
        }

        private void RetainRegistry(WorldObjectSceneRegistry registry)
        {
            if (_activeRegistries.TryGetValue(registry, out int count))
                _activeRegistries[registry] = count + 1;
            else
                _activeRegistries.Add(registry, 1);
        }

        private void ReleaseRegistry(WorldObjectSceneRegistry registry)
        {
            if (!_activeRegistries.TryGetValue(registry, out int count)) return;
            if (count <= 1)
                _activeRegistries.Remove(registry);
            else
                _activeRegistries[registry] = count - 1;
        }

        private void UnbindPresentation(SceneBindingKey key, bool releaseRegistry)
        {
            if (!_presentations.TryGetValue(key, out WorldObjectPresentationRuntime runtime)) return;
            _presentations.Remove(key);
            runtime.Dispose();
            if (releaseRegistry) ReleaseRegistry(key.Registry);
        }

        private void OnDestroy()
        {
            WorldObjectSceneLifecycle.Loaded -= OnSceneLoaded;
            WorldObjectSceneLifecycle.Unloaded -= OnSceneUnloaded;
            foreach (var pair in _presentations)
                pair.Value.Dispose();
            _presentations.Clear();
            _activeRegistries.Clear();
            _tickRegistries.Clear();
        }

        private readonly struct SceneBindingKey : IEquatable<SceneBindingKey>
        {
            private readonly WorldObjectSceneRegistry _registry;
            private readonly uint _parentId;

            public WorldObjectSceneRegistry Registry => _registry;

            public SceneBindingKey(WorldObjectSceneRegistry registry, uint parentId)
            {
                _registry = registry;
                _parentId = parentId;
            }

            public bool Equals(SceneBindingKey other) =>
                ReferenceEquals(_registry, other._registry) && _parentId == other._parentId;

            public override bool Equals(object obj) => obj is SceneBindingKey other && Equals(other);

            public override int GetHashCode() =>
                ((_registry != null ? _registry.GetHashCode() : 0) * 397) ^ (int)_parentId;
        }
    }
}
