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
        /// <summary>Draws a plan from a seed. This is where the family lives.</summary>
        public static CastlePlan Plan(int3 centre, uint seed)
        {
            var rng = new Random(seed | 1u);

            // Roughly 30-40 m across the bailey. The first draft asked for 60-84 m with a
            // 75 m sculpted skirt, which costs 38-137 million voxel writes — the run that took
            // the machine down. Scale here is a safety property, not a taste one.
            int baileyX = rng.NextInt(150, 200);
            int baileyZ = rng.NextInt(150, 200);

            return new CastlePlan
            {
                Centre = centre,
                Seed = seed,

                // Tight to the walls. A wide skirt of sculpted rock reads as a quarry, not a
                // crag, because a smooth analytic falloff quantised to voxels produces clean
                // contour rings — natural terrain hides that behind noise and this did not.
                PlateauRadius = math.max(baileyX, baileyZ) + rng.NextInt(14, 28),
                PlateauHeight = rng.NextInt(26, 44),
                CliffDrop = rng.NextInt(26, 44),

                BaileyHalfX = baileyX,
                BaileyHalfZ = baileyZ,

                WallHeight = rng.NextInt(70, 95),
                WallThickness = rng.NextInt(16, 22),

                TowerRadius = rng.NextInt(24, 32),
                TowerHeight = rng.NextInt(100, 135),

                GateTowerRadius = rng.NextInt(22, 28),
                GateTowerHeight = rng.NextInt(110, 145),

                KeepHalfX = rng.NextInt(64, 86),
                KeepHalfZ = rng.NextInt(54, 72),

                // Comfortably twice the curtain wall. A keep that only just clears the walls
                // gives the silhouette no centre, which is what the first pass looked like.
                KeepHeight = rng.NextInt(190, 240),

                FloorHeight = 38,
                Floors = rng.NextInt(4, 6),
            };
        }

        /// <summary>
        /// Voxel writes this plan implies, estimated before anything is written.
        ///
        /// Dominated by the site: sculpting a plateau fills its whole volume, so cost grows with
        /// radius squared times height. Everything above ground is shells and is comparatively
        /// free. Estimating this is the step whose absence took a machine down.
        /// </summary>
        public static long EstimateWrites(in CastlePlan plan)
        {
            double plateauArea = math.PI_DBL * plan.PlateauRadius * plan.PlateauRadius;

            // Only the surface cap is written per voxel; the volume beneath goes in as whole
            // bricks, which is why this is radius-squared rather than radius-squared-times-height.
            double siteCap = plateauArea * 8.0;

            double cliffArea = math.PI_DBL *
                ((plan.PlateauRadius + plan.CliffDrop) * (double)(plan.PlateauRadius + plan.CliffDrop)
                 - plan.PlateauRadius * (double)plan.PlateauRadius);
            double cliffCap = cliffArea * 10.0;

            double perimeter = 4.0 * (plan.BaileyHalfX + plan.BaileyHalfZ);
            double walls = perimeter * plan.WallThickness * plan.WallHeight;

            double towerRing = math.PI_DBL * plan.TowerRadius * 2.0 * 12.0;
            double towers = 6.0 * towerRing * plan.TowerHeight;

            double keep = 2.0 * (plan.KeepHalfX + plan.KeepHalfZ) * 2.0 * 8.0 * plan.KeepHeight;

            double courtyard = plateauArea * 0.4;
            double underground = 2_000_000.0;   // dungeon, passage, cave

            return (long)(siteCap + cliffCap + walls + towers + keep + courtyard + underground);
        }

        /// <summary>
        /// Builds everything, or refuses.
        ///
        /// The refusal is the point. A plan that would write more than the brush's budget is a
        /// mistake in the plan, and finding out by running it costs an afternoon and a reboot.
        /// </summary>
        public static VoxelBrush Build(RegionTable table, BrickPool pool, in CastlePlan plan, uint terrainSeed)
        {
            var brush = new VoxelBrush(table, pool);

            long estimate = EstimateWrites(in plan);
            if (estimate > brush.WriteBudget)
            {
                UnityEngine.Debug.LogError(
                    $"CastleBuilder: refusing to build. Plan implies ~{estimate:N0} voxel writes, " +
                    $"budget is {brush.WriteBudget:N0}. Reduce PlateauRadius ({plan.PlateauRadius}) " +
                    $"or PlateauHeight ({plan.PlateauHeight}) — site sculpting dominates the cost.");
                return brush;
            }

            Site(ref brush, in plan, terrainSeed);
            CurtainWalls(ref brush, in plan);
            CornerTowers(ref brush, in plan);
            Gatehouse(ref brush, in plan);
            Courtyard(ref brush, in plan);
            Keep(ref brush, in plan);
            Dungeon(ref brush, in plan);

            return brush;
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
                    // Cutting into the hill. Bulk for the body, per voxel for the new surface.
                    if (ground - target > 8)
                        brush.FillBulk(new int3(wx, target + 7, wz), new int3(1, ground - target - 6, 1), Mat.Empty);

                    for (int y = target + 1; y <= math.min(ground, target + 7); y++)
                        brush.Set(wx, y, wz, Mat.Empty);
                }
                else
                {
                    // Building the outcrop up. The cap is written per voxel because it is the
                    // visible surface and wants its material bands; the bulk beneath goes in as
                    // whole bricks, which is thousands of times cheaper than writing it voxel by
                    // voxel and waiting for each brick to collapse back to uniform.
                    int capBottom = math.max(ground, target - 6);

                    if (capBottom > ground)
                        brush.FillBulk(new int3(wx, ground, wz), new int3(1, capBottom - ground, 1), Mat.DarkStone);

                    for (int y = capBottom; y <= target; y++)
                        brush.Set(wx, y, wz, y > target - 3 ? Mat.Stone : Mat.DarkStone);
                }

                // Grass cap on the plateau, away from where the walls will sit.
                if (d < edge - 40 && rng.NextInt(0, 100) < 88)
                    brush.Set(wx, target, wz, Mat.Grass);
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

                for (int y = top - cut; y <= top + 6; y++)
                    brush.Set(x, y, z, Mat.Empty);

                for (int y = top - cut; y < top - cut + math.max(4, cut / 2); y++)
                    brush.Set(x, y, z, Mat.Water);
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
            var rng = new Random(plan.Seed ^ (uint)(start.x * 73856093) ^ (uint)(start.z * 19349663));

            for (int i = 0; i < length; i++)
            {
                int3 at = start + dir * i;

                for (int w = 0; w < thickness; w++)
                for (int y = 0; y < height; y++)
                {
                    int x = at.x + (alongX ? 0 : w);
                    int z = at.z + (alongX ? w : 0);

                    byte material = Mat.Stone;

                    // Battered plinth: wider and darker at the base.
                    if (y < 22) material = Mat.DarkStone;

                    // String course: a band two thirds up.
                    else if (y == (int)(height * 0.66f) || y == (int)(height * 0.66f) + 1)
                        material = Mat.DarkStone;

                    brush.Set(x, y + at.y, z, material);
                }

                // Wall-walk behind the parapet.
                if (i % 1 == 0)
                {
                    int walkY = at.y + height;
                    for (int w = 0; w < thickness; w++)
                    {
                        int x = at.x + (alongX ? 0 : w);
                        int z = at.z + (alongX ? w : 0);
                        brush.Set(x, walkY, z, Mat.Stone);
                    }
                }

                // Arrow slits at intervals.
                if (i % 90 == 40)
                {
                    for (int y = 40; y < 68; y++)
                    {
                        int x = at.x + (alongX ? 0 : thickness / 2);
                        int z = at.z + (alongX ? thickness / 2 : 0);

                        for (int w = 0; w < thickness; w++)
                        {
                            int sx = at.x + (alongX ? 0 : w);
                            int sz = at.z + (alongX ? w : 0);
                            brush.Set(sx, at.y + y, sz, Mat.Empty);
                        }
                    }
                }
            }

            // Parapet with crenellations, on the outward face.
            int parapetY = start.y + height + 1;
            int merlon = 26, gap = 18;

            for (int i = 0; i < length; i++)
            {
                if (i % (merlon + gap) >= merlon) continue;

                int3 at = start + dir * i;
                for (int y = 0; y < 20; y++)
                for (int w = 0; w < 8; w++)
                {
                    int x = at.x + (alongX ? 0 : w);
                    int z = at.z + (alongX ? w : 0);
                    brush.Set(x, parapetY + y, z, Mat.Stone);
                }
            }

            // Banners hung between merlons on the long runs.
            if (length > 400)
            {
                for (int i = 120; i < length - 120; i += 200)
                {
                    int3 at = start + dir * i;
                    for (int y = 0; y < 46; y++)
                    for (int w = 0; w < 14; w++)
                    {
                        int x = at.x + (alongX ? 0 : w);
                        int z = at.z + (alongX ? w : 0);
                        brush.Set(x, start.y + height - 60 + y, z, Mat.Cloth);
                    }
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

            foreach (var corner in corners)
                Tower(ref brush, in plan, corner, plan.TowerRadius, plan.TowerHeight, true);
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
            brush.Set(at.x, parapetY + 8 + radius * 2, at.z, Mat.Gold);
        }

        // -- gatehouse -----------------------------------------------------------

        private static void Gatehouse(ref VoxelBrush brush, in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            int r = plan.GateTowerRadius;
            int spacing = 78;

            var left = new int3(plan.Centre.x - spacing, baseY, gateZ);
            var right = new int3(plan.Centre.x + spacing, baseY, gateZ);

            Tower(ref brush, in plan, left, r, plan.GateTowerHeight, true);
            Tower(ref brush, in plan, right, r, plan.GateTowerHeight, true);

            // Block between the towers, then the passage carved through it.
            brush.Box(new int3(plan.Centre.x - spacing, baseY, gateZ - plan.WallThickness),
                      new int3(spacing * 2, plan.WallHeight + 40, plan.WallThickness * 2), Mat.Stone);

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

            // Bridge across the moat.
            for (int z = 0; z < 150; z++)
            for (int x = -34; x <= 34; x++)
                brush.Set(plan.Centre.x + x, baseY - 2, gateZ - plan.WallThickness - z, Mat.Wood);
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
                brush.Set(plan.Centre.x + x, baseY, plan.Centre.z + z, material);
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

            brush.SpiralStair(min.x + 26, baseY + 2, min.z + 26, 18, floors * plan.FloorHeight, Mat.Stone);

            // Windows: arched, larger on the hall floor.
            for (int f = 0; f < floors; f++)
            {
                int y = baseY + f * plan.FloorHeight + 12;
                int height = f == 1 ? 44 : 28;

                for (int i = 0; i < 3; i++)
                {
                    int x = min.x + size.x / 4 + i * size.x / 4 - 8;
                    brush.Arch(new int3(x, y, min.z), 16, height, 9, 2, Mat.Empty);
                    brush.Arch(new int3(x, y, min.z + size.z - 8), 16, height, 9, 2, Mat.Empty);
                    brush.Box(new int3(x + 3, y + 4, min.z + 2), new int3(10, height - 10, 2), Mat.Glass);
                }
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
                for (int x = min.x + inner; x < min.x + size.x - inner; x++)
                for (int h = 0; h < plan.FloorHeight - 4; h++)
                    brush.Set(x, y + h, split, Mat.Stone);

                // Doorway through the partition.
                int doorX = min.x + size.x / 2;
                for (int x = doorX - 9; x < doorX + 9; x++)
                for (int h = 0; h < 30; h++)
                    brush.Set(x, y + h, split, Mat.Empty);
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

            // Trapdoor from the ground floor into the cellar.
            int tx = keepMin.x + hx, tz = keepMin.z + hz + 40;
            brush.Box(new int3(tx - 10, cellarY + 40, tz - 10), new int3(20, 8, 20), Mat.Empty);
            brush.SpiralStair(tx, cellarY, tz, 9, 46, Mat.Stone);

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
            }

            // Secret passage: a low tunnel from the hall out towards the cliff.
            int passZ = hallMin.z - 1;
            for (int i = 0; i < 320; i++)
            {
                int z = passZ - i;
                int y = dungeonY + (int)math.round(math.sin(i * 0.02f) * 8f);
                brush.Box(new int3(tx - 14, y, z), new int3(28, 32, 1), Mat.Empty);
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
                for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y + z * z > r2) continue;
                    brush.Set(at.x + ox + x, at.y + oy + y, at.z + oz + z, Mat.Empty);
                }
            }

            // Pool in the floor, and a scatter of stalagmites.
            for (int z = -44; z <= 44; z++)
            for (int x = -44; x <= 44; x++)
            {
                if (x * x + z * z > 44 * 44) continue;
                for (int y = 0; y < 10; y++) brush.Set(at.x + x, at.y - 12 + y, at.z + z, Mat.Water);
            }

            for (int i = 0; i < 26; i++)
            {
                int sx = at.x + rng.NextInt(-95, 95);
                int sz = at.z + rng.NextInt(-95, 95);
                int h = rng.NextInt(10, 34);
                brush.Cone(sx, at.y - 2, sz, rng.NextInt(3, 7), h, Mat.DarkStone);
            }
        }
    }
}
