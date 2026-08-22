using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseTraversalFallbackContractTests
    {
        [Test]
        public void TraversalStartsFromFallbackSafeVisibilityInsteadOfFullNearConvergence()
        {
            string source = File.ReadAllText(
                "Assets/Tests/PlayMode/ShowcaseTraversalPerformanceTests.cs");
            string workflow = File.ReadAllText(".github/workflows/tests-single.yml");

            StringAssert.Contains(
                "WaitForFallbackSafeVisibleCoverage(camera, far, 1200)", source);
            StringAssert.Contains(
                "bool nearIncomplete = NearCoverageIsIncomplete(in last);", source);
            StringAssert.Contains(
                "bool fallbackSafe = !nearIncomplete || far.HoleRadiusMetres <= 0.05f;", source);
            StringAssert.Contains(
                "bool ready = last.VisibleSolidChunks > 0 && fallbackSafe;", source);
            StringAssert.DoesNotContain(
                "WaitForVisibleCoverage(camera, 1800)", source,
                "The traversal gate must exercise far fallback while near coverage streams rather than waiting for Editor-only full convergence.");
            StringAssert.Contains(
                "steps.request.outputs.test != 'VoxelEngine.Tests.PlayMode.ShowcaseTraversalPerformanceTests.ContinuousPlayerTraversalNeverStuttersOrOpensNearFarGap'",
                workflow,
                "The exact full traversal must use the refreshed checked-in VoxelShowcase bake so PlayMode plus the visible standalone capture fits the five-minute CI budget.");
        }

        [Test]
        public void SingleFrameHitchAndCoverageAcceptanceBelongToTheRealPlayer()
        {
            string source = File.ReadAllText(
                "Assets/Tests/PlayMode/ShowcaseTraversalPerformanceTests.cs");
            string capture = File.ReadAllText("tools/showcase-player-capture.sh");
            string validator = File.ReadAllText("tools/validate-showcase-traversal.py");

            StringAssert.Contains(
                "frameTimesMs, \"continuous traversal\", enforceSingleFrameMax: false",
                source,
                "The Editor loop must retain percentile/correctness guards but must not own the production single-frame hitch threshold.");
            StringAssert.Contains(
                "AssertMovingFrameTimes(frameTimesMs, \"repeated LOD-boundary traversal\");",
                source,
                "The independent LOD sweep must retain its existing Editor single-frame guard.");
            StringAssert.Contains(
                "python3 tools/validate-showcase-traversal.py", capture,
                "The exact visible traversal profile must execute the production-player acceptance validator.");
            StringAssert.Contains("MAX_SINGLE_FRAME_MS = 33.34", validator);
            StringAssert.Contains("if worst_max >= MAX_SINGLE_FRAME_MS:", validator);
            StringAssert.Contains("if worst_missing != 0:", validator);
            StringAssert.Contains("if worst_reappeared != 0:", validator);
            StringAssert.Contains("if worst_lease_fail != 0:", validator);
            StringAssert.Contains("if hole > 0.05:", validator,
                "The real-player gate must fail if the far hole opens while near coverage is incomplete.");
        }

        [Test]
        public void CommandLineAutoWalkUsesDeterministicLandmarkTangent()
        {
            string helper = File.ReadAllText(
                "Assets/Scenes/Showcase/DeterministicAutoWalkHeadingHarness.cs");
            string showcase = File.ReadAllText("Assets/Scenes/Showcase/VoxelShowcase.cs");

            StringAssert.Contains("[DefaultExecutionOrder(-10000)]", helper);
            StringAssert.Contains(
                "TryCommandLineValue(\"-voxel-autowalk-after\"", helper,
                "Normal interactive players must not install the deterministic benchmark heading helper.");
            StringAssert.Contains(
                "Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;", helper);
            StringAssert.Contains(
                "tangentYaw - ExistingAutoWalkDegreesPerSecond * Time.deltaTime", helper,
                "The helper must pre-compensate the existing StepAutoWalk turn so actual movement lands on the geometry-derived tangent.");
            StringAssert.Contains("MouseLookField.SetValue(_showcase, false);", helper,
                "Real mouse deltas must not perturb the automated route once the benchmark arms.");

            StringAssert.Contains("private float _yaw, _pitch;", showcase,
                "The harness binding intentionally depends on VoxelShowcase's private heading state.");
            StringAssert.Contains("private bool _mouseLook = true;", showcase);
            StringAssert.Contains("private Vector3 LandmarkWorldPosition()", showcase);
            StringAssert.Contains("const float DegreesPerSecond = 24f;", showcase);
            StringAssert.Contains("_yaw += DegreesPerSecond * Time.deltaTime;", showcase,
                "If StepAutoWalk's turn law changes, update the deterministic helper instead of silently changing the benchmark route.");
        }

        [Test]
        public void FarFallbackPreservesAuthoredSubtractiveTerrain()
        {
            string store = File.ReadAllText(
                "Assets/Game/Composition/Showcase/FarFieldStructureStore.cs");
            string farTerrain = File.ReadAllText("Assets/Scenes/Showcase/VoxelFarTerrain.cs");
            string world = File.ReadAllText("Assets/Game/Composition/Showcase/ShowcaseWorld.cs");

            StringAssert.Contains("if (top < terrain)", store,
                "Post-authoring surfaces below TerrainQuery must be captured instead of discarded as non-structure.");
            StringAssert.Contains("loweredTerrain ??= NewOverrideArray();", store);
            StringAssert.Contains("changed |= MergeLoweredTerrain(key, loweredTerrain);", store,
                "Authored lowering must survive later plain-terrain recaptures after eviction/regeneration.");
            StringAssert.Contains("public int AuthoredTerrainHeightAt", store);

            int terrainOverride = farTerrain.IndexOf(
                "int authoredTerrain = Structures.AuthoredTerrainHeightAt(voxelX, voxelZ);");
            int nearFootprintSuppression = farTerrain.IndexOf(
                "bool insideRequestedNearFootprint = ring == 0");
            Assert.That(terrainOverride, Is.GreaterThanOrEqualTo(0));
            Assert.That(nearFootprintSuppression, Is.GreaterThan(terrainOverride),
                "The authored terrain override must be applied before ring-zero structure suppression so closed-hole fallback still shows carved ground.");
            StringAssert.Contains(
                "int authored = Structures.AuthoredTerrainHeightAt(voxelX, voxelZ);", farTerrain,
                "Near-hole projection must use the lowered surface too or it can over-open the far hole above a deep carve.");
            StringAssert.Contains(
                "FarField.CaptureRegion(_castleRegions[i], ReadStorage, Seed);", world,
                "The permanent override must come from the already-authoritative post-castle voxel capture, not from a second renderer-side moat formula.");
        }
    }
}
