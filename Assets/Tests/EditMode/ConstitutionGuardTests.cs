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
    /// Source-level guards protect deterministic simulation code from silent cross-platform drift.
    /// </summary>
    public sealed class ConstitutionGuardTests
    {
        private static readonly string[] DeterministicSourceRoots =
        {
            // Legacy root remains protected until the final Core deletion cutover.
            "Core",
            // Target architecture roots. Non-existent roots are ignored during migration.
            "Foundation",
            "Storage",
            "Terrain",
            "Edits",
            "StructuralIntegrity"
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

        private static string VoxelEngineDir => Path.Combine(RepoRoot, "Assets", "VoxelEngine");

        private static IEnumerable<string> DeterministicSourceFiles =>
            DeterministicSourceRoots
                .Select(root => Path.Combine(VoxelEngineDir, root))
                .Where(Directory.Exists)
                .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories));

        private static IEnumerable<string> DeterministicAsmdefs =>
            DeterministicSourceRoots
                .Select(root => Path.Combine(VoxelEngineDir, root))
                .Where(Directory.Exists)
                .SelectMany(root => Directory.EnumerateFiles(root, "*.asmdef", SearchOption.AllDirectories));

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
                    violations.Add(Relative(file) + ":" + line + " uses '" + m.Value + "'");
                }
            }

            Assert.IsEmpty(violations,
                "Constitution Principle I: deterministic simulation must be integer-only.\n" +
                "Floating-point arithmetic can produce cross-hardware divergence.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void Principle1_DeterministicSimulationDoesNotReferenceUnityEngine()
        {
            var violations = DeterministicSourceFiles
                .Where(f => Regex.IsMatch(StripCommentsAndStrings(File.ReadAllText(f)),
                    @"\busing\s+UnityEngine\b|\bUnityEngine\."))
                .Select(Relative)
                .ToList();

            Assert.IsEmpty(violations,
                "Constitution Principle I: deterministic simulation must have no UnityEngine dependency.\n" +
                "Isolation is required by the headless cross-hardware parity harness.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void Principle1_DeterministicAssembliesAreExplicitAndUseOnlyDataPackages()
        {
            var violations = new List<string>();
            foreach (var asmdef in DeterministicAsmdefs)
            {
                var json = File.ReadAllText(asmdef);
                if (!json.Contains("\"autoReferenced\": false"))
                    violations.Add(Relative(asmdef) + " must set autoReferenced=false");

                foreach (var forbidden in new[]
                         {
                             "UnityEngine", "Unity.RenderPipelines", "Unity.Networking",
                             "Unity.Entities", "Unity.Physics", "Unity.Netcode"
                         })
                {
                    if (json.Contains(forbidden))
                        violations.Add(Relative(asmdef) + " references forbidden package " + forbidden);
                }
            }

            Assert.IsEmpty(violations,
                "Constitution Principle I: deterministic assemblies must remain explicit, headless data assemblies.\n\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void Principle1_DeterministicSimulationUsesNoNonDeterministicRandom()
        {
            var violations = DeterministicSourceFiles
                .Where(f => Regex.IsMatch(StripCommentsAndStrings(File.ReadAllText(f)),
                    @"\bnew\s+System\.Random\b|\bnew\s+Random\s*\(\s*\)|UnityEngine\.Random"))
                .Select(Relative)
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
                "Constitution Principle VI: device-matrix.md is the authoritative source for every numeric budget.");

            var text = File.ReadAllText(matrix);
            foreach (var required in new[] { "Frame budget", "Brick pool", "Sustained downstream", "tick rate" })
                StringAssert.Contains(required, text, "device-matrix.md must define '" + required + "'.");
        }

        private static string Relative(string path)
        {
            string normalized = path.Replace('\\', '/');
            string root = RepoRoot.Replace('\\', '/');
            return normalized.StartsWith(root + "/", StringComparison.Ordinal)
                ? normalized.Substring(root.Length + 1)
                : normalized;
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }
    }
}
