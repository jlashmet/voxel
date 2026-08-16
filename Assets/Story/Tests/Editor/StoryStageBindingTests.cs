using NUnit.Framework;

namespace MountingForce.Story.Tests
{
    public sealed class StoryStageBindingTests
    {
        [Test]
        public void BoundPointResolves()
        {
            var id = new StoryStagePointId("test");
            var point = new StoryStagePoint(new StoryInt3(1, 2, 3), new StoryInt3(0, 0, 1));
            var binding = new StoryStageBinding().Bind(id, point);
            Assert.AreEqual(point.Position, binding.Resolve(id).Position);
        }
    }
}
