using System.IO;
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
            int3 min = DragonStatueAuthoring.LocalMin + new int3(-4, -4, -4);
            int3 size = DragonStatueAuthoring.LocalSize + new int3(8, 8, 8);
            var capture = new VisualStructureCapture(min, size);

            DragonStatueAuthoring.Author(capture, int3.zero);

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
