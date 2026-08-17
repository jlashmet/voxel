using NUnit.Framework;
using VoxelEngine.Tests.Features.Fixtures;

namespace VoxelEngine.Tests.Features
{
    public sealed class CottageCompatibilityTests
    {
        [Test]
        public void SharedComponentRefactorPreservesLegacyShapeProgramExactly()
        {
            int[] expected =
            {
                // Foundation box.
                1, 0, 0, 0, 0, 64, 8, 64, 1, 0, 0, 0,
                // Wall fill.
                1, 0, 0, 8, 0, 64, 32, 64, 1, 0, 0, 0,
                // Interior carve.
                1, 0, 4, 8, 4, 56, 32, 56, 0, 0, 0, 1,
                // South doorway carve.
                1, 0, 26, 8, 0, 12, 20, 4, 0, 0, 0, 1,
                // Gable roof.
                3, 0, 0, 40, 0, 64, 16, 64, 0, 2, 0, 0, 0,
                // Door and hearth anchors.
                18, 0, 0, 32, 8, 0, 2,
                18, 0, 1, 32, 8, 32, 4,
                // End.
                0, 0,
            };

            CollectionAssert.AreEqual(expected, CottageProgram.Build(),
                "the shared-component cottage migration changed legacy shape-program output");
        }
    }
}
