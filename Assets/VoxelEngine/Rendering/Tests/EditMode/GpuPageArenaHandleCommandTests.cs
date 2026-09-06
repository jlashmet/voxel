using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    /// <summary>
    /// Exercises the production arena and its real command transport. Readback is test-only.
    /// These bounded host-ownership tests do not certify pending-page or in-flight draw lifetime.
    /// </summary>
    public sealed class GpuPageArenaHandleCommandTests
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Command
        {
            public uint Handle;
            public uint Low;
            public uint High;
            public uint Release;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Generation
        {
            public uint Low;
            public uint High;
            public ulong Value => ((ulong)High << 32) | Low;
        }

        private ComputeShader _shader;
        private GpuSurfacePageArena _arena;

        [SetUp]
        public void SetUp()
        {
            Assert.That(SystemInfo.supportsComputeShaders, Is.True,
                "This GPU regression requires a real compute device, not a skipped test.");
            ComputeShader asset = Resources.Load<ComputeShader>("GpuSurfacePageArena");
            Assert.That(asset, Is.Not.Null);
            _shader = UnityEngine.Object.Instantiate(asset);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (_arena != null)
                {
                    // Drain the actual test dispatch before releasing its resources. Never add
                    // this blocking test readback to the production presentation path.
                    var generations = new Generation[_arena.HandleCapacity];
                    _arena.DesiredGenerations.GetData(generations);
                }
            }
            finally
            {
                _arena?.Dispose();
                _arena = null;
                if (_shader != null) UnityEngine.Object.DestroyImmediate(_shader);
                _shader = null;
            }
        }

        private GpuSurfacePageArena Create(int handles)
        {
            _arena = new GpuSurfacePageArena(_shader,
                GpuSurfacePageArena.VertexPageSize,
                GpuSurfacePageArena.IndexPageSize, handles);
            return _arena;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DuplicateReleaseReturnsEachHandleOnlyOnce(bool flushBetweenReleases)
        {
            var arena = Create(1);
            Assert.That(arena.TryAcquireHandle(out int original), Is.True);
            arena.QueueRelease(original, 7UL);
            if (flushBetweenReleases) arena.FlushHandleCommands(1);
            arena.QueueRelease(original, 7UL);
            arena.FlushHandleCommands(2);

            Assert.That(arena.TryAcquireHandle(out int reused), Is.True);
            Assert.That(reused, Is.EqualTo(original));
            Assert.That(arena.TryAcquireHandle(out _), Is.False,
                "One release must not create two owners of the same GPU handle.");
        }

        [Test]
        public void InterleavedGenerationsUploadOneLatestCommandPerHandle()
        {
            var arena = Create(2);
            Assert.That(arena.TryAcquireHandle(out int first), Is.True);
            Assert.That(arena.TryAcquireHandle(out int second), Is.True);
            const ulong latest = 0x12345678abcdef01UL;
            arena.QueueGeneration(first, 1UL);
            arena.QueueGeneration(second, 19UL);
            arena.QueueGeneration(first, latest);
            arena.FlushHandleCommands(3);

            // Reading the real transport catches the race deterministically even when separate
            // GPU threads happen to write the desired-generation table in a favourable order.
            var commands = new Command[2];
            arena.HandleCommands.GetData(commands, 0, 0, commands.Length);
            Assert.That(commands[0].Handle, Is.EqualTo((uint)first));
            Assert.That(commands[0].Low, Is.EqualTo(0xabcdef01u));
            Assert.That(commands[0].High, Is.EqualTo(0x12345678u));
            Assert.That(commands[0].Release, Is.Zero);
            Assert.That(commands[1].Handle, Is.EqualTo((uint)second));
            Assert.That(commands[1].Low, Is.EqualTo(19u));

            var generations = new Generation[2];
            arena.DesiredGenerations.GetData(generations);
            Assert.That(generations[first].Value, Is.EqualTo(latest));
            Assert.That(generations[second].Value, Is.EqualTo(19UL));
        }

        [Test]
        public void QueuedReleaseCannotBeOverwrittenByAGenerationCommand()
        {
            var arena = Create(1);
            Assert.That(arena.TryAcquireHandle(out int handle), Is.True);
            arena.QueueGeneration(handle, 1UL);
            arena.QueueGeneration(handle, 2UL);
            arena.QueueRelease(handle, 3UL);
            Assert.Throws<InvalidOperationException>(() => arena.QueueGeneration(handle, 4UL),
                "A generation update cannot cancel required release cleanup.");
            arena.FlushHandleCommands(4);

            var commands = new Command[1];
            arena.HandleCommands.GetData(commands, 0, 0, 1);
            Assert.That(commands[0].Release, Is.EqualTo(1u));
            Assert.That(commands[0].Low, Is.EqualTo(3u));
            Assert.That(arena.TryAcquireHandle(out int reused), Is.True);
            Assert.That(reused, Is.EqualTo(handle));
            Assert.That(arena.TryAcquireHandle(out _), Is.False);
        }

        [Test]
        public void GenerationRequiresAnAcquiredHandle()
        {
            var arena = Create(1);
            Assert.Throws<InvalidOperationException>(() => arena.QueueGeneration(0, 1UL));
            Assert.That(arena.TryAcquireHandle(out int handle), Is.True);
            arena.QueueRelease(handle, 2UL);
            arena.FlushHandleCommands(5);
            Assert.Throws<InvalidOperationException>(() => arena.QueueGeneration(handle, 3UL));
        }

        [Test]
        public void SerialReleaseAndReacquirePreservesCapacityAcrossFlushes()
        {
            var arena = Create(1);
            for (int cycle = 0; cycle < 32; cycle++)
            {
                Assert.That(arena.TryAcquireHandle(out int handle), Is.True, $"cycle {cycle}");
                Assert.That(arena.TryAcquireHandle(out _), Is.False);
                ulong generation = (ulong)(cycle + 1) << 32;
                arena.QueueGeneration(handle, generation);
                arena.FlushHandleCommands(cycle * 2 + 6);
                var observed = new Generation[1];
                arena.DesiredGenerations.GetData(observed);
                Assert.That(observed[0].Value, Is.EqualTo(generation));
                arena.QueueRelease(handle, generation);
                arena.FlushHandleCommands(cycle * 2 + 7);
            }
            Assert.That(arena.TryAcquireHandle(out _), Is.True);
            Assert.That(arena.TryAcquireHandle(out _), Is.False);
        }

        [Test]
        public void CapacityFlushKeepsEveryDistinctHandleAndGeneration()
        {
            // Exceeds the current 1,024-command transport without changing production capacity.
            const int count = 1030;
            var arena = Create(count);
            var handles = new int[count];
            for (int i = 0; i < count; i++)
            {
                Assert.That(arena.TryAcquireHandle(out handles[i]), Is.True);
                arena.QueueGeneration(handles[i], ((ulong)(i + 1) << 32) | (uint)i);
            }
            arena.FlushHandleCommands(80);
            var generations = new Generation[count];
            arena.DesiredGenerations.GetData(generations);
            for (int i = 0; i < count; i++)
                Assert.That(generations[handles[i]].Value,
                    Is.EqualTo(((ulong)(i + 1) << 32) | (uint)i), $"handle {handles[i]}");

            for (int i = 0; i < count; i++) arena.QueueRelease(handles[i], (ulong)i + 1);
            arena.FlushHandleCommands(81);
            var acquired = new System.Collections.Generic.HashSet<int>();
            while (arena.TryAcquireHandle(out int handle))
            {
                Assert.That(acquired.Add(handle), Is.True, $"duplicate handle {handle}");
                Assert.That(acquired.Count, Is.LessThanOrEqualTo(count));
            }
            Assert.That(acquired.Count, Is.EqualTo(count));
        }
    }
}
