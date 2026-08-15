using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class StructuresStorageBoundaryTests
    {
        [Test]
        public void FeatureRasterisingUsesStorageApiInsteadOfPhysicalStorage()
        {
            string root = FindRepoRoot();
            string features = Path.Combine(root, "Assets", "VoxelEngine", "Core", "Features");
            string[] files = { "PrimitiveRasteriser.cs", "FeatureGeneration.cs" };
            string[] forbidden =
            {
                "RegionTable",
                "BrickPool",
                "BrickRef",
                "VoxelAccess",
                "VoxelDimensions.",
                "VoxelEngine.Core.Storage",
            };
            var violations = new List<string>();

            foreach (string file in files)
            {
                string source = File.ReadAllText(Path.Combine(features, file));
                foreach (string token in forbidden)
                {
                    if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                        violations.Add(file + " -> " + token);
                }
            }

            Assert.IsEmpty(violations,
                "Feature authoring hot paths must consume Storage.Api and must not reacquire " +
                "physical table/pool vocabulary.\n\n" + string.Join("\n", violations));
        }

        [Test]
        public void FeatureCallersDoNotUseRemovedTablePoolSignatures()
        {
            string root = FindRepoRoot();
            string self = Path.GetFullPath(Path.Combine(
                root, "Assets", "Tests", "EditMode", "StructuresStorageBoundaryTests.cs"));
            string[] scanRoots =
            {
                Path.Combine(root, "Assets", "Scenes"),
                Path.Combine(root, "Assets", "VoxelEngine", "CI"),
                Path.Combine(root, "Assets", "Tests"),
            };
            var raster = new Regex(
                @"PrimitiveRasteriser\.Rasterise\([\s\S]{0,400}?ref\s+\w+\s*,\s*ref\s+\w+\s*\)",
                RegexOptions.CultureInvariant);
            var generation = new Regex(
                @"FeatureGeneration\.GenerateRegion\([\s\S]{0,300}?ref\s+\w+\s*,\s*ref\s+\w+\s*\)",
                RegexOptions.CultureInvariant);
            var violations = new List<string>();

            foreach (string scanRoot in scanRoots)
            {
                if (!Directory.Exists(scanRoot)) continue;
                foreach (string path in Directory.EnumerateFiles(scanRoot, "*.cs", SearchOption.AllDirectories))
                {
                    if (string.Equals(Path.GetFullPath(path), self, StringComparison.Ordinal)) continue;
                    string source = File.ReadAllText(path);
                    if (raster.IsMatch(source))
                        violations.Add(Path.GetRelativePath(root, path) + " -> legacy rasteriser table/pool signature");
                    if (generation.IsMatch(source))
                        violations.Add(Path.GetRelativePath(root, path) + " -> legacy feature-generation table/pool signature");
                }
            }

            Assert.IsEmpty(violations,
                "Feature callers must pass Storage.Api capabilities instead of keeping removed " +
                "table/pool signatures alive.\n\n" + string.Join("\n", violations));
        }

        [Test]
        public void FullCellAuthoringCreatesMissingRegionLikeLegacyCellWrites()
        {
            var table = new RegionTable(4, Allocator.Temp);
            var pool = new BrickPool(32, Allocator.Temp);
            try
            {
                var store = new RegionMutationStore(in table, in pool);
                int3 worldBlock = new int3(VoxelReadGrid.BlocksPerRegionEdge, 0, 0);
                int3 regionCoord = new int3(1, 0, 0);
                Assert.False(table.IsResident(regionCoord));

                Assert.True(store.TryBeginCellBlock(worldBlock, false, out VoxelBlockMutation mutation));
                var cell = new VoxelCell
                {
                    BaseMaterialId = 7,
                    Surface = new VoxelSurfaceSemantics { StyleId = SurfaceStyles.Planar },
                };
                Assert.True(mutation.SetCell(0, in cell));
                Assert.True(store.CompletePartialBlock(ref mutation, true));

                Assert.True(table.IsResident(regionCoord),
                    "The first authored cell must make its containing region resident.");
                VoxelCell stored = VoxelAccess.GetCell(
                    ref table, in pool, new int3(VoxelGrid.RegionVoxelEdge, 0, 0));
                Assert.AreEqual(cell, stored);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
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