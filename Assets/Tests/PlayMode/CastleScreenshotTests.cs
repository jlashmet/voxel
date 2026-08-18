using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Storage.Api;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Renders the castle from a set of fixed viewpoints and writes PNGs to disk.
    ///
    /// This exists because the cottage was written blind. Nobody — including whoever authored it —
    /// looked at the output until it was shown to someone else, and "it looks like programmer art"
    /// was the first feedback the work ever received. Generated content cannot be iterated on
    /// without seeing it, and a passing test says nothing about whether a castle looks like a
    /// castle.
    ///
    /// The viewpoints are fixed rather than orbiting so successive runs are comparable: the point
    /// is to see whether a change improved the silhouette, not to take pretty pictures.
    /// </summary>
    /// <remarks>
    /// <see cref="NUnit.Framework.ExplicitAttribute"/>: this captures images for a human to
    /// look at rather than asserting behaviour, and it is one of the slowest things in the
    /// suite. Run it by name when you want the artefacts:
    /// <c>tools/unity-run.sh ... -testFilter CastleScreenshotTests</c>
    /// </remarks>
    [NUnit.Framework.Explicit("Artefact capture for human review; run by name.")]
    public sealed class CastleScreenshotTests
    {
        private const string OutputDirectory = "/tmp/castle_shots";
        private const int Width = 1280;
        private const int Height = 720;

        [UnityTest]
        public IEnumerator CaptureCastleViews()
        {
            Directory.CreateDirectory(OutputDirectory);

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));

            // The castle is built when the origin region completes, which happens during spawn.
            // Extraction then progresses under the shared frame budget as views are rendered.
            yield return null;

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);
            var cam = Camera.main;

            Assert.GreaterOrEqual(world.CastlePresentationLights.Length, 12,
                "occupied castle rooms need authored GPU light pools");
            Assert.AreEqual(world.CastlePresentationLights.Length,
                            world.CastlePresentationLightColours.Length,
                "every GPU light position needs a colour/intensity record");

            StoragePressure castlePressure = world.StoragePressure;
            Debug.Log($"### CASTLE voxels={world.CastleVoxels:N0} storage={castlePressure.UsedBytes:N0}" +
                      $" of {castlePressure.CapacityBytes:N0} bytes");

            // Free the camera from the character so it can be placed anywhere.
            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);
            yield return null;

            cam.farClipPlane = 4000f;
            Vector3 playerRevealPosition = cam.transform.position;
            Vector3 playerRevealLook = playerRevealPosition + cam.transform.forward * 100f;

            // Derive framing from the generated plan. The castle family now varies around a
            // 50-60 m footprint, so hard-coding the former 35 m camera orbit clips valid seeds.
            int groundVoxels = world.SurfaceHeight(256, 376);
            var plan = StructuresComposition.PlanCastle(new int3(256, groundVoxels, 376), world.Seed);
            int baseY = plan.Centre.y + plan.PlateauHeight;
            var keepMin = new int3(plan.Centre.x - plan.KeepHalfX, baseY,
                                   plan.Centre.z - plan.KeepHalfZ + 60);
            var keepSize = new int3(plan.KeepHalfX * 2, plan.KeepHeight,
                                    plan.KeepHalfZ * 2);
            var centre = new Vector3(plan.Centre.x, baseY, plan.Centre.z) * 0.1f;
            float orbitScale = math.max(plan.BaileyHalfX, plan.BaileyHalfZ) / 175f;

            var stairCamera = new Vector3(plan.Centre.x, baseY + 18, keepMin.z + 16) * 0.1f;
            var stairLook = new Vector3(plan.Centre.x - 60, baseY + 20, keepMin.z + 76) * 0.1f;
            int3 trapdoor = CastleLayout.TrapdoorCentre(in plan);
            int3 bellTower = CastleLayout.ChapelBellTowerCentre(in plan);
            int chapelWidth = math.max(78, keepSize.x / 3);
            int chapelDepth = math.max(96, keepSize.z * 3 / 5);
            var chapelMin = new int3(keepMin.x - chapelWidth + 4, baseY,
                                     keepMin.z + keepSize.z - chapelDepth - 38);
            int waterfallX = CastleLayout.WaterfallStreamX(in plan);
            int waterfallZ = CastleLayout.WaterfallLipZ(in plan);
            int waterfallY = baseY - 48;
            var waterfallPool = new Vector3(waterfallX, waterfallY, waterfallZ) * 0.1f;
            Vector3 archMin = (Vector3)((float3)world.ReferenceArchMin * ShowcaseWorld.VoxelSize);
            Vector3 archMax = (Vector3)((float3)world.ReferenceArchMax * ShowcaseWorld.VoxelSize);
            Vector3 archCentre = (archMin + archMax) * 0.5f;

            var views = new (string name, Vector3 position, Vector3 lookAt)[]
            {
                ("01_approach",    centre + new Vector3(0f, 11f, -52f * orbitScale),
                                    centre + new Vector3(0f, 10f, 0f)),
                ("02_aerial",      centre + new Vector3(-62f, 64f, -62f),
                                    centre + new Vector3(0f, 11f, 0f)),
                ("03_gate",        centre + new Vector3(0f, 5f, -38f),
                                    centre + new Vector3(0f, 10f, 0f)),
                ("04_courtyard",   centre + new Vector3(-7f, 6f, -15f),
                                    centre + new Vector3(5f, 10f, 6f)),
                ("05_silhouette",  centre + new Vector3(82f, 29f, -82f),
                                    centre + new Vector3(0f, 11f, 0f)),
                ("06_wall_detail", centre + new Vector3(-32f, 8f, -22f),
                                    centre + new Vector3(-23f, 8f, -12f)),

                // East shoulder: verifies the ravine, cascade, pool, hall balcony, and tree belt
                // as a composition rather than only asserting that their voxels were written.
                ("08_waterfall",   waterfallPool + new Vector3(17f, 10f, 10f),
                                    waterfallPool + new Vector3(-6f, 2f, 0f)),

                // The reference treats rooms and underground spaces as first-class generated
                // content. These cameras sit inside the authoritative voxel volume—no cutaway
                // mesh or separate presentation model is involved.
                ("09_great_hall",  centre + new Vector3(0f, 2.2f, 2f),
                                    centre + new Vector3(7f, 2.0f, 8f)),
                ("10_dungeon",     centre + new Vector3(-4f, -15.2f, 9f),
                                    centre + new Vector3(6f, -15.0f, 13f)),
                ("11_cave",        centre + new Vector3(0f, -15.0f, -31f),
                                    centre + new Vector3(5f, -14.5f, -27f)),

                // Presentation-only section through the southern half of the keep. The renderer
                // skips the clip volume; RegionTable and BrickPool are never modified.
                ("12_cutaway",     centre + new Vector3(0f, 25f, -56f),
                                    centre + new Vector3(0f, 11f, 7f)),

                // From just inside the open front doors, explicitly proves that the grand stair
                // is visible and reads as the route to the occupied upper floor.
                ("13_stair_hall",  stairCamera, stairLook),

                ("14_trapdoor",    new Vector3(trapdoor.x, trapdoor.y + 18, trapdoor.z - 24) * 0.1f,
                                    new Vector3(trapdoor.x, trapdoor.y + 1, trapdoor.z) * 0.1f),
                ("15_secret_archive",
                                    new Vector3(plan.Centre.x + 18, baseY - 29,
                                                keepMin.z + 25) * 0.1f,
                                    new Vector3(plan.Centre.x - 32, baseY - 30,
                                                keepMin.z + 98) * 0.1f),
                ("23_open_trapdoor",
                                    new Vector3(trapdoor.x, trapdoor.y + 18,
                                                trapdoor.z - 24) * 0.1f,
                                    new Vector3(trapdoor.x, trapdoor.y - 6,
                                                trapdoor.z) * 0.1f),
                ("16_chapel",      new Vector3(keepMin.x + 12, baseY + 20,
                                                chapelMin.z + chapelDepth / 2 - 6) * 0.1f,
                                    new Vector3(chapelMin.x + 15, baseY + 22,
                                                chapelMin.z + chapelDepth / 2) * 0.1f),
                ("17_puzzle_room", new Vector3(plan.Centre.x + 190, baseY - 148,
                                                trapdoor.z) * 0.1f,
                                    new Vector3(plan.Centre.x + 226, baseY - 150,
                                                trapdoor.z) * 0.1f),
                ("18_treasury",    new Vector3(plan.Centre.x - 190, baseY - 148,
                                                trapdoor.z) * 0.1f,
                                    new Vector3(plan.Centre.x - 230, baseY - 150,
                                                trapdoor.z) * 0.1f),
                ("19_crystal_grotto",
                                    new Vector3(plan.Centre.x + 94, baseY - 148,
                                                trapdoor.z - 386) * 0.1f,
                                    new Vector3(plan.Centre.x + 145, baseY - 150,
                                                trapdoor.z - 386) * 0.1f),
                ("20_bedchamber",  new Vector3(plan.Centre.x - 48,
                                                baseY + plan.FloorHeight + 18,
                                                keepMin.z + keepSize.z / 2 - 42) * 0.1f,
                                    new Vector3(plan.Centre.x + 37,
                                                baseY + plan.FloorHeight + 15,
                                                keepMin.z + keepSize.z / 2 - 2) * 0.1f),
                ("21_library",     new Vector3(plan.Centre.x + 55,
                                                baseY + plan.FloorHeight * 3 + 18,
                                                keepMin.z + keepSize.z / 2 - 72) * 0.1f,
                                    new Vector3(plan.Centre.x - 35,
                                                baseY + plan.FloorHeight * 3 + 15,
                                                keepMin.z + keepSize.z / 2 - 35) * 0.1f),

                ("24_bell_tower", new Vector3(bellTower.x - 160,
                                                baseY + 180,
                                                bellTower.z - 220) * 0.1f,
                                    new Vector3(bellTower.x + 8,
                                                baseY + 105,
                                                bellTower.z) * 0.1f),
                ("25_bell_stair", new Vector3(bellTower.x + 19,
                                                baseY + plan.FloorHeight * 2 + 18,
                                                bellTower.z + 18) * 0.1f,
                                    new Vector3(bellTower.x - 18,
                                                baseY + plan.FloorHeight * 2 + 14,
                                                bellTower.z - 7) * 0.1f),

                // Primary acceptance fixture for curved reconstruction, radial voussoir seams,
                // pier transitions, and moss-as-coating.
                ("26_reference_arch", archCentre + new Vector3(0f, 1.5f, -14f),
                                      archCentre + new Vector3(0f, 0.5f, 0f)),

                // The actual first-person reveal is part of the authored presentation. Keep it
                // under visual regression alongside the diagnostic orbit views.
                ("22_player_reveal", playerRevealPosition, playerRevealLook),

                // Terrain far from the castle, to tell whether the terracing is the castle's
                // sculpting or the terrain generator's own stepping.
                ("07_terrain",     centre + new Vector3(260f, 22f, 260f), centre + new Vector3(360f, 12f, 360f)),
            };

            foreach (var view in views)
            {
                if (view.name == "15_secret_archive")
                {
                    Assert.That(world.TryOpenCastleTrapdoor(world.CastleTrapdoorPosition + Vector3.up),
                                Is.True, "secret-room capture requires the hatch to open first");
                }

                bool cutaway = view.name == "12_cutaway";
                if (cutaway)
                {
                    showcase.SetCutawayPresentation(true,
                        new Vector3(keepMin.x + 8, 0f, keepMin.z - 4),
                        new Vector3(keepMin.x + keepSize.x - 8, 512f,
                                    keepMin.z + keepSize.z / 2));
                }
                else showcase.SetCutawayPresentation(false);

                cam.transform.position = view.position;
                cam.transform.LookAt(view.lookAt);

                // The arch spans several extraction chunks; give its acceptance view enough
                // budgeted frames to converge before capture. Ordinary views retain the fast
                // two-frame cadence.
                int settleFrames = view.name == "26_reference_arch" ? 24 : 2;
                for (int frame = 0; frame < settleFrames; frame++) yield return null;

                long usedBytesBeforeCapture = world.StoragePressure.UsedBytes;
                Capture(cam, Path.Combine(OutputDirectory, view.name + ".png"));
                if (cutaway)
                {
                    Assert.AreEqual(usedBytesBeforeCapture, world.StoragePressure.UsedBytes,
                        "Rendering a cutaway must not allocate, free, or carve authoritative storage.");
                }
                Debug.Log($"### SHOT {view.name} from {view.position}");
            }

            showcase.SetCutawayPresentation(false);

            Debug.Log($"### DONE wrote {views.Length} views to {OutputDirectory}");
            Assert.Greater(world.CastleVoxels, 100000, "the castle wrote almost nothing");
        }

        private static void Capture(Camera cam, string path)
        {
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var previous = cam.targetTexture;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            File.WriteAllBytes(path, texture.EncodeToPNG());

            cam.targetTexture = previous;
            Object.DestroyImmediate(texture);
            rt.Release();
        }
    }
}
