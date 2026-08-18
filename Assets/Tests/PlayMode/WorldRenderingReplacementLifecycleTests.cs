using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class WorldRenderingReplacementLifecycleTests
    {
        [Test]
        public void ReplacingStorageOwnerClearsOldWorldTransientPresentation()
        {
            using var first = new ShowcaseWorld(0x11112222u, 64, 1, 2);
            using var second = new ShowcaseWorld(0x33334444u, 64, 1, 2);

            try
            {
                var firstBinding = new RenderingWorldBinding(
                    first.ReadStorage, first.Palette, first.SurfaceRules,
                    first.CoatingRules, first.ProfileBlocks);
                RenderingComposition.ConfigureWorld(
                    in firstBinding, first.Changes, first.Seed, farFieldEnabled: true);

                RenderingComposition.SetCutaway(
                    true, new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f));
                RenderingComposition.SetLocalLights(
                    new[] { new Vector4(1f, 2f, 3f, 4f) },
                    new[] { new Vector4(5f, 6f, 7f, 8f) });
                RenderingComposition.SetFlashlight(true, Vector3.one, Vector3.forward);

                Assert.That(VoxelRenderBridge.CutawayEnabled, Is.True);
                Assert.That(VoxelRenderBridge.LocalLights.Length, Is.EqualTo(1));
                Assert.That(VoxelRenderBridge.FlashlightEnabled, Is.True);

                var secondBinding = new RenderingWorldBinding(
                    second.ReadStorage, second.Palette, second.SurfaceRules,
                    second.CoatingRules, second.ProfileBlocks);
                RenderingComposition.ConfigureWorld(
                    in secondBinding, second.Changes, second.Seed, farFieldEnabled: true);

                Assert.That(VoxelRenderBridge.CutawayEnabled, Is.False,
                    "A cutaway belongs to the old application world and must not survive replacement.");
                Assert.That(VoxelRenderBridge.LocalLights, Is.Empty,
                    "World-local presentation lights must be cleared when the storage owner changes.");
                Assert.That(VoxelRenderBridge.LocalLightColours, Is.Empty);
                Assert.That(VoxelRenderBridge.FlashlightEnabled, Is.False,
                    "The old world's camera presentation state must not bleed into the replacement world.");
            }
            finally
            {
                // Release renderer-derived pins while both storage owners are still alive.
                RenderingComposition.ResetTransientPresentation();
                RenderingComposition.ClearWorld();
            }
        }
    }
}
