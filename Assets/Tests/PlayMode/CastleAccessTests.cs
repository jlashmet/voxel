using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Core.Storage;
using VoxelEngine.Showcase;
using VoxelEngine.Structures;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Proves that the generated castle is a navigable building, not a collection of visible
    /// rooms. Horizontal reachability uses CharacterMotor's 60 cm x 180 cm occupied volume;
    /// vertical reachability is checked at every 20 cm stair tread and floor landing.
    /// </summary>
    public sealed class CastleAccessTests
    {
        [UnityTest]
        public IEnumerator SecretTrapdoorOnlyOpensForANearbyPlayer()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);

            const int cx = ShowcaseWorld.RegionVoxelEdge / 2;
            const int cz = ShowcaseWorld.RegionVoxelEdge / 2 + 120;
            int ground = world.SurfaceHeight(cx, cz);
            var plan = CastleBuilder.Plan(new int3(cx, ground, cz), world.Seed);
            int3 hatch = CastleBuilder.TrapdoorCentre(in plan);

            Assert.AreEqual(Mat.Wood, Get(world, hatch.x, hatch.y, hatch.z),
                "the secret stair must begin behind a visible closed hatch");
            Assert.That(world.TryOpenCastleTrapdoor(world.CastleTrapdoorPosition + Vector3.right * 20f),
                        Is.False, "a distant E press must not open the hatch");
            Assert.That(world.TryOpenCastleTrapdoor(world.CastleTrapdoorPosition + Vector3.up),
                        Is.True, "a nearby E press should open the hatch");
            Assert.That(world.CastleTrapdoorOpen, Is.True);

            int half = CastleBuilder.TrapdoorHalfSize;
            for (int y = hatch.y; y < hatch.y + 4; y++)
            for (int z = hatch.z - half; z < hatch.z + half; z++)
            for (int x = hatch.x - half; x < hatch.x + half; x++)
                Assert.AreEqual(Mat.Empty, Get(world, x, y, z),
                    $"opened hatch left a blocking voxel at {x},{y},{z}");
        }

        [UnityTest]
        public IEnumerator EveryKeepRoomAndHallWingConnectsToTheMainStair()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);

            const int cx = ShowcaseWorld.RegionVoxelEdge / 2;
            const int cz = ShowcaseWorld.RegionVoxelEdge / 2 + 120;
            int ground = world.SurfaceHeight(cx, cz);
            var plan = CastleBuilder.Plan(new int3(cx, ground, cz), world.Seed);

            int baseY = plan.Centre.y + plan.PlateauHeight;
            var keepMin = new int3(plan.Centre.x - plan.KeepHalfX, baseY,
                                   plan.Centre.z - plan.KeepHalfZ + 60);
            var keepSize = new int3(plan.KeepHalfX * 2, plan.KeepHeight,
                                    plan.KeepHalfZ * 2);
            int stairX = keepMin.x + 34;
            int stairZ = keepMin.z + 34;

            AssertActorClear(world, new int3(plan.Centre.x, baseY + 2, keepMin.z + 4),
                "main keep entrance");

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
            Assert.That(ActorClear(world, feet.x, feet.y, feet.z), Is.True,
                $"{label} does not fit the player capsule");
        }

        private static byte Get(ShowcaseWorld world, int x, int y, int z) =>
            VoxelAccess.GetVoxel(ref world.Table, in world.Pool, new int3(x, y, z));
    }
}
