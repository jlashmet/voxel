using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseResidencyConfigurationTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";
        private const string WorldPath = "Assets/Game/Composition/Showcase/ShowcaseWorld.cs";
        private const float RegionMetres = 51.2f;

        [Test]
        public void ShowcaseSceneKeepsVoxelRingsInsideConfiguredResidency()
        {
            string scene = File.ReadAllText(ScenePath);
            int loadRadius = ReadSerializedInt(scene, "m_LoadRadiusRegions");
            int unloadRadius = ReadSerializedInt(scene, "m_UnloadRadiusRegions");

            Assert.AreEqual(8, loadRadius,
                "The committed showcase scene must match VoxelShowcase's production load default.");
            Assert.AreEqual(11, unloadRadius,
                "The committed showcase scene must match VoxelShowcase's production unload default.");
            Assert.Greater(unloadRadius, loadRadius,
                "Showcase streaming needs unload hysteresis beyond the load window.");
            Assert.LessOrEqual(
                VoxelSurfaceScheduler.MaxVoxelRingRadiusMetres,
                loadRadius * RegionMetres + 1f,
                "The scene's load radius must cover every voxel LOD ring; otherwise newly "
              + "visible coarse chunks can request authoritative regions that were never loaded.");
            Assert.LessOrEqual(
                VoxelSurfaceScheduler.MaxVoxelRingRadiusMetres,
                unloadRadius * RegionMetres + 1f,
                "The scene's unload radius must keep already-authored landmarks resident while "
              + "the camera views them through the outer voxel LOD rings.");
        }

        [Test]
        public void BoundedStreamingMakesFarHoleConservativeAtSubRegionOffsets()
        {
            string world = File.ReadAllText(WorldPath);

            StringAssert.Contains("RefreshPending(centre)", world,
                "Full-detail residency must stay on the bounded radius-8 region budget; expanding "
              + "the loaded disc for sub-region camera offsets exhausts the mixed-brick pool.");
            StringAssert.Contains(
                "dx * dx + dz * dz > LoadRadiusRegions * LoadRadiusRegions", world,
                "The wanted set must retain its bounded integer-region disc.");
            Assert.AreEqual(1, Regex.Matches(
                    world, @"ShowcaseResidencyFootprint\.ColumnIntersectsRadius\(",
                    RegexOptions.CultureInvariant).Count,
                "Camera-relative geometry belongs only in ResidentGroundRadiusMetres: fringe "
              + "columns outside the bounded world budget must shrink the far hole, not grow residency.");
            StringAssert.Contains("math.min(LoadRadiusRegions * RegionMetres", world,
                "The published near-coverage radius must never exceed the configured physical radius.");
        }

        private static int ReadSerializedInt(string yaml, string field)
        {
            Match match = Regex.Match(
                yaml,
                $@"^\s*{Regex.Escape(field)}:\s*(-?\d+)\s*$",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
            Assert.True(match.Success, $"{ScenePath} does not serialize {field}.");
            return int.Parse(match.Groups[1].Value);
        }
    }
}
