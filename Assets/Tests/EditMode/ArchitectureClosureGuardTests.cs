using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Final-state guards for assumptions that were intentionally permissive during the
    /// subsystem migration. These tests make those migration escape hatches impossible to
    /// reintroduce once Core has been deleted.
    /// </summary>
    public sealed class ArchitectureClosureGuardTests
    {
        private static readonly string[] DeterministicRoots =
        {
            "Foundation",
            "Storage",
            "Terrain",
            "Edits",
            "StructuralIntegrity",
        };

        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                    dir = dir.Parent;

                Assert.NotNull(dir, "Could not locate project root containing Assets/.");
                return dir.FullName;
            }
        }

        [Test]
        public void DeterministicSubsystemRootsAreNoLongerOptional()
        {
            var missing = DeterministicRoots
                .Select(name => Path.Combine(RepoRoot, "Assets", "VoxelEngine", name))
                .Where(path => !Directory.Exists(path))
                .Select(Relative)
                .ToArray();

            Assert.IsEmpty(missing,
                "The architecture migration is complete; deterministic subsystem roots may no " +
                "longer be silently skipped by constitution guards.\n\n" + string.Join("\n", missing));
        }

        [Test]
        public void VoxelEngineSubsystemAsmdefsUseNamedReferences()
        {
            string root = Path.Combine(RepoRoot, "Assets", "VoxelEngine");
            Assert.IsTrue(Directory.Exists(root), "Missing VoxelEngine source root: " + root);

            var violations = new List<string>();
            foreach (string path in Directory.EnumerateFiles(root, "*.asmdef", SearchOption.AllDirectories))
            {
                string json = File.ReadAllText(path);
                if (json.IndexOf("GUID:", StringComparison.Ordinal) >= 0)
                    violations.Add(Relative(path));
            }

            Assert.IsEmpty(violations,
                "VoxelEngine subsystem asmdefs must use named references now that every owner is " +
                "migrated. GUID references would bypass the Api/Runtime dependency guard.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void ProductionSourceGraphDoesNotReferenceRetiredCore()
        {
            string retiredCore = "VoxelEngine" + ".Core";
            string[] roots =
            {
                Path.Combine(RepoRoot, "Assets", "VoxelEngine"),
                Path.Combine(RepoRoot, "Assets", "Scenes", "Showcase"),
                Path.Combine(RepoRoot, "Packages", "com.mountingforce.worldgen", "Runtime"),
            };
            string[] extensions = { ".cs", ".asmdef", ".asmref", ".json" };
            var violations = new List<string>();

            foreach (string root in roots)
            {
                Assert.IsTrue(Directory.Exists(root), "Missing production source root: " + root);

                foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (!extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                        continue;

                    if (File.ReadAllText(path).IndexOf(retiredCore, StringComparison.Ordinal) >= 0)
                        violations.Add(Relative(path));
                }
            }

            Assert.IsEmpty(violations,
                "The retired Core namespace/assembly may not reappear in production source or " +
                "assembly metadata.\n\n" + string.Join("\n", violations));
        }

        private static string Relative(string path)
        {
            string normalized = path.Replace('\\', '/');
            string root = RepoRoot.Replace('\\', '/');
            return normalized.StartsWith(root + "/", StringComparison.Ordinal)
                ? normalized.Substring(root.Length + 1)
                : normalized;
        }
    }
}
