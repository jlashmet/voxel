using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Core.Terrain;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures
{
    /// <summary>Dimensions drawn for one castle. Every field is in voxels; one voxel is 10 cm.</summary>
    public struct CastlePlan
    {
        public int3 Centre;

        public int PlateauRadius;
        public int PlateauHeight;
        public int CliffDrop;

        public int BaileyHalfX, BaileyHalfZ;
        public int WallHeight, WallThickness;

        public int TowerRadius, TowerHeight;
        public int GateTowerRadius, GateTowerHeight;

        public int KeepHalfX, KeepHalfZ, KeepHeight;
        public int FloorHeight;
        public int Floors;

        public uint Seed;
    }

    /// <summary>
    /// Builds a castle: its site, its walls, its keep, its interiors, and the dungeon beneath it.
    ///
    /// A *family* rather than one castle — every dimension is drawn from the seed within a range,
    /// so two castles share a vocabulary without sharing a floor plan. The vocabulary is what
    /// makes them read as castles; the variation is what stops them reading as copies.
    ///
    /// The castle defines its terrain rather than adapting to it. A fortress is sited on the rock
    /// it commands, and the rock is shaped by the fortress being there — so the plateau, the
    /// cliffs and the moat are part of this build, not something it is dropped onto.
    ///
    /// Runs once, offline or at load. Nothing here is on a frame budget.
    /// </summary>
    public static class CastleBuilder
    {
        public const int TrapdoorHalfSize = 8;

        /// <summary>Centre of the ground-floor hatch leading to the cellar.</summary>
        public static int3 TrapdoorCentre(in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int keepMinZ = plan.Centre.z - plan.KeepHalfZ + 60;
            return new int3(plan.Centre.x, baseY, keepMinZ + plan.KeepHalfZ + 40);
        }

        /// <summary>Draws a plan from a seed. This is where the family lives.</summary>
        public static CastlePlan Plan(int3 centre, uint seed)
        {
            var rng = new Random(seed | 1u);

            // Roughly 44-56 m across the bailey. This is about twice the footprint area of the
            // former 30-40 m plan, but not twice every linear dimension: doing that would
            // quadruple the sculpted site and exhaust the brick pool before interiors existed.
            int baileyX = rng.NextInt(220, 280);
            int baileyZ = rng.NextInt(220, 280);

            // A circular crag must contain the rectangular bailey's corners, not merely its
            // longest half-axis. The old radius left all four corner towers standing beyond the
            // sculpted site on whatever terrain happened to be underneath them.
            int plateauRadius = (int)math.ceil(math.sqrt(baileyX * baileyX + baileyZ * baileyZ))
                                + rng.NextInt(18, 32);
            int plateauHeight = rng.NextInt(26, 44);
            int cliffDrop = rng.NextInt(26, 44);
            int wallHeight = rng.NextInt(82, 108);
            int wallThickness = rng.NextInt(18, 25);
            int towerRadius = rng.NextInt(30, 39);
            int towerHeight = rng.NextInt(125, 160);
            int gateTowerRadius = rng.NextInt(28, 36);
            int gateTowerHeight = rng.NextInt(135, 172);
            int keepHalfX = rng.NextInt(92, 121);
            int keepHalfZ = rng.NextInt(78, 101);

            // Consume the former independent height draw so all later seeded choices remain
            // stable. Height now comes from the floor stack, but changing unrelated dimensions
            // would make before/after screenshots much harder to compare.
            rng.NextInt(190, 240);
            const int floorHeight = 38;
            int floors = rng.NextInt(5, 7);

            return new CastlePlan
            {
                Centre = centre,
                Seed = seed,

                // Tight to the walls. A wide skirt of sculpted rock reads as a quarry, not a
                // crag, because a smooth analytic falloff quantised to voxels produces clean
                // contour rings — natural terrain hides that behind noise and this did not.
                PlateauRadius = plateauRadius,
                PlateauHeight = plateauHeight,
                CliffDrop = cliffDrop,

                BaileyHalfX = baileyX,
                BaileyHalfZ = baileyZ,

                WallHeight = wallHeight,
                WallThickness = wallThickness,

                TowerRadius = towerRadius,
                TowerHeight = towerHeight,

                GateTowerRadius = gateTowerRadius,
                GateTowerHeight = gateTowerHeight,

                KeepHalfX = keepHalfX,
                KeepHalfZ = keepHalfZ,

                // Comfortably twice the curtain wall. A keep that only just clears the walls
                // gives the silhouette no centre, which is what the first pass looked like.
                KeepHeight = floors * floorHeight,

                FloorHeight = floorHeight,
                Floors = floors,
            };
        }

        /// <summary>
        /// Expensive-write equivalents this plan implies, estimated before anything is written.
        ///
        /// Bulk boxes, shells, cylinders, and terrain columns write whole bricks or batch a
        /// column before collapsing it, so charging them as millions of individual Set calls
        /// would reject safe plans. The weights below still grow with surface area and catch a
        /// runaway radius before construction; the brush's hard slow-write ceiling remains the
        /// authoritative second guard during every build stage.
        /// </summary>
        public static long EstimateWrites(in CastlePlan plan)
        {
            double plateauArea = math.PI_DBL * plan.PlateauRadius * plan.PlateauRadius;

            double siteCap = plateauArea * 3.0;

            double cliffArea = math.PI_DBL *
                ((plan.PlateauRadius + plan.CliffDrop) * (double)(plan.PlateauRadius + plan.CliffDrop)
                 - plan.PlateauRadius * (double)plan.PlateauRadius);
            double cliffCap = cliffArea * 4.0;

            double perimeter = 4.0 * (plan.BaileyHalfX + plan.BaileyHalfZ);
            double walls = perimeter * 240.0;

            double towers = 6.0 * math.PI_DBL * plan.TowerRadius * plan.TowerRadius * 30.0;

            double keep = plan.KeepHalfX * (double)plan.KeepHalfZ * plan.Floors * 4.0;

            double courtyard = plateauArea * 0.2;
            double underground = 1_500_000.0;   // dungeon, passage, cave

            return (long)(siteCap + cliffCap + walls + towers + keep + courtyard + underground);
        }

        /// <summary>
        /// Builds everything, or refuses.
        ///
        /// The refusal is the point. A plan that would write more than the brush's budget is a
        /// mistake in the plan, and finding out by running it costs an afternoon and a reboot.
        /// </summary>
        public static VoxelBrush Build(ref RegionTable table, ref BrickPool pool,
                                       in CastlePlan plan, uint terrainSeed)
        {
            var brush = new VoxelBrush(table, pool);

            long estimate = EstimateWrites(in plan);
            if (estimate > brush.WriteBudget)
            {
                UnityEngine.Debug.LogError(
                    $"CastleBuilder: refusing to build. Plan implies ~{estimate:N0} expensive-write equivalents, " +
                    $"budget is {brush.WriteBudget:N0}. Reduce PlateauRadius ({plan.PlateauRadius}) " +
                    $"or the primary structure dimensions before retrying.");
                return brush;
            }

            Site(ref brush, in plan, terrainSeed);
            RequireBudget(in brush, "site");
            CurtainWalls(ref brush, in plan);
            RequireBudget(in brush, "curtain walls");
            CornerTowers(ref brush, in plan);
            RequireBudget(in brush, "corner towers");
            Gatehouse(ref brush, in plan);
            RequireBudget(in brush, "gatehouse");
            Courtyard(ref brush, in plan);
            RequireBudget(in brush, "courtyard");
            Keep(ref brush, in plan);
            RequireBudget(in brush, "keep");
            Dungeon(ref brush, in plan);
            RequireBudget(in brush, "dungeon");
            LandscapeDetails(ref brush, in plan, terrainSeed);
            RequireBudget(in brush, "landscape details");

            // RegionTable and BrickPool are handle-like structs, but their scalar bookkeeping
            // (notably BrickPool's high-water mark) is copied by value. Publish the updated
            // handles back to the owner or later allocations can reuse slots the castle owns.
            table = brush.Table;
            pool = brush.Pool;

            return brush;
        }

        private static void RequireBudget(in VoxelBrush brush, string stage)
        {
            if (!brush.BudgetExceeded) return;

            throw new System.InvalidOperationException(
                $"CastleBuilder exceeded its {brush.WriteBudget:N0}-write budget while building " +
                $"the {stage}, after {brush.TotalVoxelsWritten:N0} changed voxels. " +
                "A partial castle is invalid.");
        }

        // -- site ----------------------------------------------------------------

        /// <summary>
        /// Carves the outcrop the castle stands on: a flat plateau, cliffs falling away, and a
        /// moat cut into the rock on the approach side.
        ///
        /// Done before any masonry so the walls have something to sit on, and so the cliff edge
        /// can be read when placing them.
        /// </summary>
        private static void Site(ref VoxelBrush brush, in CastlePlan plan, uint terrainSeed)
        {
            var rng = new Random(plan.Seed ^ 0x51E5u);

            int top = plan.Centre.y + plan.PlateauHeight;
            int radius = plan.PlateauRadius;
            int skirt = radius + plan.CliffDrop;

            for (int z = -skirt; z <= skirt; z++)
            for (int x = -skirt; x <= skirt; x++)
            {
                int wx = plan.Centre.x + x;
                int wz = plan.Centre.z + z;

                float d = math.sqrt(x * x + z * z);

                // Irregular edge: a perfectly circular plateau reads as a cake stand.
                float angle = math.atan2(z, x);
                float wobble = math.sin(angle * 3.7f) * 18f
                             + math.sin(angle * 8.3f) * 9f
                             + math.sin(angle * 17.1f) * 4f;

                float edge = radius + wobble;
                if (d > edge + plan.CliffDrop) continue;

                int ground = TerrainSampler.HeightAt(wx, wz, terrainSeed);

                int target;
                if (d <= edge)
                {
                    target = top;
                }
                else
                {
                    // Cliff face: steep, and broken up per column. The first version eased out of
                    // the plateau with pow(t, 0.55), which gives a long shallow shoulder — and a
                    // shallow slope in voxels is a staircase of contour terraces. Falling fast
                    // and unevenly is both more castle-like and cheaper.
                    float t = (d - edge) / plan.CliffDrop;
                    float broken = math.pow(t, 1.7f)
                                 + math.sin(angle * 11f + t * 6f) * 0.10f;

                    target = (int)math.round(math.lerp(top, ground - 14, math.saturate(broken)));
                }

                if (target <= ground)
                {
                    brush.FillColumnBulk(wx, target + 1, ground + 1, wz, Mat.Empty);
                }
                else
                {
                    // Building the outcrop up. The cap is written per voxel because it is the
                    // visible surface and wants its material bands; the bulk beneath goes in as
                    // whole bricks, which is thousands of times cheaper than writing it voxel by
                    // voxel and waiting for each brick to collapse back to uniform.
                    int stoneBottom = math.max(ground, target - 2);
                    brush.FillColumnBulk(wx, ground, stoneBottom, wz, Mat.DarkStone);
                    brush.FillColumnBulk(wx, stoneBottom, target + 1, wz, Mat.Stone);
                }

                // Grass cap on the plateau, away from where the walls will sit.
                if (d < edge - 12 && rng.NextInt(0, 100) < 92)
                    brush.FillColumnBulk(wx, target, target + 1, wz, Mat.Grass);
            }

            Moat(ref brush, in plan, top);
        }

        /// <summary>
        /// A channel cut across the approach, holding water.
        ///
        /// The first version spanned the full plateau width at plateau height, which put a slab
        /// of water in mid-air beyond the cliff edge. A moat has to be cut *into* ground that
        /// exists, so this only writes where it finds rock to cut.
        /// </summary>
        private static void Moat(ref VoxelBrush brush, in CastlePlan plan, int top)
        {
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            int moatZ = gateZ - 46;
            int halfWidth = 26;
            int depth = 34;

            int reach = plan.BaileyHalfX + 40;

            for (int z = moatZ - halfWidth; z <= moatZ + halfWidth; z++)
            for (int x = plan.Centre.x - reach; x <= plan.Centre.x + reach; x++)
            {
                // Leave a causeway to the gate rather than requiring the bridge to be the only
                // way across a full-width cut.
                if (math.abs(x - plan.Centre.x) < 22) continue;

                if (!brush.IsSolid(x, top - 1, z)) continue;   // nothing here to cut

                float t = math.abs(z - moatZ) / (float)halfWidth;
                int cut = (int)math.round(depth * (1f - t * t));
                if (cut <= 2) continue;

                brush.FillColumnBulk(x, top - cut, top + 7, z, Mat.Empty);
                brush.FillColumnBulk(x, top - cut,
                                     top - cut + math.max(4, cut / 2), z, Mat.Water);
            }
        }

        /// <summary>
        /// Composes the castle into a place rather than leaving it on an isolated analytic pad:
        /// a stream cuts through the eastern shoulder, falls into a rock pool, and a sparse tree
        /// belt frames the walls without obscuring the silhouette or the gate approach.
        /// </summary>
        private static void LandscapeDetails(ref VoxelBrush brush, in CastlePlan plan,
                                             uint terrainSeed)
        {
            int top = plan.Centre.y + plan.PlateauHeight;
            RavineWaterfall(ref brush, in plan, terrainSeed, top);
            TreeBelt(ref brush, in plan, top);
        }

        private static void RavineWaterfall(ref VoxelBrush brush, in CastlePlan plan,
                                            uint terrainSeed, int top)
        {
            int streamZ = plan.Centre.z + 42;
            int streamStartX = plan.Centre.x + plan.BaileyHalfX + plan.TowerRadius + 22;
            int fallX = plan.Centre.x + plan.PlateauRadius - 8;
            int streamLength = math.max(1, fallX - streamStartX);

            // A descending, irregular channel across the shoulder. Water occupies only the
            // bottom few voxels; the dark exposed sides are supplied by the outcrop beneath it.
            for (int x = streamStartX; x <= fallX; x++)
            {
                float t = (x - streamStartX) / (float)streamLength;
                int halfWidth = 8 + (int)math.round(t * 6f);
                int channelY = top - 7 - (int)math.round(t * 22f);

                for (int dz = -halfWidth; dz <= halfWidth; dz++)
                {
                    float across = math.abs(dz) / (float)halfWidth;
                    int bank = (int)math.round(across * across * 8f);
                    int bottom = channelY + bank;

                    brush.FillColumnBulk(x, bottom, top + 7, streamZ + dz, Mat.Empty);
                    if (math.abs(dz) <= halfWidth - 3)
                        brush.FillColumnBulk(x, bottom, bottom + 3, streamZ + dz, Mat.Water);
                }
            }

            // The pool is beyond the cliff edge and slightly below the procedural ground. A
            // fixed water plane gives the cascade a readable destination even on a noisy seed.
            int poolX = fallX + plan.CliffDrop + 24;
            int sampledGround = TerrainSampler.HeightAt(poolX, streamZ, terrainSeed);
            // The surrounding terrain varies by only a few metres. Force a deep but still
            // visible cut; a twelve-metre shaft hid opaque voxel water below the sightline.
            int poolY = math.min(top - 82, sampledGround - 12);
            const int poolRadiusX = 48;
            const int poolRadiusZ = 34;

            for (int dz = -poolRadiusZ; dz <= poolRadiusZ; dz++)
            for (int dx = -poolRadiusX; dx <= poolRadiusX; dx++)
            {
                float ellipse = dx * dx / (float)(poolRadiusX * poolRadiusX)
                              + dz * dz / (float)(poolRadiusZ * poolRadiusZ);
                if (ellipse > 1f) continue;

                // Rise all the way back to terrain height at the rim. The former seven-voxel
                // rise made a twelve-metre vertical shaft with water hidden at its bottom.
                float bankT = math.saturate((ellipse - 0.45f) / 0.55f);
                int bottom = ellipse <= 0.45f
                    ? poolY - 9
                    : (int)math.round(math.lerp(poolY - 9, top - 2,
                                                math.pow(bankT, 0.72f)));
                int wx = poolX + dx;
                int wz = streamZ + dz;

                brush.FillColumnBulk(wx, bottom, top + 6, wz, Mat.Empty);
                if (ellipse < 0.46f && bottom < poolY + 1)
                    brush.FillColumnBulk(wx, bottom, poolY + 1, wz, Mat.Water);
            }

            // A narrow sheet reads better at voxel scale than a broad opaque blue slab. The
            // empty pocket behind it keeps the falling water visibly separate from the cliff.
            for (int dx = 0; dx < 3; dx++)
            for (int dz = -8; dz <= 8; dz++)
            {
                brush.FillColumnBulk(fallX + dx, poolY + 1, top - 24,
                                     streamZ + dz, Mat.Empty);
                if (math.abs(dz) <= 6)
                    brush.FillColumnBulk(fallX + dx, poolY + 1, top - 24,
                                         streamZ + dz, Mat.Water);
            }

            // Carry the fall across the broken lower slope into the pool. This shallow stepped
            // course is visible from above, while the narrow vertical sheet reads from the side.
            int cascadeLength = math.max(1, poolX - fallX);
            for (int x = fallX; x <= poolX; x++)
            {
                float t = (x - fallX) / (float)cascadeLength;
                int waterY = (int)math.round(math.lerp(top - 25, poolY, t));
                for (int dz = -6; dz <= 6; dz++)
                {
                    brush.FillColumnBulk(x, waterY, top + 4, streamZ + dz, Mat.Empty);
                    if (math.abs(dz) <= 5)
                        brush.FillColumnBulk(x, waterY, waterY + 2, streamZ + dz, Mat.Water);
                }
            }

            // Broken rock at the pool rim hides the otherwise perfect ellipse.
            var rockRng = new Random(plan.Seed ^ 0xA11CEu);
            for (int i = 0; i < 10; i++)
            {
                float angle = rockRng.NextFloat(0f, math.PI * 2f);
                int rx = poolX + (int)math.round(math.cos(angle) * rockRng.NextFloat(38f, 54f));
                int rz = streamZ + (int)math.round(math.sin(angle) * rockRng.NextFloat(27f, 40f));
                int rockSurface = HighestSolid(ref brush, rx, rz, top + 12, poolY - 16);
                brush.Cone(rx, rockSurface + 1, rz, rockRng.NextInt(3, 6),
                           rockRng.NextInt(5, 11), Mat.DarkStone);
            }

            // A few anchored trees belong specifically to the ravine composition. The wider
            // seeded belt intentionally avoids this sector so it cannot hide the water.
            int2[] treeOffsets =
            {
                new(30, -48), new(34, 49), new(-42, 46), new(4, 55),
            };
            for (int i = 0; i < treeOffsets.Length; i++)
            {
                int tx = poolX + treeOffsets[i].x;
                int tz = streamZ + treeOffsets[i].y;
                int surface = HighestSolid(ref brush, tx, tz, top + 24, top - 180);
                Tree(ref brush, tx, surface + 1, tz, 42 + i * 3, 14 + (i & 1), Mat.Moss);
            }
        }

        private static int HighestSolid(ref VoxelBrush brush, int x, int z, int fromY, int minY)
        {
            for (int y = fromY; y >= minY; y--)
                if (brush.IsSolid(x, y, z)) return y;

            return minY;
        }

        private static void TreeBelt(ref VoxelBrush brush, in CastlePlan plan, int top)
        {
            var rng = new Random(plan.Seed ^ 0x7EE5u);
            int built = 0;

            // Rejection sampling keeps trees outside the walls, out of the gate approach, and
            // away from the waterfall. The fixed candidate ceiling makes cost deterministic.
            for (int attempt = 0; attempt < 96 && built < 22; attempt++)
            {
                float angle = rng.NextFloat(0f, math.PI * 2f);
                float radius = rng.NextFloat(plan.PlateauRadius * 0.74f,
                                             plan.PlateauRadius - 26f);
                int ox = (int)math.round(math.cos(angle) * radius);
                int oz = (int)math.round(math.sin(angle) * radius);

                bool outsideWalls = math.abs(ox) > plan.BaileyHalfX + plan.TowerRadius + 16
                                 || math.abs(oz) > plan.BaileyHalfZ + plan.TowerRadius + 16;
                bool blocksGate = oz < -plan.BaileyHalfZ && math.abs(ox) < 105;
                bool nearWaterfall = ox > plan.BaileyHalfX && math.abs(oz - 42) < 72;
                if (!outsideWalls || blocksGate || nearWaterfall) continue;

                int height = rng.NextInt(34, 58);
                int canopyRadius = rng.NextInt(12, 19);
                Tree(ref brush, plan.Centre.x + ox, top + 1, plan.Centre.z + oz,
                     height, canopyRadius, built % 3 == 0 ? Mat.Grass : Mat.Moss);
                built++;
            }
        }

        private static void Tree(ref VoxelBrush brush, int x, int y, int z,
                                 int height, int canopyRadius, byte foliage)
        {
            int trunkRadius = math.max(3, canopyRadius / 5);
            brush.Cylinder(x, y, z, trunkRadius, height, Mat.Wood);

            int centreY = y + height - canopyRadius / 2;
            int verticalRadius = canopyRadius + 5;
            for (int dz = -canopyRadius; dz <= canopyRadius; dz++)
            for (int dx = -canopyRadius; dx <= canopyRadius; dx++)
            {
                float radial = (dx * dx + dz * dz) / (float)(canopyRadius * canopyRadius);
                if (radial > 1f) continue;

                int halfHeight = math.max(1,
                    (int)math.round(math.sqrt(1f - radial) * verticalRadius));
                brush.FillColumnBulk(x + dx, centreY - halfHeight, centreY + halfHeight + 1,
                                     z + dz, foliage);
            }

            // A smaller offset crown avoids the unmistakable perfect-ellipsoid silhouette.
            int crownRadius = math.max(7, canopyRadius - 5);
            int crownX = x + canopyRadius / 3;
            int crownZ = z - canopyRadius / 4;
            int crownY = centreY + canopyRadius / 2;
            for (int dz = -crownRadius; dz <= crownRadius; dz++)
            for (int dx = -crownRadius; dx <= crownRadius; dx++)
            {
                int radialSq = dx * dx + dz * dz;
                if (radialSq > crownRadius * crownRadius) continue;
                int halfHeight = (int)math.round(math.sqrt(crownRadius * crownRadius - radialSq));
                brush.FillColumnBulk(crownX + dx, crownY - halfHeight, crownY + halfHeight + 1,
                                     crownZ + dz, foliage);
            }
        }

        // -- curtain walls -------------------------------------------------------

        private static void CurtainWalls(ref VoxelBrush brush, in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int hx = plan.BaileyHalfX, hz = plan.BaileyHalfZ;
            int t = plan.WallThickness;
            int h = plan.WallHeight;

            // Four runs. The gate side is broken by the gatehouse, which carves its own opening.
            WallRun(ref brush, in plan, new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz),
                    new int3(1, 0, 0), hx * 2, t, h, true);
            WallRun(ref brush, in plan, new int3(plan.Centre.x - hx, baseY, plan.Centre.z + hz - t),
                    new int3(1, 0, 0), hx * 2, t, h, true);
            WallRun(ref brush, in plan, new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz),
                    new int3(0, 0, 1), hz * 2, t, h, false);
            WallRun(ref brush, in plan, new int3(plan.Centre.x + hx - t, baseY, plan.Centre.z - hz),
                    new int3(0, 0, 1), hz * 2, t, h, false);

            CurtainFacadeDetails(ref brush, in plan, baseY);
        }

        /// <summary>
        /// Adds the secondary depth layer visible in the reference: battered buttresses, blind
        /// Gothic bays, machicolation corbels, and roofed sentry pinnacles. These are exterior
        /// structure rather than sealed rooms, so they enrich the silhouette without inventing
        /// inaccessible occupied space.
        /// </summary>
        private static void CurtainFacadeDetails(ref VoxelBrush brush, in CastlePlan plan, int baseY)
        {
            int hx = plan.BaileyHalfX;
            int hz = plan.BaileyHalfZ;
            int gateZ = plan.Centre.z - hz;
            int wallTop = baseY + plan.WallHeight;

            // South/front curtain: paired buttresses and recessed arched panels on both shoulders.
            for (int side = -1; side <= 1; side += 2)
            for (int bay = 0; bay < 3; bay++)
            {
                int x = plan.Centre.x + side * (112 + bay * 58);
                if (math.abs(x - plan.Centre.x) >= hx - plan.TowerRadius) continue;

                brush.Box(new int3(x - 7, baseY, gateZ - 12),
                          new int3(14, 58, 14), Mat.DarkStone);
                brush.Box(new int3(x - 5, baseY + 50, gateZ - 9),
                          new int3(10, plan.WallHeight - 44, 11), Mat.Stone);
                brush.Box(new int3(x - 9, baseY + 52, gateZ - 14),
                          new int3(18, 5, 16), Mat.Stone);

                int panelX = x + side * 26 - 10;
                brush.Arch(new int3(panelX, baseY + 28, gateZ - 2),
                           20, 38, 3, 2, Mat.DarkStone);
                brush.Arch(new int3(panelX + 6, baseY + 39, gateZ - 3),
                           8, 21, 4, 2, Mat.Empty);
            }

            // Corbels throw a repeating shadow beneath the wall walk, breaking the single flat
            // strip that dominated the first exterior renders.
            for (int x = plan.Centre.x - hx + plan.TowerRadius;
                 x <= plan.Centre.x + hx - plan.TowerRadius; x += 24)
            {
                if (math.abs(x - plan.Centre.x) < 82) continue;
                brush.Box(new int3(x - 5, wallTop - 8, gateZ - 10),
                          new int3(10, 12, 12), Mat.DarkStone);
            }

            // Two solid roofed sentry pinnacles establish the layered roofline seen between the
            // gatehouse and corner towers. They contain no inaccessible interior volume.
            for (int side = -1; side <= 1; side += 2)
            {
                int x = plan.Centre.x + side * 132;
                brush.Cylinder(x, wallTop + 1, gateZ + plan.WallThickness / 2,
                               14, 28, Mat.Stone);
                brush.Cylinder(x, wallTop + 25, gateZ + plan.WallThickness / 2,
                               17, 5, Mat.DarkStone, 10);
                brush.Cone(x, wallTop + 29, gateZ + plan.WallThickness / 2,
                           16, 32, Mat.Slate);
                brush.Box(new int3(x, wallTop + 60, gateZ + plan.WallThickness / 2),
                          new int3(2, 15, 2), Mat.Gold);
            }

            // Narrow buttresses on the side curtains keep the same architectural language in
            // oblique and aerial views instead of detailing only the postcard facade.
            for (int side = -1; side <= 1; side += 2)
            for (int z = plan.Centre.z - hz + 76; z < plan.Centre.z + hz - 54; z += 82)
            {
                int x = plan.Centre.x + side * hx;
                int outerX = x + side * 2;
                brush.Box(new int3(outerX + (side < 0 ? -10 : 0), baseY, z - 6),
                          new int3(10, 62, 12), Mat.DarkStone);
                brush.Box(new int3(outerX + (side < 0 ? -7 : 0), baseY + 54, z - 5),
                          new int3(7, plan.WallHeight - 48, 10), Mat.Stone);
            }
        }

        /// <summary>
        /// One run of curtain wall: battered plinth, masonry, string course, wall-walk, parapet.
        ///
        /// The plinth and the string course are what make it read as built rather than extruded —
        /// a wall of one flat colour is a fence no matter how tall.
        /// </summary>
        private static void WallRun(ref VoxelBrush brush, in CastlePlan plan, int3 start, int3 dir,
                                    int length, int thickness, int height, bool alongX)
        {
            int3 wallSize = alongX
                ? new int3(length, height, thickness)
                : new int3(thickness, height, length);
            brush.FillBulk(start, wallSize, Mat.Stone);

            int3 plinthSize = alongX
                ? new int3(length, 22, thickness)
                : new int3(thickness, 22, length);
            brush.FillBulk(start, plinthSize, Mat.DarkStone);

            int courseY = (int)(height * 0.66f);
            int3 courseMin = start + new int3(0, courseY, 0);
            int3 courseSize = alongX
                ? new int3(length, 2, thickness)
                : new int3(thickness, 2, length);
            brush.FillBulk(courseMin, courseSize, Mat.DarkStone);

            int3 walkMin = start + new int3(0, height, 0);
            int3 walkSize = alongX
                ? new int3(length, 1, thickness)
                : new int3(thickness, 1, length);
            brush.FillBulk(walkMin, walkSize, Mat.Stone);

            // Arrow slits at intervals.
            for (int i = 40; i < length; i += 90)
            {
                int3 slitMin = start + dir * i + new int3(0, 40, 0);
                int3 slitSize = alongX
                    ? new int3(1, 28, thickness)
                    : new int3(thickness, 28, 1);
                brush.FillBulk(slitMin, slitSize, Mat.Empty);
            }

            // Parapet with crenellations, on the outward face.
            int parapetY = start.y + height + 1;
            int merlon = 26, gap = 18;

            for (int i = 0; i < length; i += merlon + gap)
            {
                int3 at = start + dir * i;
                int blockLength = math.min(merlon, length - i);
                int3 blockSize = alongX
                    ? new int3(blockLength, 20, 8)
                    : new int3(8, 20, blockLength);
                brush.FillBulk(new int3(at.x, parapetY, at.z), blockSize, Mat.Stone);
            }

            // Banners hung between merlons on the long runs.
            if (length > 400)
            {
                for (int i = 120; i < length - 120; i += 200)
                {
                    int3 at = start + dir * i;
                    int3 bannerSize = alongX
                        ? new int3(1, 46, 14)
                        : new int3(14, 46, 1);
                    brush.FillBulk(new int3(at.x, start.y + height - 60, at.z),
                                   bannerSize, Mat.Cloth);
                }
            }
        }

        // -- towers --------------------------------------------------------------

        private static void CornerTowers(ref VoxelBrush brush, in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int hx = plan.BaileyHalfX, hz = plan.BaileyHalfZ;

            int3[] corners =
            {
                new(plan.Centre.x - hx, baseY, plan.Centre.z - hz),
                new(plan.Centre.x + hx, baseY, plan.Centre.z - hz),
                new(plan.Centre.x - hx, baseY, plan.Centre.z + hz),
                new(plan.Centre.x + hx, baseY, plan.Centre.z + hz),
            };

            for (int i = 0; i < corners.Length; i++)
            {
                // Flat fighting tops frame the entrance; roofed rear towers give the keep a
                // second, less repetitive silhouette layer behind them.
                Tower(ref brush, in plan, corners[i], plan.TowerRadius,
                      plan.TowerHeight + (i >= 2 ? 14 : 0), i >= 2);
            }
        }

        /// <summary>
        /// A drum tower: battered base, shaft, corbelled parapet, conical roof, finial.
        ///
        /// The corbel course under the parapet — a ring one voxel wider than the shaft — is a
        /// small detail that does most of the work in making a tower look defensive rather than
        /// like a pipe.
        /// </summary>
        private static void Tower(ref VoxelBrush brush, in CastlePlan plan, int3 at, int radius,
                                  int height, bool roof)
        {
            // Base, slightly wider.
            brush.Cylinder(at.x, at.y - 30, at.z, radius + 4, 42, Mat.DarkStone);

            // Shaft, hollow so it can hold a stair.
            brush.Cylinder(at.x, at.y, at.z, radius, height, Mat.Stone, radius - 12);

            // Floors inside.
            for (int f = 1; f * plan.FloorHeight < height - 20; f++)
                brush.Disc(at.x, at.y + f * plan.FloorHeight, at.z, radius - 13, Mat.Wood);

            // Spiral stair up the shaft.
            brush.SpiralStair(at.x, at.y + 2, at.z, radius - 14, height - 24, Mat.Stone);

            // Every tower needs a real ground-floor entrance. Aim it toward the castle centre;
            // gate towers therefore open into the bailey and corner turrets open into the keep.
            // The old towers contained stairs but no way for a player to reach them.
            CarveTowerDoor(ref brush, in plan, at, radius);

            // Arrow slits, three per floor, staggered.
            var rng = new Random((uint)(at.x * 8191 + at.z * 131071) | 1u);
            for (int f = 0; f * plan.FloorHeight < height - 40; f++)
            {
                int y = at.y + f * plan.FloorHeight + 18;
                float phase = rng.NextFloat(0f, 6.28f);

                for (int s = 0; s < 3; s++)
                {
                    float a = phase + s * 2.09f;
                    for (int r = radius - 14; r <= radius; r++)
                    for (int h = 0; h < 22; h++)
                    {
                        int x = at.x + (int)math.round(math.cos(a) * r);
                        int z = at.z + (int)math.round(math.sin(a) * r);
                        brush.Set(x, y + h, z, Mat.Empty);
                    }
                }
            }

            // Corbel course, then parapet.
            int parapetY = at.y + height;
            brush.Cylinder(at.x, parapetY - 4, at.z, radius + 3, 5, Mat.DarkStone, radius - 14);
            brush.Cylinder(at.x, parapetY, at.z, radius + 2, 6, Mat.Stone, radius - 12);
            brush.CrenellateRing(at.x, parapetY + 6, at.z, radius + 2, 18, Mat.Stone);

            if (!roof) return;

            brush.Cone(at.x, parapetY + 8, at.z, radius - 4, radius * 2, Mat.Slate);
            int peakY = parapetY + 8 + radius * 2;
            brush.Box(new int3(at.x, peakY, at.z), new int3(2, 30, 2), Mat.Wood);
            brush.Box(new int3(at.x + 2, peakY + 17, at.z), new int3(22, 11, 2), Mat.Cloth);
            brush.Set(at.x, peakY + 30, at.z, Mat.Gold);
        }

        private static void CarveTowerDoor(ref VoxelBrush brush, in CastlePlan plan,
                                           int3 at, int radius)
        {
            const int width = 14;
            const int height = 30;
            int dx = plan.Centre.x - at.x;
            int dz = plan.Centre.z - at.z;

            if (math.abs(dx) > math.abs(dz))
            {
                int minX = dx >= 0 ? at.x + radius - 15 : at.x - radius - 1;
                brush.Arch(new int3(minX, at.y + 2, at.z - width / 2),
                           width, height, 16, 0, Mat.Empty);
            }
            else
            {
                int minZ = dz >= 0 ? at.z + radius - 15 : at.z - radius - 1;
                brush.Arch(new int3(at.x - width / 2, at.y + 2, minZ),
                           width, height, 16, 2, Mat.Empty);
            }
        }

        // -- gatehouse -----------------------------------------------------------

        private static void Gatehouse(ref VoxelBrush brush, in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            int r = plan.GateTowerRadius;
            int spacing = 54;

            var left = new int3(plan.Centre.x - spacing, baseY, gateZ);
            var right = new int3(plan.Centre.x + spacing, baseY, gateZ);

            // Build the connecting block first. If it is stamped after the towers it fills the
            // inner halves of their rooms and stair headroom; tower interiors are authoritative.
            int blockHeight = plan.WallHeight + 22;
            brush.Box(new int3(plan.Centre.x - spacing, baseY, gateZ - plan.WallThickness),
                      new int3(spacing * 2, blockHeight, plan.WallThickness * 2), Mat.Stone);

            Tower(ref brush, in plan, left, r, plan.GateTowerHeight + 12, false);
            Tower(ref brush, in plan, right, r, plan.GateTowerHeight + 12, false);

            // Arched gate passage.
            brush.Arch(new int3(plan.Centre.x - 26, baseY, gateZ - plan.WallThickness),
                       52, 74, plan.WallThickness * 2, 2, Mat.Empty);

            // Portcullis slot, and the machicolation above the gate.
            brush.Box(new int3(plan.Centre.x - 28, baseY + 74, gateZ - 4), new int3(56, 6, 8), Mat.Empty);

            for (int i = 0; i < 9; i++)
            {
                int x = plan.Centre.x - 36 + i * 9;
                brush.Box(new int3(x, baseY + plan.WallHeight + 6, gateZ - plan.WallThickness - 6),
                          new int3(5, 14, 6), Mat.DarkStone);
            }

            brush.Crenellate(
                new int3(plan.Centre.x - spacing, baseY + blockHeight,
                         gateZ - plan.WallThickness),
                new int3(1, 0, 0), spacing * 2, 8, 18, 18, 12, Mat.Stone);

            // Long heraldic banners give the otherwise grey entrance a readable focal colour.
            for (int side = -1; side <= 1; side += 2)
            {
                int bannerX = plan.Centre.x + side * 29;
                brush.Box(new int3(bannerX - 7, baseY + 52,
                                   gateZ - plan.WallThickness - 2),
                          new int3(14, 42, 2), Mat.Cloth);
                brush.Box(new int3(bannerX - 10, baseY + 92,
                                   gateZ - plan.WallThickness - 3),
                          new int3(20, 3, 3), Mat.Gold);
            }

            // Bridge across the moat.
            for (int z = 0; z < 150; z++)
            for (int x = -34; x <= 34; x++)
                brush.FillColumnBulk(plan.Centre.x + x, baseY - 2, baseY - 1,
                                     gateZ - plan.WallThickness - z, Mat.Wood);
        }

        // -- courtyard -----------------------------------------------------------

        private static void Courtyard(ref VoxelBrush brush, in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            var rng = new Random(plan.Seed ^ 0xC0DEu);

            // Paving in the middle, worn to dirt at the edges.
            for (int z = -plan.BaileyHalfZ + 40; z < plan.BaileyHalfZ - 40; z++)
            for (int x = -plan.BaileyHalfX + 40; x < plan.BaileyHalfX - 40; x++)
            {
                byte material = rng.NextInt(0, 100) < 82 ? Mat.Stone : Mat.Dirt;
                brush.FillColumnBulk(plan.Centre.x + x, baseY, baseY + 1,
                                     plan.Centre.z + z, material);
            }

            // A well.
            int wx = plan.Centre.x - plan.BaileyHalfX / 2;
            int wz = plan.Centre.z + plan.BaileyHalfZ / 3;
            brush.Cylinder(wx, baseY + 1, wz, 16, 12, Mat.DarkStone, 11);
            brush.Cylinder(wx, baseY - 60, wz, 11, 60, Mat.Empty);
            brush.Cylinder(wx, baseY - 60, wz, 10, 14, Mat.Water);

            // Lean-to outbuildings against the inside of the wall.
            for (int i = 0; i < 3; i++)
            {
                int bx = plan.Centre.x - plan.BaileyHalfX + 60 + i * 150;
                int bz = plan.Centre.z + plan.BaileyHalfZ - 130;

                int w = rng.NextInt(70, 100), d = rng.NextInt(60, 84), h = rng.NextInt(56, 76);

                brush.HollowBox(new int3(bx, baseY, bz), new int3(w, h, d), 5, Mat.Stone, false, false);
                brush.Box(new int3(bx + w / 2 - 9, baseY, bz), new int3(18, 30, 5), Mat.Empty);
                brush.Gable(new int3(bx - 4, baseY + h, bz - 4), new int3(w + 8, 30, d + 8), true, Mat.Tile);
            }
        }

        // -- keep ----------------------------------------------------------------

        /// <summary>
        /// The keep: the tall block at the centre with the rooms in it.
        ///
        /// Built as a shell, then floors, then rooms carved and furnished, so interiors are real
        /// space rather than decoration painted on a solid block. The cutaway in the reference is
        /// only possible if the inside is actually hollow.
        /// </summary>
        private static void Keep(ref VoxelBrush brush, in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int hx = plan.KeepHalfX, hz = plan.KeepHalfZ;
            var min = new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz + 60);
            var size = new int3(hx * 2, plan.KeepHeight, hz * 2);

            // Shell with a plinth.
            brush.Box(new int3(min.x - 6, baseY - 26, min.z - 6), new int3(size.x + 12, 30, size.z + 12), Mat.DarkStone);
            brush.HollowBox(min, size, 8, Mat.Stone, false, false);

            // HollowBox writes only the shell; it does not erase terrain or the solid plinth
            // already inside that shell. Preserve the baseY floor and explicitly clear the full
            // occupied volume above it before adding floors, partitions, and furniture.
            brush.FillBulk(new int3(min.x + 8, baseY + 1, min.z + 8),
                           new int3(size.x - 16, size.y - 1, size.z - 16), Mat.Empty);

            // Corner turrets.
            for (int i = 0; i < 4; i++)
            {
                int cx = min.x + (i % 2 == 0 ? 0 : size.x);
                int cz = min.z + (i < 2 ? 0 : size.z);
                Tower(ref brush, in plan, new int3(cx, baseY, cz), 26, plan.KeepHeight + 30, true);
            }

            // Floors, rooms, and the stair that connects them.
            int floors = plan.Floors;
            for (int f = 0; f < floors; f++)
            {
                int y = baseY + f * plan.FloorHeight;
                if (f > 0) brush.Box(new int3(min.x + 8, y, min.z + 8), new int3(size.x - 16, 3, size.z - 16), Mat.Wood);

                Rooms(ref brush, in plan, min, size, y, f);
            }

            // A visible courtyard entrance, with open timber leaves against the inner wall.
            int entranceX = plan.Centre.x;
            brush.Arch(new int3(entranceX - 15, baseY + 1, min.z - 1),
                       30, 34, 10, 2, Mat.Empty);
            brush.Box(new int3(entranceX - 15, baseY + 2, min.z + 9),
                      new int3(4, 29, 3), Mat.Wood);
            brush.Box(new int3(entranceX + 11, baseY + 2, min.z + 9),
                      new int3(4, 29, 3), Mat.Wood);

            // Keep the entrance aisle clear after furnishing/clutter, so the front door cannot
            // generate behind a table or chest.
            brush.Box(new int3(entranceX - 9, baseY + 1, min.z + 8),
                      new int3(18, 24, size.z / 2 - 28), Mat.Empty);

            // A broad stair in the entrance hall makes vertical circulation immediately visible
            // from the front door. It reaches the principal chamber floor; the compact spiral
            // beside it continues through every upper room and down-world connections remain at
            // the marked trapdoor.
            int grandX = plan.Centre.x - 68;
            int grandZ = min.z + 28;
            const int grandWidth = 18;
            const int grandSteps = 19;
            const int grandRise = 2;
            const int grandRun = 3;
            brush.Box(new int3(grandX, baseY + 1, grandZ),
                      new int3(grandWidth, plan.FloorHeight + 18,
                               grandSteps * grandRun), Mat.Empty);
            brush.Stairs(new int3(grandX, baseY + 1, grandZ), grandWidth,
                         grandSteps, grandRise, grandRun, 2, Mat.Wood);

            // Timber newel posts and a lintel identify the stair opening at room scale without
            // narrowing the 1.8 m-wide flight.
            brush.Box(new int3(grandX - 3, baseY + 1, grandZ),
                      new int3(3, 20, 3), Mat.Wood);
            brush.Box(new int3(grandX + grandWidth, baseY + 1, grandZ),
                      new int3(3, 20, 3), Mat.Wood);

            // The helical stair meets every floor at an exact 38-voxel multiple. Its own
            // headroom carve cuts through the timber slabs, while its outer tread touches the
            // surrounding floor as a landing.
            int stairX = min.x + 34;
            int stairZ = min.z + 34;
            const int stairRadius = 22;
            brush.SpiralStair(stairX, baseY + 2, stairZ, stairRadius,
                              floors * plan.FloorHeight, Mat.Stone);

            // Windows: arched, larger on the hall floor.
            for (int f = 0; f < floors; f++)
            {
                int y = baseY + f * plan.FloorHeight + 12;
                int height = f == 1 ? plan.FloorHeight - 14 : plan.FloorHeight - 18;

                for (int i = 0; i < 3; i++)
                {
                    int x = min.x + size.x / 4 + i * size.x / 4 - 8;
                    bool mainEntrance = f == 0 && i == 1;
                    if (!mainEntrance)
                    {
                        brush.Arch(new int3(x, y, min.z), 16, height, 9, 2, Mat.Empty);
                        brush.Box(new int3(x + 3, y + 4, min.z + 2),
                                  new int3(10, height - 10, 2), Mat.Glass);
                    }

                    brush.Arch(new int3(x, y, min.z + size.z - 8), 16, height, 9, 2, Mat.Empty);
                }
            }

            // Floor-height string courses and projecting window hoods make the tall keep read as
            // stacked occupied storeys rather than one extruded slab. They stay outside the shell
            // and therefore do not narrow any interior route.
            for (int f = 1; f < floors; f++)
            {
                int courseY = baseY + f * plan.FloorHeight - 3;
                brush.Box(new int3(min.x - 3, courseY, min.z - 3),
                          new int3(size.x + 6, 3, 4), Mat.DarkStone);
                brush.Box(new int3(min.x - 3, courseY, min.z + size.z - 1),
                          new int3(size.x + 6, 3, 4), Mat.DarkStone);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                int bannerX = plan.Centre.x + side * 52;
                brush.Box(new int3(bannerX - 7, baseY + plan.FloorHeight * 2 + 8, min.z - 3),
                          new int3(14, 54, 3), Mat.Cloth);
                brush.Box(new int3(bannerX - 10, baseY + plan.FloorHeight * 2 + 59, min.z - 4),
                          new int3(20, 3, 4), Mat.Gold);
            }

            // Battlements and a steep roof.
            int topY = baseY + floors * plan.FloorHeight;
            brush.Box(new int3(min.x - 5, topY, min.z - 5), new int3(size.x + 10, 6, size.z + 10), Mat.DarkStone);

            for (int i = 0; i < size.x + 10; i += 44)
            {
                brush.Box(new int3(min.x - 5 + i, topY + 6, min.z - 5), new int3(24, 20, 7), Mat.Stone);
                brush.Box(new int3(min.x - 5 + i, topY + 6, min.z + size.z + 3), new int3(24, 20, 7), Mat.Stone);
            }

            brush.Gable(new int3(min.x, topY + 8, min.z), new int3(size.x, 70, size.z), true, Mat.Tile);

            GreatHallWing(ref brush, in plan, min, size, baseY);
            ChapelWing(ref brush, in plan, min, size, baseY);
        }

        /// <summary>
        /// A tall chapel accumulated against the west side of the keep. Unlike a decorative
        /// annex it has a player-sized joining arch, a continuous central aisle, and no sealed
        /// upper volume. The asymmetrical lower roofline is a defining feature of the reference.
        /// </summary>
        private static void ChapelWing(ref VoxelBrush brush, in CastlePlan plan,
                                       int3 keepMin, int3 keepSize, int baseY)
        {
            int width = math.max(78, keepSize.x / 3);
            int depth = math.max(96, keepSize.z * 3 / 5);
            int height = plan.FloorHeight * 2;
            var min = new int3(keepMin.x - width + 4, baseY,
                               keepMin.z + keepSize.z - depth - 38);
            int centreZ = min.z + depth / 2;

            brush.Box(new int3(min.x - 5, baseY - 12, min.z - 5),
                      new int3(width + 10, 16, depth + 10), Mat.DarkStone);
            brush.HollowBox(min, new int3(width, height, depth), 6,
                            Mat.Stone, false, false);
            brush.FillBulk(new int3(min.x + 6, baseY + 1, min.z + 6),
                           new int3(width - 12, height - 1, depth - 12), Mat.Empty);

            // Direct joining arch and a hard circulation core across the overlapping shells.
            brush.Arch(new int3(keepMin.x - 8, baseY + 2, centreZ - 12),
                       24, 36, 16, 0, Mat.Empty);
            brush.Box(new int3(keepMin.x - 12, baseY, centreZ - 8),
                      new int3(24, 2, 16), Mat.Stone);
            brush.Box(new int3(keepMin.x - 12, baseY + 2, centreZ - 8),
                      new int3(24, 25, 16), Mat.Empty);

            // West rose window and two tall side lancets.
            brush.Arch(new int3(min.x - 1, baseY + 30, centreZ - 16),
                       32, 34, 8, 0, Mat.Empty);
            brush.Box(new int3(min.x + 2, baseY + 35, centreZ - 10),
                      new int3(3, 24, 20), Mat.Glass);
            for (int side = -1; side <= 1; side += 2)
            {
                int z = centreZ + side * 34;
                brush.Arch(new int3(min.x + width / 2 - 7, baseY + 20, z - 6),
                           14, 38, 7, 2, Mat.Empty);
                brush.Box(new int3(min.x + width / 2 - 4, baseY + 25, z - 4),
                          new int3(8, 26, 2), Mat.Glass);
            }

            // Raised altar at the west end. Pews flank a 1.6 m aisle from the keep doorway, so
            // the room remains traversable even though it reads as densely furnished.
            brush.Box(new int3(min.x + 9, baseY + 1, centreZ - 22),
                      new int3(14, 5, 44), Mat.DarkStone);
            brush.Box(new int3(min.x + 13, baseY + 8, centreZ - 14),
                      new int3(7, 15, 28), Mat.Cloth);
            brush.Box(new int3(min.x + 10, baseY + 14, centreZ - 2),
                      new int3(12, 4, 4), Mat.Gold);
            brush.Box(new int3(min.x + 14, baseY + 10, centreZ - 2),
                      new int3(4, 13, 4), Mat.Gold);
            brush.Box(new int3(min.x + 18, baseY + 6, centreZ - 18),
                      new int3(10, 5, 36), Mat.Wood);

            for (int row = 0; row < 3; row++)
            for (int side = -1; side <= 1; side += 2)
            {
                int x = min.x + 34 + row * 15;
                int z = centreZ + side * 17;
                brush.Box(new int3(x, baseY + 2, z - 10),
                          new int3(7, 6, 20), Mat.Wood);
                brush.Box(new int3(x + 5, baseY + 7, z - 10),
                          new int3(3, 10, 20), Mat.Wood);
            }

            // Timber hammer-beam suggestion and warm hanging lamps in the tall volume.
            for (int x = min.x + 18; x < min.x + width - 12; x += 20)
            {
                brush.Box(new int3(x, baseY + 55, min.z + 7),
                          new int3(4, 4, depth - 14), Mat.Wood);
                brush.Box(new int3(x, baseY + 44, min.z + 7),
                          new int3(4, 15, 4), Mat.Wood);
                brush.Box(new int3(x, baseY + 44, min.z + depth - 11),
                          new int3(4, 15, 4), Mat.Wood);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                brush.Box(new int3(min.x + width / 2 - 2, baseY + 28,
                                   centreZ + side * 24 - 2),
                          new int3(4, 9, 4), Mat.Glass);
                brush.Box(new int3(min.x + width / 2 - 1, baseY + 37,
                                   centreZ + side * 24 - 1),
                          new int3(2, 18, 2), Mat.Gold);
            }

            brush.Gable(new int3(min.x - 4, baseY + height, min.z - 4),
                        new int3(width + 8, 42, depth + 8), false, Mat.Slate);

            // External buttresses anchor the chapel visually to the cliff-side masonry.
            for (int z = min.z + 10; z < min.z + depth - 8; z += 30)
            {
                brush.Box(new int3(min.x - 8, baseY, z),
                          new int3(10, 46, 9), Mat.DarkStone);
                brush.Box(new int3(min.x - 5, baseY + 40, z + 1),
                          new int3(7, 25, 7), Mat.Stone);
            }
        }

        /// <summary>
        /// A lower occupied wing attached off-centre to the keep. The main block alone is a
        /// symmetric tower with a roof; the reference castle reads as a place accumulated over
        /// time, with halls and chambers stepping down around the central mass.
        /// </summary>
        private static void GreatHallWing(ref VoxelBrush brush, in CastlePlan plan,
                                          int3 keepMin, int3 keepSize, int baseY)
        {
            int wingHeight = plan.FloorHeight * 2;
            int wingWidth = math.max(96, keepSize.x * 2 / 5);
            // Stop well before the rear keep turret. The former -42 extent crossed its inward
            // doorway; -72 leaves a three-metre circulation gap around the drum.
            int wingDepth = math.max(80, keepSize.z - 72);
            var wingMin = new int3(keepMin.x + keepSize.x - 4, baseY,
                                   keepMin.z + 24);

            brush.Box(new int3(wingMin.x - 4, baseY - 12, wingMin.z - 4),
                      new int3(wingWidth + 8, 16, wingDepth + 8), Mat.DarkStone);
            brush.HollowBox(wingMin, new int3(wingWidth, wingHeight, wingDepth),
                            6, Mat.Stone, false, false);
            brush.FillBulk(new int3(wingMin.x + 6, baseY + 1, wingMin.z + 6),
                           new int3(wingWidth - 12, wingHeight - 1, wingDepth - 12),
                           Mat.Empty);
            brush.Box(new int3(wingMin.x + 6, baseY + plan.FloorHeight, wingMin.z + 6),
                      new int3(wingWidth - 12, 3, wingDepth - 12), Mat.Wood);

            // Ground-floor banqueting hall. Parallel tables leave a broad processional aisle
            // through the joining arch, while the dais at the far wall gives the room a focal
            // direction visible in cutaway views.
            int hallCentreZ = wingMin.z + wingDepth / 2;
            for (int side = -1; side <= 1; side += 2)
            {
                int tableZ = hallCentreZ + side * 25;
                brush.Box(new int3(wingMin.x + 22, baseY + 7, tableZ - 5),
                          new int3(wingWidth - 46, 4, 10), Mat.Wood);
                brush.Box(new int3(wingMin.x + 27, baseY + 2, tableZ - 3),
                          new int3(4, 6, 6), Mat.Wood);
                brush.Box(new int3(wingMin.x + wingWidth - 31, baseY + 2, tableZ - 3),
                          new int3(4, 6, 6), Mat.Wood);
                brush.Box(new int3(wingMin.x + 20, baseY + 2, tableZ + side * 9 - 2),
                          new int3(wingWidth - 42, 4, 4), Mat.Wood);
            }
            brush.Box(new int3(wingMin.x + wingWidth - 20, baseY + 2, hallCentreZ - 17),
                      new int3(8, 4, 34), Mat.DarkStone);
            brush.Box(new int3(wingMin.x + wingWidth - 17, baseY + 6, hallCentreZ - 8),
                      new int3(5, 14, 16), Mat.Wood);
            brush.Box(new int3(wingMin.x + wingWidth - 16, baseY + 12, hallCentreZ - 6),
                      new int3(4, 8, 12), Mat.Cloth);

            // Upper solar/library: shelving hugs the outside walls so the balcony and keep
            // connector remain part of one unobstructed circulation loop.
            int upperY = baseY + plan.FloorHeight;
            for (int z = wingMin.z + 12; z < wingMin.z + wingDepth - 18; z += 28)
            {
                brush.Box(new int3(wingMin.x + wingWidth - 18, upperY + 3, z),
                          new int3(10, 28, 18), Mat.Wood);
                for (int shelf = 0; shelf < 3; shelf++)
                    brush.Box(new int3(wingMin.x + wingWidth - 19, upperY + 9 + shelf * 8, z - 1),
                              new int3(12, 2, 20), shelf == 1 ? Mat.Gold : Mat.Wood);
            }
            brush.Box(new int3(wingMin.x + 28, upperY + 8, hallCentreZ - 12),
                      new int3(34, 4, 24), Mat.Wood);
            brush.Box(new int3(wingMin.x + 32, upperY + 3, hallCentreZ - 8),
                      new int3(5, 6, 5), Mat.Wood);
            brush.Box(new int3(wingMin.x + 53, upperY + 3, hallCentreZ + 3),
                      new int3(5, 6, 5), Mat.Wood);

            // Paired warm fixtures make the occupied floors legible from outside at night.
            for (int floor = 0; floor < 2; floor++)
            for (int side = -1; side <= 1; side += 2)
            {
                int lampY = baseY + floor * plan.FloorHeight + 17;
                int lampZ = hallCentreZ + side * (wingDepth / 2 - 13);
                brush.Box(new int3(wingMin.x + wingWidth / 2 - 2, lampY, lampZ - 2),
                          new int3(4, 7, 4), Mat.Glass);
                brush.Box(new int3(wingMin.x + wingWidth / 2 - 3, lampY - 3, lampZ - 1),
                          new int3(6, 3, 3), Mat.Gold);
            }

            // Two tall windows on the exposed end wall, and smaller upper windows.
            for (int i = 0; i < 2; i++)
            {
                int z = wingMin.z + 14 + i * (wingDepth - 28);
                brush.Arch(new int3(wingMin.x + wingWidth - 7, baseY + 12, z),
                           16, 28, 8, 0, Mat.Empty);
                brush.Box(new int3(wingMin.x + wingWidth - 5, baseY + 16, z + 3),
                          new int3(2, 18, 10), Mat.Glass);
            }

            // The joining arch makes this real interior space rather than a building merely
            // intersecting the keep's outside wall.
            brush.Arch(new int3(wingMin.x - 8, baseY + 2,
                                wingMin.z + wingDepth / 2 - 10),
                       20, 32, 16, 0, Mat.Empty);
            brush.Arch(new int3(wingMin.x - 8, baseY + plan.FloorHeight + 2,
                                wingMin.z + wingDepth / 2 - 10),
                       20, 30, 16, 0, Mat.Empty);

            // Arch curvature is decorative, but the circulation contract is rectangular: a
            // 60 cm-wide, 180 cm-tall actor must be able to cross both joins without clipping
            // the keep shell, wing shell, or intermediate timber slab. Clear that capsule-sized
            // core last across the entire overlap instead of relying on two curved openings to
            // happen to overlap at every sample.
            int connectorZ = wingMin.z + wingDepth / 2;
            for (int floor = 0; floor < 2; floor++)
            {
                int floorY = baseY + floor * plan.FloorHeight;
                brush.Box(new int3(keepMin.x + keepSize.x - 12, floorY, connectorZ - 7),
                          new int3(24, floor == 0 ? 2 : 3, 14),
                          floor == 0 ? Mat.Stone : Mat.Wood);

                // Upper floors are three voxels thick. Begin one voxel above the slab so the
                // opening keeps a landing under the actor instead of becoming an invisible pit.
                int footY = floorY + (floor == 0 ? 2 : 3);
                brush.Box(new int3(keepMin.x + keepSize.x - 12, footY, connectorZ - 7),
                          new int3(24, 24, 14), Mat.Empty);
            }

            brush.Gable(new int3(wingMin.x - 4, baseY + wingHeight, wingMin.z - 4),
                        new int3(wingWidth + 8, 34, wingDepth + 8), true, Mat.Tile);

            // Timber balcony on the exposed end turns the wing into occupied architecture and
            // adds a horizontal layer against the keep's dominant vertical shafts.
            int balconyY = baseY + plan.FloorHeight + 4;
            int balconyZ = wingMin.z + wingDepth / 2 - 25;
            brush.Box(new int3(wingMin.x + wingWidth - 2, balconyY, balconyZ),
                      new int3(18, 4, 50), Mat.Wood);
            brush.Box(new int3(wingMin.x + wingWidth + 12, balconyY + 4, balconyZ),
                      new int3(3, 18, 3), Mat.Wood);
            brush.Box(new int3(wingMin.x + wingWidth + 12, balconyY + 4, balconyZ + 47),
                      new int3(3, 18, 3), Mat.Wood);
            brush.Box(new int3(wingMin.x + wingWidth + 12, balconyY + 18, balconyZ),
                      new int3(3, 3, 50), Mat.Wood);
        }

        /// <summary>
        /// Partitions a floor into rooms and furnishes them.
        ///
        /// Crude furniture by design: at 10 cm voxels a table is a slab on legs, and the shape
        /// reads at a glance. What matters is that rooms differ from one another — a hall, a
        /// bedchamber, a library — because identical rooms are what makes generated interiors feel
        /// generated.
        /// </summary>
        private static void Rooms(ref VoxelBrush brush, in CastlePlan plan, int3 min, int3 size, int y, int floor)
        {
            var rng = new Random(plan.Seed ^ (uint)(floor * 7919 + 13));
            int inner = 8;

            // Ground floor is one hall; upper floors are divided.
            if (floor >= 2)
            {
                int split = min.z + size.z / 2;
                brush.Box(new int3(min.x + inner, y, split),
                          new int3(size.x - inner * 2, plan.FloorHeight - 4, 1), Mat.Stone);

                // Doorway through the partition.
                int doorX = min.x + size.x / 2;
                // Preserve the three-voxel timber floor under the opening. Starting at y erased
                // the landing and turned every upper partition door into a one-cell-wide trench.
                brush.Box(new int3(doorX - 9, y + 3, split), new int3(18, 27, 1), Mat.Empty);
            }

            int cx = min.x + size.x / 2;
            int cz = min.z + size.z / 2;

            switch (floor)
            {
                case 0: // great hall: long table, benches, hearth
                    brush.Box(new int3(cx - 40, y + 1, cz - 10), new int3(80, 8, 20), Mat.Wood);
                    brush.Box(new int3(cx - 44, y + 1, cz - 20), new int3(88, 5, 6), Mat.Wood);
                    brush.Box(new int3(cx - 44, y + 1, cz + 16), new int3(88, 5, 6), Mat.Wood);
                    brush.Box(new int3(min.x + inner, y + 1, cz - 24), new int3(10, 40, 48), Mat.DarkStone);
                    brush.Box(new int3(min.x + inner + 2, y + 3, cz - 14), new int3(6, 16, 28), Mat.Empty);

                    // Warm sconces are emissive presentation voxels as well as real fixtures.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int lampZ = cz + side * 38;
                        brush.Box(new int3(min.x + inner + 10, y + 16, lampZ - 2),
                                  new int3(4, 8, 4), Mat.Glass);
                        brush.Box(new int3(min.x + inner + 8, y + 14, lampZ - 1),
                                  new int3(3, 3, 3), Mat.Gold);
                    }
                    break;

                case 1: // bedchamber: bed, chest, rug
                    brush.Box(new int3(cx + 10, y + 1, cz - 30), new int3(46, 10, 60), Mat.Wood);
                    brush.Box(new int3(cx + 10, y + 11, cz - 30), new int3(46, 5, 60), Mat.Cloth);
                    brush.Box(new int3(cx - 40, y + 1, cz + 20), new int3(24, 14, 16), Mat.Wood);
                    break;

                default: // library / stores: shelves against the walls
                    for (int i = 0; i < 4; i++)
                    {
                        brush.Box(new int3(min.x + inner + 4, y + 1, min.z + inner + 10 + i * 34),
                                  new int3(14, 34, 24), Mat.Wood);
                        brush.Box(new int3(min.x + size.x - inner - 18, y + 1, min.z + inner + 10 + i * 34),
                                  new int3(14, 34, 24), Mat.Wood);
                    }
                    break;
            }

            // A little clutter, so no two rooms are identical.
            for (int i = 0; i < rng.NextInt(2, 6); i++)
            {
                int px = rng.NextInt(min.x + inner + 8, min.x + size.x - inner - 12);
                int pz = rng.NextInt(min.z + inner + 8, min.z + size.z - inner - 12);
                brush.Box(new int3(px, y + 1, pz), new int3(rng.NextInt(6, 14), rng.NextInt(6, 18), rng.NextInt(6, 14)), Mat.Wood);
            }
        }

        // -- dungeon -------------------------------------------------------------

        /// <summary>
        /// The cellar, the dungeon below it, the secret passage, and the cave it opens into.
        ///
        /// The reference shows a trapdoor connecting the castle to a natural cave system, and that
        /// vertical connection is the point: the castle is not a model sitting on ground, it has a
        /// below.
        /// </summary>
        private static void Dungeon(ref VoxelBrush brush, in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int cellarY = baseY - 46;
            int dungeonY = cellarY - 120;

            int hx = plan.KeepHalfX, hz = plan.KeepHalfZ;
            var keepMin = new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz + 60);

            // Cellar under the keep: vaulted, low.
            brush.FillBulk(new int3(keepMin.x + 10, cellarY, keepMin.z + 10),
                           new int3(hx * 2 - 20, 40, hz * 2 - 20), Mat.Empty);
            brush.Box(new int3(keepMin.x + 8, cellarY - 4, keepMin.z + 8),
                      new int3(hx * 2 - 16, 4, hz * 2 - 16), Mat.DarkStone);

            // Secret archive and treasury. Props stay against the perimeter so the two stair
            // landings and their connecting aisle remain navigable.
            for (int z = keepMin.z + 18; z < keepMin.z + hz * 2 - 30; z += 30)
            {
                brush.Box(new int3(keepMin.x + 14, cellarY, z),
                          new int3(12, 28, 20), Mat.Wood);
                brush.Box(new int3(keepMin.x + hx * 2 - 26, cellarY, z),
                          new int3(12, 28, 20), Mat.Wood);
            }
            brush.Box(new int3(keepMin.x + 38, cellarY + 1, keepMin.z + 24),
                      new int3(28, 10, 18), Mat.Wood);
            brush.Box(new int3(keepMin.x + 42, cellarY + 11, keepMin.z + 28),
                      new int3(20, 4, 10), Mat.Gold);
            for (int i = 0; i < 4; i++)
            {
                int bx = keepMin.x + hx * 2 - 42 - (i & 1) * 18;
                int bz = keepMin.z + 24 + (i >> 1) * 22;
                brush.Cylinder(bx, cellarY, bz, 6, 12, Mat.Wood);
                brush.Box(new int3(bx - 5, cellarY + 5, bz - 7),
                          new int3(10, 2, 14), Mat.Gold);
            }

            // Trapdoor from the ground floor into the cellar.
            int3 trapdoor = TrapdoorCentre(in plan);
            int tx = trapdoor.x, tz = trapdoor.z;
            brush.Box(new int3(tx - 10, cellarY + 40, tz - 10), new int3(20, 8, 20), Mat.Empty);
            brush.SpiralStair(tx, cellarY, tz, 9, 46, Mat.Stone);

            // The stair is complete beneath a real hatch. Runtime interaction removes this
            // exact timber lid; keeping the opening closed during construction makes the secret
            // route discoverable rather than presenting the cellar as an accidental floor hole.
            brush.Box(new int3(tx - TrapdoorHalfSize, baseY, tz - TrapdoorHalfSize),
                      new int3(TrapdoorHalfSize * 2, 2, TrapdoorHalfSize * 2), Mat.Wood);
            brush.Box(new int3(tx - TrapdoorHalfSize, baseY + 2, tz - TrapdoorHalfSize),
                      new int3(3, 2, TrapdoorHalfSize * 2), Mat.Gold);
            brush.Box(new int3(tx + TrapdoorHalfSize - 3, baseY + 2, tz - TrapdoorHalfSize),
                      new int3(3, 2, TrapdoorHalfSize * 2), Mat.Gold);

            // Shaft down to the dungeon.
            brush.Cylinder(tx, dungeonY, tz, 16, cellarY - dungeonY, Mat.Empty);
            brush.SpiralStair(tx, dungeonY, tz, 13, cellarY - dungeonY, Mat.Stone);

            // Dungeon halls: a ruined colonnade, as in the reference.
            var hallMin = new int3(tx - 130, dungeonY, tz - 90);
            brush.FillBulk(hallMin, new int3(260, 46, 180), Mat.Empty);
            brush.Box(new int3(hallMin.x - 6, dungeonY - 5, hallMin.z - 6), new int3(272, 5, 192), Mat.DarkStone);

            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 2; j++)
            {
                int px = hallMin.x + 50 + i * 80;
                int pz = hallMin.z + 55 + j * 70;
                brush.Cylinder(px, dungeonY, pz, 12, 46, Mat.Stone);
                brush.Cylinder(px, dungeonY + 42, pz, 15, 4, Mat.DarkStone);
                brush.Box(new int3(px - 2, dungeonY + 23, pz - 14),
                          new int3(4, 8, 4), Mat.Glass);
                brush.Box(new int3(px - 2, dungeonY + 20, pz - 13),
                          new int3(4, 3, 3), Mat.Gold);
            }

            // A ruined ritual dais and broken benches tell a story without closing the central
            // route from the stair to the secret passage.
            brush.Box(new int3(tx - 34, dungeonY, hallMin.z + 18),
                      new int3(68, 5, 26), Mat.DarkStone);
            brush.Box(new int3(tx - 12, dungeonY + 5, hallMin.z + 24),
                      new int3(24, 9, 14), Mat.Stone);
            brush.Box(new int3(tx - 4, dungeonY + 14, hallMin.z + 28),
                      new int3(8, 12, 6), Mat.Gold);
            for (int side = -1; side <= 1; side += 2)
            for (int row = 0; row < 3; row++)
                brush.Box(new int3(tx + side * 54 - 20, dungeonY + 1,
                                   hallMin.z + 76 + row * 28),
                          new int3(40, 5, 8), row == 1 ? Mat.DarkStone : Mat.Wood);

            // Secret passage: a low tunnel from the hall out towards the cliff.
            int passZ = hallMin.z - 1;
            for (int i = 0; i < 320; i++)
            {
                int z = passZ - i;
                int y = dungeonY + (int)math.round(math.sin(i * 0.02f) * 8f);
                for (int x = tx - 14; x < tx + 14; x++)
                    brush.FillColumnBulk(x, y, y + 32, z, Mat.Empty);
                brush.Box(new int3(tx - 16, y - 2, z), new int3(32, 2, 1), Mat.DarkStone);
            }

            Cave(ref brush, in plan, new int3(tx, dungeonY, passZ - 320));
        }

        /// <summary>A natural cavern at the end of the passage, with a pool.</summary>
        private static void Cave(ref VoxelBrush brush, in CastlePlan plan, int3 at)
        {
            var rng = new Random(plan.Seed ^ 0xCAFEu);

            // Blobby chamber: several overlapping spheres, which is what stops it reading as a room.
            // Five blobs at 2.5-4 m radius. The first draft used seven at up to 7.8 m, which is
            // ten million voxels of carving on its own — a cavern the size of a cathedral, costed
            // by nobody.
            for (int b = 0; b < 5; b++)
            {
                int ox = rng.NextInt(-55, 55);
                int oy = rng.NextInt(-14, 26);
                int oz = rng.NextInt(-55, 55);
                int r = rng.NextInt(25, 40);
                int r2 = r * r;

                for (int z = -r; z <= r; z++)
                for (int x = -r; x <= r; x++)
                {
                    int verticalSq = r2 - x * x - z * z;
                    if (verticalSq < 0) continue;

                    int halfHeight = (int)math.floor(math.sqrt(verticalSq));
                    brush.FillColumnBulk(at.x + ox + x,
                                         at.y + oy - halfHeight,
                                         at.y + oy + halfHeight + 1,
                                         at.z + oz + z, Mat.Empty);
                }
            }

            // Pool in the floor, and a scatter of stalagmites.
            for (int z = -44; z <= 44; z++)
            for (int x = -44; x <= 44; x++)
            {
                if (x * x + z * z > 44 * 44) continue;
                brush.FillColumnBulk(at.x + x, at.y - 12, at.y - 2,
                                     at.z + z, Mat.Water);
            }

            for (int i = 0; i < 26; i++)
            {
                int sx = at.x + rng.NextInt(-95, 95);
                int sz = at.z + rng.NextInt(-95, 95);
                int h = rng.NextInt(10, 34);
                brush.Cone(sx, at.y - 2, sz, rng.NextInt(3, 7), h, Mat.DarkStone);
            }

            // Ceiling formations mirror the floor scatter and break up the cavern's upper
            // silhouette. A narrow bridge keeps the water chamber explorable on foot.
            for (int i = 0; i < 18; i++)
            {
                int sx = at.x + rng.NextInt(-78, 78);
                int sz = at.z + rng.NextInt(-78, 78);
                brush.HangingCone(sx, at.y + rng.NextInt(24, 37), sz,
                                  rng.NextInt(3, 7), rng.NextInt(10, 28), Mat.DarkStone);
            }
            brush.Box(new int3(at.x - 5, at.y - 2, at.z - 52),
                      new int3(10, 3, 104), Mat.Wood);
            for (int z = -48; z <= 48; z += 16)
            {
                brush.Box(new int3(at.x - 8, at.y + 1, at.z + z),
                          new int3(3, 8, 3), Mat.Wood);
                brush.Box(new int3(at.x + 5, at.y + 1, at.z + z),
                          new int3(3, 8, 3), Mat.Wood);
            }

            // Small crystal/gold clusters create distant landmarks rather than uniform cave
            // clutter. Glass supplies the cool cyan read; gold marks the secret chamber reward.
            int3[] crystalCentres =
            {
                new(at.x - 58, at.y - 2, at.z - 34),
                new(at.x + 61, at.y - 2, at.z + 28),
                new(at.x + 48, at.y - 2, at.z - 51),
            };
            foreach (int3 crystal in crystalCentres)
            {
                brush.Cone(crystal.x, crystal.y, crystal.z, 5, 22, Mat.Glass);
                brush.Cone(crystal.x - 7, crystal.y, crystal.z + 4, 3, 14, Mat.Gold);
                brush.Cone(crystal.x + 6, crystal.y, crystal.z + 6, 3, 17, Mat.Glass);
            }

            int3[] caveLights =
            {
                new(at.x - 48, at.y + 12, at.z - 28),
                new(at.x + 44, at.y + 10, at.z - 18),
                new(at.x - 38, at.y + 14, at.z + 38),
                new(at.x + 50, at.y + 11, at.z + 32),
            };
            foreach (var light in caveLights)
            {
                brush.Box(light, new int3(4, 9, 4), Mat.Glass);
                brush.Box(light - new int3(1, 3, 1), new int3(6, 3, 6), Mat.Gold);
            }
        }
    }
}
