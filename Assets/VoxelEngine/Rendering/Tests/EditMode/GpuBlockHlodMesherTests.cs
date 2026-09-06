using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using Object = UnityEngine.Object;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuBlockHlodMesherTests
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Vertex { public Vector3 Position, Normal; public uint Material, Active; }
        private ComputeShader _summaryShader, _meshShader, _arenaShader;
        private GpuVoxelBrickMirror _mirror;
        private GpuSurfacePageArena _arena;
        private ComputeBuffer _requests, _summaries, _descriptors, _counters;
        private int _edge, _handle;
        private const ulong Generation = 0x1234567800000001UL;
        private static readonly int3 OriginBrick = new(-2, 3, -4);

        [TearDown]
        public void TearDown()
        {
            _requests?.Dispose(); _summaries?.Dispose(); _descriptors?.Dispose(); _counters?.Dispose();
            _mirror?.Dispose(); _arena?.Dispose();
            if (_summaryShader != null) Object.DestroyImmediate(_summaryShader);
            if (_meshShader != null) Object.DestroyImmediate(_meshShader);
            if (_arenaShader != null) Object.DestroyImmediate(_arenaShader);
        }

        private void Setup(int edge = 1, bool unknownHalo = false)
        {
            Assert.That(SystemInfo.supportsComputeShaders, Is.True);
            _edge = edge;
            _summaryShader = Object.Instantiate(Resources.Load<ComputeShader>("GpuBlockHlodSummary"));
            _meshShader = Object.Instantiate(Resources.Load<ComputeShader>("GpuBlockHlodMesher"));
            _arenaShader = Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfacePageArena"));
            _mirror = new GpuVoxelBrickMirror(16);
            _arena = new GpuSurfacePageArena(_arenaShader, 4096, 8192, 2);
            Assert.That(_arena.TryAcquireHandle(out _handle), Is.True);
            _arena.QueueGeneration(_handle, Generation);
            _arena.FlushHandleCommands(1);
            int padded = edge + 2, sources = padded * padded * padded;
            _requests = new ComputeBuffer(sources, 16);
            _summaries = new ComputeBuffer(sources * GpuBlockHlodSummary.WordsPerBlock, 4);
            var requests = new int4[sources];
            for (int z = 0; z < padded; z++)
            for (int y = 0; y < padded; y++)
            for (int x = 0; x < padded; x++)
                requests[x + padded * (y + padded * z)] = new int4(
                    OriginBrick + new int3(x - 1, y - 1, z - 1), unknownHalo ? 0 : 1);
            _requests.SetData(requests);
            _descriptors = new ComputeBuffer(1, GpuSurfaceExtractor.BatchChunkDescriptor.Stride);
            _descriptors.SetData(new[] { new GpuSurfaceExtractor.BatchChunkDescriptor
            {
                OriginX = OriginBrick.x * 8, OriginY = OriginBrick.y * 8, OriginZ = OriginBrick.z * 8,
                SourceStep = 8, VoxelSize = 0.1f, Handle = (uint)_handle,
                GenerationLow = unchecked((uint)Generation), GenerationHigh = (uint)(Generation >> 32)
            } });
            _counters = new ComputeBuffer(21, 4);
        }

        private void Uniform(int3 local, byte material = 200) => _mirror.Publish(
            VoxelBrickDelta.UniformAt(OriginBrick + local, 1, material), default, default, default, 0, false);

        private uint[] Build(bool write = true, bool split = false)
        {
            GpuBlockHlodSummary.Dispatch(_summaryShader, _mirror, _requests, _summaries,
                _requests.count, (1u << 11) | (1u << 16));
            int bricks = _edge * _edge * _edge;
            int slice = split ? 1 : bricks;
            for (int start = 0; start < bricks; start += slice)
                GpuBlockHlodMesher.Count(_meshShader, _summaries, _descriptors, _counters,
                    _edge, 1, start, slice);
            _arena.AllocateBatch(_descriptors, _counters, 1, 17, 1);
            if (write)
                for (int start = 0; start < bricks; start += slice)
                    GpuBlockHlodMesher.Write(_meshShader, _summaries, _descriptors, _counters,
                        _arena, _edge, 1, start, slice);
            _arena.PublishBatch(_descriptors, _counters, 1, 17, 1);
            var words = new uint[21];
            // Test-only observation after ordered completion. Production keeps all geometry on GPU.
            _counters.GetData(words);
            return words;
        }

        [Test]
        public void UniformBrickProducesClosedOutwardBoxInPagedArena()
        {
            Setup(); Uniform(int3.zero);
            uint[] words = Build();
            VerifyBox(words, int3.zero, new int3(8), 6, 200);
            Assert.That(ReadReady(_arena.LiveChunkGeometry), Is.Zero, "Write must not auto-publish.");
            _arena.CommitPending(_handle, Generation, 1);
            Assert.That(ReadReady(_arena.LiveChunkGeometry), Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdjacentBricksSuppressSharedFacesAcrossDispatchSlices(bool split)
        {
            Setup(2); Uniform(int3.zero); Uniform(new int3(1, 0, 0));
            VerifyBox(Build(split: split), int3.zero, new int3(16, 8, 8), 10, 200);
        }

        [Test]
        public void IsolatedVoxelKeepsFeaturePreservingSubcellBounds()
        {
            Setup();
            using var owned = new NativeArray<byte>(512, Allocator.Temp);
            var voxels = owned;
            voxels[7 + 8 * (7 + 8 * 7)] = 3;
            using var semantics = new NativeArray<ushort>(512, Allocator.Temp);
            using var boundaries = new NativeArray<byte>(512, Allocator.Temp);
            _mirror.Publish(VoxelBrickDelta.MixedAt(OriginBrick, 1, 0), voxels, semantics, boundaries, 0, true);
            VerifyBox(Build(), new int3(6), new int3(8), 6, 3);
        }

        [Test]
        public void ProductionExtractorRoutesStepEightThroughDenseSummaryAndPagedWrite()
        {
            Setup(); Uniform(int3.zero);
            _mirror.FlushPendingUploads();
            ComputeShader shader = Object.Instantiate(Resources.Load<ComputeShader>("VoxelBrickMesher"));
            try
            {
                using var extractor = new GpuSurfaceExtractor(shader, 1, 1, 3);
                using var tables = GpuTransvoxelTables.CreateDefault();
                using var resources = extractor.CreateCountBatchResources(1);
                var requests = new[] { new GpuChunkExtraction(OriginBrick * 8,
                    OriginBrick - 1, 8, 0.1f, handle: _handle, generation: Generation) };
                extractor.DispatchCountBatch(_mirror, tables, requests, 1, _counters, resources);
                extractor.PrefixCountBatch(_counters, 1, 1, 1);
                _arena.AllocateBatch(resources.Chunks, _counters, 1, 17, 1);
                extractor.DispatchBaseWriteBatch(_mirror, tables, 1, _counters, resources,
                    _arena.Vertices, _arena.Indices, pageArena: _arena, frame: 1);
                var words = new uint[21]; _counters.GetData(words);
                VerifyBox(words, int3.zero, new int3(8), 6, 200);
            }
            finally { Object.DestroyImmediate(shader); }
        }

        [TestCase(4, 0u, 0u, 0u, 0u, true)]
        [TestCase(4, 1u, 0u, 0u, 0u, false)]
        [TestCase(4, 0u, 4u, 6u, 0u, false)]
        [TestCase(4, 0u, 0u, 0u, 1u, false)]
        [TestCase(2, 0u, 0u, 0u, 0u, false)]
        public void FallbackDecisionPreservesExistingGeometryProfilesAndErrors(
            int step, uint profiles, uint vertices, uint indices, uint unsupported, bool selected)
        {
            Setup(); Uniform(int3.zero);
            var descriptors = new GpuSurfaceExtractor.BatchChunkDescriptor[1];
            _descriptors.GetData(descriptors);
            descriptors[0].SourceStep = step; descriptors[0].ProfileCount = profiles;
            _descriptors.SetData(descriptors);
            var words = new uint[21]; words[4] = unsupported; words[6] = vertices; words[7] = indices;
            _counters.SetData(words);
            using var selection = new ComputeBuffer(1, 4);
            GpuBlockHlodMesher.SelectFallback(_meshShader, _descriptors, _counters, selection, 1);
            GpuBlockHlodSummary.Dispatch(_summaryShader, _mirror, _requests, _summaries, _requests.count, 0);
            GpuBlockHlodMesher.Count(_meshShader, _summaries, _descriptors, _counters, 1, 1, 0, 1, selection);
            _counters.GetData(words);
            Assert.That(words[4], Is.EqualTo(unsupported));
            Assert.That(words[6], Is.EqualTo(selected ? 24u : vertices));
            Assert.That(words[7], Is.EqualTo(selected ? 36u : indices));
        }

        [Test]
        public void ReusedStepFourLaneReplacesThinFeatureWithOrdinaryGeometryThenAir()
        {
            Setup();
            ComputeShader shader = Object.Instantiate(Resources.Load<ComputeShader>("VoxelBrickMesher"));
            try
            {
                using var extractor = new GpuSurfaceExtractor(shader, 2, 1, 3);
                using var tables = GpuTransvoxelTables.CreateDefault();
                using var resources = extractor.CreateCountBatchResources(1);
                using var owned = new NativeArray<byte>(512, Allocator.Temp);
                using var semantics = new NativeArray<ushort>(512, Allocator.Temp);
                using var boundaries = new NativeArray<byte>(512, Allocator.Temp);
                var voxels = owned; voxels[1 + 8 * (1 + 8)] = 7;
                var requests = new[] { new GpuChunkExtraction(OriginBrick * 8, OriginBrick - 1, 4, 0.1f) };
                for (int phase = 0; phase < 3; phase++)
                {
                    if (phase == 0)
                        _mirror.Publish(VoxelBrickDelta.MixedAt(OriginBrick, 1, 0), voxels, semantics, boundaries, 0, true);
                    else
                        _mirror.Publish(phase == 1 ? VoxelBrickDelta.UniformAt(OriginBrick, 2, 7)
                            : VoxelBrickDelta.EmptyAt(OriginBrick, 3), default, default, default, 0, false);
                    extractor.DispatchCountBatch(_mirror, tables, requests, 1, _counters, resources);
                    var selected = new uint[1]; resources.HlodSelection.GetData(selected);
                    var words = new uint[21]; _counters.GetData(words);
                    Assert.That(words[4], Is.Zero);
                    Assert.That(selected[0], Is.EqualTo(phase == 1 ? 0u : 1u), $"phase {phase}");
                    if (phase == 2)
                        Assert.That(words[6] | words[7], Is.Zero, "Empty sources must not retain the previous fallback.");
                    else
                    {
                        Assert.That(words[6], Is.GreaterThan(0));
                        Assert.That(words[7], Is.GreaterThan(0));
                    }
                }
            }
            finally { Object.DestroyImmediate(shader); }
        }

        [Test]
        public void StepFourThinVoxelSurvivesProductionGpuCountAndPagedWrite()
        {
            Setup();
            using var voxels = new NativeArray<byte>(512, Allocator.Temp);
            using var semantics = new NativeArray<ushort>(512, Allocator.Temp);
            using var boundaries = new NativeArray<byte>(512, Allocator.Temp);
            var writable = voxels;
            writable[1 + 8 * (1 + 8)] = 7;
            _mirror.Publish(VoxelBrickDelta.MixedAt(OriginBrick, 1, 0), voxels, semantics, boundaries, 0, true);
            ComputeShader shader = Object.Instantiate(Resources.Load<ComputeShader>("VoxelBrickMesher"));
            try
            {
                using var extractor = new GpuSurfaceExtractor(shader, 2, 1, 3);
                using var tables = GpuTransvoxelTables.CreateDefault();
                using var resources = extractor.CreateCountBatchResources(1);
                var requests = new[] { new GpuChunkExtraction(OriginBrick * 8,
                    OriginBrick - 1, 4, 0.1f, handle: _handle, generation: Generation) };
                extractor.DispatchCountBatch(_mirror, tables, requests, 1, _counters, resources);
                var selected = new uint[1]; resources.HlodSelection.GetData(selected);
                Assert.That(selected[0], Is.EqualTo(1), "Fixture must exercise the GPU false-empty branch.");
                extractor.PrefixCountBatch(_counters, 1, 1, 1);
                _arena.AllocateBatch(resources.Chunks, _counters, 1, 17, 1);
                extractor.DispatchBaseWriteBatch(_mirror, tables, 1, _counters, resources,
                    _arena.Vertices, _arena.Indices, pageArena: _arena, frame: 1);
                var words = new uint[21]; _counters.GetData(words);
                VerifyBox(words, int3.zero, new int3(2), 6, 7);
            }
            finally { Object.DestroyImmediate(shader); }
        }

        [Test]
        public void UnknownHaloRejectsCandidateWithoutAllocatingDrawableGeometry()
        {
            Setup(unknownHalo: true); Uniform(int3.zero);
            uint[] words = Build();
            Assert.That(words[4], Is.Not.Zero);
            Assert.That(words[14], Is.EqualTo(4));
            Assert.That(ReadReady(_arena.PendingChunkGeometry), Is.Zero);
            Assert.That(ReadReady(_arena.LiveChunkGeometry), Is.Zero);
        }

        [Test]
        public void MissingWriteFailsExistingArenaFinalization()
        {
            Setup(); Uniform(int3.zero);
            uint[] words = Build(write: false);
            Assert.That(words[14], Is.EqualTo(5));
            Assert.That(ReadReady(_arena.PendingChunkGeometry), Is.Zero);
            Assert.That(ReadReady(_arena.LiveChunkGeometry), Is.Zero);
        }

        [Test]
        public void KnownAirFinalizesAnEmptyCandidate()
        {
            Setup();
            uint[] words = Build();
            Assert.That(words[14], Is.Zero);
            Assert.That(words[6] | words[7] | words[12] | words[13], Is.Zero);
        }

        private uint ReadReady(ComputeBuffer buffer)
        {
            // Read one 32-byte record without reinterpreting the buffer's element stride.
            var records = new GeometryRecord[2]; buffer.GetData(records);
            return records[_handle].Ready;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct GeometryRecord
        {
            public uint Low, High, Bank, Vertices, Indices, VertexPages, IndexPages, Ready;
        }

        private void VerifyBox(uint[] words, int3 min, int3 max, int quads, uint material)
        {
            Assert.That(words[14], Is.Zero, string.Join(",", words));
            Assert.That(words[6], Is.EqualTo(quads * 4));
            Assert.That(words[7], Is.EqualTo(quads * 6));
            Assert.That(words[12], Is.EqualTo(words[6]));
            Assert.That(words[13], Is.EqualTo(words[7]));
            var vertices = new Vertex[_arena.Vertices.count]; _arena.Vertices.GetData(vertices);
            var indices = new uint[_arena.Indices.count]; _arena.Indices.GetData(indices);
            var pages = new uint[_arena.IndexPageTable.count]; _arena.IndexPageTable.GetData(pages);
            var vertexPages = new uint[_arena.VertexPageTable.count]; _arena.VertexPageTable.GetData(vertexPages);
            int vertexTable = (_handle * 2 + (int)words[18]) * GpuSurfacePageArena.MaxVertexPagesPerChunk;
            int table = (_handle * 2 + (int)words[18]) * GpuSurfacePageArena.MaxIndexPagesPerChunk;
            Vector3 lower = (Vector3)(float3)(OriginBrick * 8 + min) * 0.1f;
            Vector3 upper = (Vector3)(float3)(OriginBrick * 8 + max) * 0.1f;
            Vector3 centre = (lower + upper) * 0.5f;
            float area = 0;
            for (int i = 0; i < quads * 6; i += 3)
            {
                Vertex[] triangle = new Vertex[3];
                for (int k = 0; k < 3; k++)
                {
                    int local = i + k;
                    uint slot = pages[table + local / GpuSurfacePageArena.IndexPageSize]
                        * GpuSurfacePageArena.IndexPageSize + (uint)(local % GpuSurfacePageArena.IndexPageSize);
                    uint localVertex = indices[slot];
                    Assert.That(localVertex, Is.LessThan(words[6]), "Draw indices must be chunk-local.");
                    uint physicalVertex = vertexPages[vertexTable + localVertex / GpuSurfacePageArena.VertexPageSize]
                        * GpuSurfacePageArena.VertexPageSize + localVertex % GpuSurfacePageArena.VertexPageSize;
                    triangle[k] = vertices[physicalVertex];
                    Assert.That(triangle[k].Material, Is.EqualTo(material));
                    Assert.That(triangle[k].Active, Is.EqualTo(0xFF00u));
                    for (int axis = 0; axis < 3; axis++)
                        Assert.That(triangle[k].Position[axis], Is.InRange(lower[axis] - 0.0001f, upper[axis] + 0.0001f));
                }
                Vector3 cross = Vector3.Cross(triangle[1].Position - triangle[0].Position,
                    triangle[2].Position - triangle[0].Position);
                Assert.That(Vector3.Dot(cross, triangle[0].Normal), Is.GreaterThan(0.00001f));
                Assert.That(Vector3.Dot(triangle[0].Position - centre, triangle[0].Normal), Is.GreaterThan(0));
                area += cross.magnitude * 0.5f;
            }
            Vector3 size = upper - lower;
            Assert.That(area, Is.EqualTo(2 * (size.x * size.y + size.y * size.z + size.z * size.x)).Within(0.0001f));
        }
    }
}
