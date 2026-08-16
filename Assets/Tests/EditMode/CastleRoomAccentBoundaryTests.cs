using System;
using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleRoomAccentBoundaryTests
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
        public void SpatialRoomFurnishingConsumesPlannerOwnedAccentsWithoutRng()
        {
            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string furnisher = File.ReadAllText(Path.Combine(
                runtimeDirectory, "CastleRoomFurnisher.cs"));
            string keep = File.ReadAllText(Path.Combine(
                runtimeDirectory, "CastleKeepRealizer.cs"));
            string validator = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleKeepFloorPlanValidator.cs"));

            string legacy = Slice(
                furnisher,
                "internal static void Furnish(ref VoxelBrush",
                "internal static void FurnishPlanned(");
            string planned = Slice(
                furnisher,
                "internal static void FurnishPlanned(",
                "private static void FurnishFixed(");

            StringAssert.Contains("new Random(", legacy,
                "Dimension-only compatibility builds must keep their historical RNG path.");
            StringAssert.DoesNotContain("Random", planned,
                "Spatial furnishing must consume already-planned accent data without rerolling it.");
            StringAssert.DoesNotContain("NextInt", planned);
            StringAssert.DoesNotContain("NextBool", planned);
            StringAssert.Contains("CastleRoomAccentPlan accents", planned);
            StringAssert.Contains("FurnishPlannedAccents(", planned);

            StringAssert.Contains("CastleRoomFurnisher.FurnishPlanned(", keep,
                "The spatial keep path must use the no-RNG furnishing entry point.");
            StringAssert.Contains("roomPlan.Accents", keep);

            StringAssert.Contains("MissingAccentPlan", validator);
            StringAssert.Contains("CastleRoomAccentPlanValidator.TryValidate(", validator,
                "Runtime-ready keep floors must reject missing or invalid planned accents.");
        }

        private static string Slice(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, $"Missing source marker: {startMarker}");
            int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            Assert.Greater(end, start, $"Missing source marker: {endMarker}");
            return source.Substring(start, end - start);
        }
    }
}
