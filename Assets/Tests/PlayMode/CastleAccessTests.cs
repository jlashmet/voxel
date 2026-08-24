using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
using VoxelEngine.Storage.Api;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Structures.Api;
using Mat = Game.Materials.Api.GameMaterialIds;   // engine-side Mat constants were removed

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Proves that the generated castle is a navigable building, not a collection of visible
    /// rooms. Horizontal reachability uses CharacterMotor's 60 cm x 180 cm occupied volume;
    /// vertical reachability is checked at every 20 cm stair tread and floor landing.
    /// </summary>
    public sealed class CastleAccessTests
    {
        /// <summary>
        /// Loads the showcase and waits for the castle to finish building.
        ///
        /// These tests used to load the scene, yield a single frame, and read castle voxels
        /// straight out of the world. That worked while the castle was built in one blocking
        /// pass. It is now staged across frames so the build does not stall the showcase, which
        /// takes on the order of 180 frames, and every assertion here was reading empty terrain
        /// and reporting a missing gate, a missing keep floor, or a missing river.
        ///
        /// Waiting on CastleVoxels rather than a fixed frame count keeps the test honest if the
        /// build gets faster or slower; the cap only exists so a genuine hang fails as a test
        /// rather than hanging the run.
        /// </summary>
        private static IEnumerator LoadShowcaseWithCastle()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);

            for (int frame = 0; frame < 900 && world.CastleVoxels == 0; frame++)
                yield return null;

            Assert.Greater(world.CastleVoxels, 0,
                "The castle did not finish building within 900 frames, so nothing below is "
              + "testing the castle.");
        }

        [UnityTest]
        public IEnumerator SecretTrapdoorOnlyOpensForANearbyPlayer()
        {
            yield return LoadShowcaseWithCastle();

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);

            const int cx = ShowcaseWorld.RegionVoxelEdge / 2;
            const int cz = ShowcaseWorld.RegionVoxelEdge / 2 + 120;
            int ground = world.SurfaceHeight(cx, cz);
            var plan = StructuresComposition.PlanCastle(new int3(cx, ground, cz), world.Seed);
            int3 hatch = CastleLayout.TrapdoorCentre(in plan);

            Assert.AreEqual(Mat.Wood, Get(world, hatch.x, hatch.y, hatch.z),
                "the secret stair must begin behind a visible closed hatch");
            Assert.That(world.TryOpenCastleTrapdoor(world.CastleTrapdoorPosition + Vector3.right * 20f),
                        Is.False, "a distant E press must not open the hatch");
            Assert.That(world.TryOpenCastleTrapdoor(world.CastleTrapdoorPosition + Vector3.up),
                        Is.True, "a nearby E press should open the hatch");
            Assert.That(world.CastleTrapdoorOpen, Is.True);

            int half = CastleLayout.TrapdoorHalfSize;
            for (int y = hatch.y; y < hatch.y + 4; y++)
            for (int z = hatch.z - half; z < hatch.z + half; z++)
            for (int x = hatch.x - half; x < hatch.x + half; x++)
                Assert.AreEqual(Mat.Empty, Get(world, x, y, z),
                    $"opened hatch left a blocking voxel at {x},{y},{z}");
        }

        [UnityTest]
        public IEnumerator FrontGateOpensForANearbyPlayerAndClearsThePassage()
        {
            yield return LoadShowcaseWithCastle();

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);

            const int cx = ShowcaseWorld.RegionVoxelEdge / 2;
            const int cz = ShowcaseWorld.RegionVoxelEdge / 2 + 120;
            int ground = world.SurfaceHeight(cx, cz);
            CastlePlan plan = StructuresComposition.PlanCastle(new int3(cx, ground, cz), world.Seed);
            int3 min = CastleLayout.FrontGateMinimum(in plan);

            Assert.AreEqual(Mat.Wood,
                Get(world, min.x + 6, min.y + 8, min.z),
                "the front arch must begin with a visible closed timber gate");
            Assert.AreEqual(Mat.DarkStone,
                Get(world, min.x + CastleLayout.FrontGateWidth / 2, min.y + 8, min.z),
                "the closed gate must include visible structural ironwork");
            Assert.That(world.TryOpenCastleFrontGate(world.CastleFrontGatePosition
                                                     + Vector3.forward * 20f), Is.False,
                "a distant E interaction must not open the castle gate");
            Assert.That(world.TryOpenCastleFrontGate(world.CastleFrontGatePosition), Is.True,
                "a player on the bridge should be able to open the front gate");
            Assert.That(world.CastleFrontGateOpen, Is.True);

            int half = CastleLayout.FrontGateWidth / 2;
            int archTop = CastleLayout.FrontGateHeight - half;
            for (int d = 0; d < CastleLayout.FrontGateDepth; d++)
            for (int w = 0; w < CastleLayout.FrontGateWidth; w++)
            for (int h = 0; h < CastleLayout.FrontGateHeight; h++)
            {
                int dx = w - half;
                if (h > archTop && dx * dx + (h - archTop) * (h - archTop) > half * half)
                    continue;
                Assert.AreEqual(Mat.Empty, Get(world, min.x + w, min.y + h, min.z + d),
                    $"opened front gate left a blocking voxel at {w},{h},{d}");
            }
        }

        [UnityTest]
        public IEnumerator CastleLandscapeContainsConnectedWaterLevelsAndSupportedBridge()
        {
            yield return LoadShowcaseWithCastle();

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);

            const int cx = ShowcaseWorld.RegionVoxelEdge / 2;
            const int cz = ShowcaseWorld.RegionVoxelEdge / 2 + 120;
            int ground = world.SurfaceHeight(cx, cz);
            CastlePlan plan = StructuresComposition.PlanCastle(new int3(cx, ground, cz), world.Seed);
            int top = plan.Centre.y + plan.PlateauHeight;
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            int riverZ = gateZ - plan.WallThickness - 92;
            int riverY = top - CastleLayout.LowerRiverDepth;

            Assert.AreEqual(Mat.Water, Get(world, cx, riverY, riverZ),
                "the lower approach river is missing beneath the bridge");
            Assert.AreEqual(Mat.Wood, Get(world, cx, top - 2, riverZ),
                "the timber bridge does not cross the lower river");
            Assert.AreEqual(Mat.DarkStone, Get(world, cx + 24, riverY + 10, riverZ),
                "the bridge deck has no masonry pier carrying it from the river bed");

            bool grassBank = false;
            bool dirtBank = false;
            int bankX = cx + 80;
            int bankChannelZ = riverZ
                + (int)math.round(math.sin((bankX - plan.Centre.x) * 0.028f) * 8f
                                  + math.sin((bankX - plan.Centre.x) * 0.071f) * 3f);
            int bankZ = bankChannelZ + 65;
            for (int y = riverY; y <= top; y++)
            {
                byte material = Get(world, bankX, y, bankZ);
                grassBank |= material == Mat.Grass;
                dirtBank |= material == Mat.Dirt;
            }
            Assert.True(grassBank && dirtBank,
                "the gorge wall must expose both a grass lip and dirt strata");

            int streamX = CastleLayout.WaterfallStreamX(in plan);
            int lipZ = CastleLayout.WaterfallLipZ(in plan);
            int streamStartZ = plan.Centre.z + plan.BaileyHalfZ + plan.TowerRadius + 18;
            int streamZ = (streamStartZ + lipZ) / 2;
            float streamT = (streamStartZ - streamZ)
                          / (float)math.max(1, streamStartZ - lipZ);
            int streamCentreX = streamX
                              + (int)math.round(math.sin(streamT * math.PI * 3.2f) * 7f);
            int streamY = top - 6 - (int)math.round(streamT * 11f);
            Assert.AreEqual(Mat.Water, Get(world, streamCentreX, streamY, streamZ),
                "the upper stream beside the castle is missing");

            int poolY = top - 80;
            Assert.AreEqual(Mat.Cascade, Get(world, streamX, poolY + 2, lipZ),
                "the upper stream does not form a waterfall into its plunge pool");

            int lowerRiverAtOutlet = CastleLayout.LowerRiverZAt(in plan, streamX);
            int poolZ = lowerRiverAtOutlet + 27;
            int outletZ = (poolZ + lowerRiverAtOutlet) / 2;
            float outletT = (poolZ - outletZ)
                          / (float)math.max(1, poolZ - lowerRiverAtOutlet);
            int outletY = (int)math.round(math.lerp(poolY, riverY, outletT));
            Assert.AreEqual(Mat.Water, Get(world, streamX, outletY, outletZ),
                "the waterfall pool is not connected to the lower river");

            int poolRadiusX = 68;
            for (int x = streamX - poolRadiusX - 10; x <= streamX + poolRadiusX + 10; x += 4)
            for (int z = lowerRiverAtOutlet - 10; z <= lipZ + 30; z += 4)
            {
                bool waterBelow = false;
                bool structurallyAnchored = false;
                for (int y = top - CastleLayout.LowerRiverDepth - 12; y <= top + 8; y++)
                {
                    byte material = Get(world, x, y, z);
                    if (material == Mat.Water || material == Mat.Cascade)
                    {
                        waterBelow = true;
                        structurallyAnchored = false;
                        continue;
                    }
                    if (material == Mat.Empty || !waterBelow) continue;
                    bool looseTerrain = material == Mat.Grass || material == Mat.Dirt
                                     || material == Mat.Moss || material == Mat.Sand;
                    Assert.False(looseTerrain && !structurallyAnchored,
                        $"unsupported terrain shelf remains above water at {x},{y},{z}");
                    structurallyAnchored = true;
                }
            }
        }

        [UnityTest]
        public IEnumerator SceneIssue20260823014108038WaterfallRemainsVisibleAndUnoccluded()
        {
            yield return LoadShowcaseWithCastle();

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);

            CastlePlan plan = (CastlePlan)typeof(ShowcaseWorld)
                .GetField("_castlePlan", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(world);
            int top = plan.Centre.y + plan.PlateauHeight;
            int streamX = CastleLayout.WaterfallStreamX(in plan);
            int lipZ = CastleLayout.WaterfallLipZ(in plan);
            int poolY = top - 80;

            int streamStartZ = plan.Centre.z + plan.BaileyHalfZ + plan.TowerRadius + 18;
            int streamZ = (streamStartZ + lipZ) / 2;
            float streamT = (streamStartZ - streamZ)
                          / (float)math.max(1, streamStartZ - lipZ);
            int streamCentreX = streamX
                              + (int)math.round(math.sin(streamT * math.PI * 3.2f) * 7f);
            int streamY = top - 6 - (int)math.round(streamT * 11f);

            int streamWater = 0;
            for (int y = streamY - 8; y <= streamY + 8; y++)
            for (int x = streamCentreX - 20; x <= streamCentreX + 20; x++)
                if (Get(world, x, y, streamZ) == Mat.Water) streamWater++;
            Assert.That(streamWater, Is.GreaterThan(8),
                "the authored upper stream must carry a visible water volume toward the lip");

            int cascadeVoxels = 0;
            for (int y = poolY - 8; y <= top - 16; y++)
            for (int x = streamX - 23; x <= streamX + 23; x++)
                if (Get(world, x, y, lipZ) == Mat.Cascade) cascadeVoxels++;
            Assert.That(cascadeVoxels, Is.GreaterThan(100),
                "the saved castle-ravine view must contain a substantial waterfall cascade");

            int clearY = (poolY + top - 16) / 2;
            Assert.AreEqual(Mat.Empty, Get(world, streamX + 28, clearY, lipZ),
                "terrain must not occlude the east air lane beside the waterfall");
            Assert.AreEqual(Mat.Empty, Get(world, streamX - 28, clearY, lipZ),
                "terrain must not occlude the west air lane beside the waterfall");
            Assert.AreEqual(Mat.Empty, Get(world, streamX, clearY, lipZ + 4),
                "terrain must not bridge across the cleared ravine behind the waterfall");
        }

        [UnityTest]
        public IEnumerator PlayerEInteractionUsesMotorProximityAndClearsItsPrompt()
        {
            yield return LoadShowcaseWithCastle();

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);
            var motor = (CharacterMotor)typeof(VoxelShowcase)
                .GetField("_motor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);

            motor.Position = world.CastleTrapdoorPosition + Vector3.right * 20f;
            Assert.That(showcase.InteractionPromptVisible, Is.False,
                "the E prompt must not advertise an out-of-range interaction");
            Assert.That(showcase.TryInteract(), Is.False,
                "the player-facing interaction must reject a distant motor position");

            motor.Position = world.CastleTrapdoorPosition + Vector3.up;
            Assert.That(showcase.InteractionPromptVisible, Is.True,
                "a nearby player should receive the E prompt");
            Assert.That(showcase.TryInteract(), Is.True,
                "the same operation bound to E should open the nearby hatch");
            Assert.That(world.CastleTrapdoorOpen, Is.True);
            Assert.That(showcase.InteractionPromptVisible, Is.False,
                "the prompt must disappear after the one-shot hatch interaction completes");
        }

        [UnityTest]
        public IEnumerator EveryKeepRoomAndHallWingConnectsToTheMainStair()
        {
            yield return LoadShowcaseWithCastle();

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);

            const int cx = ShowcaseWorld.RegionVoxelEdge / 2;
            const int cz = ShowcaseWorld.RegionVoxelEdge / 2 + 120;
            int ground = world.SurfaceHeight(cx, cz);
            var plan = StructuresComposition.PlanCastle(new int3(cx, ground, cz), world.Seed);

            int baseY = plan.Centre.y + plan.PlateauHeight;
            var keepMin = new int3(plan.Centre.x - plan.KeepHalfX, baseY,
                                   plan.Centre.z - plan.KeepHalfZ + 60);
            var keepSize = new int3(plan.KeepHalfX * 2, plan.KeepHeight,
                                    plan.KeepHalfZ * 2);
            int stairX = keepMin.x + 34;
            int stairZ = keepMin.z + 34;

            AssertActorClear(world, new int3(plan.Centre.x, baseY + 2, keepMin.z + 4),
                "main keep entrance");

            // The rear timber oriel is occupied volume, not a sealed exterior badge. Both
            // storeys share the keep floor elevation and their broad thresholds stay clear.
            int orielMinX = plan.Centre.x + 18;
            int orielWallZ = keepMin.z + keepSize.z;
            for (int storey = 2; storey <= 3; storey++)
            {
                int footY = baseY + storey * plan.FloorHeight + 4;
                AssertActorClear(world, new int3(orielMinX + 22, footY, orielWallZ - 3),
                    $"rear oriel threshold storey {storey}");
                AssertActorClear(world, new int3(orielMinX + 22, footY, orielWallZ + 10),
                    $"rear oriel room storey {storey}");
            }

            // The asymmetrical chapel is an occupied wing, not facade dressing. Its central
            // aisle must connect the altar end directly to the keep joining arch.
            int chapelWidth = math.max(78, keepSize.x / 3);
            int chapelDepth = math.max(96, keepSize.z * 3 / 5);
            var chapelMin = new int3(keepMin.x - chapelWidth + 4, baseY,
                                     keepMin.z + keepSize.z - chapelDepth - 38);
            int chapelCentreZ = chapelMin.z + chapelDepth / 2;
            for (int x = chapelMin.x + 31; x <= keepMin.x + 4; x += 2)
                AssertActorClear(world, new int3(x, baseY + 2, chapelCentreZ),
                    $"chapel aisle at x={x}");

            // The offset bell tower is four occupied storeys, not a sealed skyline prop. Its
            // ground threshold joins the chapel's rear aisle and its spiral must provide a clear
            // landing on every upper floor.
            int3 bellCentre = CastleLayout.ChapelBellTowerCentre(in plan);
            int bellMinZ = bellCentre.z - CastleLayout.ChapelBellTowerSize / 2;
            for (int z = chapelCentreZ; z <= bellMinZ + 9; z += 2)
                AssertActorClear(world, new int3(bellCentre.x, baseY + 2, z),
                    $"chapel bell-tower threshold at z={z}");

            int bellStairX = bellCentre.x + CastleLayout.ChapelBellTowerSize / 2 - 19;
            for (int floor = 1; floor < 4; floor++)
                AssertStairLanding(world, bellStairX, baseY + 2, bellCentre.z,
                                   CastleLayout.ChapelBellTowerStairRadius,
                                   floor * plan.FloorHeight - 2,
                                   $"chapel bell tower floor {floor}");

            // Curtain, gatehouse, and keep turrets are rooms too. Each must have a player-sized
            // inward-facing entrance and a first-floor landing on its own stair.
            int hx = plan.BaileyHalfX, hz = plan.BaileyHalfZ;
            var towers = new List<(int3 centre, int radius)>
            {
                (new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz), plan.TowerRadius),
                (new int3(plan.Centre.x + hx, baseY, plan.Centre.z - hz), plan.TowerRadius),
                (new int3(plan.Centre.x - hx, baseY, plan.Centre.z + hz), plan.TowerRadius),
                (new int3(plan.Centre.x + hx, baseY, plan.Centre.z + hz), plan.TowerRadius),
                (new int3(plan.Centre.x - 54, baseY, plan.Centre.z - hz), plan.GateTowerRadius),
                (new int3(plan.Centre.x + 54, baseY, plan.Centre.z - hz), plan.GateTowerRadius),
                (new int3(keepMin.x, baseY, keepMin.z), 26),
                (new int3(keepMin.x + keepSize.x, baseY, keepMin.z), 26),
                (new int3(keepMin.x, baseY, keepMin.z + keepSize.z), 26),
                (new int3(keepMin.x + keepSize.x, baseY, keepMin.z + keepSize.z), 26),
            };

            foreach (var tower in towers)
            {
                AssertTowerDoor(world, in plan, tower.centre, tower.radius);
                AssertStairLanding(world, tower.centre.x, baseY + 2, tower.centre.z,
                                   tower.radius - 14, plan.FloorHeight - 2,
                                   $"tower at {tower.centre}");
            }

            int wingWidth = math.max(96, keepSize.x * 2 / 5);
            int wingDepth = math.max(80, keepSize.z - 72);
            var wingMin = new int3(keepMin.x + keepSize.x - 4, baseY, keepMin.z + 24);

            for (int floor = 0; floor < plan.Floors; floor++)
            {
                int floorY = baseY + floor * plan.FloorHeight;
                int footY = floor == 0 ? floorY + 2 : floorY + 4;
                var source = FindWalkable(world, new int2(stairX + 27, stairZ), footY,
                                          keepMin, keepSize, wingMin, wingWidth, wingDepth,
                                          parity: null);

                var targets = new List<int2>
                {
                    new(plan.Centre.x, keepMin.z + keepSize.z / 4),
                    // Offset the rear-room sample from the authored trapdoor. The closed lid is
                    // intentionally a separate walkable island above its stairwell; the room's
                    // circulation should be tested beside it, while the dedicated interaction
                    // test owns the hatch state contract.
                    new(plan.Centre.x + 46, keepMin.z + keepSize.z * 3 / 4),
                };

                if (floor < 2)
                    targets.Add(new int2(wingMin.x + wingWidth / 2,
                                         wingMin.z + wingDepth / 2));

                foreach (int2 nominal in targets)
                {
                    var target = FindWalkable(world, nominal, footY, keepMin, keepSize,
                                              wingMin, wingWidth, wingDepth, source);
                    Assert.That(CanReach(world, source, target, footY, keepMin, keepSize,
                                         wingMin, wingWidth, wingDepth), Is.True,
                        $"floor {floor}: room at {nominal} is disconnected from the main stair");
                }

                if (floor >= 2)
                {
                    var partitionDoor = FindWalkable(world,
                        new int2(plan.Centre.x, keepMin.z + keepSize.z / 2), footY,
                        keepMin, keepSize, wingMin, wingWidth, wingDepth, source);
                    Assert.That(CanReach(world, source, partitionDoor, footY, keepMin, keepSize,
                                         wingMin, wingWidth, wingDepth), Is.True,
                        $"floor {floor}: partition doorway is blocked");
                }

                if (floor > 0)
                    AssertStairLanding(world, stairX, baseY + 2, stairZ, 22,
                                       floor * plan.FloorHeight - 2,
                                       $"keep floor {floor}");
            }

            // The occupied lower world is part of the circulation graph too: ground -> cellar
            // and cellar -> dungeon are separate flights, both ending on a walkable landing.
            int cellarY = baseY - 46;
            int dungeonY = cellarY - 120;
            int trapX = plan.Centre.x;
            int trapZ = keepMin.z + plan.KeepHalfZ + 40;
            Assert.That(world.TryOpenCastleTrapdoor(world.CastleTrapdoorPosition + Vector3.up),
                        Is.True, "the access graph must be evaluated with its secret hatch open");
            AssertStairLanding(world, trapX, cellarY, trapZ, 9, 44, "cellar to ground");
            AssertStairLanding(world, trapX, dungeonY, trapZ, 13, 118, "dungeon to cellar");

            // Distinct underground set pieces are part of the same walkable graph. Sample the
            // deliberately authored centre lines up to each furnished focal area.
            for (int x = trapX + 120; x <= trapX + 208; x += 2)
                AssertActorClear(world, new int3(x, dungeonY + 2, trapZ),
                    $"puzzle-room branch at x={x}");
            for (int x = trapX - 120; x >= trapX - 258; x -= 2)
                AssertActorClear(world, new int3(x, dungeonY + 2, trapZ),
                    $"treasury branch at x={x}");

            int mainCaveZ = trapZ - 411;
            int sideCaveZ = mainCaveZ + 25;
            for (int x = trapX; x <= trapX + 128; x += 2)
                AssertActorClear(world, new int3(x, dungeonY + 2, sideCaveZ),
                    $"crystal-grotto branch at x={x}");

            Assert.AreEqual(Mat.Empty, Get(world, trapX, dungeonY + 50, mainCaveZ),
                "the main cave must have the tall natural vault visible in the reference");
            Assert.AreEqual(Mat.Water, Get(world, trapX + 20, dungeonY - 5, mainCaveZ),
                "the main cave is missing its reflective lower pool");
            Assert.AreEqual(Mat.Cascade,
                Get(world, trapX + 27, dungeonY + 10, mainCaveZ - 76),
                "the rear cave waterfall is missing");
            Assert.AreEqual(Mat.Crystal,
                Get(world, trapX - 58, dungeonY + 6, mainCaveZ - 34),
                "the cave lacks the cyan crystal focal material from the reference");
        }

        private static void AssertStairLanding(ShowcaseWorld world, int cx, int baseY, int cz,
                                               int radius, int riseFromBase, string label)
        {
            const int rise = 2;
            const int run = 3;
            int step = riseFromBase / rise;
            int innerRadius = math.max(2, radius - 10);
            float walkingRadius = (innerRadius + radius) * 0.5f;
            float angle = (step + 0.4f) * (run / walkingRadius);
            int x = cx + (int)math.round(math.cos(angle) * walkingRadius);
            int z = cz + (int)math.round(math.sin(angle) * walkingRadius);
            int y = baseY + step * rise;

            Assert.AreNotEqual(Mat.Empty, Get(world, x, y, z), $"{label}: missing landing tread");
            for (int h = 2; h < 18; h++)
                Assert.AreEqual(Mat.Empty, Get(world, x, y + h, z),
                    $"{label}: blocked headroom at +{h}");
        }

        private static void AssertTowerDoor(ShowcaseWorld world, in CastlePlan plan,
                                            int3 tower, int radius)
        {
            int dx = plan.Centre.x - tower.x;
            int dz = plan.Centre.z - tower.z;
            int doorX = tower.x;
            int doorZ = tower.z;

            if (math.abs(dx) > math.abs(dz))
                doorX = dx >= 0 ? tower.x + radius - 7 : tower.x - radius + 7;
            else
                doorZ = dz >= 0 ? tower.z + radius - 7 : tower.z - radius + 7;

            AssertActorClear(world, new int3(doorX, tower.y + 2, doorZ),
                $"tower entrance at {tower}");
        }

        private static int2 FindWalkable(ShowcaseWorld world, int2 nominal, int footY,
                                         int3 keepMin, int3 keepSize, int3 wingMin,
                                         int wingWidth, int wingDepth, int2? parity)
        {
            for (int radius = 0; radius <= 18; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var p = nominal + new int2(dx, dz);
                if (parity.HasValue && (((p.x - parity.Value.x) & 1) != 0
                                     || ((p.y - parity.Value.y) & 1) != 0)) continue;
                if (!InsideInterior(p, keepMin, keepSize, wingMin, wingWidth, wingDepth)) continue;
                if (ActorClear(world, p.x, footY, p.y)) return p;
            }

            Assert.Fail($"No actor-sized clear point near {nominal} at y={footY}.");
            return nominal;
        }

        private static bool CanReach(ShowcaseWorld world, int2 start, int2 target, int footY,
                                     int3 keepMin, int3 keepSize, int3 wingMin,
                                     int wingWidth, int wingDepth)
        {
            var queue = new Queue<int2>();
            var visited = new HashSet<int2>();
            queue.Enqueue(start);
            visited.Add(start);
            int2[] directions = { new(2, 0), new(-2, 0), new(0, 2), new(0, -2) };

            while (queue.Count > 0)
            {
                int2 current = queue.Dequeue();
                if (current.Equals(target)) return true;

                foreach (int2 direction in directions)
                {
                    int2 next = current + direction;
                    if (visited.Contains(next)
                        || !InsideInterior(next, keepMin, keepSize, wingMin, wingWidth, wingDepth)
                        || !ActorClear(world, next.x, footY, next.y)) continue;
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
            foreach (int2 p in visited)
            {
                minX = math.min(minX, p.x); maxX = math.max(maxX, p.x);
                minZ = math.min(minZ, p.y); maxZ = math.max(maxZ, p.y);
            }
            Debug.Log($"Castle access miss at y={footY}: {start} -> {target}; " +
                      $"reached {visited.Count} samples, x={minX}..{maxX}, z={minZ}..{maxZ}");
            return false;
        }

        private static bool InsideInterior(int2 p, int3 keepMin, int3 keepSize,
                                           int3 wingMin, int wingWidth, int wingDepth)
        {
            bool keep = p.x >= keepMin.x + 8 && p.x < keepMin.x + keepSize.x - 8
                     && p.y >= keepMin.z + 8 && p.y < keepMin.z + keepSize.z - 8;
            bool wing = p.x >= wingMin.x + 6 && p.x < wingMin.x + wingWidth - 6
                     && p.y >= wingMin.z + 6 && p.y < wingMin.z + wingDepth - 6;
            int connectorZ = wingMin.z + wingDepth / 2;
            bool connector = p.x >= keepMin.x + keepSize.x - 12
                          && p.x < wingMin.x + 8
                          && math.abs(p.y - connectorZ) <= 7;
            return keep || wing || connector;
        }

        private static bool ActorClear(ShowcaseWorld world, int cx, int footY, int cz)
        {
            for (int y = footY; y < footY + 18; y++)
            for (int z = cz - 3; z < cz + 3; z++)
            for (int x = cx - 3; x < cx + 3; x++)
                if (Get(world, x, y, z) != Mat.Empty) return false;

            return Get(world, cx, footY - 2, cz) != Mat.Empty
                || Get(world, cx, footY - 1, cz) != Mat.Empty;
        }

        private static void AssertActorClear(ShowcaseWorld world, int3 feet, string label)
        {
            if (ActorClear(world, feet.x, feet.y, feet.z)) return;

            for (int y = feet.y; y < feet.y + 18; y++)
            for (int z = feet.z - 3; z < feet.z + 3; z++)
            for (int x = feet.x - 3; x < feet.x + 3; x++)
            {
                byte material = Get(world, x, y, z);
                if (material != Mat.Empty)
                    Assert.Fail($"{label} has material {material} blocking the player capsule " +
                                $"at {x},{y},{z}");
            }

            Assert.Fail($"{label} has no solid floor beneath {feet}");
        }

        private static byte Get(ShowcaseWorld world, int x, int y, int z) =>
            world.SurfaceQuery.TryRead(new int3(x, y, z), out VoxelCell cell)
                ? cell.BaseMaterialId : VoxelGrid.MaterialEmpty;
    }
}
