using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class StructureVisualStatePersistenceTests
    {
        [NUnit.Framework.Test]
        public void RevisionChangesExactlyWhenSemanticVisualStateChanges()
        {
            var states=new StructureVisualStateStore();
            Assert.AreEqual(0ul,states.Revision);
            states.Set(17,Game.WorldBuilder.Api.StructureVisualState.Intact);
            states.Remove(17);states.Clear();
            Assert.AreEqual(0ul,states.Revision);
            states.Set(17,Game.WorldBuilder.Api.StructureVisualState.Removed);
            Assert.AreEqual(1ul,states.Revision);
            states.Set(17,Game.WorldBuilder.Api.StructureVisualState.Removed);
            Assert.AreEqual(1ul,states.Revision);
            states.Set(17,Game.WorldBuilder.Api.StructureVisualState.Ruined);
            Assert.AreEqual(2ul,states.Revision);
            Assert.IsTrue(states.Remove(17));
            Assert.AreEqual(3ul,states.Revision);
            states.Set(17,Game.WorldBuilder.Api.StructureVisualState.Removed);
            states.Clear();
            Assert.AreEqual(5ul,states.Revision);
        }

        [NUnit.Framework.Test]
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
