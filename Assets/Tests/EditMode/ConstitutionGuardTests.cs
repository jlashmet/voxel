using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Executable form of the project constitution (.specify/memory/constitution.md).
    ///
    /// These are source-level guards rather than unit tests. They exist because the
    /// invariants they protect fail *silently* — a float in Core does not throw, it
    /// causes two players on different hardware to slowly disagree about the world,
    /// which surfaces as an unreproducible bug report weeks later.
    ///
    /// Run in CI on every commit. A failure here is a design violation, not a bug.
    /// </summary>
    public sealed class ConstitutionGuardTests
    {
        private static string RepoRoot
        {
            get
            {
                // Tests run with cwd = project root (the folder containing Assets/).
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                    dir = dir.Parent;
                Assert.NotNull(dir, "Could not locate project root containing Assets/.");
                return dir.FullName;
            }
        }

        private static string CoreDir => Path.Combine(RepoRoot, "Assets", "VoxelEngine", "Core");

        private static IEnumerable<string> CoreSourceFiles =>
            Directory.Exists(CoreDir)
                ? Directory.EnumerateFiles(CoreDir, "*.cs", SearchOption.AllDirectories)
                : Enumerable.Empty<string>();

        /// <summary>
        /// Strips line comments, block comments, and string literals so that the word
        /// "float" in a comment or a message does not trip the guard.
        /// </summary>
        private static string StripCommentsAndStrings(string source)
        {
            source = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            source = Regex.Replace(source, @"//[^\n]*", " ");
            source = Regex.Replace(source, @"@""(?:[^""]|"""")*""", " \"\" ");
            source = Regex.Replace(source, @"""(?:\\.|[^""\\])*""", " \"\" ");
            return source;
        }

        // -------------------------------------------------------------------
        // Principle I — Determinism is integer and CPU-side
        // -------------------------------------------------------------------

        [Test]
        public void Principle1_CoreContainsNoFloatingPointTypes()
        {
            // Matches declarations and casts of float/double/decimal and the
            // Unity.Mathematics float vector/matrix family.
            var forbidden = new Regex(
                @"\b(float|double|decimal|float2|float3|float4|float2x2|float3x3|float4x4|quaternion)\b",
                RegexOptions.Compiled);

            var violations = new List<string>();

            foreach (var file in CoreSourceFiles)
            {
                var text = StripCommentsAndStrings(File.ReadAllText(file));
                foreach (Match m in forbidden.Matches(text))
                {
                    var line = text.Take(m.Index).Count(c => c == '\n') + 1;
                    violations.Add($"{Path.GetFileName(file)}:{line} uses '{m.Value}'");
                }
            }

            Assert.IsEmpty(violations,
                "Constitution Principle I: Core must be integer-only.\n" +
                "Floating-point arithmetic is not reproducible across platforms, and the\n" +
                "client population spans PC, console, and mobile GPUs. A float here causes\n" +
                "silent cross-hardware divergence.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void Principle1_CoreDoesNotReferenceUnityEngine()
        {
            var violations = CoreSourceFiles
                .Where(f => Regex.IsMatch(StripCommentsAndStrings(File.ReadAllText(f)),
                                          @"\busing\s+UnityEngine\b|\bUnityEngine\."))
                .Select(Path.GetFileName)
                .ToList();

            Assert.IsEmpty(violations,
                "Constitution Principle I: Core must have no UnityEngine dependency.\n" +
                "Core isolation is what makes the headless cross-hardware parity harness\n" +
                "possible at all. Losing it costs the project SC-003.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void Principle1_CoreAsmdefIsNotAutoReferencedAndDeclaresOnlyDataPackages()
        {
            var asmdef = Path.Combine(CoreDir, "VoxelEngine.Core.asmdef");
            Assert.IsTrue(File.Exists(asmdef), $"Missing {asmdef}");

            var json = File.ReadAllText(asmdef);

            // noEngineReferences=true would be the ideal compiler-level enforcement, but
            // it is not achievable: Unity.Collections and Unity.Mathematics both forward
            // their types through UnityEngine modules, so excluding engine references
            // makes NativeArray and int3 unresolvable. (Verified by compilation, not
            // assumed.) Isolation is therefore enforced by the source-level guard in
            // Principle1_CoreDoesNotReferenceUnityEngine plus the narrow reference list
            // asserted here.
            StringAssert.Contains("\"autoReferenced\": false", json,
                "Core must not be auto-referenced, so dependencies on it are explicit.");

            foreach (var forbidden in new[]
                     {
                         "UnityEngine", "Unity.RenderPipelines", "Unity.Networking",
                         "Unity.Entities", "Unity.Physics", "Unity.Netcode"
                     })
            {
                StringAssert.DoesNotContain(forbidden, json,
                    $"Constitution Principle I: Core must not reference {forbidden}. " +
                    "Core isolation is what makes the headless cross-hardware parity " +
                    "harness possible; losing it costs the project SC-003.");
            }
        }

        [Test]
        public void Principle1_CoreUsesNoNonDeterministicRandom()
        {
            var violations = CoreSourceFiles
                .Where(f => Regex.IsMatch(StripCommentsAndStrings(File.ReadAllText(f)),
                                          @"\bnew\s+System\.Random\b|\bnew\s+Random\s*\(\s*\)|UnityEngine\.Random"))
                .Select(Path.GetFileName)
                .ToList();

            Assert.IsEmpty(violations,
                "Constitution Principle I: Core randomness must be explicitly seeded.\n" +
                "Replication transmits causes, not effects — every client re-derives the\n" +
                "same result from the same seed. An unseeded RNG breaks that contract.\n\n" +
                string.Join("\n", violations));
        }

        // -------------------------------------------------------------------
        // Principle IV — Device class affects presentation only
        // -------------------------------------------------------------------

        [Test]
        public void Principle4_DeviceTierBudgetContainsNoSimulationParameters()
        {
            var budgetType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(t => t.Name == "DeviceTierBudget");

            if (budgetType == null)
                Assert.Ignore("DeviceTierBudget not yet implemented (T077). Guard activates with it.");

            // Substrings that indicate a simulation parameter has leaked into the
            // presentation budget. interestRadius is the specific documented trap:
            // tying update range to draw range silently disadvantages mobile players.
            var forbidden = new[]
            {
                "interest", "tick", "collision", "hit", "reconcil",
                "simulation", "authority", "validation", "budgetvoxel"
            };

            var violations =
                (from member in budgetType
                        .GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
                                    BindingFlags.Instance | BindingFlags.Static)
                    where member.MemberType is MemberTypes.Field or MemberTypes.Property
                    let lower = member.Name.ToLowerInvariant()
                    from bad in forbidden
                    where lower.Contains(bad)
                    select $"{budgetType.Name}.{member.Name} (matched '{bad}')")
                .Distinct()
                .ToList();

            Assert.IsEmpty(violations,
                "Constitution Principle IV: device class may affect presentation only.\n" +
                "DeviceTierBudget must structurally omit every simulation parameter, so the\n" +
                "type system prevents the coupling rather than code review.\n\n" +
                string.Join("\n", violations));
        }

        // -------------------------------------------------------------------
        // Principle VI — Quantitative targets before optimisation work
        // -------------------------------------------------------------------

        [Test]
        public void Principle6_DeviceMatrixExistsAndDefinesBudgets()
        {
            var matrix = Path.Combine(RepoRoot, "specs", "001-destructible-voxel-engine", "device-matrix.md");
            Assert.IsTrue(File.Exists(matrix),
                "Constitution Principle VI: device-matrix.md is the authoritative source " +
                "for every numeric budget. Without it, performance criteria are unfalsifiable.");

            var text = File.ReadAllText(matrix);
            foreach (var required in new[] { "Frame budget", "Brick pool", "Sustained downstream", "tick rate" })
            {
                StringAssert.Contains(required, text,
                    $"device-matrix.md must define '{required}'.");
            }
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }
    }
}
