using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialProjectionAuthorityTests
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

        [Test]
        public void StructuresApiHasSingleLegacyKeepOffsetAuthority()
        {
            string apiDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api");
            int offsetDeclarations = 0;

            foreach (string file in Directory.GetFiles(apiDirectory, "*.cs"))
            {
                string source = File.ReadAllText(file);
                StringAssert.DoesNotContain(
                    "CastleSpatialLayoutProjector",
                    source,
                    $"{Path.GetFileName(file)} reintroduced a second spatial castle projection.");

                if (source.Contains("public const int LegacyKeepCentreZOffset"))
                    offsetDeclarations++;
            }

            Assert.AreEqual(
                1,
                offsetDeclarations,
                "CastleLayout must be the only Structures.Api declaration of the legacy keep anchor offset.");

            string completion = File.ReadAllText(Path.Combine(
                apiDirectory, "CastleKeepTurretPlanCompletion.cs"));
            StringAssert.Contains("CastleLayout.LegacyKeepCentreZOffset", completion,
                "Keep turret completion must share the authoritative compatibility projection offset.");
            StringAssert.DoesNotContain("KeepHalfZ + 60", completion,
                "Keep turret completion must not reintroduce raw legacy keep-anchor math.");
        }
    }
}
