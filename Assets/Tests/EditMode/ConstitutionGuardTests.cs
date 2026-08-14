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
    /// invariants they protect fail silently — a float in deterministic simulation does
    /// not throw, it causes two players on different hardware to slowly disagree.
    /// </summary>
    public sealed class ConstitutionGuardTests
    {
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

        // This list deliberately spans both the legacy Core location and the target
        // subsystem locations. As cutovers move files, the determinism guard follows
        // them instead of silently becoming an empty directory scan.
        private static readonly string[] DeterministicRelativeDirectories =
        {
            Path.Combine("Assets", "VoxelEngine", "Core"), // migration-only; removed with final Core deletion
            Path.Combine("Assets", "VoxelEngine", "Foundation"),
            Path.Combine("Assets", "VoxelEngine", "Storage", "Api"),
            Path.Combine("Assets", "VoxelEngine", "Storage", "Runtime"),
            Path.Combine("Assets", "VoxelEngine", "Terrain", "Api"),
            Path.Combine("Assets", "VoxelEngine", "Terrain", "Runtime"),
            Path.Combine("Assets", "VoxelEngine", "Edits", "Api"),
            Path.Combine("Assets", "VoxelEngine", "Edits", "Runtime"),
            Path.Combine("Assets", "VoxelEngine", "Structures", "Api"),
            Path.Combine("Assets", "VoxelEngine", "Structures", "Runtime", "Deterministic"),
            Path.Combine("Assets", "VoxelEngine", "StructuralIntegrity", "Api"),
            Path.Combine("Assets", "VoxelEngine", "StructuralIntegrity", "Runtime")
        };

        private static IEnumerable<string> DeterministicSourceFiles =>
            DeterministicRelativeDirectories
                .Select(relative => Path.Combine(RepoRoot, relative))
                .Where(Directory.Exists)
                .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
                .Distinct(StringComparer.OrdinalIgnoreCase);

        private static IEnumerable<string> DeterministicAsmdefs =>
            DeterministicRelativeDirectories
                .Select(relative => Path.Combine(RepoRoot, relative))
                .Where(Directory.Exists)
                .SelectMany(dir => Directory.EnumerateFiles(dir, "*.asmdef", SearchOption.AllDirectories))
                .Distinct(StringComparer.OrdinalIgnoreCase);

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
        public void Principle1_DeterministicSimulationContainsNoFloatingPointTypes()
        {
            var forbidden = new Regex(
                @"\b(float|double|decimal|float2|float3|float4|float2x2|float3x3|float4x4|quaternion)\b",
                RegexOptions.Compiled);

            var violations = new List<string>();

            foreach (var file in DeterministicSourceFiles)
            {
                var text = StripCommentsAndStrings(File.ReadAllText(file));
                foreach (Match m in forbidden.Matches(text))
                {
                    var line = text.Take(m.Index).Count(c => c == '\n') + 1;
                    violations.Add($"{RelativePath(file)}:{line} uses '{m.Value}'");
                }
            }

            Assert.IsEmpty(violations,
                "Constitution Principle I: deterministic simulation must be integer-only.\n" +
                "Floating-point arithmetic is not reproducible across all supported hardware.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void Principle1_DeterministicSimulationDoesNotReferenceUnityEngine()
        {
            var violations = DeterministicSourceFiles
                .Where(f => Regex.IsMatch(StripCommentsAndStrings(File.ReadAllText(f)),
                    @"\busing\s+UnityEngine\b|\bUnityEngine\."))
                .Select(RelativePath)
                .ToList();

            Assert.IsEmpty(violations,
                "Constitution Principle I: deterministic simulation must have no UnityEngine dependency.\n" +
                "Isolation is required for the headless cross-hardware parity harness.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void Principle1_DeterministicAsmdefsAreExplicitAndDoNotReferencePresentationOrNetworking()
        {
            var violations = new List<string>();
            foreach (var asmdef in DeterministicAsmdefs)
            {
                var json = File.ReadAllText(asmdef);
                if (!Regex.IsMatch(json, "\\\"autoReferenced\\\"\\s*:\\s*false"))
                    violations.Add($"{RelativePath(asmdef)} must set autoReferenced=false");

                foreach (var forbidden in new[]
                         {
                             "UnityEngine", "Unity.RenderPipelines", "Unity.Networking",
                             "Unity.Entities", "Unity.Physics", "Unity.Netcode",
                             "VoxelEngine.Rendering", "VoxelEngine.Net"
                         })
                {
                    if (json.Contains(forbidden))
                        violations.Add($"{RelativePath(asmdef)} references forbidden '{forbidden}'");
                }
            }

            Assert.IsEmpty(violations,
                "Constitution Principle I: deterministic assemblies must stay isolated.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void Principle1_DeterministicSimulationUsesNoNonDeterministicRandom()
        {
            var violations = DeterministicSourceFiles
                .Where(f => Regex.IsMatch(StripCommentsAndStrings(File.ReadAllText(f)),
                    @"\bnew\s+System\.Random\b|\bnew\s+Random\s*\(\s*\)|UnityEngine\.Random"))
                .Select(RelativePath)
                .ToList();

            Assert.IsEmpty(violations,
                "Constitution Principle I: simulation randomness must be explicitly seeded.\n" +
                "Replication transmits causes, not effects; every client must re-derive the same result.\n\n" +
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
                "DeviceTierBudget must structurally omit every simulation parameter.\n\n" +
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

        private static string RelativePath(string path)
        {
            var root = RepoRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(root.Length)
                : path;
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }
    }
}
