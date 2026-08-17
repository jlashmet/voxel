using UnityEngine;

namespace Game.Composition.WorldObjects.Runtime
{
    /// <summary>
    /// Ensures one persistent Unity composition owner exists for WorldObjects. Structure/cave realization code
    /// can obtain Current and load deterministic scenes without scene-specific bootstrap duplication.
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
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
