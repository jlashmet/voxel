#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps Unity/CoreRP's runtime Rendering Debugger out of persistent CI PlayMode sessions.
/// CoreRP's DebugUpdater polls UnityEngine.Input when its legacy-input compile path is present;
/// projects configured for Input System-only correctly reject that poll. CI does not need the
/// interactive Rendering Debugger, so disable only that editor-process facility instead of
/// weakening Player Settings or gameplay input policy.
/// </summary>
[InitializeOnLoad]
internal static class VoxelCiRenderingDebuggerGuard
{
    private const string PersistentActiveKey = "Voxel.CI.Persistent.Active";
    private const string ResultsRootEnvironmentVariable = "VOXEL_CI_RESULTS_ROOT";

    static VoxelCiRenderingDebuggerGuard()
    {
        if (!IsPersistentCiProcess())
            return;

        DisableRuntimeRenderingDebugger();
        EditorApplication.update += DisableWhilePersistentCiRuns;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static bool IsPersistentCiProcess() =>
        SessionState.GetBool(PersistentActiveKey, false)
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ResultsRootEnvironmentVariable));

    private static void DisableWhilePersistentCiRuns()
    {
        if (!IsPersistentCiProcess())
        {
            EditorApplication.update -= DisableWhilePersistentCiRuns;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            return;
        }

        DisableRuntimeRenderingDebugger();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange _) =>
        DisableRuntimeRenderingDebugger();

    private static void DisableRuntimeRenderingDebugger()
    {
        Type managerType = FindType("UnityEngine.Rendering.DebugManager");
        if (managerType != null)
        {
            object manager = managerType
                .GetProperty("instance", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            PropertyInfo runtimeUi = managerType.GetProperty(
                "enableRuntimeUI",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (manager != null && runtimeUi?.CanWrite == true)
                runtimeUi.SetValue(manager, false);
        }

        Type updaterType = FindType("UnityEngine.Rendering.DebugUpdater");
        if (updaterType == null)
            return;

        MethodInfo setEnabled = updaterType.GetMethod(
            "SetEnabled",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(bool) },
            modifiers: null);
        if (setEnabled != null)
        {
            setEnabled.Invoke(null, new object[] { false });
            return;
        }

        // Package implementations that do not expose the helper still should not retain an
        // updater object after CI has disabled runtime UI. Remove only that package-owned editor
        // object; game components and input systems are untouched.
        UnityEngine.Object[] updaters = Resources.FindObjectsOfTypeAll(updaterType);
        for (int i = 0; i < updaters.Length; i++)
            if (updaters[i] != null)
                UnityEngine.Object.DestroyImmediate(updaters[i]);
    }

    private static Type FindType(string fullName) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, throwOnError: false))
            .FirstOrDefault(type => type != null);
}
#endif
