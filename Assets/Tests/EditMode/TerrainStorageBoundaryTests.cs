using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
using VoxelEngine.Terrain.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class TerrainStorageBoundaryTests
    {
        // TerrainGenerator takes the material set explicitly: the engine generates from opaque
        // indices and the game owns their meaning. Matches GameTerrainMaterials.Default,
        // duplicated because this is an engine test assembly.
        private static readonly VoxelEngine.Terrain.Api.TerrainMaterialSet BoundaryTerrainMaterials =
            new VoxelEngine.Terrain.Api.TerrainMaterialSet(5, 1, 3); // Bedrock, Stone, Sand

        [Test]
        public void TerrainGeneratorDependsOnlyOnStorageGenerationApi()
        {
            string root = FindRepoRoot();
            string path = Path.Combine(
                root, "Assets", "VoxelEngine", "Terrain", "Runtime", "TerrainGenerator.cs");
            string source = File.ReadAllText(path);
            string codeOnly = Regex.Replace(source,
                @"//.*?$|/\*.*?\*/",
                string.Empty,
                RegexOptions.Multiline | RegexOptions.Singleline);
            string[] forbidden =
            {
                "RegionTable",
                "BrickPool",
                "BrickRef",
                "VoxelDimensions.",
                "VoxelEngine.Storage.Runtime",
            };

            foreach (string token in forbidden)
                Assert.Less(codeOnly.IndexOf(token, StringComparison.Ordinal), 0,
                    "Terrain generation must not depend on physical Storage type: " + token);
            StringAssert.Contains("VoxelEngine.Storage.Api", codeOnly);
            StringAssert.Contains("IRegionGenerationStore", codeOnly);
        }

        [Test]
        public void TableAndStandaloneGenerationProduceIdenticalRegionContent()
        {
            const uint seed = 0xC0FFEEu;
            int3 coord = new int3(-1, 0, 2);

            var table = new RegionTable(1, Allocator.Persistent);
            var standalone = new Region(coord, Allocator.Temp);
            try
            {
                var tableStore = new RegionGenerationStore(in table);
                var standaloneStore = new StandaloneRegionGenerationStore(in standalone);

                TerrainGenerator.Generate(tableStore, coord, seed, BoundaryTerrainMaterials);
                TerrainGenerator.Generate(standaloneStore, coord, seed, BoundaryTerrainMaterials);

                Assert.IsTrue(table.TryGetRegion(coord, out Region generated));
                Assert.AreEqual(standalone.BrickRefs.Length, generated.BrickRefs.Length);
                for (int i = 0; i < generated.BrickRefs.Length; i++)
                    Assert.AreEqual(standalone.BrickRefs[i].Value, generated.BrickRefs[i].Value,
                        "Generation-store implementations diverged at logical block " + i);
            }
            finally
            {
                standalone.Dispose();
                if (table.IsCreated) table.Dispose();
            }
        }

        [Test]
        public void StandaloneGenerationStoreRejectsForeignRegion()
        {
            var region = new Region(new int3(2, 3, 4), Allocator.Temp);
            try
            {
                var store = new StandaloneRegionGenerationStore(in region);
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    store.AcquireRegion(new int3(2, 3, 5)));
            }
            finally
            {
                region.Dispose();
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
