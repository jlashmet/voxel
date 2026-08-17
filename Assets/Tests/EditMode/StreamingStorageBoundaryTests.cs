using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Streaming.Api;
using VoxelEngine.Streaming.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class StreamingStorageBoundaryTests
    {
        [Test]
        public void StreamingRuntimeUsesApisInsteadOfPhysicalOrNetworkingImplementations()
        {
            string root = FindRepoRoot();
            string streaming = Path.Combine(root, "Assets", "VoxelEngine", "Streaming");
            string runtime = Path.Combine(streaming, "Runtime");
            string[] forbidden =
            {
                "RegionTable",
                "BrickPool",
                "BrickRef",
                "VoxelAccess",
                "VoxelDimensions.",
                "VoxelEngine.Storage.Runtime",
            };
            var violations = new List<string>();

            foreach (string path in Directory.EnumerateFiles(runtime, "*.cs", SearchOption.TopDirectoryOnly))
            {
                string source = File.ReadAllText(path);
                foreach (string token in forbidden)
                {
                    if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                        violations.Add(Path.GetFileName(path) + " -> " + token);
                }
            }

            string asmdefPath = Path.Combine(runtime, "VoxelEngine.Streaming.Runtime.asmdef");
            string asmdef = File.ReadAllText(asmdefPath);
            if (asmdef.IndexOf("\"VoxelEngine.Storage.Runtime\"", StringComparison.Ordinal) >= 0)
                violations.Add("VoxelEngine.Streaming.Runtime.asmdef -> VoxelEngine.Storage.Runtime");
            if (asmdef.IndexOf("\"VoxelEngine.Net\"", StringComparison.Ordinal) >= 0)
                violations.Add("VoxelEngine.Streaming.Runtime.asmdef -> VoxelEngine.Net");
            if (asmdef.IndexOf("\"VoxelEngine.Storage.Api\"", StringComparison.Ordinal) < 0)
                violations.Add("VoxelEngine.Streaming.Runtime.asmdef -> missing VoxelEngine.Storage.Api");
            if (asmdef.IndexOf("\"VoxelEngine.Streaming.Api\"", StringComparison.Ordinal) < 0)
                violations.Add("VoxelEngine.Streaming.Runtime.asmdef -> missing VoxelEngine.Streaming.Api");

            Assert.IsEmpty(violations,
                "Streaming Runtime must own policy/orchestration while consuming foreign systems only through APIs.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void PlayModeTestsDoNotUseRemovedPhysicalStreamingSignatures()
        {
            string playMode = Path.Combine(FindRepoRoot(), "Assets", "Tests", "PlayMode");
            string[] removedSignatures =
            {
                "PublishLoaded(ref table, ref pool",
                "EvictWithoutWriteBack(regionCoord, ref table, ref pool)",
                "Update(playerPos, k_TickInterval, ref table, pool)",
            };
            var violations = new List<string>();

            foreach (string path in Directory.EnumerateFiles(playMode, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(path);
                foreach (string signature in removedSignatures)
                {
                    if (source.IndexOf(signature, StringComparison.Ordinal) >= 0)
                        violations.Add(Path.GetFileName(path) + " -> " + signature);
                }
            }

            Assert.IsEmpty(violations,
                "Tests must exercise Streaming through its Storage.Api boundary rather than " +
                "keeping deleted table/pool signatures alive.\n\n" + string.Join("\n", violations));
        }

        [Test]
        public void StreamingServiceCanBeInstantiatedWithoutNetworking()
        {
            IRegionStreaming streaming = new RegionStreamingService(new RecordingResidencyStore());
            Assert.NotNull(streaming);
        }

        [Test]
        public void FirstCompletedRegionPublishesThroughResidencyStore()
        {
            Type loader = typeof(RegionLoader);
            Type completionType = loader.GetNestedType("CompletedRegion", BindingFlags.NonPublic);
            Assert.NotNull(completionType);

            FieldInfo completionsField = loader.GetField("_completions",
                BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo countField = loader.GetField("_completionCount",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo pushMethod = loader.GetMethod("PushCompletion",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(completionsField);
            Assert.NotNull(countField);
            Assert.NotNull(pushMethod);

            object previousCompletions = completionsField.GetValue(null);
            object previousCount = countField.GetValue(null);
            try
            {
                completionsField.SetValue(null, Array.CreateInstance(completionType, 64));
                countField.SetValue(null, 0);

                int3 expected = new int3(4, -2, 7);
                object completion = Activator.CreateInstance(completionType);
                completionType.GetField("RegionCoord",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(completion, expected);
                completionType.GetField("MipLevel",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(completion, (byte)2);

                pushMethod.Invoke(null, new[] { completion });

                var store = new RecordingResidencyStore();
                int published = RegionLoader.PublishLoaded(store, 1000f);
                Assert.AreEqual(1, published);
                Assert.AreEqual(1, store.Ensured.Count);
                Assert.AreEqual(expected, store.Ensured[0],
                    "The first completion must be read from the same zero-based slot it was written to.");
            }
            finally
            {
                completionsField.SetValue(null, previousCompletions);
                countField.SetValue(null, previousCount);
            }
        }

        [Test]
        public void TraversalEvictionUsesActualResidentSetNotOnlyCurrentPlayerCube()
        {
            var store = new RecordingResidencyStore();
            int3 current = int3.zero;
            int3 farBehind = new int3(-100, 0, 0);
            store.EnsureRegionResident(current);
            store.EnsureRegionResident(farBehind);

            int unloadBlocks = (int)(ResidencyManager.UnloadRadiusMetres_PC / 0.8f);
            int evicted = ResidencyManager.EvictResidentRegionsOutsideRadius(
                float3.zero, unloadBlocks, store);

            Assert.AreEqual(1, evicted);
            Assert.True(store.IsRegionResident(current),
                "A region inside the unload sphere was incorrectly evicted.");
            Assert.False(store.IsRegionResident(farBehind),
                "A region left completely behind the current unload cube remained resident.");
            CollectionAssert.Contains(store.Evicted, farBehind);
        }

        private static string FindRepoRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Assets")))
                directory = directory.Parent;
            Assert.NotNull(directory, "Could not locate project root containing Assets/.");
            return directory.FullName;
        }

        private sealed class RecordingResidencyStore : IRegionResidencyStore
        {
            public readonly List<int3> Ensured = new List<int3>();
            public readonly List<int3> Evicted = new List<int3>();

            public StoragePressure Pressure => default;
            public bool IsRegionResident(int3 regionCoord) => Ensured.Contains(regionCoord);

            public NativeArray<int3> GetResidentRegionCoords(Allocator allocator)
            {
                var result = new NativeArray<int3>(Ensured.Count, allocator);
                for (int i = 0; i < Ensured.Count; i++) result[i] = Ensured[i];
                return result;
            }

            public void EnsureRegionResident(int3 regionCoord)
            {
                if (!Ensured.Contains(regionCoord)) Ensured.Add(regionCoord);
            }

            public bool EvictRegion(int3 regionCoord)
            {
                if (!Ensured.Remove(regionCoord)) return false;
                Evicted.Add(regionCoord);
                return true;
            }
        }
    }
}