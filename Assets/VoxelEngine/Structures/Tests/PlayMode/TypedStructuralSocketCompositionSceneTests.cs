using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Structures.Tests.RuntimeSupport;

namespace VoxelEngine.Structures.Tests.PlayMode
{
    public sealed class TypedStructuralSocketCompositionSceneTests
    {
        [UnityTest]
        public IEnumerator FocusedValidationDriver_ComposesFourExamples_AndRejectsRequiredIncompatibleSocket()
        {
            // This isolated fixture does not exercise the SRP Rendering Debugger. With Input-System-only
            // player settings, older SRP Core debug input code can otherwise read UnityEngine.Input during
            // the first PlayMode frame and fail this unrelated structural test. Unity's supported runtime
            // switch is DebugManager.enableRuntimeUI=false; use reflection so this tiny test assembly does
            // not gain a production render-pipeline dependency solely for harness setup.
            DisableRenderPipelineRuntimeDebugUi();

            var host = new GameObject("Typed Structural Socket Composition Test Host");
            var driver = host.AddComponent<TypedStructuralSocketCompositionSceneDriver>();

            yield return null;

            Assert.That(driver.Complete, Is.True, "Focused structural validation did not complete.");
            Assert.That(driver.Passed, Is.True, driver.Detail);

            Object.Destroy(host);
        }

        private static void DisableRenderPipelineRuntimeDebugUi()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type debugManagerType = assemblies[i].GetType("UnityEngine.Rendering.DebugManager", false);
                if (debugManagerType == null) continue;

                PropertyInfo instanceProperty = debugManagerType.GetProperty(
                    "instance", BindingFlags.Public | BindingFlags.Static);
                object instance = instanceProperty?.GetValue(null);
                PropertyInfo runtimeUiProperty = debugManagerType.GetProperty(
                    "enableRuntimeUI", BindingFlags.Public | BindingFlags.Instance);
                if (instance != null && runtimeUiProperty?.CanWrite == true)
                    runtimeUiProperty.SetValue(instance, false);
                return;
            }
        }
    }
}
