using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Structures.Tests.RuntimeSupport;

namespace VoxelEngine.Structures.Tests.PlayMode
{
    internal static class RenderDebugUiTestBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void DisableBeforeSceneLoad()
        {
            Disable();
        }

        internal static void Disable()
        {
            // This isolated fixture does not exercise the SRP Rendering Debugger. With Input-System-only
            // player settings, older SRP Core debug input code can otherwise read UnityEngine.Input before
            // the first PlayMode test body starts. Disable the supported runtime UI surface before scene
            // load, without adding a production render-pipeline dependency solely for harness setup.
            Type debugManagerType = Type.GetType(
                "UnityEngine.Rendering.DebugManager, Unity.RenderPipelines.Core.Runtime",
                throwOnError: false);
            if (debugManagerType == null)
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    debugManagerType = assemblies[i].GetType("UnityEngine.Rendering.DebugManager", false);
                    if (debugManagerType != null) break;
                }
            }
            if (debugManagerType == null) return;

            PropertyInfo instanceProperty = debugManagerType.GetProperty(
                "instance", BindingFlags.Public | BindingFlags.Static);
            object instance = instanceProperty?.GetValue(null);
            PropertyInfo runtimeUiProperty = debugManagerType.GetProperty(
                "enableRuntimeUI", BindingFlags.Public | BindingFlags.Instance);
            if (instance != null && runtimeUiProperty?.CanWrite == true)
                runtimeUiProperty.SetValue(instance, false);
        }
    }

    public sealed class TypedStructuralSocketCompositionSceneTests
    {
        [UnityTest]
        public IEnumerator FocusedValidationDriver_ComposesFourExamples_AndRejectsRequiredIncompatibleSocket()
        {
            // Keep the opt-out idempotent in the fixture body as a guard if the test runner changes
            // its play-mode initialization order in a future Unity/test-framework version.
            RenderDebugUiTestBootstrap.Disable();

            var host = new GameObject("Typed Structural Socket Composition Test Host");
            var driver = host.AddComponent<TypedStructuralSocketCompositionSceneDriver>();

            yield return null;

            Assert.That(driver.Complete, Is.True, "Focused structural validation did not complete.");
            Assert.That(driver.Passed, Is.True, driver.Detail);

            UnityEngine.Object.Destroy(host);
        }
    }
}
