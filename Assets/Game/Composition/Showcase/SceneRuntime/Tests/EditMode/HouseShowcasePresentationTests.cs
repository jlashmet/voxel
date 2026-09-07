using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class HouseShowcasePresentationTests
    {
        [Test]
        public void ProductionSurfaceDebugTint_IsWhiteSoMaterialShadingRemainsEnabled()
        {
            Assert.That(
                HouseShowcase.ProductionSurfaceDebugTint,
                Is.EqualTo(Color.white),
                "HouseShowcase must use the renderer's production surface mode; any non-white " +
                "debug tint enables normal/coverage visualization and bypasses material textures.");
        }
    }
}
