using System;
using UnityEngine;

namespace Game.Structures.Runtime
{
    /// <summary>Unity lifecycle wrapper that binds one generated WorldObject scene to the presentation sink.</summary>
    [DisallowMultipleComponent]
    public sealed class UnityWorldObjectSceneHost : MonoBehaviour
    {
        [SerializeField] private UnityWorldObjectPresentationSink _sink;
        private WorldObjectGeneratedScene _scene;
        private WorldObjectPresentationRuntime _presentation;

        public WorldObjectGeneratedScene Scene => _scene;
        public bool IsBound => _scene != null;

        private void Awake()
        {
            if (_sink == null)
                _sink = GetComponent<UnityWorldObjectPresentationSink>();
            if (_sink == null)
                _sink = gameObject.AddComponent<UnityWorldObjectPresentationSink>();
        }

        public void Bind(WorldObjectGeneratedScene scene)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            Unbind();
            _scene = scene;
            _presentation = new WorldObjectPresentationRuntime(scene, _sink);
        }

        public void Unbind()
        {
            _presentation?.Dispose();
            _presentation = null;
            _scene = null;
            if (_sink != null) _sink.Clear();
        }

        public int TickRuntime(int ticks = 1)
        {
            return _scene?.Runtime.Tick(ticks) ?? 0;
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
