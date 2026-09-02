using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuBrickCacheResolverTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickCacheResolver.compute";
        private const uint DirectoryOccupied = 1u;
        private const int DirectoryWordsPerEntry = 5;

        [Test]
        public void PersistentDirectoryResolvesToExactDenseBrickEntries()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Resolver shader missing at {ShaderPath}");
            int kernel = shader.FindKernel("CSResolveBrickCache");

            const int edge = 4;
            const int directoryCapacity = 128;
            const int directoryMask = directoryCapacity - 1;
            const int directoryWordOffset = 8;
            var words = new uint[directoryWordOffset
                               + directoryCapacity * DirectoryWordsPerEntry];
            var expected = new uint[edge * edge * edge];

            int cursor = 0;
            for (int z = -2; z <= 1; z++)
            for (int y = -2; y <= 1; y++)
            for (int x = -2; x <= 1; x++)
            {
                uint entry;
                if (((x + y + z) & 3) == 0)
                    entry = 0u;
                else if ((y & 1) == 0)
                    entry = 1u | ((uint)(1 + ((x - z) & 7)) << 8);
                else
                    entry = 2u | ((uint)(17 + cursor) << 16);
                expected[cursor++] = entry;
                InsertDirectory(words, directoryWordOffset, directoryMask, x, y, z, entry);
            }

            using var materials = Structured(words);
            using var header = Structured(new[]
            {
                unchecked((uint)directoryWordOffset),
                unchecked((uint)directoryMask),
            });
            using var resolved = new ComputeBuffer(expected.Length, sizeof(uint),
                                                   ComputeBufferType.Structured);
            resolved.SetData(new uint[expected.Length]);

            shader.SetBuffer(kernel, "_BrickMaterials", materials);
            shader.SetBuffer(kernel, "_PersistentLookupHeader", header);
            shader.SetBuffer(kernel, "_ResolvedBrickCacheWrite", resolved);
            shader.SetInts("_ResolvedBrickCacheOrigin", -2, -2, -2);
            shader.SetInt("_ResolvedBrickCacheEdge", edge);
            shader.Dispatch(kernel, (expected.Length + 63) / 64, 1, 1);

            var actual = new uint[expected.Length];
            resolved.GetData(actual);
            CollectionAssert.AreEqual(expected, actual,
                "The isolated GPU resolver must preserve empty, uniform, and mixed packed entries exactly.");
        }

        private static void InsertDirectory(uint[] words, int wordOffset, int mask,
                                            int x, int y, int z, uint entry)
        {
            uint start = HashBrickCoordinate(x, y, z) & unchecked((uint)mask);
            for (uint probe = 0u; probe <= unchecked((uint)mask); probe++)
            {
                uint slot = (start + probe) & unchecked((uint)mask);
                int word = wordOffset + (int)slot * DirectoryWordsPerEntry;
                if (words[word + 4] != 0u) continue;
                words[word + 0] = unchecked((uint)x);
                words[word + 1] = unchecked((uint)y);
                words[word + 2] = unchecked((uint)z);
                words[word + 3] = entry;
                words[word + 4] = DirectoryOccupied;
                return;
            }
            Assert.Fail("Synthetic persistent directory unexpectedly filled.");
        }

        private static uint HashBrickCoordinate(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u;
                h ^= (uint)y * 0xd8163841u;
                h ^= (uint)z * 0xcb1ab31fu;
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                return h;
            }
        }

        private static ComputeBuffer Structured(uint[] data)
        {
            var buffer = new ComputeBuffer(data.Length, sizeof(uint), ComputeBufferType.Structured);
            buffer.SetData(data);
            return buffer;
        }
    }
}
