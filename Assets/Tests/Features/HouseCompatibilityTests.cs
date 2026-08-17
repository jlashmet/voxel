using NUnit.Framework;
using VoxelEngine.Structures.Api;
using VoxelEngine.Tests.Features.Fixtures;

namespace VoxelEngine.Tests.Features
{
    public sealed class HouseCompatibilityTests
    {
        [Test]
        public void CottageCompatibilityPresetEmitsLegacyProgramExactly()
        {
            int[] expected = new ProgramBuilder()
                .Box(0, 0, 0, 64, 8, 64,
                    CottageFixture.MaterialStone, PrimitiveMode.Fill)
                .Box(0, 8, 0, 64, 32, 64,
                    CottageFixture.MaterialStone, PrimitiveMode.Fill)
                .Box(4, 8, 4, 56, 32, 56,
                    0, PrimitiveMode.Carve)
                .Box(26, 8, 0, 12, 20, 4,
                    0, PrimitiveMode.Carve)
                .Prism(0, 40, 0, 64, 16, 64,
                    PrismProfile.Gable, CottageFixture.MaterialWood, PrimitiveMode.Fill)
                .Anchor(CottageProgram.AnchorDoor, 32, 8, 0, Facing.South)
                .Anchor(CottageProgram.AnchorHearth, 32, 8, 32, Facing.Up)
                .End()
                .Build();

            int[] actual = CottageProgram.Build();

            CollectionAssert.AreEqual(expected, actual,
                "refactoring the compatibility cottage through shared house components changed its shape program");
        }
    }
}
