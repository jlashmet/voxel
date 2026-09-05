using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Rendering.Runtime;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class AdditionalSurfaceTextureLayerTests
    {
        [TearDown]
        public void ResetLayers()
        {
            VoxelPresentationCatalogue.ConfigureAdditionalTextureLayers(null, null);
        }

        [Test]
        public void AdditionalLayers_AreOpaqueOrderedSlotsAndRespectRendererCapacity()
        {
            var albedo = new Texture2D[6];
            var normals = new Texture2D[2];

            VoxelPresentationCatalogue.ConfigureAdditionalTextureLayers(albedo, normals);

            Assert.That(VoxelPresentationCatalogue.AdditionalTextureLayerCount, Is.EqualTo(6));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                VoxelPresentationCatalogue.ConfigureAdditionalTextureLayers(
                    new Texture2D[VoxelPresentationCatalogue.MaxMaterials - 8 + 1], null));
        }

        [Test]
        public void Reconfiguration_ReplacesRatherThanAccumulatesExtraLayerState()
        {
            VoxelPresentationCatalogue.ConfigureAdditionalTextureLayers(new Texture2D[6], null);
            VoxelPresentationCatalogue.ConfigureAdditionalTextureLayers(new Texture2D[3], null);

            Assert.That(VoxelPresentationCatalogue.AdditionalTextureLayerCount, Is.EqualTo(3));
        }
    }
}
