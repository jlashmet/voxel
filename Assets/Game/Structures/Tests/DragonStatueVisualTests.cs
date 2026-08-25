using System.IO;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DragonStatueVisualTests
    {
        [Test]
        public void DragonStatue_WritesIsolatedRenderedGeometryPng()
        {
            int3 min = DragonStatueWorldBuilderObject.LocalMin + new int3(-4, -4, -4);
            int3 size = DragonStatueWorldBuilderObject.LocalSize + new int3(8, 8, 8);
            var capture = new VisualStructureCapture(min, size);
            var placement = DragonStatueWorldBuilderObject.CreatePlacement(
                new GeneratedPropId(0xD12A60UL),
                sceneId: 1,
                slotId: 1,
                origin: int3.zero,
                facing: new int3(0, 0, -1));
            var context = new DecorationContext
            {
                WorldSeed = 0xD12A60u,
                StructureId = 1,
                SpaceId = 1,
                StyleId = 1,
                StructureKind = DecorationStructureKind.Castle,
                SpaceKind = DecorationSpaceKind.ExteriorYard,
                Wealth = DecorationWealthTier.Noble,
                Condition = DecorationConditionTier.Worn,
                Environment = DecorationEnvironmentTags.Exterior,
            };

            Assert.That(DragonStatueWorldBuilderObject.Descriptor.IsWellFormed, Is.True);
            Assert.That(placement.IsWellFormed, Is.True);
            Assert.That(DecorationVoxelStampBackend.TryAuthor(capture, in placement, in context), Is.True);

            Assert.That(capture.TotalVoxelsWritten, Is.GreaterThan(20000),
                "Dragon statue should contain enough sampled implicit detail to read as a sculpt.");
            string path = capture.RenderPng("dragon-statue-sdf", 1440, 1100);
            Assert.That(File.Exists(path), Is.True, $"Expected visual artifact at {path}");
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(4096),
                $"Visual artifact was unexpectedly small: {path}");
            TestContext.WriteLine($"Generated SDF dragon statue: {path}");
        }
    }
}
