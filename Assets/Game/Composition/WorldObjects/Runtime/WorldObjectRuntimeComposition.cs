using System;
using System.Collections.Generic;
using Game.Structures.Api;
using Game.Structures.Runtime;
using UnityEngine;
using VoxelEngine.Structures.Api;

namespace Game.Composition.WorldObjects.Runtime
{
    /// <summary>
    /// Unity-facing composition owner for generated WorldObject scenes. Game/structure realization calls Load*
    /// when a site becomes live and Unload when it streams out. Sparse state remains in the registry and can be
    /// snapshotted/restored by the save-game layer independently from transient Unity presentation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldObjectRuntimeComposition : MonoBehaviour
    {
        [SerializeField] private UnityWorldObjectPresentationSink _presentationSink;

        private readonly WorldObjectSceneRegistry _registry = new WorldObjectSceneRegistry();
        private readonly Dictionary<uint, WorldObjectPresentationRuntime> _presentations =
            new Dictionary<uint, WorldObjectPresentationRuntime>();

        public WorldObjectSceneRegistry Registry => _registry;
        public int LoadedSceneCount => _registry.LoadedSceneCount;

        private void Awake()
        {
            if (_presentationSink == null)
                _presentationSink = GetComponent<UnityWorldObjectPresentationSink>();
            if (_presentationSink == null)
                _presentationSink = gameObject.AddComponent<UnityWorldObjectPresentationSink>();
        }

        public WorldObjectGeneratedScene LoadCastle(IStructureAuthoringSession geometry,
            uint worldSeed, uint parentId, in CastlePlan plan)
        {
            WorldObjectGeneratedScene scene = _registry.LoadCastleForUnityDynamicPresentation(
                geometry, worldSeed, parentId, in plan);
            BindPresentation(parentId, scene);
            return scene;
        }

        public WorldObjectGeneratedScene LoadMineCave(IStructureAuthoringSession geometry,
            uint worldSeed, uint parentId, DecorationBounds chamber)
        {
            WorldObjectGeneratedScene scene = _registry.LoadMineCaveForUnityDynamicPresentation(
                geometry, worldSeed, parentId, chamber);
            BindPresentation(parentId, scene);
            return scene;
        }

        public WorldObjectGeneratedScene LoadDecorations(uint parentId, DecorationPlacement[] placements)
        {
            WorldObjectGeneratedScene scene = _registry.LoadDecorations(parentId, placements);
            BindPresentation(parentId, scene);
            return scene;
        }

        public bool Unload(uint parentId)
        {
            UnbindPresentation(parentId);
            return _registry.Unload(parentId);
        }

        public bool Forget(uint parentId)
        {
            UnbindPresentation(parentId);
            return _registry.RemovePersistentState(parentId);
        }

        public WorldObjectStateDelta[] Snapshot(uint parentId) => _registry.Snapshot(parentId);

        public void Restore(uint parentId, WorldObjectStateDelta[] deltas)
        {
            UnbindPresentation(parentId);
            _registry.Restore(parentId, deltas);
        }

        public int Tick(int ticks = 1) => _registry.TickLoaded(ticks);

        private void BindPresentation(uint parentId, WorldObjectGeneratedScene scene)
        {
            UnbindPresentation(parentId);
            _presentations[parentId] = new WorldObjectPresentationRuntime(scene, _presentationSink);
        }

        private void UnbindPresentation(uint parentId)
        {
            if (!_presentations.TryGetValue(parentId, out WorldObjectPresentationRuntime runtime)) return;
            _presentations.Remove(parentId);
            runtime.Dispose();
        }

        private void OnDestroy()
        {
            foreach (var pair in _presentations)
                pair.Value.Dispose();
            _presentations.Clear();
        }
    }
}
