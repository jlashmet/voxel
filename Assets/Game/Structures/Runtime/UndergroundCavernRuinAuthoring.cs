using System;
using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Reusable high-level underground destination built on the generic cave-network and decoration
    /// contracts. It deliberately contains no showcase coordinates: callers supply a surface cave
    /// request and this composer derives the destination from authored traversal semantics.
    /// </summary>
    public struct UndergroundCavernRuinConfig
    {
        public int MinimumDestinationTraversal;
        public int HostStartSegment;
        public int HostPadding;
        public int CavernRadius;
        public int CavernHeight;
        public int RuinForwardOffset;
        public int RuinWidth;
        public int RuinDepth;
        public int RuinHeight;
        public int NaturalInstancesPerKind;
        public int LanternInstancesPerKind;

        public bool IsWellFormed =>
            MinimumDestinationTraversal > 0 && HostStartSegment >= 0 && HostPadding >= 4 &&
            CavernRadius >= 90 && CavernHeight >= 100 &&
            RuinForwardOffset >= 20 && RuinWidth >= 60 && RuinDepth >= 50 && RuinHeight >= 40 &&
            NaturalInstancesPerKind >= 1 && NaturalInstancesPerKind <= 8 &&
            LanternInstancesPerKind >= 1 && LanternInstancesPerKind <= 8;

        public static UndergroundCavernRuinConfig DeepAncientRuin => new UndergroundCavernRuinConfig
        {
            MinimumDestinationTraversal = 2400,
            HostStartSegment = 7,
            HostPadding = 10,
            CavernRadius = 175,
            CavernHeight = 200,
            RuinForwardOffset = 82,
            RuinWidth = 116,
            RuinDepth = 76,
            RuinHeight = 62,
            NaturalInstancesPerKind = 6,
            LanternInstancesPerKind = 4,
        };
    }

    public struct UndergroundCavernRuinResult
    {
        public CaveAuthoringResult Cave;
        public CaveTraversalCandidate Destination;
        public DecorationBounds CavernBounds;
        public DecorationBounds RuinBounds;
        public MineCaveLightRequest[] LocalLights;
        public int StatueCount;
        public int StalactiteCount;
        public int GeologicalCategoryCount;
        public long VoxelsWritten;

        public bool IsWellFormed =>
            Destination.IsWellFormed && CavernBounds.IsWellFormed && RuinBounds.IsWellFormed &&
            StatueCount == 2 && StalactiteCount > 0 && GeologicalCategoryCount >= 3 &&
            LocalLights != null && LocalLights.Length > 0;
    }

    public static class UndergroundCavernRuinAuthoring
    {
        private const ulong DecorationSalt = 0x554E44455252554Eul; // UNDERRUN

        public static UndergroundCavernRuinResult Author(
            IStructureAuthoringSession authoring,
            in CaveGenerationRequest caveRequest,
            in CaveConfig caveConfig,
            in CaveMaterialPalette cavePalette,
            in UndergroundCavernRuinConfig config)
        {
            if (authoring == null) throw new ArgumentNullException(nameof(authoring));
            if (!caveRequest.IsWellFormed || !caveConfig.IsWellFormed || !config.IsWellFormed)
                throw new ArgumentException("Underground cavern/ruin authoring requires valid cave and destination configuration.");
            if (caveConfig.TurnChancePercent != 0)
                throw new ArgumentException(
                    "The deep-host sleeve requires a deterministic straight primary descent; use authored transition chambers for curvature.",
                    nameof(caveConfig));

            long startWrites = authoring.TotalVoxelsWritten;
            AuthorDeepHostSleeve(authoring, in caveRequest, in caveConfig, in cavePalette, in config);

            CaveAuthoringResult cave = CaveAuthoring.Author(
                authoring, in caveRequest, in caveConfig, in cavePalette);
            CavePlacementRequirements requirements =
                CavePlacementRequirements.AnyReachableTerminal(config.MinimumDestinationTraversal);
            var preferences = new CavePlacementPreferences
            {
                PreferredFlags = CaveTraversalFlags.MainPath | CaveTraversalFlags.Terminal,
            };
            if (!CavePlacementResolver.TrySelectBest(
                    in cave.TraversalCandidates, in requirements, in preferences,
                    out CaveTraversalCandidate destination))
                throw new InvalidOperationException(
                    "The cave network did not produce a reachable terminal at the required traversal depth.");

            // Guaranteed spatial transitions make the long approach read as a cave rather than a
            // uniform service tunnel even though the primary route stays cardinal and predictable.
            AuthorTransitionChambers(authoring, in caveRequest, in caveConfig, in cavePalette);

            int3 forward = FacingVector(destination.ExitFacing);
            int innerRadius = config.CavernRadius - 18;
            int floorY = destination.Position.y;
            int3 cavernCentre = destination.Position + forward * 34;
            AuthorCavernEnvelope(authoring, cavernCentre, floorY, innerRadius, in cavePalette, in config);

            CaveWalkablePatch patch = CavernPatch(
                FeatureHash.Mix(caveRequest.Seed ^ DecorationSalt), cavernCentre, floorY,
                destination.ExitFacing, innerRadius, config.CavernHeight);
            if (!CaveDecorationSpaceAdapter.TryCreate(
                    in patch, out DecorationSpace space, out DecorationContext context,
                    out CaveDecorationCandidate[] candidates, out DecorationExclusion[] exclusions))
                throw new InvalidOperationException("Could not derive shared decoration surfaces for the destination cavern.");

            if (!NaturalCaveDecorationPlanner.TryPlan(
                    in space, in context, candidates, exclusions, config.NaturalInstancesPerKind,
                    out NaturalCaveDecorationInstance[] natural) ||
                !NaturalCaveDecorationPresentation.TryAuthorVoxelStamps(authoring, natural))
                throw new InvalidOperationException("Could not author natural destination-cavern formations.");

            CountGeology(natural, out int stalactites, out int geologicalCategories);

            int3 ruinCentre = cavernCentre + forward * config.RuinForwardOffset;
            DecorationBounds ruinBounds = AuthorRuin(
                authoring, ruinCentre, floorY, destination.ExitFacing, caveRequest.TerrainSeed, in config);
            AuthorStatues(authoring, ruinCentre, floorY, destination.ExitFacing, in config);

            // Reuse the mine-cave wall-fixture planner, then retain only its supported lanterns.
            // This gives every emitted point light a matching authored fixture without turning the
            // ancient cavern into a mine full of rails, carts and support beams.
            if (!MineCaveDecorationPlanner.TryPlan(
                    in space, in context, candidates, exclusions, config.LanternInstancesPerKind,
                    out MineCaveDecorationInstance[] mine))
                throw new InvalidOperationException("Could not resolve shared cavern lantern placements.");
            MineCaveDecorationInstance[] lanterns = OnlyLanterns(mine);
            if (lanterns.Length == 0 ||
                !MineCaveDecorationPresentation.TryAuthorGeometry(authoring, lanterns, in context))
                throw new InvalidOperationException("The destination cavern produced no supported lantern geometry.");
            MineCaveLightRequest[] lights =
                MineCaveDecorationPresentation.CollectLightRequests(lanterns, in context);
            if (lights.Length == 0)
                throw new InvalidOperationException("The supported cavern lanterns produced no local-light requests.");

            // The deterministic cavern shell is deliberately solid before its interior is opened.
            // Because the shell overlaps the final primary-route spans, its back wall can seal an
            // otherwise valid cave endpoint after CaveAuthoring has already reported it reachable.
            // Reassert a narrow gameplay corridor last, after formations, rubble and fixtures, so
            // semantic traversal remains physically walkable through the shell and ruin doorway.
            AuthorProtectedDestinationRoute(
                authoring,
                destination.Position,
                ruinCentre,
                floorY,
                destination.ExitFacing,
                in caveConfig,
                in config);

            var cavernBounds = new DecorationBounds
            {
                Min = new int3(cavernCentre.x - innerRadius, floorY, cavernCentre.z - innerRadius),
                MaxExclusive = new int3(cavernCentre.x + innerRadius + 1,
                    floorY + config.CavernHeight - 14,
                    cavernCentre.z + innerRadius + 1),
            };

            return new UndergroundCavernRuinResult
            {
                Cave = cave,
                Destination = destination,
                CavernBounds = cavernBounds,
                RuinBounds = ruinBounds,
                LocalLights = lights,
                StatueCount = 2,
                StalactiteCount = stalactites,
                GeologicalCategoryCount = geologicalCategories,
                VoxelsWritten = authoring.TotalVoxelsWritten - startWrites,
            };
        }

        private static void AuthorDeepHostSleeve(
            IStructureAuthoringSession a,
            in CaveGenerationRequest request,
            in CaveConfig cave,
            in CaveMaterialPalette palette,
            in UndergroundCavernRuinConfig config)
        {
            int3 direction = FacingVector(request.Entrance.Facing);
            int cross = cave.TunnelWidth / 2 + cave.WallRoughness + config.HostPadding;
            int height = cave.TunnelHeight + cave.CeilingRoughness + cave.FloorRoughness + config.HostPadding * 2;
            int3 current = request.EntranceWorldPosition + direction * request.Entrance.ClearanceLength;

            for (int segment = 0; segment < cave.MainSegmentCount; segment++)
            {
                int drop = segment < cave.SurfaceDescentSegments ? cave.SurfaceDescentPerSegment : 0;
                int targetY = math.max(
                    current.y - drop,
                    request.Origin.y + cave.MinVerticalOffset);
                int3 next = new int3(
                    current.x + direction.x * cave.SegmentLength,
                    targetY,
                    current.z + direction.z * cave.SegmentLength);

                if (segment >= config.HostStartSegment)
                {
                    int minX = math.min(current.x, next.x) - cross;
                    int minZ = math.min(current.z, next.z) - cross;
                    int sizeX = math.abs(next.x - current.x) + cross * 2 + 1;
                    int sizeZ = math.abs(next.z - current.z) + cross * 2 + 1;
                    int minY = math.min(current.y, next.y) - cave.FloorRoughness - config.HostPadding;
                    int maxY = math.max(current.y, next.y) + cave.TunnelHeight + cave.CeilingRoughness + config.HostPadding;
                    a.FillBulk(new int3(minX, minY, minZ),
                        new int3(sizeX, math.max(height, maxY - minY), sizeZ), palette.Rock);
                }
                current = next;
            }
        }

        private static void AuthorTransitionChambers(
            IStructureAuthoringSession a,
            in CaveGenerationRequest request,
            in CaveConfig cave,
            in CaveMaterialPalette palette)
        {
            int3 direction = FacingVector(request.Entrance.Facing);
            int3 side = new int3(-direction.z, 0, direction.x);
            int[] segments = { 17, 31, 43 };
            for (int i = 0; i < segments.Length; i++)
            {
                int segment = math.min(segments[i], cave.SurfaceDescentSegments - 1);
                int3 centre = request.EntranceWorldPosition
                    + direction * (request.Entrance.ClearanceLength + segment * cave.SegmentLength)
                    + side * (i == 1 ? -14 : 14);
                centre.y -= segment * cave.SurfaceDescentPerSegment;
                int radius = 28 + i * 4;
                int chamberHeight = 42 + i * 5;
                a.Cylinder(centre.x, centre.y, centre.z, radius, chamberHeight, palette.Opening);
                a.Disc(centre.x, centre.y - 1, centre.z, radius, palette.Rock);
            }
        }

        private static void AuthorCavernEnvelope(
            IStructureAuthoringSession a,
            int3 centre,
            int floorY,
            int innerRadius,
            in CaveMaterialPalette palette,
            in UndergroundCavernRuinConfig config)
        {
            int outerRadius = config.CavernRadius;
            int outerBase = floorY - 8;
            int outerHeight = config.CavernHeight;

            // A thin explicit host shell makes the destination deterministic even in sparse deep
            // storage. The interior carve is several times larger than any approach chamber.
            a.Cylinder(centre.x, outerBase, centre.z, outerRadius, outerHeight,
                palette.Rock, innerRadius);
            a.Disc(centre.x, outerBase, centre.z, outerRadius, palette.Rock);
            a.Disc(centre.x, outerBase + outerHeight - 1, centre.z, outerRadius, palette.Rock);
            a.Cylinder(centre.x, floorY, centre.z, innerRadius,
                config.CavernHeight - 14, palette.Opening);
            a.Disc(centre.x, floorY - 1, centre.z, innerRadius, palette.Rock);

            // Offset voids and rock shoulders break the perfect cylinder into readable recesses.
            int shoulderRadius = innerRadius / 3;
            a.Cylinder(centre.x + innerRadius / 2, floorY + 8, centre.z - innerRadius / 3,
                shoulderRadius, config.CavernHeight - 42, palette.Opening);
            a.Cylinder(centre.x - innerRadius / 2, floorY + 4, centre.z + innerRadius / 3,
                shoulderRadius + 8, config.CavernHeight - 56, palette.Opening);
            a.Cone(centre.x - innerRadius / 2, floorY, centre.z - innerRadius / 2,
                24, 48, palette.Rock);
            a.Cone(centre.x + innerRadius / 3, floorY, centre.z + innerRadius / 2,
                20, 38, palette.Rock);
        }

        private static void AuthorProtectedDestinationRoute(
            IStructureAuthoringSession a,
            int3 destination,
            int3 ruinCentre,
            int floorY,
            Facing facing,
            in CaveConfig cave,
            in UndergroundCavernRuinConfig config)
        {
            int3 forward = FacingVector(facing);
            int halfWidth = math.max(6, cave.TunnelWidth / 4);
            int clearanceHeight = math.max(24, cave.TunnelHeight - 4);
            int backtrack = math.max(
                cave.SegmentLength,
                config.CavernRadius - 34 + cave.WallRoughness + 8);
            int3 start = destination - forward * backtrack;
            int3 end = ruinCentre;

            int minX = math.min(start.x, end.x) - halfWidth;
            int minZ = math.min(start.z, end.z) - halfWidth;
            int sizeX = math.abs(end.x - start.x) + halfWidth * 2 + 1;
            int sizeZ = math.abs(end.z - start.z) + halfWidth * 2 + 1;
            a.Carve(
                new int3(minX, floorY, minZ),
                new int3(sizeX, clearanceHeight, sizeZ));
        }

        private static CaveWalkablePatch CavernPatch(
            ulong seed,
            int3 centre,
            int floorY,
            Facing facing,
            int radius,
            int height)
        {
            int length = radius * 2 - 24;
            int3 forward = FacingVector(facing);
            int3 end = new int3(centre.x, floorY, centre.z) + forward * (length / 2);
            return new CaveWalkablePatch
            {
                PatchId = CaveDecorationSpaceAdapter.FoldSeed(seed),
                Seed = seed == 0 ? 1ul : seed,
                End = end,
                Facing = facing,
                Width = radius * 2 - 24,
                Length = length,
                Height = height - 20,
            };
        }

        private static DecorationBounds AuthorRuin(
            IStructureAuthoringSession a,
            int3 centre,
            int floorY,
            Facing facing,
            uint weatherSeed,
            in UndergroundCavernRuinConfig config)
        {
            bool alongX = facing == Facing.East || facing == Facing.West;
            int sizeX = alongX ? config.RuinDepth : config.RuinWidth;
            int sizeZ = alongX ? config.RuinWidth : config.RuinDepth;
            int3 min = new int3(centre.x - sizeX / 2, floorY, centre.z - sizeZ / 2);
            int3 size = new int3(sizeX, config.RuinHeight, sizeZ);

            a.HollowBox(min, size, 5, GameMaterialIds.MasonryLarge, floor: true, ceiling: false);

            int3 forward = FacingVector(facing);
            int3 side = new int3(-forward.z, 0, forward.x);
            int3 entrance = centre - forward * (alongX ? sizeX / 2 : sizeZ / 2);
            int3 doorMin = entrance - side * 16 + new int3(0, 1, 0);
            if (alongX)
                a.Carve(new int3(doorMin.x - (forward.x > 0 ? 0 : 6), doorMin.y, doorMin.z), new int3(7, 34, 33));
            else
                a.Carve(new int3(doorMin.x, doorMin.y, doorMin.z - (forward.z > 0 ? 0 : 6)), new int3(33, 34, 7));

            // Heavy entrance jambs/lintel and interior columns leave the ruin structurally legible.
            int3 jambA = entrance + side * 21;
            int3 jambB = entrance - side * 21;
            a.Cylinder(jambA.x, floorY, jambA.z, 5, 42, GameMaterialIds.MasonryMedium);
            a.Cylinder(jambB.x, floorY, jambB.z, 5, 42, GameMaterialIds.MasonryMedium);
            if (alongX)
                a.Box(new int3(entrance.x - 3, floorY + 35, centre.z - 24), new int3(7, 8, 49), GameMaterialIds.MasonryMedium);
            else
                a.Box(new int3(centre.x - 24, floorY + 35, entrance.z - 3), new int3(49, 8, 7), GameMaterialIds.MasonryMedium);

            for (int s = -1; s <= 1; s += 2)
            {
                int3 column = centre + side * 34 + forward * 12 * s;
                a.Cylinder(column.x, floorY + 1, column.z, 4, 48, GameMaterialIds.MasonryMedium);
            }

            // Roof remnants and missing corners read as collapse rather than an unfinished box.
            if (alongX)
            {
                a.Box(new int3(min.x + 10, floorY + config.RuinHeight - 5, min.z + 5),
                    new int3(sizeX / 3, 5, sizeZ - 10), GameMaterialIds.MasonrySmall);
                a.Carve(new int3(min.x + sizeX - 18, floorY + config.RuinHeight - 24, min.z),
                    new int3(19, 25, 28));
            }
            else
            {
                a.Box(new int3(min.x + 5, floorY + config.RuinHeight - 5, min.z + 10),
                    new int3(sizeX - 10, 5, sizeZ / 3), GameMaterialIds.MasonrySmall);
                a.Carve(new int3(min.x, floorY + config.RuinHeight - 24, min.z + sizeZ - 18),
                    new int3(28, 25, 19));
            }

            for (int i = -2; i <= 2; i++)
            {
                int3 rubble = entrance - forward * (18 + math.abs(i) * 4) + side * (i * 13);
                a.Box(new int3(rubble.x - 4, floorY, rubble.z - 4),
                    new int3(8 + math.abs(i), 5 + (i & 1), 8), GameMaterialIds.MasonrySmall);
            }
            a.Weather(min, size, Coatings.Moss, weatherSeed ^ 0x5255494Eu, 28);

            return new DecorationBounds { Min = min, MaxExclusive = min + size };
        }

        private static void AuthorStatues(
            IStructureAuthoringSession a,
            int3 ruinCentre,
            int floorY,
            Facing facing,
            in UndergroundCavernRuinConfig config)
        {
            int3 forward = FacingVector(facing);
            int3 side = new int3(-forward.z, 0, forward.x);
            int frontOffset = config.RuinForwardOffset / 2;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int3 p = ruinCentre - forward * frontOffset + side * (config.RuinWidth / 2 - 8) * sign;
                a.Box(new int3(p.x - 10, floorY, p.z - 10), new int3(21, 8, 21), GameMaterialIds.MasonryLarge);
                a.Box(new int3(p.x - 7, floorY + 8, p.z - 7), new int3(6, 25, 14), GameMaterialIds.DarkStone);
                a.Box(new int3(p.x + 2, floorY + 8, p.z - 7), new int3(6, 25, 14), GameMaterialIds.DarkStone);
                a.Box(new int3(p.x - 9, floorY + 31, p.z - 8), new int3(18, 28, 16), GameMaterialIds.DarkStone);
                a.Cylinder(p.x, floorY + 58, p.z, 7, 13, GameMaterialIds.DarkStone);
                a.Box(new int3(p.x - 16, floorY + 36, p.z - 5), new int3(7, 22, 10), GameMaterialIds.DarkStone);
                a.Box(new int3(p.x + 10, floorY + 36, p.z - 5), new int3(7, 22, 10), GameMaterialIds.DarkStone);
                a.Weather(new int3(p.x - 16, floorY, p.z - 10), new int3(33, 71, 21),
                    Coatings.Moss, (uint)(0x57A70000u + sign + 2), 18);
            }
        }

        private static MineCaveDecorationInstance[] OnlyLanterns(MineCaveDecorationInstance[] all)
        {
            int count = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].Kind == MineCaveDecorationKind.Lantern && all[i].IsWellFormed)
                    count++;
            var result = new MineCaveDecorationInstance[count];
            int output = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].Kind == MineCaveDecorationKind.Lantern && all[i].IsWellFormed)
                    result[output++] = all[i];
            return result;
        }

        private static void CountGeology(
            NaturalCaveDecorationInstance[] natural,
            out int stalactites,
            out int categories)
        {
            stalactites = 0;
            bool stone = false, crystal = false, stalagmite = false, stalactite = false;
            for (int i = 0; i < natural.Length; i++)
            {
                if (!natural[i].IsWellFormed || natural[i].Backend != DecorationRenderBackend.VoxelStamp)
                    continue;
                switch (natural[i].Kind)
                {
                    case NaturalCaveDecorationKind.Stone: stone = true; break;
                    case NaturalCaveDecorationKind.Crystal: crystal = true; break;
                    case NaturalCaveDecorationKind.Stalagmite: stalagmite = true; break;
                    case NaturalCaveDecorationKind.Stalactite:
                        stalactite = true;
                        stalactites++;
                        break;
                }
            }
            categories = (stone ? 1 : 0) + (crystal ? 1 : 0) +
                         (stalagmite ? 1 : 0) + (stalactite ? 1 : 0);
        }

        private static int3 FacingVector(Facing facing)
        {
            switch (facing)
            {
                case Facing.East: return new int3(1, 0, 0);
                case Facing.South: return new int3(0, 0, -1);
                case Facing.West: return new int3(-1, 0, 0);
                default: return new int3(0, 0, 1);
            }
        }
    }
}
