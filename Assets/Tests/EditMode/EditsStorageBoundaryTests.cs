using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class EditsStorageBoundaryTests
    {
        [Test]
        public void DeterministicApplierUsesOnlyStorageMutationApi()
        {
            string root = FindRepoRoot();
            string path = Path.Combine(
                root, "Assets", "VoxelEngine", "Edits", "Runtime", "DeterministicAlterationApplier.cs");
            string source = StripComments(File.ReadAllText(path));
            string[] forbidden =
            {
                "RegionTable",
                "BrickPool",
                "BrickRef",
                "OccupancyMask",
                "VoxelDimensions",
                "VoxelEngine.Core.Storage",
                "VoxelEngine.Core.Occupancy",
            };

            foreach (string token in forbidden)
                Assert.Less(source.IndexOf(token, StringComparison.Ordinal), 0,
                    "Deterministic edits must not depend on physical Storage type: " + token);

            StringAssert.Contains("VoxelEngine.Storage.Api", source);
            StringAssert.Contains("IRegionMutationStore", source);
            StringAssert.Contains("VoxelBlockMutation", source);
        }

        [Test]
        public void LegacyPhysicalMutationSignaturesAreGoneFromNetAndTests()
        {
            string root = FindRepoRoot();
            string[] roots =
            {
                Path.Combine(root, "Assets", "VoxelEngine", "Net"),
                Path.Combine(root, "Assets", "Tests"),
            };
            string[] forbidden =
            {
                "DeterministicAlterationApplier.TryApply(ref table",
                "DeterministicAlterationApplier.HasRequiredResidency(ref table",
                "TryApplyAlteration(ref RegionTable table, ref BrickPool pool",
                "EventApplication.Apply(ref table",
                "EventApplication.Apply(ref tableA",
                "EventApplication.Apply(ref tableB",
            };
            var violations = new List<string>();

            foreach (string scanRoot in roots)
            foreach (string path in Directory.EnumerateFiles(scanRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(path) == nameof(EditsStorageBoundaryTests) + ".cs")
                    continue; // The guard necessarily contains the forbidden literals it searches for.

                string source = StripComments(File.ReadAllText(path));
                foreach (string token in forbidden)
                    if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                        violations.Add(Path.GetRelativePath(root, path) + " -> " + token);
            }

            Assert.IsEmpty(violations,
                "Callers must use IRegionMutationStore rather than the removed physical mutation signatures.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void UnusedPartialMaterializationRollsBackWithoutLeakingPoolSlot()
        {
            var table = new RegionTable(1, Allocator.Persistent);
            var pool = new BrickPool(16, Allocator.Persistent);
            try
            {
                table.LoadRegion(int3.zero);
                var storage = new RegionMutationStore(in table, in pool);

                Assert.IsTrue(storage.TryBeginPartialBlock(
                    int3.zero, 1, false, out VoxelBlockMutation mutation));
                Assert.IsTrue(mutation.IsCreated,
                    "Changing part of an empty uniform block must materialize a mutable payload.");
                Assert.AreEqual(1, pool.AllocatedCount);

                Assert.IsFalse(storage.CompletePartialBlock(ref mutation, false));
                Assert.AreEqual(0, pool.AllocatedCount,
                    "A materialized block with no actual voxel change must return its slot.");

                Assert.IsTrue(table.TryGetRegion(int3.zero, out Region region));
                Assert.IsTrue(region.GetBrick(0, 0, 0).IsEmpty,
                    "Rollback must restore the original uniform block reference.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void PartialMutationMaterializesThenCollapsesBackToUniform()
        {
            var table = new RegionTable(1, Allocator.Persistent);
            var pool = new BrickPool(16, Allocator.Persistent);
            try
            {
                table.LoadRegion(int3.zero);
                var storage = new RegionMutationStore(in table, in pool);

                Assert.IsTrue(storage.TryBeginPartialBlock(
                    int3.zero, 7, false, out VoxelBlockMutation build));
                Assert.IsTrue(build.SetMaterial(0, 7));
                Assert.IsTrue(storage.CompletePartialBlock(ref build, true));
                Assert.AreEqual(1, pool.AllocatedCount,
                    "One edited voxel keeps the logical block mixed.");

                Assert.IsTrue(storage.TryBeginPartialBlock(
                    int3.zero, VoxelGrid.MaterialEmpty, false, out VoxelBlockMutation clear));
                Assert.IsTrue(clear.SetMaterial(0, VoxelGrid.MaterialEmpty));
                Assert.IsTrue(storage.CompletePartialBlock(ref clear, true));
                Assert.AreEqual(0, pool.AllocatedCount,
                    "Returning every voxel to one material must collapse and free mixed storage.");

                Assert.IsTrue(table.TryGetRegion(int3.zero, out Region region));
                Assert.IsTrue(region.GetBrick(0, 0, 0).IsEmpty);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void HardSurfaceOnlyChangeCommitsWithoutMaterializingBlock()
        {
            var table = new RegionTable(1, Allocator.Persistent);
            var pool = new BrickPool(16, Allocator.Persistent);
            try
            {
                table.LoadRegion(int3.zero);
                var storage = new RegionMutationStore(in table, in pool);

                Assert.IsTrue(storage.TryBeginPartialBlock(
                    int3.zero, VoxelGrid.MaterialEmpty, true, out VoxelBlockMutation mutation));
                Assert.IsFalse(mutation.IsCreated,
                    "Writing the existing uniform material must not allocate mixed storage.");
                Assert.IsTrue(mutation.MetadataChanged);
                Assert.IsTrue(storage.CompletePartialBlock(ref mutation, false));
                Assert.AreEqual(0, pool.AllocatedCount);

                Assert.IsTrue(table.TryGetRegion(int3.zero, out Region region));
                Assert.IsTrue(region.IsHardSurfaceBrick(0));
                Assert.IsTrue(region.Dirty,
                    "A semantic hard-surface change is authoritative even without material change.");
                Assert.IsTrue(region.GetBrick(0, 0, 0).IsEmpty);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        private static string StripComments(string source)
        {
            source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(source, @"//.*?$", string.Empty, RegexOptions.Multiline);
        }

        private static string FindRepoRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Assets")))
                directory = directory.Parent;
            Assert.NotNull(directory, "Could not locate project root containing Assets/.");
            return directory.FullName;
        }
    }
}
