using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures
{
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
        public struct IncrementalBuild
        {
            internal VoxelBrush Brush;
            internal CastlePlan Plan;
            internal uint TerrainSeed;
            internal int Stage;
            internal int KeepStage;
            internal int SitePhase;
            internal int SiteCursor;
            internal Random SiteRandom;

            public bool IsCreated => Stage > 0;
            public bool IsComplete => Stage > 8;
            public int StageNumber => Stage;
            public long TotalVoxelsWritten => Brush.TotalVoxelsWritten;
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
            // A 4.6 m module gives the principal rooms the vertical proportion visible in the
            // reference. The former 3.8 m stack made even the great hall read like a low modern
            // dining room once its ceiling, chandelier, and player eye height shared the space.
            const int floorHeight = 46;
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
        public static VoxelBrush Build(IRegionReadSource reads,
                                       IRegionMutationStore mutations,
                                       in CastlePlan plan, uint terrainSeed,
                                       IMaterialAuthoringCatalogue materials)
        {
            IncrementalBuild build = BeginBuild(
                reads, mutations, in plan, terrainSeed, materials);
            while (!build.IsComplete) StepBuild(ref build);
            return build.Brush;
        }

        /// <summary>
        /// Starts the same all-or-nothing castle build as <see cref="Build"/>, split at semantic
        /// stage boundaries so a runtime caller does not execute the entire landmark in one
        /// scene-load callback.
        /// </summary>
        public static IncrementalBuild BeginBuild(IRegionReadSource reads,
                                                  IRegionMutationStore mutations,
                                                  in CastlePlan plan, uint terrainSeed,
                                                  IMaterialAuthoringCatalogue materials)
        {
            var brush = new VoxelBrush(reads, mutations, materials);

            long estimate = EstimateWrites(in plan);
            if (estimate > brush.WriteBudget)
            {
                throw new System.InvalidOperationException(
                    $"CastleBuilder: refusing to build. Plan implies ~{estimate:N0} expensive-write equivalents, " +
                    $"budget is {brush.WriteBudget:N0}. Reduce PlateauRadius ({plan.PlateauRadius}) " +
                    $"or the primary structure dimensions before retrying.");
            }

            return new IncrementalBuild
            {
                Brush = brush,
                Plan = plan,
                TerrainSeed = terrainSeed,
                Stage = 1,
            };
        }

        /// <summary>Executes one bounded semantic stage.</summary>
        public static bool StepBuild(ref IncrementalBuild build)
        {
            if (!build.IsCreated || build.IsComplete) return true;

            string stage;
            switch (build.Stage)
            {
                case 1:
                    stage = "site";
                    if (!StepSite(ref build.Brush, in build.Plan, build.TerrainSeed,
                                  ref build.SitePhase, ref build.SiteCursor,
                                  ref build.SiteRandom))
                    {
                        RequireBudget(in build.Brush, stage);
                        return false;
                    }
                    break;
                case 2: CurtainWalls(ref build.Brush, in build.Plan); stage = "curtain walls"; break;
                case 3: CornerTowers(ref build.Brush, in build.Plan); stage = "corner towers"; break;
                case 4: Gatehouse(ref build.Brush, in build.Plan); stage = "gatehouse"; break;
                case 5: Courtyard(ref build.Brush, in build.Plan); stage = "courtyard"; break;
                case 6:
                    stage = $"keep {build.KeepStage + 1}";
                    if (!StepKeep(ref build.Brush, in build.Plan, ref build.KeepStage))
                    {
                        RequireBudget(in build.Brush, stage);
                        return false;
                    }
                    break;
                case 7: Dungeon(ref build.Brush, in build.Plan); stage = "dungeon"; break;
                case 8:
                    LandscapeDetails(ref build.Brush, in build.Plan, build.TerrainSeed);
                    stage = "landscape details";
                    break;
                default: return true;
            }
            RequireBudget(in build.Brush, stage);

            // Storage capability objects share their backing state; allocator/table bookkeeping
            // stays inside Storage instead of escaping through VoxelBrush.
            build.Stage++;
            return build.IsComplete;
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
        private static bool StepSite(ref VoxelBrush brush, in CastlePlan plan, uint terrainSeed,
                                     ref int phase, ref int cursor, ref Random rng)
        {
            int top = plan.Centre.y + plan.PlateauHeight;
            int radius = plan.PlateauRadius;
            int skirt = radius + plan.CliffDrop;

            if (phase == 0)
            {
                if (cursor == 0) rng = new Random(plan.Seed ^ 0x51E5u);
                int rowEnd = math.min(skirt * 2 + 1, cursor + 4);
                for (; cursor < rowEnd; cursor++)
                {
                    int z = cursor - skirt;
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
                        if (d <= edge) target = top;
                        else
                        {
                    // Cliff face: steep, and broken up per column. The first version eased out of
                    // the plateau with pow(t, 0.55), which gives a long shallow shoulder — and a
                    // shallow slope in voxels is a staircase of contour terraces. Falling fast
                    // and unevenly is both more castle-like and cheaper.
                            float t = (d - edge) / plan.CliffDrop;
                            float broken = math.pow(t, 1.7f)
                                         + math.sin(angle * 11f + t * 6f) * 0.10f;

                            target = (int)math.round(math.lerp(
                                top, ground - 14, math.saturate(broken)));
                        }

                        if (target <= ground)
                            brush.FillColumnBulk(wx, target + 1, ground + 1, wz, Mat.Empty);
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

                        if (d < edge - 12 && rng.NextInt(0, 100) < 92)
                            brush.FillColumnBulk(wx, target, target + 1, wz, Mat.Grass);
                    }
                }
                if (cursor <= skirt * 2) return false;
                phase = 1;
                cursor = 0;
            }

            int reach = plan.PlateauRadius + plan.CliffDrop - 8;
            int columnEnd = math.min(reach * 2 + 1, cursor + 2);
            LowerRiverGorge(ref brush, in plan, top, cursor, columnEnd, reach);
            cursor = columnEnd;
            return cursor > reach * 2;
        }

        /// <summary>
        /// Cuts the approach shelf into two unmistakable terrain levels. The castle and gate road
        /// remain on the upper grass cap while a broad lower river crosses beneath the bridge.
        /// Dirt, grass, and exposed dark rock are authored as actual bank strata rather than a
        /// colour decal, so the height change reads from both the hero view and ground level.
        /// </summary>
        private static void LowerRiverGorge(ref VoxelBrush brush, in CastlePlan plan, int top,
                                            int firstColumn, int endColumn, int reach)
        {
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            int riverZ = gateZ - plan.WallThickness - 92;
            const int halfWidth = 90;
            const int waterHalfWidth = 42;
            int riverY = top - CastleLayout.LowerRiverDepth;
            for (int column = firstColumn; column < endColumn; column++)
            {
                int x = plan.Centre.x - reach + column;
                int meander = (int)math.round(math.sin((x - plan.Centre.x) * 0.028f) * 8f
                                            + math.sin((x - plan.Centre.x) * 0.071f) * 3f);
                int channelZ = riverZ + meander;

                for (int dz = -halfWidth; dz <= halfWidth; dz++)
                {
                    int z = channelZ + dz;
                    int existingSurface = HighestSolid(ref brush, x, z, top + 5, riverY - 30);
                    if (existingSurface < riverY - 20) continue;

                    float across = math.abs(dz) / (float)halfWidth;
                    float bank = math.smoothstep(0.18f, 1f, across);
                    int authoredTerrace = dz < 0 ? top - 32 : top - 1;
                    int terraceTop = math.min(authoredTerrace, existingSurface);
                    int surface = (int)math.round(math.lerp(riverY - 9, terraceTop, bank));

                    brush.FillColumnBulk(x, surface + 1,
                                         math.max(top + 8, existingSurface + 2), z, Mat.Empty);

                    // Four visible dirt courses sit above broken foundation rock. The outermost
                    // bank receives a grass lip, giving the gorge the green/brown/grey layering
                    // seen in the reference instead of one monotonous cut stone wall.
                    int dirtDepth = across > 0.46f ? 5 : 2;
                    brush.FillColumnBulk(x, surface - dirtDepth, surface, z,
                                         across > 0.38f ? Mat.Dirt : Mat.DarkStone);
                    if (across > 0.56f)
                        brush.FillColumnBulk(x, surface, surface + 1, z, Mat.Grass);

                    if (math.abs(dz) <= waterHalfWidth)
                    {
                        int bed = riverY - 10
                                + (int)math.round(math.abs(dz) * 4f / waterHalfWidth);
                        brush.FillColumnBulk(x, bed, riverY + 1, z, Mat.Water);
                    }
                }
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
            ApproachPlanting(ref brush, in plan, top);
            WallFootingOvergrowth(ref brush, in plan, top);
            RemoveFloatingRiverTerrain(ref brush, in plan, top);
        }

        private static void WallFootingOvergrowth(ref VoxelBrush brush, in CastlePlan plan, int top)
        {
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            var rng = new Random(plan.Seed ^ 0xB07A11u);

            // Low vegetation, fallen blocks, and damp stone overlap the wall silhouette at
            // ground level. Keep the central bridge lane completely clear and bias the larger
            // pieces toward buttresses, where real collapse debris naturally accumulates.
            for (int side = -1; side <= 1; side += 2)
            for (int bay = 0; bay < 7; bay++)
            {
                int x = plan.Centre.x + side * (96 + bay * 24);
                if (math.abs(x - plan.Centre.x) >= plan.BaileyHalfX - plan.TowerRadius) continue;
                int z = gateZ - 17 - rng.NextInt(0, 10);
                int surface = HighestSolid(ref brush, x, z, top + 12, top - 80);
                int shrubRadius = rng.NextInt(4, 8);
                brush.Cone(x, surface + 1, z, shrubRadius, rng.NextInt(5, 11),
                           (bay & 1) == 0 ? Mat.Moss : Mat.Grass);

                int rubbleX = x + side * rng.NextInt(8, 15);
                int rubbleZ = z - rng.NextInt(2, 10);
                int rubbleY = HighestSolid(ref brush, rubbleX, rubbleZ, top + 12, top - 80);
                brush.Box(new int3(rubbleX, rubbleY + 1, rubbleZ),
                          new int3(rng.NextInt(4, 9), rng.NextInt(3, 7), rng.NextInt(4, 8)),
                          bay % 3 == 0 ? Mat.Stone : Mat.DarkStone);
            }

            // Tapered ivy tongues replace rectangular green decals. They climb from vegetation
            // at the footing and break into narrower off-axis strands as they rise.
            int[] ivyOffsets = { -214, -162, -108, 116, 171, 218 };
            for (int i = 0; i < ivyOffsets.Length; i++)
            {
                int rootX = plan.Centre.x + ivyOffsets[i];
                if (math.abs(ivyOffsets[i]) >= plan.BaileyHalfX - plan.TowerRadius) continue;
                int ivyHeight = 24 + (i * 13 % 31);
                for (int y = 0; y < ivyHeight; y += 6)
                {
                    int width = math.max(2, 9 - y / 7);
                    int drift = ((i & 1) == 0 ? 1 : -1) * (y / 10);
                    brush.Box(new int3(rootX + drift, top + 2 + y, gateZ - 2),
                              new int3(width, math.min(7, ivyHeight - y), 2), Mat.Moss);
                }
            }

            // Two irregular foreground copses create depth layers in the hero view without
            // hiding the gate or repeating the evenly spaced procedural tree belt.
            int2[] copseOffsets =
            {
                new(-260, -82), new(-282, -48), new(266, -62), new(292, -30),
            };
            for (int i = 0; i < copseOffsets.Length; i++)
            {
                int x = plan.Centre.x + copseOffsets[i].x;
                int z = gateZ + copseOffsets[i].y;
                int surface = HighestSolid(ref brush, x, z, top + 18, top - 120);
                Pine(ref brush, x, surface + 1, z, 44 + i * 5, 13 + (i & 1) * 3,
                     i == 1 ? Mat.Moss : Mat.Grass);
            }
        }

        private static void RavineWaterfall(ref VoxelBrush brush, in CastlePlan plan,
                                            uint terrainSeed, int top)
        {
            int streamX = CastleLayout.WaterfallStreamX(in plan);
            int lipZ = CastleLayout.WaterfallLipZ(in plan);
            int riverZ = CastleLayout.LowerRiverZAt(in plan, streamX);
            int streamStartZ = plan.Centre.z + plan.BaileyHalfZ + plan.TowerRadius + 18;
            int streamLength = math.max(1, streamStartZ - lipZ);

            // The high stream now runs lengthwise beside the east curtain wall. Its meander and
            // exposed dark banks keep the route legible from both the gate approach and aerial
            // views before it reaches the front-facing cliff lip.
            for (int z = streamStartZ; z >= lipZ; z--)
            {
                float t = (streamStartZ - z) / (float)streamLength;
                int centreX = streamX
                            + (int)math.round(math.sin(t * math.PI * 3.2f) * 7f);
                int halfWidth = 10 + (int)math.round(t * 5f);
                int channelY = top - 6 - (int)math.round(t * 11f);
                for (int dx = -halfWidth; dx <= halfWidth; dx++)
                {
                    float across = math.abs(dx) / (float)halfWidth;
                    int bottom = channelY + (int)math.round(across * across * 8f);
                    brush.FillColumnBulk(centreX + dx, bottom, top + 8, z, Mat.Empty);
                    if (math.abs(dx) <= halfWidth - 3)
                        brush.FillColumnBulk(centreX + dx, bottom, bottom + 3, z, Mat.Water);
                }
            }

            // A lower plunge pool overlaps the north bank of the lower river. This guarantees
            // the two water levels are physically connected and gives the fall a broad reflective
            // destination rather than a decorative dead-end bowl.
            int poolX = streamX;
            int poolZ = riverZ + 27;
            int poolY = top - 80;
            const int poolRadiusX = 68;
            const int poolRadiusZ = 43;
            for (int dz = -poolRadiusZ; dz <= poolRadiusZ; dz++)
            for (int dx = -poolRadiusX; dx <= poolRadiusX; dx++)
            {
                float ellipse = dx * dx / (float)(poolRadiusX * poolRadiusX)
                              + dz * dz / (float)(poolRadiusZ * poolRadiusZ);
                if (ellipse > 1f) continue;
                float rim = math.saturate((ellipse - 0.66f) / 0.34f);
                int bottom = ellipse < 0.66f
                    ? poolY - 9
                    : (int)math.round(math.lerp(poolY - 9, poolY + 17,
                                                math.pow(rim, 0.72f)));
                brush.FillColumnBulk(poolX + dx, bottom, top + 7, poolZ + dz, Mat.Empty);
                if (ellipse < 0.68f)
                    brush.FillColumnBulk(poolX + dx, bottom, poolY + 1,
                                         poolZ + dz, Mat.Water);
            }

            // Broad front-facing curtain. Clear a recessed shadow pocket first, then author five
            // voxels of water thickness so grazing camera rays cannot skip the cascade.
            for (int dz = -7; dz <= 7; dz++)
            for (int dx = -30; dx <= 30; dx++)
            {
                brush.FillColumnBulk(streamX + dx, poolY + 1, top - 16,
                                     lipZ + dz, Mat.Empty);
                if (dz <= 0 && dz >= -5 && math.abs(dx) <= 23)
                {
                    int edge = math.abs(dx);
                    int raggedTop = top - 16 - edge / 7
                                  - math.abs((dx * 13 + dz * 7) % 3);
                    int raggedBottom = poolY + 1 + math.max(0, edge - 18) / 2;
                    brush.FillColumnBulk(streamX + dx, raggedBottom, raggedTop,
                                         lipZ + dz, Mat.Cascade);
                }
            }

            // Stepped, widening outflow lowers the pool into the river's reflective plane.
            int outletLength = math.max(1, poolZ - riverZ);
            for (int z = poolZ; z >= riverZ; z--)
            {
                float t = (poolZ - z) / (float)outletLength;
                int waterY = (int)math.round(math.lerp(poolY, top - CastleLayout.LowerRiverDepth, t));
                int halfWidth = 18 + (int)math.round(t * 8f);
                for (int dx = -halfWidth; dx <= halfWidth; dx++)
                {
                    float across = math.abs(dx) / (float)halfWidth;
                    int bed = waterY - 7 + (int)math.round(across * across * 6f);
                    brush.FillColumnBulk(streamX + dx, bed, top + 5, z, Mat.Empty);
                    if (math.abs(dx) <= halfWidth - 3)
                        brush.FillColumnBulk(streamX + dx, bed, waterY + 1, z, Mat.Water);
                }
            }

            // Broken masonry and asymmetrical trees frame, but never cross, the water curtain.
            var rockRng = new Random(plan.Seed ^ 0xA11CEu);
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 4; i++)
            {
                int rx = streamX + side * (34 + i * 8);
                int rz = lipZ + 5 + i * 7;
                int surface = HighestSolid(ref brush, rx, rz, top + 12, poolY - 16);
                brush.Cone(rx, surface + 1, rz, rockRng.NextInt(4, 7),
                           rockRng.NextInt(7, 14), Mat.DarkStone);
            }

            int2[] treeOffsets = { new(-88, 58), new(92, 72), new(-105, -28), new(108, -18) };
            for (int i = 0; i < treeOffsets.Length; i++)
            {
                int tx = poolX + treeOffsets[i].x;
                int tz = poolZ + treeOffsets[i].y;
                int surface = HighestSolid(ref brush, tx, tz, top + 24, top - 180);
                if ((i & 1) == 0)
                    Tree(ref brush, tx, surface + 1, tz, 40 + i * 3, 15, Mat.Moss);
                else
                    Pine(ref brush, tx, surface + 1, tz, 45 + i * 3, 14, Mat.Grass);
            }

        }

        private static void RemoveFloatingRiverTerrain(ref VoxelBrush brush,
                                                       in CastlePlan plan, int top)
        {
            int streamX = CastleLayout.WaterfallStreamX(in plan);
            int lipZ = CastleLayout.WaterfallLipZ(in plan);
            int riverZ = CastleLayout.LowerRiverZAt(in plan, streamX);
            const int poolRadiusX = 68;

            // Run after every planting/debris pass. Pool carving can expose old cap fragments,
            // and later foliage can otherwise reintroduce green shelves above the water.
            for (int x = streamX - poolRadiusX - 10; x <= streamX + poolRadiusX + 10; x++)
            for (int z = riverZ - 10; z <= lipZ + 30; z++)
            {
                bool waterBelow = false;
                bool structurallyAnchored = false;
                for (int y = top - CastleLayout.LowerRiverDepth - 12; y <= top + 8; y++)
                {
                    byte material = brush.Get(x, y, z);
                    if (material == Mat.Water || material == Mat.Cascade)
                    {
                        waterBelow = true;
                        structurallyAnchored = false;
                        continue;
                    }
                    if (material == Mat.Empty || !waterBelow) continue;

                    bool looseTerrain = material == Mat.Grass || material == Mat.Dirt
                                     || material == Mat.Moss || material == Mat.Sand;
                    if (looseTerrain && !structurallyAnchored)
                        brush.Set(x, y, z, Mat.Empty);
                    else
                        structurallyAnchored = true;
                }
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
                int waterfallOffsetX = CastleLayout.WaterfallStreamX(in plan) - plan.Centre.x;
                int waterfallOffsetZ = CastleLayout.WaterfallLipZ(in plan) - plan.Centre.z;
                bool nearWaterfall = math.abs(ox - waterfallOffsetX) < 125
                                  && math.abs(oz - waterfallOffsetZ) < 165;
                if (!outsideWalls || blocksGate || nearWaterfall) continue;

                int height = rng.NextInt(34, 58);
                int canopyRadius = rng.NextInt(12, 19);
                Tree(ref brush, plan.Centre.x + ox, top + 1, plan.Centre.z + oz,
                     height, canopyRadius, built % 3 == 0 ? Mat.Grass : Mat.Moss);
                built++;
            }
        }

        /// <summary>
        /// A composed foreground frame rather than uniform procedural scatter. Dark conifers and
        /// broken stone sit outside the broad gate/bridge lane, matching the reference's wooded
        /// ravine setting while preserving an unmistakable route into the castle.
        /// </summary>
        private static void ApproachPlanting(ref VoxelBrush brush, in CastlePlan plan, int top)
        {
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            int2[] offsets =
            {
                new(-178, -92), new(168, -78), new(-235, -105), new(235, -110),
                new(-154, 42), new(184, 62),
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                int x = plan.Centre.x + offsets[i].x;
                int z = gateZ + offsets[i].y;
                int surface = HighestSolid(ref brush, x, z, top + 20, top - 170);
                if ((i & 1) == 0)
                    Pine(ref brush, x, surface + 1, z, 58 + (i % 3) * 8,
                         18 + (i & 1) * 3, i % 3 == 0 ? Mat.Grass : Mat.Moss);
                else
                    Tree(ref brush, x, surface + 1, z, 44 + (i % 3) * 6,
                         15 + (i % 2) * 3, i % 3 == 0 ? Mat.Grass : Mat.Moss);

                // Irregular companion rocks visually root each tree and prevent the repeated
                // verticals from reading as a planted avenue.
                int side = (i & 1) == 0 ? -1 : 1;
                int rockX = x + side * (13 + i % 3 * 3);
                int rockZ = z + 8 - i * 3;
                int rockY = HighestSolid(ref brush, rockX, rockZ, top + 20, top - 170);
                brush.Cone(rockX, rockY + 1, rockZ, 4 + i % 3, 6 + i % 4,
                           i % 2 == 0 ? Mat.DarkStone : Mat.Stone);
            }
        }

        private static void Pine(ref VoxelBrush brush, int x, int y, int z,
                                 int height, int radius, byte foliage)
        {
            // Trees are semantic vegetation now. CastleBuilder no longer authors voxel
            // trunks/crowns; ShowcaseTreePopulation publishes deterministic TreeInstances.
        }

        private static void Tree(ref VoxelBrush brush, int x, int y, int z,
                                 int height, int canopyRadius, byte foliage)
        {
            // Trees are semantic vegetation now. CastleBuilder no longer authors voxel
            // trunks/crowns; ShowcaseTreePopulation publishes deterministic TreeInstances.
        }

        private static void FoliageBlob(ref VoxelBrush brush, int x, int y, int z,
                                        int radius, int verticalRadius, byte foliage)
        {
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                float radial = (dx * dx + dz * dz) / (float)(radius * radius);
                if (radial > 1f) continue;
                int halfHeight = math.max(1,
                    (int)math.round(math.sqrt(1f - radial) * verticalRadius));
                brush.FillColumnBulk(x + dx, y - halfHeight, y + halfHeight + 1,
                                     z + dz, foliage);
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

            // Timber hoardings make the curtain feel accumulated rather than generated as one
            // uninterrupted stone operation. Their deep overhangs, posts, rails, and lean-to
            // roofs add the mid-scale shadow rhythm missing between buttresses and crenellations.
            for (int side = -1; side <= 1; side += 2)
            {
                const int width = 58;
                int galleryX = plan.Centre.x + side * (hx * 3 / 5) - width / 2;
                int galleryY = wallTop - 34;
                int galleryZ = gateZ - 17;

                brush.Box(new int3(galleryX, galleryY, galleryZ),
                          new int3(width, 4, 20), Mat.Wood);
                for (int post = 4; post < width - 2; post += 16)
                {
                    brush.Box(new int3(galleryX + post, galleryY + 4, galleryZ + 2),
                              new int3(3, 21, 3), Mat.Wood);
                    brush.Box(new int3(galleryX + post, galleryY - 10, galleryZ + 13),
                              new int3(4, 12, 4), Mat.DarkStone);
                }
                brush.Box(new int3(galleryX + 2, galleryY + 13, galleryZ),
                          new int3(width - 4, 3, 3), Mat.Wood);
                brush.Box(new int3(galleryX - 3, galleryY + 24, galleryZ - 2),
                          new int3(width + 6, 3, 24), Mat.Tile);
                brush.Box(new int3(galleryX + 3, galleryY + 7, galleryZ + 3),
                          new int3(4, 7, 4), Mat.LitWindow);
                brush.Box(new int3(galleryX + width - 7, galleryY + 7, galleryZ + 3),
                          new int3(4, 7, 4), Mat.LitWindow);
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

            // Sparse moss and rain staining anchor the pale masonry to the wet ravine setting.
            // Every patch is only two voxels deep on the outside face, so weathering cannot fill
            // an arrow slit, narrow the wall walk, or create an inaccessible interior volume.
            int2[] frontWeathering =
            {
                new(-190, 15), new(-146, 7), new(-94, 24),
                new(103, 10), new(158, 27), new(205, 13),
            };
            for (int i = 0; i < frontWeathering.Length; i++)
            {
                int patchX = plan.Centre.x + frontWeathering[i].x;
                int patchY = baseY + frontWeathering[i].y;
                int width = 13 + (i * 7 % 17);
                int height = 5 + (i * 5 % 13);
                if (math.abs(patchX - plan.Centre.x) > hx - plan.TowerRadius - width) continue;
                brush.Box(new int3(patchX, patchY, gateZ - 2),
                          new int3(width, height, 2), Mat.Moss);
                if ((i & 1) == 0)
                    brush.Box(new int3(patchX + width / 3, baseY + 2, gateZ - 2),
                              new int3(5, frontWeathering[i].y + 4, 2), Mat.Moss);
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
                // The reference is deliberately accumulated and asymmetric: its western front
                // tower is an older, taller watch tower while the opposite drum remains lower.
                // Both retain their real spiral stair and courtyard door; this is usable height,
                // not a sealed silhouette prop. Roofed rear towers form a second skyline layer.
                int heightVariation = i == 0 ? 58 : i == 1 ? 8 : i == 2 ? 30 : 14;
                int towerHeight = plan.TowerHeight + heightVariation;
                Tower(ref brush, in plan, corners[i], plan.TowerRadius,
                      towerHeight, i >= 2);
                if (i < 2)
                    FrontTowerWindows(ref brush, corners[i], plan.TowerRadius,
                                      towerHeight, plan.FloorHeight);
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

            // Shallow floor-height belt courses break the otherwise uninterrupted cylinder into
            // occupied storeys. They project only three voxels from the outside skin and never
            // enter the stair room.
            for (int y = at.y + plan.FloorHeight; y < at.y + height - 28;
                 y += plan.FloorHeight)
            {
                brush.Cylinder(at.x, y - 2, at.z, radius + 2, 3,
                               Mat.DarkStone, radius - 1);
            }

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

        /// <summary>
        /// Places consistent occupied openings on the hero-facing arc of a drum tower. Random
        /// arrow slits are useful around the full circumference, but they can leave the entire
        /// approach elevation blank for a valid seed. These arched windows remain in the outer
        /// masonry shell and therefore cannot intersect the spiral stair or its landings.
        /// </summary>
        private static void FrontTowerWindows(ref VoxelBrush brush, int3 at, int radius,
                                              int height, int floorHeight)
        {
            const int width = 14;
            const int windowHeight = 24;
            int frontZ = at.z - radius - 2;

            for (int floor = 1; floor * floorHeight + windowHeight + 12 < height; floor++)
            {
                int y = at.y + floor * floorHeight + 9;

                // Projecting dark-stone hood and sill provide real shadow depth around the warm
                // opening. Carve afterward so the hood becomes a ring rather than a filled badge.
                brush.Arch(new int3(at.x - width / 2 - 3, y - 3, frontZ - 3),
                           width + 6, windowHeight + 6, 5, 2, Mat.DarkStone);
                brush.Arch(new int3(at.x - width / 2, y, frontZ - 4),
                           width, windowHeight, 20, 2, Mat.Empty);
                brush.Arch(new int3(at.x - width / 2 + 3, y + 3, frontZ + 2),
                           width - 6, windowHeight - 7, 2, 2, Mat.LitWindow);
                brush.Box(new int3(at.x - 1, y + 4, frontZ + 1),
                          new int3(2, windowHeight - 10, 3), Mat.DarkStone);
                brush.Box(new int3(at.x - width / 2 + 3, y + windowHeight / 2, frontZ + 1),
                          new int3(width - 6, 2, 3), Mat.DarkStone);
                brush.Box(new int3(at.x - width / 2 - 4, y - 4, frontZ - 4),
                          new int3(width + 8, 3, 6), Mat.DarkStone);
            }
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

            int leftHeight = plan.GateTowerHeight + 38;
            int rightHeight = plan.GateTowerHeight + 12;
            Tower(ref brush, in plan, left, r, leftHeight, false);
            Tower(ref brush, in plan, right, r, rightHeight, false);
            FrontTowerWindows(ref brush, left, r, leftHeight, plan.FloorHeight);
            FrontTowerWindows(ref brush, right, r, rightHeight, plan.FloorHeight);

            // Arched gate passage.
            brush.Arch(new int3(plan.Centre.x - 26, baseY, gateZ - plan.WallThickness),
                       52, 74, plan.WallThickness * 2, 2, Mat.Empty);

            // A real closed double gate occupies the approach arch. It is deliberately one
            // authored arch volume so the E interaction can remove exactly the door without
            // invoking blast physics or disturbing the surrounding gatehouse masonry.
            brush.Arch(CastleLayout.FrontGateMinimum(in plan), CastleLayout.FrontGateWidth, CastleLayout.FrontGateHeight,
                       CastleLayout.FrontGateDepth, 2, Mat.Wood);
            int3 gateMin = CastleLayout.FrontGateMinimum(in plan);
            for (int band = 0; band < 3; band++)
                brush.Box(new int3(gateMin.x + 2, gateMin.y + 10 + band * 13, gateMin.z),
                          new int3(CastleLayout.FrontGateWidth - 4, 3, CastleLayout.FrontGateDepth), Mat.DarkStone);
            brush.Box(new int3(plan.Centre.x - 2, gateMin.y + 2, gateMin.z),
                      new int3(4, 44, CastleLayout.FrontGateDepth), Mat.DarkStone);
            for (int side = -1; side <= 1; side += 2)
                brush.Box(new int3(plan.Centre.x + side * 8 - 2, gateMin.y + 23, gateMin.z),
                          new int3(4, 4, 2), Mat.Gold);

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

            // Heavy longitudinal beams make the deck read as a load-bearing object from the
            // lower river. They also visually tie the masonry piers into the timber span.
            int bridgeNearZ = gateZ - plan.WallThickness - 149;
            int bridgeFarZ = gateZ - plan.WallThickness;
            for (int side = -1; side <= 1; side += 2)
                brush.Box(new int3(plan.Centre.x + side * 25 - 4, baseY - 7, bridgeNearZ),
                          new int3(8, 5, 150), Mat.DarkStone);

            int riverZ = gateZ - plan.WallThickness - 92;
            int riverY = baseY - CastleLayout.LowerRiverDepth;
            int[] pierOffsets = { -27, 0, 27 };
            for (int p = 0; p < pierOffsets.Length; p++)
            for (int side = -1; side <= 1; side += 2)
            {
                int pierZ = riverZ + pierOffsets[p];
                brush.Box(new int3(plan.Centre.x + side * 24 - 6, riverY - 2, pierZ - 6),
                          new int3(12, baseY - riverY - 5, 12), Mat.DarkStone);
                brush.Box(new int3(plan.Centre.x + side * 24 - 9, baseY - 12, pierZ - 8),
                          new int3(18, 6, 16), Mat.Stone);
            }

            // Timber rails, stone abutments, and regularly spaced posts turn the former floating
            // plank into a believable defended approach. The full 5.2 m centre lane stays clear
            // for the player and for destruction debris.
            for (int side = -1; side <= 1; side += 2)
            {
                int railX = plan.Centre.x + side * 32;
                brush.Box(new int3(railX - 2, baseY + 8, bridgeNearZ),
                          new int3(4, 4, 150), Mat.Wood);
                for (int z = bridgeNearZ; z <= bridgeFarZ; z += 24)
                    brush.Box(new int3(railX - 3, baseY - 1, z),
                              new int3(6, 17, 6), Mat.Wood);
            }
            brush.Box(new int3(plan.Centre.x - 42, baseY - 12, bridgeNearZ - 8),
                      new int3(84, 12, 14), Mat.DarkStone);
            brush.Box(new int3(plan.Centre.x - 40, baseY - 5, bridgeFarZ - 5),
                      new int3(80, 7, 12), Mat.Stone);
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
        private static bool StepKeep(ref VoxelBrush brush, in CastlePlan plan, ref int stage)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int hx = plan.KeepHalfX, hz = plan.KeepHalfZ;
            var min = new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz + 60);
            var size = new int3(hx * 2, plan.KeepHeight, hz * 2);
            int floors = plan.Floors;

            if (stage == 0)
            {
                // Shell with a plinth.
                brush.Box(new int3(min.x - 6, baseY - 26, min.z - 6),
                          new int3(size.x + 12, 30, size.z + 12), Mat.DarkStone);
                brush.HollowBox(min, size, 8, Mat.Stone, false, false);

                // HollowBox writes only the shell; it does not erase terrain or the solid plinth
                // already inside that shell. Preserve the baseY floor and explicitly clear the
                // occupied volume before adding floors, partitions, and furniture.
                brush.FillBulk(new int3(min.x + 8, baseY + 1, min.z + 8),
                               new int3(size.x - 16, size.y - 1, size.z - 16), Mat.Empty);
            }

            else if (stage == 1)
            {
                // Corner turrets.
                for (int i = 0; i < 4; i++)
                {
                    int cx = min.x + (i % 2 == 0 ? 0 : size.x);
                    int cz = min.z + (i < 2 ? 0 : size.z);
                    Tower(ref brush, in plan, new int3(cx, baseY, cz), 26,
                          plan.KeepHeight + 30, true);
                }
            }

            else if (stage == 2)
            {
                // Floors and rooms.
                for (int f = 0; f < floors; f++)
                {
                    int y = baseY + f * plan.FloorHeight;
                    if (f > 0)
                        brush.Box(new int3(min.x + 8, y, min.z + 8),
                                  new int3(size.x - 16, 3, size.z - 16), Mat.Wood);

                    Rooms(ref brush, in plan, min, size, y, f);
                }
            }

            else if (stage == 3)
            {
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
            const int grandRise = 2;
            const int grandRun = 3;
            int grandSteps = plan.FloorHeight / grandRise;
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

            // The helical stair meets every floor at an exact floor-height multiple. Its own
            // headroom carve cuts through the timber slabs, while its outer tread touches the
            // surrounding floor as a landing.
            int stairX = min.x + 34;
            int stairZ = min.z + 34;
            const int stairRadius = 22;
                brush.SpiralStair(stairX, baseY + 2, stairZ, stairRadius,
                                  floors * plan.FloorHeight, Mat.Stone);
            }

            else if (stage == 4)
            {
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
                                      new int3(10, height - 10, 2), Mat.LitWindow);
                            brush.Box(new int3(x + 7, y + 5, min.z + 1),
                                      new int3(2, height - 12, 3), Mat.DarkStone);
                            brush.Box(new int3(x + 3, y + height / 2, min.z + 1),
                                      new int3(10, 2, 3), Mat.DarkStone);
                        }

                        brush.Arch(new int3(x, y, min.z + size.z - 8),
                                   16, height, 9, 2, Mat.Empty);
                    }
                }
            }

            else if (stage == 5)
            {
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

            // A few damp streaks climb from the keep plinth. Keeping them below the principal
            // windows preserves the pale central mass while breaking the pristine lower facade.
            int2[] keepStains =
            {
                new(-74, 5), new(-35, 14), new(42, 8), new(76, 20),
            };
            for (int i = 0; i < keepStains.Length; i++)
            {
                int stainX = plan.Centre.x + keepStains[i].x;
                int stainHeight = 8 + (i * 6 % 15);
                brush.Box(new int3(stainX, baseY + keepStains[i].y, min.z - 2),
                          new int3(9 + (i & 1) * 6, stainHeight, 2), Mat.Moss);
                brush.Box(new int3(stainX + 3, baseY + 2, min.z - 2),
                          new int3(3, keepStains[i].y + 5, 2), Mat.Moss);
            }

                KeepRearOriel(ref brush, in plan, min, size, baseY);
            }

            else if (stage == 6)
            {
                // Battlements and a steep roof.
                int topY = baseY + floors * plan.FloorHeight;
                brush.Box(new int3(min.x - 5, topY, min.z - 5),
                          new int3(size.x + 10, 6, size.z + 10), Mat.DarkStone);

            for (int i = 0; i < size.x + 10; i += 44)
            {
                brush.Box(new int3(min.x - 5 + i, topY + 6, min.z - 5), new int3(24, 20, 7), Mat.Stone);
                brush.Box(new int3(min.x - 5 + i, topY + 6, min.z + size.z + 3), new int3(24, 20, 7), Mat.Stone);
            }

            brush.Gable(new int3(min.x, topY + 8, min.z), new int3(size.x, 70, size.z), true, Mat.Tile);

            KeepRooflineDetails(ref brush, in plan, min, size, topY);

            GreatHallWing(ref brush, in plan, min, size, baseY);
                ChapelWing(ref brush, in plan, min, size, baseY);
            }

            stage++;
            return stage > 6;
        }

        /// <summary>
        /// A two-storey timber oriel accumulated on the rear keep wall. Each level opens directly
        /// into an existing room, so the silhouette gain is still real accessible architecture
        /// rather than a sealed facade prop.
        /// </summary>
        private static void KeepRearOriel(ref VoxelBrush brush, in CastlePlan plan,
                                          int3 keepMin, int3 keepSize, int baseY)
        {
            const int width = 44;
            const int depth = 22;
            int minX = plan.Centre.x + 18;
            int wallZ = keepMin.z + keepSize.z;
            // Start above the curtain wall so the occupied volume participates in the exterior
            // silhouette rather than being almost entirely hidden behind the battlements.
            int firstFloorY = baseY + plan.FloorHeight * 2;

            // Stone corbels visibly carry the timber volume from below.
            for (int x = 3; x < width - 2; x += 12)
                brush.Box(new int3(minX + x, firstFloorY - 13, wallZ + 2),
                          new int3(5, 13, 14), Mat.DarkStone);

            for (int storey = 0; storey < 2; storey++)
            {
                int y = firstFloorY + storey * plan.FloorHeight;
                brush.Box(new int3(minX, y, wallZ - 2),
                          new int3(width, 4, depth), Mat.Wood);
                brush.Box(new int3(minX, y + 4, wallZ + depth - 5),
                          new int3(width, plan.FloorHeight - 7, 4), Mat.Wood);
                brush.Box(new int3(minX, y + 4, wallZ),
                          new int3(4, plan.FloorHeight - 7, depth - 3), Mat.Wood);
                brush.Box(new int3(minX + width - 4, y + 4, wallZ),
                          new int3(4, plan.FloorHeight - 7, depth - 3), Mat.Wood);

                // Three tall glazed bays separated by structural posts.
                for (int bay = 0; bay < 3; bay++)
                {
                    int bayX = minX + 5 + bay * 13;
                    brush.Box(new int3(bayX, y + 9, wallZ + depth - 4),
                              new int3(9, plan.FloorHeight - 18, 3), Mat.LitWindow);
                }

                // A broad threshold connects each bay to the generated room and leaves the
                // timber floor intact beneath the actor.
                brush.Box(new int3(minX + 8, y + 4, wallZ - 8),
                          new int3(width - 16, 25, 12), Mat.Empty);
                brush.Box(new int3(minX + 4, y + 4, wallZ + 4),
                          new int3(width - 8, plan.FloorHeight - 8, depth - 9), Mat.Empty);
            }

            int roofY = firstFloorY + plan.FloorHeight * 2;
            brush.Gable(new int3(minX - 4, roofY, wallZ - 4),
                        new int3(width + 8, 24, depth + 8), true, Mat.Tile);
            brush.Box(new int3(minX - 3, firstFloorY + plan.FloorHeight - 1, wallZ - 1),
                      new int3(width + 6, 3, depth + 1), Mat.DarkStone);
        }

        /// <summary>
        /// Breaks the keep's single long roof into the clustered silhouette of the reference:
        /// an off-centre masonry lantern, two front dormers, and a tall heraldic finial. These are
        /// open belfry/dormer structures over the roof rather than inaccessible occupied rooms.
        /// </summary>
        private static void KeepRooflineDetails(ref VoxelBrush brush, in CastlePlan plan,
                                                int3 min, int3 size, int topY)
        {
            int roofFrontZ = min.z - 2;

            // Paired dormers project through the front roof plane. Their lit glazing gives the
            // large roof a human scale in the long approach view.
            for (int side = -1; side <= 1; side += 2)
            {
                int dormerX = plan.Centre.x + side * 52;
                brush.Box(new int3(dormerX - 12, topY + 25, roofFrontZ),
                          new int3(24, 25, 18), Mat.Stone);
                brush.Arch(new int3(dormerX - 6, topY + 32, roofFrontZ - 1),
                           12, 16, 4, 2, Mat.Empty);
                brush.Box(new int3(dormerX - 3, topY + 35, roofFrontZ),
                          new int3(6, 10, 2), Mat.LitWindow);
                brush.Gable(new int3(dormerX - 15, topY + 49, roofFrontZ - 4),
                            new int3(30, 20, 25), true, Mat.Slate);
            }

            // An open, off-centre belfry rises from the ridge. Four piers and connecting lintels
            // create visible sky openings, so it reads as architectural depth rather than a
            // solid voxel cube pretending to contain another room.
            int lanternX = plan.Centre.x + size.x / 7;
            int lanternZ = min.z + size.z / 2;
            int lanternY = topY + 63;
            const int half = 24;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                brush.Box(new int3(lanternX + sx * half - 5, lanternY,
                                   lanternZ + sz * half - 5),
                          new int3(10, 48, 10), Mat.Stone);

            // Thin walls between the piers make the belfry read as a small square tower at long
            // range. Large arches immediately carve those walls back to open air, so there is no
            // sealed rooftop room and the sky remains visible through it from every direction.
            brush.Box(new int3(lanternX - half - 5, lanternY,
                               lanternZ - half - 5),
                      new int3(half * 2 + 10, 48, 8), Mat.Stone);
            brush.Box(new int3(lanternX - half - 5, lanternY,
                               lanternZ + half - 3),
                      new int3(half * 2 + 10, 48, 8), Mat.Stone);
            brush.Box(new int3(lanternX - half - 5, lanternY,
                               lanternZ - half + 3),
                      new int3(8, 48, half * 2 - 6), Mat.Stone);
            brush.Box(new int3(lanternX + half - 3, lanternY,
                               lanternZ - half + 3),
                      new int3(8, 48, half * 2 - 6), Mat.Stone);
            brush.Arch(new int3(lanternX - 13, lanternY + 7,
                                lanternZ - half - 6), 26, 34, 10, 2, Mat.Empty);
            brush.Arch(new int3(lanternX - 13, lanternY + 7,
                                lanternZ + half - 4), 26, 34, 10, 2, Mat.Empty);
            brush.Arch(new int3(lanternX - half - 6, lanternY + 7,
                                lanternZ - 13), 26, 34, 10, 0, Mat.Empty);
            brush.Arch(new int3(lanternX + half - 4, lanternY + 7,
                                lanternZ - 13), 26, 34, 10, 0, Mat.Empty);

            brush.Box(new int3(lanternX - half - 5, lanternY + 40,
                               lanternZ - half - 5),
                      new int3(half * 2 + 10, 9, 10), Mat.DarkStone);
            brush.Box(new int3(lanternX - half - 5, lanternY + 40,
                               lanternZ + half - 5),
                      new int3(half * 2 + 10, 9, 10), Mat.DarkStone);
            brush.Box(new int3(lanternX - half - 5, lanternY + 40,
                               lanternZ - half + 5),
                      new int3(10, 9, half * 2 - 10), Mat.DarkStone);
            brush.Box(new int3(lanternX + half - 5, lanternY + 40,
                               lanternZ - half + 5),
                      new int3(10, 9, half * 2 - 10), Mat.DarkStone);
            // A flat machicolated crown echoes the tall square towers in the reference and reads
            // clearly from the approach; the earlier little gable collapsed into a table shape.
            brush.Box(new int3(lanternX - half - 8, lanternY + 49,
                               lanternZ - half - 8),
                      new int3(half * 2 + 16, 7, half * 2 + 16), Mat.DarkStone);
            for (int x = -half - 7; x <= half - 5; x += 18)
            {
                brush.Box(new int3(lanternX + x, lanternY + 56,
                                   lanternZ - half - 7), new int3(11, 15, 8), Mat.Stone);
                brush.Box(new int3(lanternX + x, lanternY + 56,
                                   lanternZ + half - 1), new int3(11, 15, 8), Mat.Stone);
            }
            for (int z = -half + 8; z <= half - 10; z += 18)
            {
                brush.Box(new int3(lanternX - half - 7, lanternY + 56,
                                   lanternZ + z), new int3(8, 15, 11), Mat.Stone);
                brush.Box(new int3(lanternX + half - 1, lanternY + 56,
                                   lanternZ + z), new int3(8, 15, 11), Mat.Stone);
            }
            brush.Box(new int3(lanternX - 1, lanternY + 70, lanternZ - 1),
                      new int3(3, 30, 3), Mat.Gold);
            brush.Box(new int3(lanternX + 2, lanternY + 86, lanternZ - 1),
                      new int3(24, 11, 3), Mat.Cloth);
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
                      new int3(3, 24, 20), Mat.LitWindow);
            // Stone tracery divides the former single glowing rectangle into a cross and four
            // warm panes. At room scale this reads as a leaded rose window rather than a lamp.
            brush.Box(new int3(min.x + 1, baseY + 35, centreZ - 2),
                      new int3(5, 24, 4), Mat.DarkStone);
            brush.Box(new int3(min.x + 1, baseY + 45, centreZ - 10),
                      new int3(5, 4, 20), Mat.DarkStone);
            for (int side = -1; side <= 1; side += 2)
            {
                int z = centreZ + side * 34;
                brush.Arch(new int3(min.x + width / 2 - 7, baseY + 20, z - 6),
                           14, 38, 7, 2, Mat.Empty);
                brush.Box(new int3(min.x + width / 2 - 4, baseY + 25, z - 4),
                          new int3(8, 26, 2), Mat.LitWindow);
            }

            // A layered sanctuary replaces the old flat red block: two stone steps, a timber
            // altar table, a triptych reredos, canopy, columns, cross, and clustered candles.
            // It ends before x=min+31, preserving the full 1.6 m processional aisle contract.
            brush.Box(new int3(min.x + 7, baseY + 1, centreZ - 27),
                      new int3(21, 2, 54), Mat.DarkStone);
            brush.Box(new int3(min.x + 9, baseY + 3, centreZ - 24),
                      new int3(17, 2, 48), Mat.Stone);
            brush.Box(new int3(min.x + 19, baseY + 7, centreZ - 21),
                      new int3(8, 5, 42), Mat.Wood);
            brush.Box(new int3(min.x + 17, baseY + 5, centreZ - 24),
                      new int3(3, 9, 4), Mat.Wood);
            brush.Box(new int3(min.x + 17, baseY + 5, centreZ + 20),
                      new int3(3, 9, 4), Mat.Wood);

            for (int panel = -1; panel <= 1; panel++)
            {
                int panelWidth = panel == 0 ? 15 : 11;
                int panelZ = centreZ + panel * 17 - panelWidth / 2;
                brush.Box(new int3(min.x + 7, baseY + 12, panelZ),
                          new int3(3, panel == 0 ? 28 : 23, panelWidth), Mat.Cloth);
                brush.Box(new int3(min.x + 6, baseY + 10, panelZ - 2),
                          new int3(2, 3, panelWidth + 4), Mat.Gold);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                int columnZ = centreZ + side * 25 - 4;
                brush.Box(new int3(min.x + 6, baseY + 7, columnZ),
                          new int3(8, 36, 8), Mat.DarkStone);
                brush.Box(new int3(min.x + 4, baseY + 40, columnZ - 2),
                          new int3(12, 6, 12), Mat.Stone);
            }
            brush.Box(new int3(min.x + 5, baseY + 43, centreZ - 31),
                      new int3(11, 6, 62), Mat.DarkStone);
            brush.Box(new int3(min.x + 11, baseY + 20, centreZ - 2),
                      new int3(10, 4, 4), Mat.Gold);
            brush.Box(new int3(min.x + 14, baseY + 14, centreZ - 2),
                      new int3(4, 17, 4), Mat.Gold);

            for (int candle = -2; candle <= 2; candle++)
            {
                int candleZ = centreZ + candle * 7;
                brush.Box(new int3(min.x + 20, baseY + 12, candleZ - 1),
                          new int3(2, 5 + (candle & 1), 2), Mat.Glass);
                brush.Box(new int3(min.x + 19, baseY + 11, candleZ - 2),
                          new int3(4, 2, 4), Mat.Gold);
            }

            for (int row = 0; row < 3; row++)
            for (int side = -1; side <= 1; side += 2)
            {
                int x = min.x + 34 + row * 15;
                int z = centreZ + side * 17;
                brush.Box(new int3(x, baseY + 2, z - 10),
                          new int3(7, 6, 20), Mat.Wood);
                brush.Box(new int3(x + 5, baseY + 7, z - 10),
                          new int3(3, 10, 20), Mat.Wood);
                brush.Box(new int3(x + 1, baseY + 9, z - 8),
                          new int3(4, 2, 16), row == 0 ? Mat.Gold : Mat.Wood);
            }

            // Hammer-beam tie bars and mirrored stepped braces shape a legible timber vault
            // beneath the tall roof instead of leaving one featureless flat ceiling.
            for (int x = min.x + 24; x < min.x + width - 5; x += 24)
            {
                brush.Box(new int3(x, baseY + 49, min.z + 7),
                          new int3(4, 4, depth - 14), Mat.Wood);
                for (int step = 0; step < 12; step++)
                {
                    int braceY = baseY + 50 + step * 2;
                    int southZ = min.z + 8 + step * 3;
                    int northZ = min.z + depth - 12 - step * 3;
                    brush.Box(new int3(x, braceY, southZ),
                              new int3(4, 3, 5), Mat.Wood);
                    brush.Box(new int3(x, braceY, northZ),
                              new int3(4, 3, 5), Mat.Wood);
                }
            }

            // Two properly scaled chandeliers recede down the centre line. Cross-arms, chains,
            // and four small lamps read as fixtures; the former 70 cm glowing cubes did not.
            int[] chandelierX = { min.x + 30, min.x + 52 };
            for (int i = 0; i < chandelierX.Length; i++)
            {
                int cx = chandelierX[i];
                int fixtureY = baseY + 39 + i * 2;
                brush.Box(new int3(cx - 1, fixtureY + 3, centreZ - 1),
                          new int3(2, 26 - i * 2, 2), Mat.Gold);
                brush.Box(new int3(cx - 10, fixtureY, centreZ - 1),
                          new int3(20, 3, 2), Mat.Gold);
                brush.Box(new int3(cx - 1, fixtureY, centreZ - 10),
                          new int3(2, 3, 20), Mat.Gold);
                int2[] lamps = { new(-9, 0), new(8, 0), new(0, -9), new(0, 8) };
                for (int lamp = 0; lamp < lamps.Length; lamp++)
                    brush.Box(new int3(cx + lamps[lamp].x - 1, fixtureY - 3,
                                       centreZ + lamps[lamp].y - 1),
                              new int3(3, 5, 3), Mat.Glass);
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

            ChapelBellTower(ref brush, in plan, baseY);
        }

        /// <summary>
        /// An occupied bell/solar tower behind the chapel. Its offset mass breaks the keep's
        /// bilateral silhouette in the same way as the accumulated square towers in the
        /// reference. Every storey is real interior space reached from the chapel by a spiral
        /// stair; the upper volume is therefore neither facade dressing nor a sealed prop.
        /// </summary>
        private static void ChapelBellTower(ref VoxelBrush brush, in CastlePlan plan, int baseY)
        {
            const int size = CastleLayout.ChapelBellTowerSize;
            int height = plan.FloorHeight * 4;
            int3 centre = CastleLayout.ChapelBellTowerCentre(in plan);
            var min = new int3(centre.x - size / 2, baseY, centre.z - size / 2);

            brush.Box(new int3(min.x - 5, baseY - 16, min.z - 5),
                      new int3(size + 10, 20, size + 10), Mat.DarkStone);
            brush.HollowBox(min, new int3(size, height, size), 6,
                            Mat.Stone, false, false);
            brush.FillBulk(new int3(min.x + 6, baseY + 1, min.z + 6),
                           new int3(size - 12, height - 1, size - 12), Mat.Empty);

            // Four stacked occupied chambers, with the stair authored after the slabs so it
            // carves consistent headroom through each landing.
            for (int floor = 1; floor < 4; floor++)
            {
                int floorY = baseY + floor * plan.FloorHeight;
                brush.Box(new int3(min.x + 6, floorY, min.z + 6),
                          new int3(size - 12, 3, size - 12), Mat.Wood);
            }

            int stairX = min.x + size - 19;
            int stairZ = min.z + size / 2;
            brush.SpiralStair(stairX, baseY + 2, stairZ,
                              CastleLayout.ChapelBellTowerStairRadius, height - 4, Mat.Stone);

            // The chapel-to-tower threshold lies on the chapel's rear wall. Restore a solid
            // landing, then clear an actor-sized core across both overlapping shells.
            int connectorX = centre.x;
            int keepDepth = plan.KeepHalfZ * 2;
            int chapelDepth = math.max(96, keepDepth * 3 / 5);
            int chapelCentreZ = min.z + 6 - chapelDepth / 2;
            int aisleStartZ = chapelCentreZ - 6;
            brush.Box(new int3(connectorX - 8, baseY, aisleStartZ),
                      new int3(16, 2, min.z + 12 - aisleStartZ), Mat.Stone);
            brush.Arch(new int3(connectorX - 9, baseY + 2, min.z - 9),
                       18, 32, 18, 2, Mat.Empty);
            brush.Box(new int3(connectorX - 7, baseY + 2, aisleStartZ),
                      new int3(14, 24, min.z + 12 - aisleStartZ), Mat.Empty);

            // Recessed windows on three exposed faces advertise all four usable storeys. Warm
            // glazing and deep dark-stone hoods keep the openings legible in the long hero shot.
            for (int floor = 0; floor < 4; floor++)
            {
                int windowY = baseY + floor * plan.FloorHeight + 12;
                int windowHeight = plan.FloorHeight - 18;

                brush.Arch(new int3(min.x - 2, windowY, centre.z - 7),
                           14, windowHeight, 10, 0, Mat.Empty);
                brush.Box(new int3(min.x + 2, windowY + 4, centre.z - 4),
                          new int3(2, windowHeight - 9, 8), Mat.LitWindow);
                brush.Box(new int3(min.x - 4, windowY - 3, centre.z - 11),
                          new int3(5, 3, 22), Mat.DarkStone);

                for (int side = -1; side <= 1; side += 2)
                {
                    // The ground-floor south bay is the chapel doorway. A glazed window in the
                    // same bay looked plausible from outside but silently sealed the route.
                    if (floor == 0 && side < 0) continue;

                    int z = side < 0 ? min.z - 2 : min.z + size - 8;
                    brush.Arch(new int3(centre.x - 7, windowY, z),
                               14, windowHeight, 10, 2, Mat.Empty);
                    int glassZ = side < 0 ? min.z + 2 : min.z + size - 4;
                    brush.Box(new int3(centre.x - 4, windowY + 4, glassZ),
                              new int3(8, windowHeight - 9, 2), Mat.LitWindow);
                }
            }

            // Compact furnishing leaves the eastern half and the full stair ring clear. Each
            // level has a distinct use: sacristy storage, scriptorium desk, solar bench, bells.
            for (int floor = 0; floor < 3; floor++)
            {
                int floorY = baseY + floor * plan.FloorHeight;
                brush.Box(new int3(min.x + 8, floorY + 3, min.z + 9),
                          new int3(10, 24, 18), Mat.Wood);
                brush.Box(new int3(min.x + 19, floorY + 8, min.z + 11),
                          new int3(15, 4, 12), Mat.Wood);
                brush.Box(new int3(min.x + 21, floorY + 3, min.z + 13),
                          new int3(4, 6, 4), Mat.Wood);
                brush.Box(new int3(min.x + 28, floorY + 12, min.z + 14),
                          new int3(3, 7, 3), floor == 2 ? Mat.Glass : Mat.Gold);
            }

            int bellY = baseY + plan.FloorHeight * 3 + 14;
            brush.Box(new int3(min.x + 9, bellY - 8, centre.z - 2),
                      new int3(size - 31, 4, 4), Mat.Wood);
            for (int i = 0; i < 2; i++)
            {
                int bellX = min.x + 17 + i * 16;
                brush.Box(new int3(bellX, bellY, centre.z - 5),
                          new int3(9, 10, 10), Mat.Gold);
                brush.Box(new int3(bellX + 3, bellY + 10, centre.z - 2),
                          new int3(3, 10, 3), Mat.Wood);
            }

            // A projecting crown and steep slate roof add a second square-tower profile to the
            // skyline without inventing another inaccessible rooftop chamber.
            int topY = baseY + height;
            brush.Box(new int3(min.x - 5, topY, min.z - 5),
                      new int3(size + 10, 7, size + 10), Mat.DarkStone);
            for (int x = min.x - 4; x < min.x + size + 2; x += 18)
            {
                brush.Box(new int3(x, topY + 7, min.z - 4),
                          new int3(11, 15, 8), Mat.Stone);
                brush.Box(new int3(x, topY + 7, min.z + size - 4),
                          new int3(11, 15, 8), Mat.Stone);
            }
            brush.Gable(new int3(min.x + 2, topY + 10, min.z + 2),
                        new int3(size - 4, 46, size - 4), true, Mat.Slate);
            brush.Box(new int3(centre.x - 1, topY + 53, centre.z - 1),
                      new int3(3, 25, 3), Mat.Gold);
            brush.Box(new int3(centre.x + 2, topY + 66, centre.z - 1),
                      new int3(20, 9, 3), Mat.Cloth);
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
                          new int3(2, 18, 10), Mat.LitWindow);
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
                case 0: // great hall: constructed table, benches, hearth, throne and chandelier
                    // Exposed transverse roof beams establish the new hall height and break the
                    // broad timber ceiling into bays. They remain well above player headroom and
                    // the grand-stair opening.
                    for (int beamZ = min.z + 22; beamZ < min.z + size.z - 18; beamZ += 34)
                        brush.Box(new int3(min.x + 9, y + plan.FloorHeight - 8, beamZ),
                                  new int3(size.x - 18, 5, 5), Mat.Wood);

                    // Tabletop and four trestle legs. The former solid 8-voxel-high block read as
                    // a plinth; negative space under furniture is what makes its scale legible.
                    brush.Box(new int3(cx - 42, y + 8, cz - 9), new int3(84, 3, 18), Mat.Wood);
                    for (int sideX = -1; sideX <= 1; sideX += 2)
                    for (int sideZ = -1; sideZ <= 1; sideZ += 2)
                        brush.Box(new int3(cx + sideX * 34 - 3, y + 1, cz + sideZ * 6 - 3),
                                  new int3(6, 7, 6), Mat.Wood);

                    // Benches with seats and sparse legs instead of solid rails.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int benchZ = cz + side * 18;
                        brush.Box(new int3(cx - 44, y + 5, benchZ - 3),
                                  new int3(88, 3, 6), Mat.Wood);
                        for (int leg = -1; leg <= 1; leg += 2)
                            brush.Box(new int3(cx + leg * 34 - 2, y + 1, benchZ - 2),
                                      new int3(4, 4, 4), Mat.Wood);
                    }

                    brush.Box(new int3(min.x + inner, y + 1, cz - 24), new int3(10, 40, 48), Mat.DarkStone);
                    brush.Box(new int3(min.x + inner + 2, y + 3, cz - 14), new int3(6, 16, 28), Mat.Empty);
                    brush.Box(new int3(min.x + inner + 5, y + 3, cz - 10),
                              new int3(4, 6, 20), Mat.Gold);

                    // Raised high seat at the rear of the hall, clear of the entrance/stair axis.
                    brush.Box(new int3(cx + 57, y + 1, cz - 18),
                              new int3(14, 4, 36), Mat.DarkStone);
                    brush.Box(new int3(cx + 60, y + 5, cz - 9),
                              new int3(8, 11, 18), Mat.Wood);
                    brush.Box(new int3(cx + 62, y + 10, cz - 7),
                              new int3(5, 10, 14), Mat.Cloth);

                    // Compact chandelier above actor headroom, with four warm candles.
                    brush.Box(new int3(cx - 1, y + 33, cz - 1), new int3(2, 9, 2), Mat.Gold);
                    brush.Box(new int3(cx - 13, y + 30, cz - 1), new int3(26, 3, 2), Mat.Wood);
                    brush.Box(new int3(cx - 1, y + 30, cz - 13), new int3(2, 3, 26), Mat.Wood);
                    int2[] candleOffsets = { new(-12, 0), new(12, 0), new(0, -12), new(0, 12) };
                    foreach (int2 candle in candleOffsets)
                    {
                        brush.Box(new int3(cx + candle.x - 2, y + 27, cz + candle.y - 2),
                                  new int3(4, 6, 4), Mat.Glass);
                        brush.Box(new int3(cx + candle.x - 1, y + 26, cz + candle.y - 1),
                                  new int3(2, 2, 2), Mat.Gold);
                    }

                    // Heraldic hangings and table settings supply the warm colour rhythm and
                    // small-scale occupation visible in the reference without narrowing aisles.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int hangingX = cx + side * 48;
                        brush.Box(new int3(hangingX - 10, y + 13,
                                           min.z + size.z - inner - 1),
                                  new int3(20, 25, 2), Mat.Cloth);
                        brush.Box(new int3(hangingX - 13, y + 36,
                                           min.z + size.z - inner - 2),
                                  new int3(26, 3, 3), Mat.Gold);
                    }
                    for (int setting = -3; setting <= 3; setting++)
                    {
                        int settingX = cx + setting * 11;
                        brush.Disc(settingX, y + 11, cz, 3, Mat.Gold);
                        if ((setting & 1) == 0)
                            brush.Box(new int3(settingX - 1, y + 12, cz - 1),
                                      new int3(2, 5, 2), Mat.Glass);
                    }

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

                case 1: // bedchamber: framed bed, mattress, canopy, chest, wardrobe and rug
                    int bedX = cx + 24;
                    int bedZ = cz - 23;

                    // Shallow ceiling bays give the large chamber the timber rhythm of the
                    // reference without lowering the 4.6 m clear room volume.
                    for (int beamZ = min.z + 22; beamZ < min.z + size.z - 18; beamZ += 36)
                        brush.Box(new int3(min.x + 9, y + plan.FloorHeight - 7, beamZ),
                                  new int3(size.x - 18, 4, 4), Mat.Wood);

                    for (int bxSide = 0; bxSide <= 1; bxSide++)
                    for (int bzSide = 0; bzSide <= 1; bzSide++)
                        brush.Box(new int3(bedX + bxSide * 22, y + 3, bedZ + bzSide * 39),
                                  new int3(4, 7, 4), Mat.Wood);
                    brush.Box(new int3(bedX, y + 8, bedZ), new int3(26, 3, 43), Mat.Wood);
                    brush.Box(new int3(bedX + 2, y + 11, bedZ + 2), new int3(22, 4, 39), Mat.Cloth);
                    brush.Box(new int3(bedX, y + 3, bedZ + 39), new int3(26, 22, 4), Mat.Wood);
                    brush.Box(new int3(bedX + 3, y + 16, bedZ + 40), new int3(20, 7, 2), Mat.Cloth);

                    // Two canopy posts and a cloth valance suggest luxury without roofing over
                    // the entire bed and turning it into another solid volume.
                    brush.Box(new int3(bedX, y + 11, bedZ), new int3(3, 19, 3), Mat.Wood);
                    brush.Box(new int3(bedX + 23, y + 11, bedZ), new int3(3, 19, 3), Mat.Wood);
                    brush.Box(new int3(bedX, y + 11, bedZ + 39), new int3(3, 19, 3), Mat.Wood);
                    brush.Box(new int3(bedX + 23, y + 11, bedZ + 39), new int3(3, 19, 3), Mat.Wood);
                    brush.Box(new int3(bedX, y + 27, bedZ), new int3(26, 3, 5), Mat.Cloth);
                    brush.Box(new int3(bedX, y + 27, bedZ + 37), new int3(26, 3, 5), Mat.Cloth);
                    brush.Box(new int3(bedX, y + 28, bedZ + 3), new int3(3, 2, 34), Mat.Wood);
                    brush.Box(new int3(bedX + 23, y + 28, bedZ + 3), new int3(3, 2, 34), Mat.Wood);

                    // Bedside chests and warm lamps establish a useful scale hierarchy around
                    // the canopy rather than leaving it isolated in an empty floor plate.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int tableX = side < 0 ? bedX - 14 : bedX + 31;
                        brush.Box(new int3(tableX, y + 3, bedZ + 4),
                                  new int3(9, 8, 11), Mat.Wood);
                        brush.Box(new int3(tableX + 3, y + 11, bedZ + 7),
                                  new int3(3, 6, 4), Mat.Glass);
                        brush.Box(new int3(tableX + 4, y + 10, bedZ + 8),
                                  new int3(2, 2, 2), Mat.Gold);
                    }

                    brush.Box(new int3(cx - 42, y + 3, cz + 24), new int3(22, 11, 15), Mat.Wood);
                    brush.Box(new int3(cx - 43, y + 13, cz + 23), new int3(24, 3, 17), Mat.Gold);
                    brush.Box(new int3(min.x + size.x - inner - 26, y + 3, min.z + inner + 12),
                              new int3(18, 28, 22), Mat.Wood);
                    brush.Box(new int3(cx - 32, y + 3, cz - 26),
                              new int3(48, 1, 52), Mat.Cloth);

                    // Fireplace, mantle, and paired wall hangings occupy the perimeter and leave
                    // the tested bed-to-door route untouched.
                    brush.Box(new int3(min.x + inner, y + 3, cz + 25),
                              new int3(9, 28, 36), Mat.DarkStone);
                    brush.Arch(new int3(min.x + inner + 1, y + 5, cz + 33),
                               20, 17, 8, 0, Mat.Empty);
                    brush.Box(new int3(min.x + inner + 4, y + 5, cz + 37),
                              new int3(4, 7, 12), Mat.Gold);
                    brush.Box(new int3(min.x + inner - 2, y + 29, cz + 22),
                              new int3(13, 4, 42), Mat.Wood);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int hangingZ = cz + side * 48;
                        brush.Box(new int3(min.x + size.x - inner - 2, y + 15,
                                           hangingZ - 10),
                                  new int3(2, 24, 20), Mat.Cloth);
                        brush.Box(new int3(min.x + size.x - inner - 3, y + 37,
                                           hangingZ - 13),
                                  new int3(3, 3, 26), Mat.Gold);
                    }

                    // Two backed chairs and a low drinks table make the hearth a second focal
                    // group. They remain against the west wall, outside the central bed-door and
                    // spiral-stair circulation lanes.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int chairZ = cz + 25 + side * 18;
                        int chairX = min.x + inner + 31;
                        brush.Box(new int3(chairX, y + 4, chairZ - 5),
                                  new int3(10, 4, 10), Mat.Wood);
                        brush.Box(new int3(chairX, y + 8, chairZ - 5),
                                  new int3(4, 13, 10), Mat.Wood);
                        brush.Box(new int3(chairX + 2, y + 8, chairZ - 4),
                                  new int3(7, 3, 8), Mat.Cloth);
                    }
                    brush.Cylinder(min.x + inner + 48, y + 3, cz + 25,
                                   7, 7, Mat.Wood);
                    brush.Disc(min.x + inner + 48, y + 10, cz + 25, 9, Mat.Gold);

                    // A compact six-light chandelier hangs over the rug rather than the canopy,
                    // balancing the warm bedside lamps and fireplace.
                    int bedLampX = cx - 18;
                    brush.Box(new int3(bedLampX - 1, y + 32, cz - 1),
                              new int3(2, 10, 2), Mat.Gold);
                    brush.Box(new int3(bedLampX - 12, y + 30, cz - 1),
                              new int3(24, 2, 2), Mat.Wood);
                    brush.Box(new int3(bedLampX - 1, y + 30, cz - 10),
                              new int3(2, 2, 20), Mat.Wood);
                    int2[] bedroomCandles =
                    {
                        new(-10, 0), new(10, 0), new(-5, -8), new(5, -8),
                        new(-5, 8), new(5, 8),
                    };
                    foreach (int2 candle in bedroomCandles)
                        brush.Box(new int3(bedLampX + candle.x - 2, y + 27,
                                           cz + candle.y - 2),
                                  new int3(4, 6, 4), Mat.Glass);
                    break;

                default: // library / stores: shelves against the walls
                    for (int i = 0; i < 4; i++)
                    {
                        int shelfZ = min.z + inner + 10 + i * 34;
                        brush.Box(new int3(min.x + inner + 4, y + 3, shelfZ),
                                  new int3(14, 34, 24), Mat.Wood);
                        brush.Box(new int3(min.x + size.x - inner - 18, y + 3, shelfZ),
                                  new int3(14, 34, 24), Mat.Wood);
                        for (int shelf = 0; shelf < 3; shelf++)
                        {
                            // Individual varied spines read as books at player scale; the former
                            // full-width red/gold bands looked like painted cabinet drawers.
                            for (int book = 0; book < 6; book++)
                            {
                                byte books = (book + i + shelf) % 3 == 0
                                    ? Mat.Gold : Mat.Cloth;
                                int bookHeight = 4 + ((book * 3 + i + shelf) % 4);
                                int bookZ = shelfZ + 2 + book * 3;
                                brush.Box(new int3(min.x + inner + 17,
                                                   y + 8 + shelf * 9, bookZ),
                                          new int3(3, bookHeight, 2), books);
                                brush.Box(new int3(min.x + size.x - inner - 20,
                                                   y + 8 + shelf * 9, bookZ),
                                          new int3(3, bookHeight, 2), books);
                            }
                        }
                    }

                    // Separate reading desks in the two partitioned halves, offset from the
                    // centre doorway and spiral landing.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int deskZ = cz + side * 43;
                        brush.Box(new int3(cx - 22, y + 10, deskZ - 10),
                                  new int3(44, 3, 20), Mat.Wood);
                        brush.Box(new int3(cx - 18, y + 3, deskZ - 7),
                                  new int3(5, 7, 5), Mat.Wood);
                        brush.Box(new int3(cx + 13, y + 3, deskZ + 2),
                                  new int3(5, 7, 5), Mat.Wood);
                        brush.Box(new int3(cx - 2, y + 13, deskZ - 1),
                                  new int3(4, 3, 3), Mat.Glass);

                        // Uneven stacks keep the broad desk surfaces from reading as empty slabs.
                        for (int book = 0; book < 3; book++)
                            brush.Box(new int3(cx - 15 + book * 8, y + 13 + book,
                                               deskZ + 4),
                                      new int3(7, 1, 9),
                                      (book & 1) == 0 ? Mat.Cloth : Mat.Gold);

                        // A backed reading chair remains offset from the centre doorway.
                        brush.Box(new int3(cx + 25, y + 4, deskZ - 4),
                                  new int3(9, 4, 9), Mat.Wood);
                        brush.Box(new int3(cx + 31, y + 8, deskZ - 4),
                                  new int3(3, 12, 9), Mat.Wood);
                    }

                    brush.Box(new int3(cx - 42, y + 3, cz - 31),
                              new int3(84, 1, 62), Mat.Cloth);
                    for (int beamZ = min.z + 24; beamZ < min.z + size.z - 20; beamZ += 38)
                        brush.Box(new int3(min.x + 9, y + plan.FloorHeight - 7, beamZ),
                                  new int3(size.x - 18, 4, 4), Mat.Wood);

                    // One fixture per partitioned reading room. The former central chandelier
                    // was embedded in the dividing wall and left both desks at the edge of its
                    // light pool.
                    for (int roomSide = -1; roomSide <= 1; roomSide += 2)
                    {
                        int lampZ = cz + roomSide * 42;
                        brush.Box(new int3(cx - 1, y + 31, lampZ - 1),
                                  new int3(2, 10, 2), Mat.Gold);
                        brush.Box(new int3(cx - 10, y + 29, lampZ - 1),
                                  new int3(20, 2, 2), Mat.Wood);
                        brush.Box(new int3(cx - 1, y + 29, lampZ - 10),
                                  new int3(2, 2, 20), Mat.Wood);
                        int2[] libraryCandles =
                        {
                            new(-9, 0), new(9, 0), new(0, -9), new(0, 9),
                        };
                        foreach (int2 candle in libraryCandles)
                            brush.Box(new int3(cx + candle.x - 2, y + 25,
                                               lampZ + candle.y - 2),
                                      new int3(4, 6, 4), Mat.Glass);

                        // Paired wall sconces extend the light rhythm down both shelf aisles.
                        brush.Box(new int3(min.x + inner + 17, y + 18, lampZ - 2),
                                  new int3(4, 7, 4), Mat.Glass);
                        brush.Box(new int3(min.x + size.x - inner - 21, y + 18, lampZ - 2),
                                  new int3(4, 7, 4), Mat.Glass);
                    }
                    break;
            }

            // A little clutter, so no two rooms are identical.
            for (int i = 0; i < rng.NextInt(2, 5); i++)
            {
                bool leftWall = rng.NextBool();
                int px = leftWall ? min.x + inner + 22 : min.x + size.x - inner - 30;
                int pz = rng.NextInt(min.z + inner + 8, min.z + size.z - inner - 12);
                int radius = rng.NextInt(4, 7);
                brush.Cylinder(px, y + 3, pz, radius, rng.NextInt(8, 14), Mat.Wood);
                brush.Box(new int3(px - radius, y + 7, pz - radius - 1),
                          new int3(radius * 2, 2, radius * 2 + 2), Mat.Gold);
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

                // Individual archive spines sit on the aisle-facing shelf planes. Solid wooden
                // blocks alone read as crates; irregular red/gold volumes establish scale and
                // make the secret room's purpose legible from the stair landing.
                for (int shelf = 0; shelf < 3; shelf++)
                for (int book = 0; book < 5; book++)
                {
                    int bookZ = z + 2 + book * 3;
                    int bookY = cellarY + 5 + shelf * 8;
                    int bookHeight = 4 + ((book + shelf * 2 + z) & 3);
                    byte bookMaterial = ((book + shelf) & 2) == 0 ? Mat.Cloth : Mat.Gold;
                    brush.Box(new int3(keepMin.x + 25, bookY, bookZ),
                              new int3(3, bookHeight, 2), bookMaterial);
                    brush.Box(new int3(keepMin.x + hx * 2 - 28, bookY, bookZ),
                              new int3(3, bookHeight, 2), bookMaterial);
                }
            }

            // Timber ceiling bays and a floor runner make the long cellar read as one authored
            // archive while keeping every addition above or flush with the central route.
            for (int beamZ = keepMin.z + 18; beamZ < keepMin.z + hz * 2 - 20; beamZ += 38)
                brush.Box(new int3(keepMin.x + 10, cellarY + 34, beamZ),
                          new int3(hx * 2 - 20, 4, 4), Mat.Wood);
            brush.Box(new int3(plan.Centre.x - 12, cellarY, keepMin.z + 18),
                      new int3(24, 1, hz * 2 - 42), Mat.Cloth);

            // Offset reading desk and chair: close enough to provide a focal group, but west of
            // the north-south stair aisle that connects both spiral landings.
            int archiveDeskX = plan.Centre.x - 55;
            int archiveDeskZ = keepMin.z + hz;
            brush.Box(new int3(archiveDeskX - 18, cellarY + 8, archiveDeskZ - 10),
                      new int3(36, 3, 20), Mat.Wood);
            brush.Box(new int3(archiveDeskX - 14, cellarY + 1, archiveDeskZ - 7),
                      new int3(5, 7, 5), Mat.Wood);
            brush.Box(new int3(archiveDeskX + 9, cellarY + 1, archiveDeskZ + 2),
                      new int3(5, 7, 5), Mat.Wood);
            for (int folio = 0; folio < 3; folio++)
                brush.Box(new int3(archiveDeskX - 10 + folio * 8,
                                   cellarY + 11 + folio, archiveDeskZ - 4),
                          new int3(7, 1, 10), folio == 1 ? Mat.Gold : Mat.Cloth);
            brush.Box(new int3(archiveDeskX + 23, cellarY + 4, archiveDeskZ - 5),
                      new int3(9, 4, 10), Mat.Wood);
            brush.Box(new int3(archiveDeskX + 29, cellarY + 8, archiveDeskZ - 5),
                      new int3(3, 12, 10), Mat.Wood);

            // The local-light probes already live near these fixtures; visible sconces now
            // explain the warm pools instead of leaving the ceiling apparently self-lit.
            for (int side = -1; side <= 1; side += 2)
            {
                int lampX = plan.Centre.x + side * 55;
                brush.Box(new int3(lampX - 2, cellarY + 17, keepMin.z + hz - 2),
                          new int3(4, 8, 4), Mat.Glass);
                brush.Box(new int3(lampX - 3, cellarY + 14, keepMin.z + hz - 1),
                          new int3(6, 3, 3), Mat.Gold);
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
            int3 trapdoor = CastleLayout.TrapdoorCentre(in plan);
            int tx = trapdoor.x, tz = trapdoor.z;
            brush.Box(new int3(tx - 10, cellarY + 40, tz - 10), new int3(20, 8, 20), Mat.Empty);
            brush.SpiralStair(tx, cellarY, tz, 9, 46, Mat.Stone);

            // The stair is complete beneath a real hatch. Runtime interaction removes this
            // exact timber lid; keeping the opening closed during construction makes the secret
            // route discoverable rather than presenting the cellar as an accidental floor hole.
            brush.Box(new int3(tx - CastleLayout.TrapdoorHalfSize, baseY, tz - CastleLayout.TrapdoorHalfSize),
                      new int3(CastleLayout.TrapdoorHalfSize * 2, 2, CastleLayout.TrapdoorHalfSize * 2), Mat.Wood);
            brush.Box(new int3(tx - CastleLayout.TrapdoorHalfSize, baseY + 2, tz - CastleLayout.TrapdoorHalfSize),
                      new int3(3, 2, CastleLayout.TrapdoorHalfSize * 2), Mat.Gold);
            brush.Box(new int3(tx + CastleLayout.TrapdoorHalfSize - 3, baseY + 2, tz - CastleLayout.TrapdoorHalfSize),
                      new int3(3, 2, CastleLayout.TrapdoorHalfSize * 2), Mat.Gold);

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

            DungeonSideChambers(ref brush, tx, tz, dungeonY);

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

        /// <summary>
        /// Distinct puzzle and treasury beats branching from the ruined hall. Both corridors are
        /// straight, level, and player-sized; the chambers use carved rock as their envelope so
        /// they feel excavated beneath the castle rather than like more rooms in the keep.
        /// </summary>
        private static void DungeonSideChambers(ref VoxelBrush brush, int tx, int trapZ,
                                                int dungeonY)
        {
            const int corridorHalf = 10;
            const int corridorHeight = 30;

            // East branch: puzzle room.
            int puzzleMinX = tx + 176;
            int puzzleMinZ = trapZ - 58;
            brush.Box(new int3(tx + 118, dungeonY + 2, trapZ - corridorHalf),
                      new int3(70, corridorHeight, corridorHalf * 2), Mat.Empty);
            brush.Box(new int3(tx + 118, dungeonY, trapZ - corridorHalf),
                      new int3(70, 2, corridorHalf * 2), Mat.DarkStone);
            brush.FillBulk(new int3(puzzleMinX, dungeonY + 2, puzzleMinZ),
                           new int3(100, 38, 116), Mat.Empty);
            brush.Box(new int3(puzzleMinX, dungeonY, puzzleMinZ),
                      new int3(100, 2, 116), Mat.DarkStone);

            // Inlaid floor graph: four arms converge on the centre tile, making the chamber read
            // as a designed puzzle space from the doorway without raising the walking surface.
            brush.Box(new int3(puzzleMinX + 8, dungeonY + 1, trapZ - 2),
                      new int3(84, 1, 4), Mat.Slate);
            brush.Box(new int3(puzzleMinX + 48, dungeonY + 1, puzzleMinZ + 8),
                      new int3(4, 1, 100), Mat.Slate);
            for (int ring = 0; ring < 3; ring++)
            {
                int inset = 18 + ring * 10;
                brush.Box(new int3(puzzleMinX + inset, dungeonY + 1, puzzleMinZ + 15),
                          new int3(2, 1, 86), ring == 1 ? Mat.Gold : Mat.Cloth);
                brush.Box(new int3(puzzleMinX + 98 - inset, dungeonY + 1, puzzleMinZ + 15),
                          new int3(2, 1, 86), ring == 1 ? Mat.Gold : Mat.Cloth);
            }

            // Four rune plinths surround a clear centre tile; their varied materials make the
            // interaction grammar legible even before puzzle behaviour exists.
            int puzzleCx = puzzleMinX + 50;
            int puzzleCz = trapZ;
            int2[] runeOffsets = { new(-26, -30), new(26, -30), new(-26, 30), new(26, 30) };
            for (int i = 0; i < runeOffsets.Length; i++)
            {
                int px = puzzleCx + runeOffsets[i].x;
                int pz = puzzleCz + runeOffsets[i].y;
                brush.Box(new int3(px - 8, dungeonY + 2, pz - 8),
                          new int3(16, 8, 16), Mat.Stone);
                brush.Disc(px, dungeonY + 10, pz, 6, Mat.DarkStone);
                brush.Cone(px, dungeonY + 11, pz, 3 + (i & 1),
                           8 + i * 2, i % 2 == 0 ? Mat.Glass : Mat.Gold);
                brush.Cone(px + (i < 2 ? 5 : -5), dungeonY + 11, pz + 4,
                           2, 6 + (i & 1) * 2, Mat.Glass);
            }
            brush.Box(new int3(puzzleCx - 14, dungeonY + 2, puzzleCz - 14),
                      new int3(28, 3, 28), Mat.Slate);
            brush.Disc(puzzleCx, dungeonY + 5, puzzleCz, 8, Mat.DarkStone);
            brush.Cone(puzzleCx, dungeonY + 6, puzzleCz, 4, 10, Mat.Glass);
            brush.Cone(puzzleCx - 6, dungeonY + 6, puzzleCz + 4, 2, 7, Mat.Gold);

            // A broken rune shrine closes the long eastward sightline without occupying the
            // centre puzzle tile. It gives the room a reward-facing direction like the reference
            // puzzle chamber rather than ending in unlit carved rock.
            int shrineX = puzzleMinX + 91;
            brush.Box(new int3(shrineX - 5, dungeonY + 2, puzzleCz - 28),
                      new int3(7, 30, 7), Mat.Stone);
            brush.Box(new int3(shrineX - 5, dungeonY + 2, puzzleCz + 21),
                      new int3(7, 30, 7), Mat.Stone);
            brush.Box(new int3(shrineX - 6, dungeonY + 28, puzzleCz - 28),
                      new int3(8, 6, 56), Mat.DarkStone);
            brush.Box(new int3(shrineX - 10, dungeonY + 3, puzzleCz - 12),
                      new int3(10, 6, 24), Mat.DarkStone);
            brush.Cone(shrineX - 7, dungeonY + 9, puzzleCz, 4, 16, Mat.Glass);

            // Broken perimeter arches imply a much older buried hall without closing the central
            // route from its corridor to the puzzle dais.
            for (int arch = 0; arch < 2; arch++)
            {
                int z = puzzleMinZ + 16 + arch * 84;
                brush.Cylinder(puzzleMinX + 12, dungeonY + 2, z, 7, 31, Mat.Stone);
                brush.Cylinder(puzzleMinX + 88, dungeonY + 2, z, 7, 31, Mat.Stone);
            }
            for (int x = puzzleMinX + 15; x < puzzleMinX + 92; x += 25)
                brush.Box(new int3(x, dungeonY + 32, puzzleMinZ + 5),
                          new int3(4, 4, 106), Mat.Wood);
            for (int side = -1; side <= 1; side += 2)
            {
                brush.Box(new int3(puzzleMinX + 50 - 2, dungeonY + 18,
                                   trapZ + side * 49 - 2),
                          new int3(4, 8, 4), Mat.Glass);
                brush.Box(new int3(puzzleMinX + 48, dungeonY + 15,
                                   trapZ + side * 49 - 1),
                          new int3(6, 3, 3), Mat.Gold);
            }

            // West branch: secret treasury.
            int treasuryMinX = tx - 276;
            int treasuryMinZ = trapZ - 52;
            brush.Box(new int3(tx - 188, dungeonY + 2, trapZ - corridorHalf),
                      new int3(70, corridorHeight, corridorHalf * 2), Mat.Empty);
            brush.Box(new int3(tx - 188, dungeonY, trapZ - corridorHalf),
                      new int3(70, 2, corridorHalf * 2), Mat.DarkStone);
            brush.FillBulk(new int3(treasuryMinX, dungeonY + 2, treasuryMinZ),
                           new int3(100, 36, 104), Mat.Empty);
            brush.Box(new int3(treasuryMinX, dungeonY, treasuryMinZ),
                      new int3(100, 2, 104), Mat.DarkStone);

            // Repeating ceiling ribs and shelf bays turn the rock cut into an occupied vault.
            for (int x = treasuryMinX + 12; x < treasuryMinX + 94; x += 24)
                brush.Box(new int3(x, dungeonY + 30, treasuryMinZ + 5),
                          new int3(5, 4, 94), Mat.Wood);
            for (int side = -1; side <= 1; side += 2)
            for (int bay = 0; bay < 3; bay++)
            {
                int x = treasuryMinX + 18 + bay * 30;
                int z = trapZ + side * 45;
                brush.Box(new int3(x - 9, dungeonY + 2, z - 5),
                          new int3(18, 23, 10), Mat.Wood);
                brush.Box(new int3(x - 10, dungeonY + 9, z - 6),
                          new int3(20, 2, 12), Mat.Gold);
                brush.Box(new int3(x - 10, dungeonY + 18, z - 6),
                          new int3(20, 2, 12), Mat.Gold);
            }

            // Chests and coin tables remain against the edges; the central carpet is the route
            // and visual reveal from the tunnel.
            for (int side = -1; side <= 1; side += 2)
            for (int row = 0; row < 3; row++)
            {
                int x = treasuryMinX + 24 + row * 27;
                int z = trapZ + side * 34;
                brush.Box(new int3(x - 8, dungeonY + 2, z - 7),
                          new int3(16, 10, 14), Mat.Wood);
                brush.Box(new int3(x - 9, dungeonY + 10, z - 8),
                          new int3(18, 3, 16), Mat.Gold);
            }
            brush.Box(new int3(treasuryMinX + 18, dungeonY + 1, trapZ - 8),
                      new int3(62, 1, 16), Mat.Cloth);
            brush.Box(new int3(treasuryMinX + 15, dungeonY + 2, treasuryMinZ + 12),
                      new int3(70, 5, 12), Mat.Gold);
            for (int pile = 0; pile < 5; pile++)
            {
                int px = treasuryMinX + 18 + pile * 16;
                int pz = treasuryMinZ + 21 + (pile & 1) * 7;
                brush.Cone(px, dungeonY + 7, pz, 5, 7 + (pile % 3) * 3, Mat.Gold);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                brush.Box(new int3(treasuryMinX + 50 - 2, dungeonY + 17,
                                   trapZ + side * 45 - 2),
                          new int3(4, 8, 4), Mat.Glass);
                brush.Box(new int3(treasuryMinX + 48, dungeonY + 14,
                                   trapZ + side * 45 - 1),
                          new int3(6, 3, 3), Mat.Gold);
            }
        }

        /// <summary>A natural cavern at the end of the passage, with a pool.</summary>
        private static void Cave(ref VoxelBrush brush, in CastlePlan plan, int3 at)
        {
            var rng = new Random(plan.Seed ^ 0xCAFEu);

            // A cathedral-scale but bounded natural chamber. Overlapping ellipsoids create an
            // asymmetric nave, side bays, and a high rear vault like the reference instead of
            // exposing the concentric shells of several small spherical cuts.
            CarveCavernEllipsoid(ref brush, at + new int3(0, 27, 0),
                                 new int3(82, 36, 104), 0.17f);
            CarveCavernEllipsoid(ref brush, at + new int3(-58, 23, -18),
                                 new int3(56, 30, 72), 1.43f);
            CarveCavernEllipsoid(ref brush, at + new int3(62, 25, 30),
                                 new int3(60, 33, 74), 2.71f);
            CarveCavernEllipsoid(ref brush, at + new int3(12, 31, -72),
                                 new int3(66, 37, 62), 4.19f);

            // A low east tunnel opens into a second crystal grotto. Carve it before decoration
            // so later props cannot be accidentally erased, and lay a continuous stone path at
            // the same elevation as the main pool bridge.
            int sideCaveX = at.x + 145;
            int sideCaveZ = at.z + 25;
            brush.Box(new int3(at.x - 5, at.y + 2, sideCaveZ - 10),
                      new int3(159, 30, 20), Mat.Empty);
            brush.Box(new int3(at.x - 5, at.y - 1, sideCaveZ - 10),
                      new int3(159, 3, 20), Mat.DarkStone);

            CarveCavernEllipsoid(ref brush,
                                 new int3(sideCaveX - 10, at.y + 17, sideCaveZ - 5),
                                 new int3(40, 31, 47), 0.91f);
            CarveCavernEllipsoid(ref brush,
                                 new int3(sideCaveX + 24, at.y + 15, sideCaveZ + 16),
                                 new int3(35, 27, 38), 2.23f);
            CarveCavernEllipsoid(ref brush,
                                 new int3(sideCaveX + 3, at.y + 23, sideCaveZ - 28),
                                 new int3(31, 33, 34), 3.77f);
            brush.Box(new int3(at.x - 5, at.y - 1, sideCaveZ - 10),
                      new int3(159, 3, 20), Mat.DarkStone);
            brush.Disc(sideCaveX, at.y - 1, sideCaveZ, 28, Mat.DarkStone);

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
                // Preserve the long entrance-to-waterfall reveal. The old unrestricted scatter
                // regularly planted a full-height formation directly in the player's sightline.
                if (math.abs(sx - at.x) < 24 && sz < at.z + 55 && sz > at.z - 92)
                    sx += sx < at.x ? -32 : 32;
                int h = rng.NextInt(10, 34);
                brush.Cone(sx, at.y - 2, sz, rng.NextInt(3, 7), h, Mat.DarkStone);
            }

            // Ceiling formations mirror the floor scatter and break up the cavern's upper
            // silhouette. Their roots now reach the high carved vault rather than floating in
            // the middle of the former low spherical chamber.
            for (int i = 0; i < 18; i++)
            {
                int sx = at.x + rng.NextInt(-78, 78);
                int sz = at.z + rng.NextInt(-78, 78);
                brush.HangingCone(sx, at.y + rng.NextInt(48, 61), sz,
                                  rng.NextInt(3, 8), rng.NextInt(12, 31), Mat.DarkStone);
            }
            // Ancient stone causeway and broken parapet replace the clean timber footbridge.
            brush.Box(new int3(at.x - 5, at.y - 2, at.z - 52),
                      new int3(10, 3, 104), Mat.DarkStone);
            int2[] causewayRemains =
            {
                new(-8, -42), new(5, -13), new(-8, 25), new(5, 45),
            };
            for (int i = 0; i < causewayRemains.Length; i++)
                brush.Box(new int3(at.x + causewayRemains[i].x, at.y + 1,
                                   at.z + causewayRemains[i].y),
                          new int3(3, 4 + (i & 1) * 3, 3),
                          i == 2 ? Mat.Moss : Mat.Stone);

            // A cool spring falls from the rear vault into the main pool. The recessed pocket,
            // irregular curtain edge, and nearby ruins recreate the cyan focal waterfall in the
            // reference cave rather than relying on uniformly blue ambient light.
            int fallZ = at.z - 76;
            brush.Box(new int3(at.x + 15, at.y - 3, fallZ - 8),
                      new int3(24, 31, 13), Mat.Empty);
            for (int x = -8; x <= 8; x++)
            for (int z = -1; z <= 0; z++)
            {
                int topY = at.y + 27 - math.abs(x) / 3
                         - math.abs((x * 5 + z * 3) % 3);
                brush.FillColumnBulk(at.x + 27 + x, at.y - 2, topY,
                                     fallZ + z, Mat.Cascade);
            }
            brush.Disc(at.x + 27, at.y - 2, fallZ + 8, 28, Mat.Water);

            // Fragmentary columns frame the waterfall without turning the natural cavern into a
            // rectangular room. Missing upper courses and moss communicate age and collapse.
            for (int side = -1; side <= 1; side += 2)
            {
                int columnX = at.x + 27 + side * 20;
                brush.Cylinder(columnX, at.y - 2, fallZ + 4, 6,
                               side < 0 ? 30 : 22, Mat.Stone);
                brush.Cylinder(columnX, at.y + (side < 0 ? 24 : 16), fallZ + 4,
                               8, 4, Mat.DarkStone);
                brush.Cone(columnX + side * 4, at.y - 1, fallZ + 10,
                           5, 8, Mat.Moss);
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
                brush.Cone(crystal.x, crystal.y, crystal.z, 3, 13, Mat.Crystal);
                brush.Cone(crystal.x - 5, crystal.y, crystal.z + 3, 2, 8, Mat.Moss);
                brush.Cone(crystal.x + 4, crystal.y, crystal.z + 4, 2, 10, Mat.Crystal);
            }

            // The side grotto is the cool-colour reward at the end of the branch. A shallow
            // spring and ruined arch provide a layered focal composition; slender crystals stay
            // at the perimeter instead of forming the former bright picket fence around a cube.
            brush.Disc(sideCaveX, at.y + 1, sideCaveZ, 15, Mat.Water);
            brush.Box(new int3(sideCaveX - 20, at.y + 2, sideCaveZ - 3),
                      new int3(40, 2, 6), Mat.DarkStone);

            int archX = sideCaveX + 28;
            for (int side = -1; side <= 1; side += 2)
            {
                int pillarZ = sideCaveZ + side * 16;
                brush.Cylinder(archX, at.y + 2, pillarZ, 6, 29, Mat.Stone);
                brush.Cylinder(archX, at.y + 27, pillarZ, 8, 4, Mat.DarkStone);
            }
            brush.Box(new int3(archX - 4, at.y + 28, sideCaveZ - 22),
                      new int3(8, 6, 44), Mat.DarkStone);
            brush.Box(new int3(archX + 1, at.y + 11, sideCaveZ - 3),
                      new int3(5, 14, 6), Mat.Crystal);
            brush.Box(new int3(archX - 2, at.y + 8, sideCaveZ - 6),
                      new int3(11, 4, 12), Mat.Stone);

            for (int i = 0; i < 9; i++)
            {
                float angle = i * (math.PI * 2f / 9f) + 0.23f;
                float radius = 27f + (i % 3) * 5f;
                int cx = sideCaveX + (int)math.round(math.cos(angle) * radius);
                int cz = sideCaveZ + (int)math.round(math.sin(angle) * radius);
                int crystalHeight = 7 + (i * 5 % 8);
                brush.Cone(cx, at.y + 2, cz, 2, crystalHeight,
                           i == 2 || i == 7 ? Mat.Moss : Mat.Crystal);
                if ((i & 1) == 0)
                    brush.Cone(cx + 5, at.y + 2, cz - 3, 2,
                               math.max(7, crystalHeight - 6), Mat.Crystal);
            }
            brush.HangingCone(sideCaveX - 25, at.y + 48, sideCaveZ - 20,
                              6, 21, Mat.DarkStone);
            brush.HangingCone(sideCaveX + 3, at.y + 51, sideCaveZ + 20,
                              7, 25, Mat.DarkStone);
            brush.HangingCone(sideCaveX + 30, at.y + 46, sideCaveZ - 15,
                              5, 18, Mat.DarkStone);

            // Cave dressing is intentionally random, but circulation is not. Reassert the final
            // tunnel core after stalagmites/crystals so no seed can decorate across the route;
            // carry the causeway through the spring to the grotto centre so the player capsule
            // cannot clip the water rim during the reveal.
            brush.Box(new int3(at.x - 5, at.y + 2, sideCaveZ - 8),
                      new int3(151, 22, 16), Mat.Empty);
            brush.Box(new int3(at.x - 5, at.y - 1, sideCaveZ - 8),
                      new int3(151, 3, 16), Mat.DarkStone);

            int3[] caveLights =
            {
                new(at.x - 48, at.y + 12, at.z - 28),
                new(at.x + 44, at.y + 10, at.z - 18),
                new(at.x - 38, at.y + 14, at.z + 38),
                new(at.x + 50, at.y + 11, at.z + 32),
            };
            foreach (var light in caveLights)
            {
                brush.Box(light, new int3(1, 3, 1), Mat.Glass);
                brush.Box(light - new int3(1, 1, 1), new int3(3, 1, 3), Mat.Gold);
            }
        }

        private static void CarveCavernEllipsoid(ref VoxelBrush brush, int3 centre, int3 radii,
                                                 float phase)
        {
            float inverseX = 1f / (radii.x * radii.x);
            float inverseZ = 1f / (radii.z * radii.z);
            for (int z = -radii.z; z <= radii.z; z++)
            for (int x = -radii.x; x <= radii.x; x++)
            {
                // Several broad, incommensurate waves displace the implicit chamber boundary.
                // The previous perfect ellipsoid exposed concentric voxel courses from every
                // viewpoint and read as a manufactured pipe. Keeping this deterministic retains
                // reproducible generation while producing shelves, pinches, and an uneven vault.
                float boundary = 1f
                    + math.sin(x * 0.091f + z * 0.037f + phase) * 0.085f
                    + math.sin(x * 0.031f - z * 0.073f + phase * 1.7f) * 0.065f
                    + math.sin((x + z) * 0.151f - phase * 0.8f) * 0.025f;
                float radial = (x * x * inverseX + z * z * inverseZ)
                             / math.max(0.76f, boundary);
                if (radial > 1f) continue;
                float profile = math.sqrt(1f - radial);
                int halfHeight = (int)math.floor(radii.y * profile);
                int floorWarp = (int)math.round(
                    math.sin(x * 0.117f + z * 0.053f + phase) * 2.2f
                  + math.sin(z * 0.181f - phase) * 1.1f);
                int roofWarp = (int)math.round(
                    math.sin(x * 0.067f - z * 0.101f + phase * 2.0f) * 3.5f
                  + math.sin((x - z) * 0.139f + phase) * 1.6f);
                brush.FillColumnBulk(centre.x + x, centre.y - halfHeight + floorWarp,
                                     centre.y + halfHeight + roofWarp + 1,
                                     centre.z + z, Mat.Empty);
            }
        }
    }
}
