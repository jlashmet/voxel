using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using Mat = Game.Materials.Api.GameMaterialIds;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class CastleFrontGateVisibleOpenRegressionTests
    {
        [UnityTest]
        public IEnumerator NearbyInteractionRevealsBothOpenLeavesAndKeepsCentrePassageClear()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase, "VoxelShowcase scene driver was not created.");
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            Assert.NotNull(world, "VoxelShowcase did not create its production world.");

            for (int frame = 0; frame < 900 && world.CastleVoxels == 0; frame++)
                yield return null;
            Assert.Greater(world.CastleVoxels, 0,
                "The castle did not finish building within 900 frames.");

            const int cx = ShowcaseWorld.RegionVoxelEdge / 2;
            const int cz = ShowcaseWorld.RegionVoxelEdge / 2 + 120;
            int ground = world.SurfaceHeight(cx, cz);
            var plan = StructuresComposition.PlanCastle(new int3(cx, ground, cz), world.Seed);
            int3 min = Game.Structures.Api.CastleLayout.FrontGateMinimum(in plan);

            Assert.AreEqual(Mat.Wood, Get(world, min.x + 6, min.y + 8, min.z),
                "The captured interaction must begin from a visibly closed timber gate.");
            Assert.That(world.TryOpenCastleFrontGate(world.CastleFrontGatePosition), Is.True,
                "The same nearby production interaction bound to E must open the gate.");
            Assert.That(world.CastleFrontGateOpen, Is.True);

            // The old regression stopped here and required the whole gate to disappear. Preserve
            // that collision guarantee only for the former closed-leaf plane.
            for (int d = 0; d < Game.Structures.Api.CastleLayout.FrontGateDepth; d++)
                Assert.AreEqual(Mat.Empty,
                    Get(world, plan.Centre.x, min.y + 8, min.z + d),
                    $"The opened doorway still blocks its centre at depth {d}.");

            int half = Game.Structures.Api.CastleLayout.FrontGateWidth / 2;
            int availableDepth = plan.WallThickness * 2
                               - 2
                               - Game.Structures.Api.CastleLayout.FrontGateDepth
                               - 2;
            int leafLength = math.min(Game.Structures.Api.CastleLayout.FrontGateWidth / 2 - 2,
                                      availableDepth);
            Assert.That(leafLength, Is.GreaterThanOrEqualTo(8),
                "The shipped gatehouse must have room to show the opened leaves.");

            int sampleStep = leafLength / 2;
            float t = sampleStep / (float)(leafLength - 1);
            int inward = (int)math.round(t * math.max(3, math.min(8, half - 6)));
            int z = min.z + Game.Structures.Api.CastleLayout.FrontGateDepth + sampleStep;
            int leftX = plan.Centre.x - half + inward;
            int rightX = plan.Centre.x + half - 2 - inward;

            Assert.AreEqual(Mat.Wood, Get(world, leftX, min.y + 8, z),
                "Opening the gate must reveal the left timber leaf instead of deleting the gate.");
            Assert.AreEqual(Mat.Wood, Get(world, rightX, min.y + 8, z),
                "Opening the gate must reveal the right timber leaf instead of deleting the gate.");
            Assert.AreEqual(Mat.DarkStone, Get(world, leftX, min.y + 10, z),
                "The opened left leaf must retain visible iron strap detail.");
            Assert.AreEqual(Mat.DarkStone, Get(world, rightX, min.y + 10, z),
                "The opened right leaf must retain visible iron strap detail.");

            // The leaves swing toward the jambs; the actor lane through the middle remains empty
            // all the way past the opened-leaf depth, not just at the four-voxel front plane.
            for (int i = 0; i < leafLength; i++)
                Assert.AreEqual(Mat.Empty,
                    Get(world, plan.Centre.x, min.y + 8,
                        min.z + Game.Structures.Api.CastleLayout.FrontGateDepth + i),
                    $"Opened leaves intrude into the centre passage at step {i}.");

            Assert.That(world.TryOpenCastleFrontGate(world.CastleFrontGatePosition), Is.False,
                "The gate interaction must remain one-shot after the visible opened state is exposed.");
        }

        private static byte Get(ShowcaseWorld world, int x, int y, int z) =>
            world.SurfaceQuery.TryRead(new int3(x, y, z), out VoxelCell cell)
                ? cell.BaseMaterialId : VoxelGrid.MaterialEmpty;
    }
}
