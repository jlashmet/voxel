using NUnit.Framework;
using VoxelEngine.Composition;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class RenderingCompositionPresentationTests
    {
        [Test]
        public void CloudOpacityCanBeChangedAndRestoredThroughComposition()
        {
            float original = RenderingComposition.GetCloudOpacity();
            try
            {
                RenderingComposition.SetCloudOpacity(-1f);
                Assert.AreEqual(0f, RenderingComposition.GetCloudOpacity());

                RenderingComposition.SetCloudOpacity(2f);
                Assert.AreEqual(1f, RenderingComposition.GetCloudOpacity());
            }
            finally
            {
                RenderingComposition.SetCloudOpacity(original);
            }

            Assert.AreEqual(original, RenderingComposition.GetCloudOpacity());
        }
    }
}
