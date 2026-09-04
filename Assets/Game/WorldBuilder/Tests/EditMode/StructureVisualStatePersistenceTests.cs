using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class StructureVisualStatePersistenceTests
    {
        [Test]
        public void IntactIsImplicitAndDoesNotRequireRetainingDetailedState()
        {
            const ulong structureId = 17UL;
            var states = new StructureVisualStateStore();
            states.Set(structureId, Game.WorldBuilder.Api.StructureVisualState.Removed);
            states.Set(structureId, Game.WorldBuilder.Api.StructureVisualState.Intact);

            Assert.That(states.Get(structureId), Is.EqualTo(Game.WorldBuilder.Api.StructureVisualState.Intact));
        }
    }
}
