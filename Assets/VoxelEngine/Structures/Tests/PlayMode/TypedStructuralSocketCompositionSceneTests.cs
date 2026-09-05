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
            // The focused Structures fixture does not exercise SRP debug UI. The project uses the
            // Input System backend while the current render-pipeline package's DebugUpdater still
            // polls UnityEngine.Input when runtime debug UI is enabled. Disable that package UI for
            // the frame without adding a Rendering package dependency to the Structures test asmdef.
            object debugManager = null;
            PropertyInfo runtimeUiProperty = null;
            bool? previousRuntimeUi = TryDisableRenderPipelineRuntimeDebugUi(
                out debugManager, out runtimeUiProperty);

            var host = new GameObject("Typed Structural Socket Composition Test Host");
            try
            {
                var driver = host.AddComponent<TypedStructuralSocketCompositionSceneDriver>();

                yield return null;

                Assert.That(driver.Complete, Is.True, "Focused structural validation did not complete.");
                Assert.That(driver.Passed, Is.True, driver.Detail);
            }
            finally
            {
                Object.Destroy(host);
                if (previousRuntimeUi.HasValue && debugManager != null && runtimeUiProperty != null)
                    runtimeUiProperty.SetValue(debugManager, previousRuntimeUi.Value);
            }
        }

        private static bool? TryDisableRenderPipelineRuntimeDebugUi(
            out object debugManager,
            out PropertyInfo runtimeUiProperty)
        {
            debugManager = null;
            runtimeUiProperty = null;

            Type debugManagerType = Type.GetType(
                "UnityEngine.Rendering.DebugManager, Unity.RenderPipelines.Core.Runtime",
                throwOnError: false);
            if (debugManagerType == null)
                return null;

            PropertyInfo instanceProperty = debugManagerType.GetProperty(
                "instance", BindingFlags.Public | BindingFlags.Static);
            runtimeUiProperty = debugManagerType.GetProperty(
                "enableRuntimeUI", BindingFlags.Public | BindingFlags.Instance);
            if (instanceProperty == null || runtimeUiProperty == null ||
                !runtimeUiProperty.CanRead || !runtimeUiProperty.CanWrite)
                return null;

            debugManager = instanceProperty.GetValue(null);
            if (debugManager == null)
                return null;

            bool previous = (bool)runtimeUiProperty.GetValue(debugManager);
            runtimeUiProperty.SetValue(debugManager, false);
            return previous;
        }
    }
}
