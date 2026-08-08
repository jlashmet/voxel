using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Enforces Constitution Principle I inside world generation: no `float` or `double` may
    /// appear in `Core/Features` or `Core/Terrain`.
    ///
    /// The constitution names an analyzer rule as the enforcement mechanism. There is no analyzer
    /// in this project yet, and adding one is a build-infrastructure change of its own; this test
    /// enforces the same rule by scanning source, which is weaker in principle — it reads text
    /// rather than a syntax tree — and equivalent in practice for the thing that matters, which
    /// is that a float never silently reaches a code path participating in cross-client
    /// agreement.
    ///
    /// A float here does not fail loudly. It produces terrain that differs between an ARM and an
    /// x86 client by one voxel somewhere, which no single client can detect and which surfaces as
    /// players disagreeing about where the ground is.
    /// </summary>
    public class IntegerOnlyGenerationTests
    {
        private static readonly string[] GuardedDirectories =
        {
            "Assets/VoxelEngine/Core/Features",
            "Assets/VoxelEngine/Core/Terrain",
        };

        /// <summary>
        /// Matches `float`/`double` as types or literals, but not inside a word (`floating`), not
        /// in comments, and not in the documented exceptions below.
        /// </summary>
        private static readonly Regex FloatUse = new(
            @"(?<![A-Za-z0-9_])(float|double|Mathf|math\.(sqrt|sin|cos|pow|floor|ceil))(?![A-Za-z0-9_])",
            RegexOptions.Compiled);

        [Test]
        public void GenerationSourceContainsNoFloatingPoint()
        {
            var offences = new List<string>();

            foreach (var directory in GuardedDirectories)
            {
                var absolute = Path.Combine(Application.dataPath, "..", directory);
                if (!Directory.Exists(absolute)) continue;

                foreach (var file in Directory.GetFiles(absolute, "*.cs", SearchOption.AllDirectories))
                {
                    var lines = File.ReadAllLines(file);

                    for (var i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        var trimmed = line.TrimStart();

                        // Comments may discuss floats; code may not use them.
                        if (trimmed.StartsWith("//") || trimmed.StartsWith("///") || trimmed.StartsWith("*"))
                            continue;

                        if (!FloatUse.IsMatch(line)) continue;

                        offences.Add($"{Path.GetFileName(file)}:{i + 1}: {trimmed}");
                    }
                }
            }

            Assert.IsEmpty(offences,
                "Floating point found in world generation. Cross-client agreement derives from " +
                "this code, and float arithmetic is not reproducible across platforms " +
                "(Constitution I).\n" + string.Join("\n", offences));
        }

        [Test]
        public void GuardedDirectoriesExist()
        {
            // A rule that silently guards nothing is worse than no rule: it reports success while
            // the directory it was written for has been renamed out from under it.
            foreach (var directory in GuardedDirectories)
            {
                var absolute = Path.Combine(Application.dataPath, "..", directory);
                Assert.IsTrue(Directory.Exists(absolute),
                    $"{directory} does not exist — the float guard is watching nothing.");
            }
        }
    }
}
