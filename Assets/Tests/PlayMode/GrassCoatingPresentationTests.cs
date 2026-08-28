using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using VoxelEngine.Rendering.Api;
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
                "The renderer-owned moss coating must track the authored grass texture layer.");
            Assert.That(mossSampling.y, Is.EqualTo(grass.Surface.x).Within(0.0001f),
                "The moss presentation metadata must stay at the authored grass texel density.");
            Assert.That(mossSampling.z, Is.Zero.Within(0.0001f),
                "Moss must tint the already-presented grass instead of independently resampling " +
                "the same artwork through a second texturing path.");
        }
    }
}
