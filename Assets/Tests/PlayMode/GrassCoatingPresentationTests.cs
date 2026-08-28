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
            MaterialPresentationDefinition[] definitions = GameMaterialRenderingDefinitions.Create();
            MaterialPresentationDefinition grass = definitions[GameMaterialIds.Grass];
            MaterialPresentationDefinition moss = definitions[GameMaterialIds.Moss];
            UnityEngine.Vector4 mossCoatingSampling = VoxelPresentationCatalogue.CoatingSampling[1];

            Assert.That(moss.Sampling.x, Is.EqualTo(grass.Sampling.x),
                "Grass and Moss base materials must reuse the same authored grass texture layer.");
            Assert.That(moss.Surface.x, Is.EqualTo(grass.Surface.x).Within(0.0001f),
                "Crossing from Grass to Moss base material must not enlarge the grass artwork.");
            Assert.That(moss.Sampling.z, Is.EqualTo(grass.Sampling.z).Within(0.0001f),
                "Grass and Moss must use the same projection for their shared texture artwork.");

            Assert.That(mossCoatingSampling.x, Is.EqualTo(grass.Sampling.x),
                "The renderer-owned moss coating must track the authored grass texture layer.");
            Assert.That(mossCoatingSampling.y, Is.EqualTo(grass.Surface.x).Within(0.0001f),
                "The moss presentation metadata must stay at the authored grass texel density.");
            Assert.That(mossCoatingSampling.z, Is.Zero.Within(0.0001f),
                "Moss coating must tint the presented surface instead of independently resampling " +
                "the same grass artwork through a second texturing path.");
        }
    }
}
