using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using VoxelEngine.Rendering.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class GrassCoatingPresentationTests
    {
        [Test]
        [Category("Rendering")]
        public void GrassAndMossCoatingShareAuthoredTextureDensity()
        {
            MaterialPresentationDefinition grass =
                GameMaterialRenderingDefinitions.Create()[GameMaterialIds.Grass];
            UnityEngine.Vector4 mossSampling = VoxelPresentationCatalogue.CoatingSampling[1];

            Assert.That(mossSampling.x, Is.EqualTo(grass.Sampling.x),
                "The renderer-owned moss coating must reuse the authored grass texture layer.");
            Assert.That(mossSampling.y, Is.EqualTo(grass.Surface.x).Within(0.0001f),
                "Crossing a moss coating boundary must not change the apparent grass motif size.");
        }
    }
}
