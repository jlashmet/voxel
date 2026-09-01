using System;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MountainDragonBakedArtifactTests
    {
        [Test]
        public void CheckedInBake_ResourceTextDecodesThroughPinnedTransport()
        {
            TextAsset payload = Resources.Load<TextAsset>(MountainDragonBakedArtifact.ResourcePath);
            Assert.That(payload, Is.Not.Null);
            Assert.DoesNotThrow(() => MountainDragonBakedArtifact.DecodeBase64(payload.text));
        }

        [Test]
        public void CheckedInBake_DecodesCanonicalArtifactAndPreservesDragonAnatomy()
        {
            BakedVoxelStructure bake = MountainDragonBakedArtifact.Load();

            Assert.That(bake.Cells.Length, Is.EqualTo(MountainDragonBakedArtifact.ExpectedCellCount));
            Assert.That(bake.SourceTriangleCount, Is.EqualTo(MountainDragonVoxelBakePolicy.ExpectedSourceTriangleCount));
            Assert.That(bake.VoxelSize, Is.EqualTo(MountainDragonVoxelBakePolicy.SourceVoxelSize));
            Assert.That(bake.Size, Is.EqualTo(new int3(99, 107, 107)));
            Assert.That(bake.GridOrigin, Is.EqualTo(new int3(-47, -32, 16)));
            Assert.That(bake.InteriorFilled, Is.True);

            AssertRegion(bake, "body",       new int3(35, 40, 35), new int3(70, 75, 75), 20_000);
            AssertRegion(bake, "left wing",  new int3(0, 62, 55),  new int3(35, 107, 107), 3_500);
            AssertRegion(bake, "right wing", new int3(70, 62, 55), new int3(99, 107, 107), 3_000);
            AssertRegion(bake, "head/horns", new int3(45, 35, 75), new int3(80, 65, 107), 3_000);
            AssertRegion(bake, "left foot/claws",  new int3(20, 15, 42), new int3(48, 38, 82), 6_000);
            AssertRegion(bake, "right foot/claws", new int3(52, 15, 42), new int3(80, 38, 82), 4_500);
            AssertRegion(bake, "curled tail", new int3(30, 65, 10), new int3(70, 105, 45), 4_500);
        }

        [Test]
        public void CheckedInBake_CorruptTransportFailsClosed()
        {
            TextAsset payload = Resources.Load<TextAsset>(MountainDragonBakedArtifact.ResourcePath);
            Assert.That(payload, Is.Not.Null);
            string text = payload.text;
            int index = text.Length / 2;
            char replacement = text[index] == 'A' ? 'B' : 'A';
            string corrupt = text.Substring(0, index) + replacement + text.Substring(index + 1);

            Assert.Throws<InvalidOperationException>(() => MountainDragonBakedArtifact.DecodeBase64(corrupt));
        }

        private static void AssertRegion(
            BakedVoxelStructure bake,
            string label,
            int3 minInclusive,
            int3 maxExclusive,
            int minimumCells)
        {
            int count = 0;
            for (int i = 0; i < bake.Cells.Length; i++)
            {
                int3 p = bake.Cells[i].Position;
                if (math.all(p >= minInclusive) && math.all(p < maxExclusive)) count++;
            }
            Assert.That(count, Is.GreaterThanOrEqualTo(minimumCells),
                $"Pinned mountain-dragon {label} region lost required baked anatomy.");
        }
    }
}
