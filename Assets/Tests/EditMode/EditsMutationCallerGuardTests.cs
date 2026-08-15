using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class EditsMutationCallerGuardTests
    {
        [Test]
        public void NetAndTestsDoNotCallPhysicalMutationSignaturesAcrossLineBreaks()
        {
            string root = FindRepoRoot();
            string[] roots =
            {
                Path.Combine(root, "Assets", "VoxelEngine", "Net"),
                Path.Combine(root, "Assets", "Tests"),
            };
            var forbidden = new[]
            {
                new Regex(@"DeterministicAlterationApplier\.TryApply\s*\(\s*ref\s+table\s*,\s*ref\s+pool", RegexOptions.Singleline),
                new Regex(@"DeterministicAlterationApplier\.HasRequiredResidency\s*\(\s*ref\s+table", RegexOptions.Singleline),
                new Regex(@"EventApplication\.Apply\s*\(\s*ref\s+(?:table|tableA|tableB)", RegexOptions.Singleline),
                new Regex(@"TryApplyAlteration\s*\(\s*ref\s+RegionTable\s+table\s*,\s*ref\s+BrickPool\s+pool", RegexOptions.Singleline),
            };
            var violations = new List<string>();

            foreach (string scanRoot in roots)
            foreach (string path in Directory.EnumerateFiles(scanRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(path);
                foreach (Regex pattern in forbidden)
                    if (pattern.IsMatch(source))
                        violations.Add(Path.GetRelativePath(root, path) + " -> " + pattern);
            }

            Assert.IsEmpty(violations,
                "Legacy physical edit mutation calls must not return.\n\n" + string.Join("\n", violations));
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
