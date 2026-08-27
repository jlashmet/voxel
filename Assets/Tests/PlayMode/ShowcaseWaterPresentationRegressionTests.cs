using System.Reflection;
using NUnit.Framework;
using VoxelEngine.Composition;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ShowcaseWaterPresentationRegressionTests
    {
        private const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void VoxelShowcaseLoadRestoresWaterAfterDiagnosticDisable()
        {
            RenderingComposition.SetWaterRenderEnabled(false);
            try
            {
                Assert.That(ReadWaterRenderEnabled(), Is.False,
                    "The discriminator must begin from the leaked diagnostic state.");

                InvokeRestoreForScene("VoxelShowcase");

                Assert.That(ReadWaterRenderEnabled(), Is.True,
                    "Loading the production VoxelShowcase must restore its authored water presentation.");
            }
            finally
            {
                RenderingComposition.SetWaterRenderEnabled(true);
            }
        }

        [Test]
        public void OtherSceneLoadDoesNotOverrideExplicitWaterDiagnostic()
        {
            RenderingComposition.SetWaterRenderEnabled(false);
            try
            {
                InvokeRestoreForScene("WorldbuildingGalleryShowcase");
                Assert.That(ReadWaterRenderEnabled(), Is.False,
                    "The fix must not broaden into a global override of explicit water diagnostics.");
            }
            finally
            {
                RenderingComposition.SetWaterRenderEnabled(true);
            }
        }

        private static void InvokeRestoreForScene(string sceneName)
        {
            var assembly = Assembly.Load("VoxelEngine.Showcase");
            var type = assembly.GetType(
                "VoxelEngine.Showcase.VoxelShowcasePresentationDefaults",
                throwOnError: true);
            var method = type.GetMethod("RestoreForScene", StaticNonPublic);
            Assert.That(method, Is.Not.Null, "Production Showcase presentation reset entrypoint must exist.");
            method.Invoke(null, new object[] { sceneName });
        }

        private static bool ReadWaterRenderEnabled()
        {
            var assembly = Assembly.Load("VoxelEngine.Rendering.Runtime");
            var type = assembly.GetType(
                "VoxelEngine.Rendering.Runtime.VoxelRenderBridge",
                throwOnError: true);
            var field = type.GetField(
                "WaterRenderEnabled",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Renderer diagnostic water switch must remain observable.");
            return (bool)field.GetValue(null);
        }
    }
}
