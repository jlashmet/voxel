using System.Collections.Generic;
using System.IO;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class RavenSculptureVisualTests
    {
        [Test]
        public void RavenSculpture_WritesHighResolutionTexturedPng()
        {
            int3 min = RavenSculptureAuthoring.LocalMin;
            int3 size = RavenSculptureAuthoring.LocalSize;
            var capture = new VisualStructureCapture(min, size);
            var placement = RavenSculptureWorldBuilderObject.CreatePlacement(
                new GeneratedPropId(0x524156454EUL),
                sceneId: 1,
                slotId: 1,
                origin: int3.zero,
                facing: new int3(0, 0, -1));
            var context = new DecorationContext
            {
                WorldSeed = 0x52415645u,
                StructureId = 1,
                SpaceId = 1,
                StyleId = 1,
                StructureKind = DecorationStructureKind.Castle,
                SpaceKind = DecorationSpaceKind.ExteriorYard,
                Wealth = DecorationWealthTier.Noble,
                Condition = DecorationConditionTier.Worn,
                Environment = DecorationEnvironmentTags.Exterior,
            };

            Assert.That(RavenSculptureWorldBuilderObject.Descriptor.IsWellFormed, Is.True);
            Assert.That(placement.IsWellFormed, Is.True);
            Assert.That(DecorationVoxelStampBackend.TryAuthor(capture, in placement, in context), Is.True);

            int occupied = 0;
            var materials = new HashSet<byte>();
            int3 max = min + size;
            for (int y = min.y; y < max.y; y++)
            for (int z = min.z; z < max.z; z++)
            for (int x = min.x; x < max.x; x++)
            {
                byte material = capture.Get(x, y, z);
                if (material == GameMaterialIds.Empty) continue;
                occupied++;
                materials.Add(material);
            }

            Assert.Multiple(() =>
            {
                Assert.That(occupied, Is.GreaterThan(160000),
                    "The raven should retain dense anatomy and layered feather forms.");
                Assert.That(materials.Count, Is.GreaterThanOrEqualTo(7),
                    "The sculpture should preserve its deliberately textured material regions.");
                Assert.That(materials, Does.Contain(GameMaterialIds.DarkStone));
                Assert.That(materials, Does.Contain(GameMaterialIds.Slate));
                Assert.That(materials, Does.Contain(GameMaterialIds.Crystal));
                Assert.That(materials, Does.Contain(GameMaterialIds.Glass));
                Assert.That(materials, Does.Contain(GameMaterialIds.Gold));
            });
            AssertPaddingIsEmpty(capture, min, size);

            string path = capture.RenderPng("raven-sculpture-high-resolution", 1600, 1600);
            Assert.That(File.Exists(path), Is.True, $"Expected visual artifact at {path}");
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(8192),
                $"Visual artifact was unexpectedly small: {path}");

            string artifactDirectory = Path.Combine(
                Directory.GetCurrentDirectory(), "Artifacts", "SingleTest");
            Directory.CreateDirectory(artifactDirectory);
            string artifactPath = Path.Combine(artifactDirectory, "raven-sculpture-high-resolution.png");
            File.Copy(path, artifactPath, true);
            TestContext.WriteLine($"Generated high-resolution voxel raven: {artifactPath}");
        }

        private static void AssertPaddingIsEmpty(VisualStructureCapture capture, int3 min, int3 size)
        {
            int3 max = min + size - 1;
            int boundaryOccupied = 0;
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                if (capture.Get(min.x, y, z) != GameMaterialIds.Empty) boundaryOccupied++;
                if (capture.Get(max.x, y, z) != GameMaterialIds.Empty) boundaryOccupied++;
            }
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                if (capture.Get(x, y, min.z) != GameMaterialIds.Empty) boundaryOccupied++;
                if (capture.Get(x, y, max.z) != GameMaterialIds.Empty) boundaryOccupied++;
            }
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                if (capture.Get(x, min.y, z) != GameMaterialIds.Empty) boundaryOccupied++;
                if (capture.Get(x, max.y, z) != GameMaterialIds.Empty) boundaryOccupied++;
            }
            Assert.That(boundaryOccupied, Is.Zero,
                "The authored raven touched its declared bounds; enlarge the footprint or fix the sculpt.");
        }
    }
}
