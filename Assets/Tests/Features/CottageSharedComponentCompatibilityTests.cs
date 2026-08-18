using NUnit.Framework;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Tests.Features.Fixtures;

namespace VoxelEngine.Tests.Features
{
    public sealed class CottageSharedComponentCompatibilityTests
    {
        [Test]
        public void SharedComponentRefactorPreservesLegacyOpcodeStream()
        {
            int[] actual = CottageProgram.Build();
            int[] expected = LegacyOpcodeStream();

            CollectionAssert.AreEqual(expected, actual,
                "routing cottage defaults through shared structure configs changed the legacy shape program");
        }

        private static int[] LegacyOpcodeStream()
        {
            const int width = 64;
            const int depth = 64;
            const int wallHeight = 32;
            const int wallThickness = 4;
            const int foundationDepth = 8;
            const int roofHeight = 16;
            const int doorWidth = 12;
            const int doorHeight = 20;

            var b = new ProgramBuilder();
            b.Box(0, 0, 0, width, foundationDepth, depth,
                CottageFixture.MaterialStone, PrimitiveMode.Fill);
            b.Box(0, foundationDepth, 0, width, wallHeight, depth,
                CottageFixture.MaterialStone, PrimitiveMode.Fill);
            b.Box(wallThickness, foundationDepth, wallThickness,
                width - 2 * wallThickness, wallHeight, depth - 2 * wallThickness,
                0, PrimitiveMode.Carve);
            b.Box(width / 2 - doorWidth / 2, foundationDepth, 0,
                doorWidth, doorHeight, wallThickness, 0, PrimitiveMode.Carve);
            b.Prism(0, foundationDepth + wallHeight, 0, width, roofHeight, depth,
                PrismProfile.Gable, CottageFixture.MaterialWood, PrimitiveMode.Fill);
            b.Anchor(CottageProgram.AnchorDoor, width / 2, foundationDepth, 0, Facing.South);
            b.Anchor(CottageProgram.AnchorHearth, width / 2, foundationDepth, depth / 2, Facing.Up);
            b.End();
            return b.Build();
        }
    }
}
