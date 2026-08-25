using System.Collections.Generic;
using System.IO;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

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
                Assert.That(materials.Count, Is.GreaterThanOrEqualTo(9),
                    "The sculpture should preserve feather, anatomy, eye, branch, and lichen material regions.");
                Assert.That(materials, Does.Contain(GameMaterialIds.DarkStone));
                Assert.That(materials, Does.Contain(GameMaterialIds.Slate));
                Assert.That(materials, Does.Contain(GameMaterialIds.Crystal));
                Assert.That(materials, Does.Contain(GameMaterialIds.Glass));
                Assert.That(materials, Does.Contain(GameMaterialIds.Bedrock));
                Assert.That(materials, Does.Contain(GameMaterialIds.Stone));
                Assert.That(materials, Does.Contain(GameMaterialIds.Gold));
                Assert.That(materials, Does.Contain(GameMaterialIds.Wood),
                    "The raven should remain visibly perched on its authored branch.");
                Assert.That(materials, Does.Contain(GameMaterialIds.Moss),
                    "The branch should retain sparse lichen/moss texture accents.");
            });
            AssertPaddingIsEmpty(capture, min, size);

            // The raven faces -Z. A side-biased +X camera exposes the bill profile and near eye;
            // the default isometric direction looks too closely down the bill axis to judge them.
            string path = VisualStructureDiagnosticRenderer.Render(
                capture,
                min,
                size,
                "raven-sculpture-high-resolution",
                new Vector3(1f, 0.55f, 0.15f),
                1600,
                1600);
            ApplyRavenPalette(path);
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

        private static void ApplyRavenPalette(string path)
        {
            byte[] materials =
            {
                GameMaterialIds.DarkStone,
                GameMaterialIds.Slate,
                GameMaterialIds.Crystal,
                GameMaterialIds.Glass,
                GameMaterialIds.Bedrock,
                GameMaterialIds.Stone,
                GameMaterialIds.Gold,
                GameMaterialIds.Wood,
                GameMaterialIds.Moss,
            };
            Color32[] ravenColors =
            {
                new Color32(27, 33, 42, 255),
                new Color32(42, 57, 72, 255),
                new Color32(54, 82, 109, 255),
                new Color32(73, 58, 102, 255),
                new Color32(20, 23, 28, 255),
                new Color32(58, 63, 69, 255),
                new Color32(211, 157, 47, 255),
                new Color32(91, 59, 38, 255),
                new Color32(62, 79, 45, 255),
            };
            float[] faceShades = { 1.12f, 0.54f, 0.90f, 0.66f, 0.78f, 0.62f };
            var replacements = new Dictionary<uint, Color32>(materials.Length * faceShades.Length);
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            for (int shadeIndex = 0; shadeIndex < faceShades.Length; shadeIndex++)
            {
                Color32 source = Shade(HashedMaterialColor(materials[materialIndex]), faceShades[shadeIndex]);
                Color32 target = Shade(ravenColors[materialIndex], faceShades[shadeIndex]);
                replacements[ColorKey(source)] = target;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Assert.That(texture.LoadImage(File.ReadAllBytes(path), false), Is.True);
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
                if (replacements.TryGetValue(ColorKey(pixels[i]), out Color32 replacement))
                    pixels[i] = replacement;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static Color32 HashedMaterialColor(byte material)
        {
            uint hash = material * 2654435761u;
            return new Color32(
                (byte)(96 + ((hash >> 16) & 95)),
                (byte)(96 + ((hash >> 8) & 95)),
                (byte)(96 + (hash & 95)),
                255);
        }

        private static Color32 Shade(Color32 color, float factor) => new Color32(
            (byte)math.clamp((int)(color.r * factor), 0, 255),
            (byte)math.clamp((int)(color.g * factor), 0, 255),
            (byte)math.clamp((int)(color.b * factor), 0, 255),
            255);

        private static uint ColorKey(Color32 color) =>
            ((uint)color.r << 24) | ((uint)color.g << 16) | ((uint)color.b << 8) | color.a;

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
