using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Uses the ShowcasePerformanceTests prefix so the existing isolated showcase performance
    /// shard executes these checks without mixing them into a broader scene-heavy process.
    /// </summary>
    public sealed class ShowcasePerformanceTestsDebrisAllocation
    {
        [Test]
        public void DebrisCpuBookkeepingUsesFixedValueStorage()
        {
            Type record = typeof(GpuDebrisSystem).GetNestedType(
                "ChunkRecord", BindingFlags.NonPublic);
            Assert.NotNull(record);
            Assert.True(record.IsValueType,
                "Per-debris ChunkRecord bookkeeping must stay in the fixed slot array; a reference "
              + "type allocates one managed object for every submitted debris chunk.");

            FieldInfo arguments = typeof(GpuDebrisSystem).GetField(
                "_drawArguments", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(arguments,
                "Indirect draw arguments must have persistent reusable CPU storage.");
            Assert.AreEqual(typeof(uint[]), arguments.FieldType);
        }

        [UnityTest]
        public IEnumerator WarmDrawArgumentRefreshDoesNotAllocatePerCall()
        {
            yield return null;

            var debris = new GpuDebrisSystem();
            try
            {
                if (!debris.Available)
                {
                    Assert.Ignore("Compute shaders are unavailable on this test device.");
                    yield break;
                }

                MethodInfo method = typeof(GpuDebrisSystem).GetMethod(
                    "UpdateDrawArguments", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(method);
                var refresh = (Action<GpuDebrisSystem>)method.CreateDelegate(
                    typeof(Action<GpuDebrisSystem>));

                // Warm Unity's managed/native bridge before measuring the recurring path.
                for (int i = 0; i < 16; i++) refresh(debris);

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 256; i++) refresh(debris);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.LessOrEqual(allocated, 1024L,
                    $"Refreshing indirect debris draw arguments allocated {allocated:N0} managed "
                  + "bytes over 256 warmed calls. The old per-call uint[5] allocation creates "
                  + "GC pressure during destruction bursts.");
            }
            finally
            {
                debris.Dispose();
            }
        }
    }
}
