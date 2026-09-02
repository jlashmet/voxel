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

            const int edge = 4;
            var origins = new[] { new Vector3Int(-2, -2, -2) };
            uint[] actual = Resolve(origins, edge, out uint[] expected);

            CollectionAssert.AreEqual(expected, actual,
                "The isolated GPU resolver must preserve empty, uniform, and mixed packed entries exactly.");
        }

        [Test]
        public void BatchedPersistentDirectoryResolvesIndependentDenseSlicesInOneDispatch()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device.");

            const int edge = 4;
            var origins = new[]
            {
                new Vector3Int(-2, -2, -2),
                new Vector3Int(5, -1, 7),
            };
            uint[] actual = Resolve(origins, edge, out uint[] expected);

            CollectionAssert.AreEqual(expected, actual,
                "A batched resolver dispatch must preserve each request's origin and write only its dense-cache slice.");
        }

        private static uint[] Resolve(Vector3Int[] origins, int edge, out uint[] expected)
        {
            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Resolver shader missing at {ShaderPath}");
            int kernel = shader.FindKernel("CSResolveBrickCache");

            const int directoryCapacity = 256;
            const int directoryMask = directoryCapacity - 1;
            const int directoryWordOffset = 8;
            int sliceLength = edge * edge * edge;
            var words = new uint[directoryWordOffset
                               + directoryCapacity * DirectoryWordsPerEntry];
            expected = new uint[sliceLength * origins.Length];
            var requests = new Vector4Int[origins.Length];

            for (int requestIndex = 0; requestIndex < origins.Length; requestIndex++)
            {
                Vector3Int origin = origins[requestIndex];
                int sliceBase = requestIndex * sliceLength;
                requests[requestIndex] = new Vector4Int(origin.x, origin.y, origin.z, sliceBase);

                int cursor = 0;
                for (int z = 0; z < edge; z++)
                for (int y = 0; y < edge; y++)
                for (int x = 0; x < edge; x++)
                {
                    int worldX = origin.x + x;
                    int worldY = origin.y + y;
                    int worldZ = origin.z + z;
                    uint entry;
                    if (((worldX + worldY + worldZ) & 3) == 0)
                        entry = 0u;
                    else if ((worldY & 1) == 0)
                        entry = 1u | ((uint)(1 + ((worldX - worldZ) & 7)) << 8);
                    else
                        entry = 2u | ((uint)(17 + sliceBase + cursor) << 16);

                    expected[sliceBase + cursor++] = entry;
                    InsertDirectory(words, directoryWordOffset, directoryMask,
                                    worldX, worldY, worldZ, entry);
                }
            }

            using var materials = Structured(words);
            using var header = Structured(new[]
            {
                unchecked((uint)directoryWordOffset),
                unchecked((uint)directoryMask),
            });
            using var requestBuffer = new ComputeBuffer(requests.Length, sizeof(int) * 4,
                                                        ComputeBufferType.Structured);
            requestBuffer.SetData(requests);
            using var resolved = new ComputeBuffer(expected.Length, sizeof(uint),
                                                   ComputeBufferType.Structured);
            resolved.SetData(new uint[expected.Length]);

            shader.SetBuffer(kernel, "_BrickMaterials", materials);
            shader.SetBuffer(kernel, "_PersistentLookupHeader", header);
            shader.SetBuffer(kernel, "_ResolvedBrickCacheRequests", requestBuffer);
            shader.SetBuffer(kernel, "_ResolvedBrickCacheWrite", resolved);
            shader.SetInt("_ResolvedBrickCacheEdge", edge);
            shader.SetInt("_ResolvedBrickCacheRequestCount", requests.Length);
            shader.Dispatch(kernel, (sliceLength + 63) / 64, requests.Length, 1);

            var actual = new uint[expected.Length];
            resolved.GetData(actual);
            return actual;
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
