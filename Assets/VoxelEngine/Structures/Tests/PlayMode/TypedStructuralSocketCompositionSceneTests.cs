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
            try
            {
                Disable();
            }
            catch (TargetInvocationException error) when (error.InnerException is NullReferenceException)
            {
                // SRP Core can expose DebugManager.instance before the backing persistent-runtime-UI
                // object exists. This opt-out is test harness setup, so defer exactly that known
                // too-early setter failure and require the same operation to succeed in the fixture
                // body after scene load. Do not catch unrelated exceptions here: production failures
                // (including Input System regressions) must remain visible.
                Debug.LogWarning("Structures test SRP debug UI suppression deferred until scene load.");
            }
            catch (NullReferenceException)
            {
                // Some Unity/Mono reflection paths surface the target NRE directly rather than
                // wrapping it. Keep the exception policy equally narrow in that runtime shape.
                Debug.LogWarning("Structures test SRP debug UI suppression deferred until scene load.");
            }
        }

        internal static bool Disable()
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
            if (debugManagerType == null) return true;

            PropertyInfo instanceProperty = debugManagerType.GetProperty(
                "instance", BindingFlags.Public | BindingFlags.Static);
            object instance = instanceProperty?.GetValue(null);
            PropertyInfo runtimeUiProperty = debugManagerType.GetProperty(
                "enableRuntimeUI", BindingFlags.Public | BindingFlags.Instance);
            if (instance == null || runtimeUiProperty?.CanWrite != true) return false;

            runtimeUiProperty.SetValue(instance, false);
            return true;
        }
    }

    public sealed class TypedStructuralSocketCompositionSceneTests
    {
        [UnityTest]
        public IEnumerator FocusedValidationDriver_ComposesFourExamples_AndRejectsRequiredIncompatibleSocket()
        {
            // The before-scene hook may legitimately be too early for SRP's backing runtime-UI
            // object. Once the fixture body starts, suppression must succeed rather than silently
            // masking a broken harness state.
            Assert.That(
                RenderDebugUiTestBootstrap.Disable(),
                Is.True,
                "SRP debug runtime UI could not be disabled after scene load.");

            var host = new GameObject("Typed Structural Socket Composition Test Host");
            var driver = host.AddComponent<TypedStructuralSocketCompositionSceneDriver>();

            yield return null;

            Assert.That(driver.Complete, Is.True, "Focused structural validation did not complete.");
            Assert.That(driver.Passed, Is.True, driver.Detail);

            UnityEngine.Object.Destroy(host);
        }
    }
}
