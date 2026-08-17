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
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldObjectRuntimeComposition : MonoBehaviour
    {
        [SerializeField] private UnityWorldObjectPresentationSink _presentationSink;

        private readonly WorldObjectSceneRegistry _registry = new WorldObjectSceneRegistry();
        private readonly Dictionary<SceneBindingKey, WorldObjectPresentationRuntime> _presentations =
            new Dictionary<SceneBindingKey, WorldObjectPresentationRuntime>();

        public WorldObjectSceneRegistry Registry => _registry;
        public int LoadedSceneCount => _registry.LoadedSceneCount;
        public int PresentedSceneCount => _presentations.Count;

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

        public int Tick(int ticks = 1) => _registry.TickLoaded(ticks);

        private void OnSceneLoaded(WorldObjectSceneRegistry registry, uint parentId,
            WorldObjectGeneratedScene scene)
        {
            SceneBindingKey key = new SceneBindingKey(registry, parentId);
            UnbindPresentation(key);
            _presentations[key] = new WorldObjectPresentationRuntime(scene, _presentationSink);
        }

        private void OnSceneUnloaded(WorldObjectSceneRegistry registry, uint parentId)
        {
            UnbindPresentation(new SceneBindingKey(registry, parentId));
        }

        private void UnbindPresentation(SceneBindingKey key)
        {
            if (!_presentations.TryGetValue(key, out WorldObjectPresentationRuntime runtime)) return;
            _presentations.Remove(key);
            runtime.Dispose();
        }

        private void OnDestroy()
        {
            WorldObjectSceneLifecycle.Loaded -= OnSceneLoaded;
            WorldObjectSceneLifecycle.Unloaded -= OnSceneUnloaded;
            foreach (var pair in _presentations)
                pair.Value.Dispose();
            _presentations.Clear();
        }

        private readonly struct SceneBindingKey : IEquatable<SceneBindingKey>
        {
            private readonly WorldObjectSceneRegistry _registry;
            private readonly uint _parentId;

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
