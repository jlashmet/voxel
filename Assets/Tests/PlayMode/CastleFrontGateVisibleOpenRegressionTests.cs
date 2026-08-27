using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        private const int VerificationWidth = 1928;
        private const int VerificationHeight = 836;

        [UnityTest]
        public IEnumerator NearbyInteractionAnimatesBothGateLeavesAndKeepsCentrePassageClear()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase, "VoxelShowcase scene driver was not created.");
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            var motor = (CharacterMotor)typeof(VoxelShowcase)
                .GetField("_motor", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            Assert.NotNull(world, "VoxelShowcase did not create its production world.");
            Assert.NotNull(motor, "VoxelShowcase did not create its production character motor.");

            for (int frame = 0; frame < 900 && world.CastleVoxels == 0; frame++)
                yield return null;
            Assert.Greater(world.CastleVoxels, 0,
                "The castle did not finish building within 900 frames.");

            const int cx = ShowcaseWorld.RegionVoxelEdge / 2;
            const int cz = ShowcaseWorld.RegionVoxelEdge / 2 + 120;
            int ground = world.SurfaceHeight(cx, cz);
            CastlePlan plan = StructuresComposition.PlanCastle(new int3(cx, ground, cz), world.Seed);
            int3 min = CastleLayout.FrontGateMinimum(in plan);

            Assert.AreEqual(Mat.Wood, Get(world, min.x + 6, min.y + 8, min.z),
                "The captured interaction must begin from a visibly closed timber gate.");
            Assert.AreEqual(Mat.DarkStone,
                Get(world, plan.Centre.x, min.y + 8, min.z),
                "The closed gate must retain its authored structural ironwork.");

            // The runtime driver performs this snapshot every closed frame. Calling it explicitly
            // keeps the temporal regression deterministic while still exercising the same
            // production world operation and player-facing TryInteract path.
            world.PrepareCastleFrontGateAnimation();
            motor.Position = world.CastleFrontGatePosition;
            Assert.That(showcase.TryInteract(), Is.True,
                "The same nearby operation bound to E must accept the castle gate interaction.");
            Assert.That(world.CastleFrontGateOpen, Is.True);

            // LateUpdate restores pose zero in the E frame, preventing the old one-frame deletion.
            world.StepCastleFrontGateAnimation(0f);
            Assert.That(world.CastleFrontGateAnimating, Is.True);
            Assert.That(world.CastleFrontGateAnimationProgress, Is.EqualTo(0f).Within(0.001f));
            HashSet<int3> closedPose = CaptureGateMaterials(world, min);
            Assert.That(closedPose.Count, Is.GreaterThan(500),
                "Animation pose zero must restore the substantial authored gate, not an empty arch.");
            Assert.AreEqual(Mat.Wood, Get(world, min.x + 6, min.y + 8, min.z),
                "The E frame must still render the closed timber leaf before it starts swinging.");

            world.StepCastleFrontGateAnimation(0.45f);
            Assert.That(world.CastleFrontGateAnimationProgress, Is.InRange(0.49f, 0.51f));
            HashSet<int3> middlePose = CaptureGateMaterials(world, min);
            Assert.That(middlePose.SetEquals(closedPose), Is.False,
                "Halfway through the transition the physical leaf cells must have moved.");

            world.StepCastleFrontGateAnimation(0.45f);
            Assert.That(world.CastleFrontGateAnimating, Is.False);
            Assert.That(world.CastleFrontGateAnimationProgress, Is.EqualTo(1f).Within(0.001f));
            HashSet<int3> openPose = CaptureGateMaterials(world, min);
            Assert.That(openPose.SetEquals(closedPose), Is.False,
                "The completed transition must not snap back to the closed gate.");
            Assert.That(openPose.SetEquals(middlePose), Is.False,
                "The animation must have a distinct completed pose rather than stopping halfway.");

            int half = CastleLayout.FrontGateWidth / 2;
            for (int d = 0; d < CastleLayout.FrontGateDepth; d++)
            {
                Assert.AreEqual(Mat.Empty, Get(world, plan.Centre.x, min.y + 8, min.z + d),
                    $"The completed doorway still blocks its centre at front depth {d}.");
            }

            int deepWoodLeft = 0;
            int deepWoodRight = 0;
            int deepIronLeft = 0;
            int deepIronRight = 0;
            int deepestZ = min.z + half + CastleLayout.FrontGateDepth + 4;
            for (int z = min.z + CastleLayout.FrontGateDepth; z <= deepestZ; z++)
            for (int y = min.y + 4; y < min.y + CastleLayout.FrontGateHeight; y++)
            for (int x = min.x; x < min.x + CastleLayout.FrontGateWidth; x++)
            {
                byte material = Get(world, x, y, z);
                bool left = x < plan.Centre.x;
                if (material == Mat.Wood)
                {
                    if (left) deepWoodLeft++; else deepWoodRight++;
                }
                else if (material == Mat.DarkStone)
                {
                    if (left) deepIronLeft++; else deepIronRight++;
                }
            }

            Assert.That(deepWoodLeft, Is.GreaterThan(100),
                "The completed animation must leave a visible left timber leaf inside the gatehouse.");
            Assert.That(deepWoodRight, Is.GreaterThan(100),
                "The completed animation must leave a visible right timber leaf inside the gatehouse.");
            Assert.That(deepIronLeft, Is.GreaterThan(10),
                "The opened left leaf must retain its authored iron detail.");
            Assert.That(deepIronRight, Is.GreaterThan(10),
                "The opened right leaf must retain its authored iron detail.");

            for (int z = min.z + CastleLayout.FrontGateDepth; z <= deepestZ; z++)
            for (int x = plan.Centre.x - 2; x <= plan.Centre.x + 1; x++)
                Assert.AreEqual(Mat.Empty, Get(world, x, min.y + 8, z),
                    $"Opened leaves intrude into the player lane at {x},{min.y + 8},{z}.");

            Assert.That(showcase.TryInteract(), Is.False,
                "The gate interaction must remain one-shot after the visible opened state completes.");

            // Give the production render scheduler time to rebuild the changed gate chunks, then
            // render the exact captured camera pose into a native-size verification artifact.
            for (int frame = 0; frame < 20; frame++)
                yield return null;
            CaptureVerificationImage();
        }

        private static HashSet<int3> CaptureGateMaterials(ShowcaseWorld world, int3 min)
        {
            int half = CastleLayout.FrontGateWidth / 2;
            var result = new HashSet<int3>();
            for (int z = min.z; z <= min.z + half + CastleLayout.FrontGateDepth + 4; z++)
            for (int y = min.y; y < min.y + CastleLayout.FrontGateHeight; y++)
            for (int x = min.x; x < min.x + CastleLayout.FrontGateWidth; x++)
            {
                byte material = Get(world, x, y, z);
                if (material == Mat.Wood || material == Mat.DarkStone || material == Mat.Gold)
                    result.Add(new int3(x, y, z));
            }
            return result;
        }

        private static void CaptureVerificationImage()
        {
            Camera camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            Assert.NotNull(camera, "VoxelShowcase has no camera for verification capture.");

            Vector3 originalPosition = camera.transform.position;
            Quaternion originalRotation = camera.transform.rotation;
            RenderTexture originalTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            var target = new RenderTexture(
                VerificationWidth, VerificationHeight, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(
                VerificationWidth, VerificationHeight, TextureFormat.RGB24, false);
            try
            {
                camera.transform.SetPositionAndRotation(
                    new Vector3(25.616580963134767f, 26.35015869140625f, 11.290130615234375f),
                    new Quaternion(0.10458954423666f, -0.007642569486051798f,
                                   -0.000803764967713505f, -0.9944857954978943f));
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, VerificationWidth, VerificationHeight), 0, 0);
                texture.Apply(false, false);

                byte[] png = texture.EncodeToPNG();
                Assert.That(png.Length, Is.GreaterThan(100_000),
                    "Native verification render was unexpectedly empty or trivial.");

                string workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
                if (string.IsNullOrEmpty(workspace))
                    workspace = Directory.GetCurrentDirectory();
                string output = Path.Combine(
                    workspace, "Artifacts", "SingleTest", "verification-final.png");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllBytes(output, png);
                Debug.Log($"SCENEISSUE_VERIFICATION {output} {VerificationWidth}x{VerificationHeight}");
            }
            finally
            {
                camera.targetTexture = originalTarget;
                camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
                RenderTexture.active = previousActive;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
            }
        }

        private static byte Get(ShowcaseWorld world, int x, int y, int z) =>
            world.SurfaceQuery.TryRead(new int3(x, y, z), out VoxelCell cell)
                ? cell.BaseMaterialId : VoxelGrid.MaterialEmpty;
    }
}
