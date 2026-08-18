using System;
using System.IO;
using NUnit.Framework;
using VoxelEngine.Composition;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class StorageAllocationSafetyTests
    {
        [Test]
        public void PathologicalMixedBrickRequestIsBoundedBeforeAllocation()
        {
            int bounded = VoxelEngineBootstrap.ClampMixedBrickCapacityToBudget(
                int.MaxValue,
                VoxelEngineBootstrap.MaximumMixedBrickAllocationBytes,
                minimumCapacity: 1);

            Assert.Less(bounded, 262_144,
                "the shared storage ceiling must keep one eager BrickPool well below the former stress-test request");
            Assert.Greater(bounded, 65_536,
                "the safety ceiling should preserve headroom above the normal showcase pool size");
        }

        [Test]
        public void StorageLifetimeAppliesBudgetBeforeConstructingBrickPool()
        {
            string source = File.ReadAllText(
                "Assets/VoxelEngine/Composition/VoxelEngineBootstrap.cs");

            int lifetime = source.IndexOf(
                "public StorageRuntimeLifetime(", StringComparison.Ordinal);
            int nextMember = source.IndexOf(
                "internal ref RegionTable Table", lifetime, StringComparison.Ordinal);
            Assert.GreaterOrEqual(lifetime, 0);
            Assert.Greater(nextMember, lifetime);

            string constructor = source.Substring(lifetime, nextMember - lifetime);
            int clamp = constructor.IndexOf(
                "boundedMixedBrickCapacity = ClampMixedBrickCapacityToBudget(",
                StringComparison.Ordinal);
            int allocation = constructor.IndexOf(
                "new BrickPool(boundedMixedBrickCapacity, Allocator.Persistent)",
                StringComparison.Ordinal);

            Assert.GreaterOrEqual(clamp, 0,
                "all storage lifetime paths must apply the shared memory ceiling");
            Assert.Greater(allocation, clamp,
                "BrickPool must receive only the bounded capacity, never the raw caller request");
            StringAssert.DoesNotContain(
                "new BrickPool(mixedBrickCapacity, Allocator.Persistent)", constructor,
                "a direct eager allocation would re-open the machine-freeze failure mode");
        }
    }
}
