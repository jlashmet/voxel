using UnityEngine;

namespace Game.Composition.WorldObjects.Runtime
{
    /// <summary>
    /// Ensures one persistent Unity composition owner exists before scene startup can publish generated
    /// WorldObject scenes. Authoritative registries may live elsewhere; this owner observes their lifecycle for
    /// dynamic presentation while retaining its own registry only for simple direct callers.
    /// </summary>
    public static class WorldObjectRuntimeBootstrap
    {
        private static WorldObjectRuntimeComposition _current;

        public static WorldObjectRuntimeComposition Current
        {
            get
            {
                if (_current == null)
                    Ensure();
                return _current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            if (_current != null) return;
            _current = Object.FindFirstObjectByType<WorldObjectRuntimeComposition>();
            if (_current != null) return;

            var root = new GameObject("WorldObjectRuntime");
            Object.DontDestroyOnLoad(root);
            _current = root.AddComponent<WorldObjectRuntimeComposition>();
        }
    }
}
