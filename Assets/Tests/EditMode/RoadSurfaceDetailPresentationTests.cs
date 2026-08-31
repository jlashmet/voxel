using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class RoadSurfaceDetailPresentationTests
    {
        [Test]
        public void SmoothSurfaceDetailHasVisibleGenericResponseWithoutStorageGrowth()
        {
            Vector4 response = VoxelPresentationCatalogue.SurfaceDetailResponse[SurfaceStyles.Smooth];
            Assert.That(response.x, Is.GreaterThan(0f),
                "Persisted smooth-surface detail must visibly affect albedo instead of becoming inert metadata.");
            Assert.That(response.y, Is.GreaterThan(0f),
                "Persisted smooth-surface detail must carry a restrained roughness response.");
            Assert.That(response.w, Is.GreaterThan(0f),
                "High detail codes must have a bounded transition width rather than a binary seam.");

            var authored = new VoxelSurfaceSemantics
            {
                StyleId = SurfaceStyles.MaterialDefault,
                Detail = 23,
            };
            VoxelSurfaceSemantics restored = VoxelSurfaceSemantics.FromStorage(authored.PackedStorage);
            Assert.AreEqual(23, restored.Detail,
                "Road wear must continue through the existing five-bit persisted detail channel.");
            Assert.AreEqual(SurfaceStyles.MaterialDefault, restored.StyleId,
                "Presentation detail must not require a new road-specific persisted reconstruction identity.");
        }
    }
}
