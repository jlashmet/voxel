using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Adapts the existing second-floor keep bedchamber to the generic decoration model. Legacy
    /// architectural/detail clusters that remain during migration are exposed as exclusions so
    /// procedural furniture never intersects them.
    /// </summary>
    public static class CastleBedroomDecorationAdapter
    {
        public const uint SpaceDiscriminator = 0xBEDCA57u;
        private const int InnerWallOffset = 8;
        private const int FloorSlabThickness = 3;
        private const int CeilingReserve = 8;

        public static bool TryResolve(
            in CastlePlan plan,
            out DecorationSpace space,
            out DecorationContext context,
            out DecorationExclusion[] exclusions,
            out DecorationPlacement[] placements)
        {
            space = default;
            context = default;
            exclusions = new DecorationExclusion[0];
            placements = new DecorationPlacement[0];

            if (plan.Floors < 2 || plan.FloorHeight <= FloorSlabThickness + CeilingReserve + 8)
                return false;

            space = CreateSpace(in plan);
            context = CreateContext(in plan, space.SpaceId);
            exclusions = CreateExclusions(in plan, in space);
            return BedroomSceneResolver.TryResolve(in space, in context, exclusions, out placements);
        }

        public static DecorationSpace CreateSpace(in CastlePlan plan)
        {
            int3 keepMin = CastleKeepCoreAuthoring.Minimum(in plan);
            int3 keepSize = CastleKeepCoreAuthoring.Size(in plan);
            int floorY = plan.Centre.y + plan.PlateauHeight + plan.FloorHeight;
            uint structureId = CastleStructureId(in plan);
            uint spaceId = DecorationSeed.Derive(structureId, SpaceDiscriminator);

            return new DecorationSpace
            {
                SpaceId = spaceId,
                Kind = DecorationSpaceKind.Bedroom,
                Bounds = new DecorationBounds
                {
                    Min = new int3(
                        keepMin.x + InnerWallOffset,
                        floorY + FloorSlabThickness,
                        keepMin.z + InnerWallOffset),
                    MaxExclusive = new int3(
                        keepMin.x + keepSize.x - InnerWallOffset,
                        floorY + plan.FloorHeight - CeilingReserve,
                        keepMin.z + keepSize.z - InnerWallOffset),
                },
            };
        }

        public static DecorationContext CreateContext(in CastlePlan plan, uint spaceId)
        {
            uint structureId = CastleStructureId(in plan);
            return new DecorationContext
            {
                WorldSeed = plan.Seed,
                StructureId = structureId,
                SpaceId = spaceId,
                StyleId = DecorationSeed.Derive(plan.Seed, 0x57A1Eu),
                StructureKind = DecorationStructureKind.Castle,
                SpaceKind = DecorationSpaceKind.Bedroom,
                Wealth = DecorationWealthTier.Noble,
                Condition = DecorationConditionTier.Maintained,
                Environment = DecorationEnvironmentTags.Interior | DecorationEnvironmentTags.Residential,
            };
        }

        public static DecorationExclusion[] CreateExclusions(
            in CastlePlan plan,
            in DecorationSpace space)
        {
            int3 keepMin = CastleKeepCoreAuthoring.Minimum(in plan);
            int3 keepSize = CastleKeepCoreAuthoring.Size(in plan);
            int floorY = plan.Centre.y + plan.PlateauHeight + plan.FloorHeight;
            int cx = keepMin.x + keepSize.x / 2;
            int cz = keepMin.z + keepSize.z / 2;

            var exclusions = new DecorationExclusion[12];
            exclusions[0] = new DecorationExclusion
            {
                Kind = DecorationExclusionKind.Stair | DecorationExclusionKind.Navigation,
                Bounds = GrandStairExclusion(in plan, in space, keepMin),
            };
            exclusions[1] = new DecorationExclusion
            {
                Kind = DecorationExclusionKind.Stair | DecorationExclusionKind.Navigation,
                Bounds = SpiralStairExclusion(in space, keepMin),
            };

            int windowHeight = plan.FloorHeight - 14;
            for (int i = 0; i < 3; i++)
            {
                int x = keepMin.x + keepSize.x / 4 + i * keepSize.x / 4 - 8;
                int index = 2 + i * 2;
                exclusions[index] = new DecorationExclusion
                {
                    Kind = DecorationExclusionKind.Gameplay,
                    Bounds = new DecorationBounds
                    {
                        Min = new int3(x - 4, floorY + 10, space.Bounds.Min.z),
                        MaxExclusive = new int3(x + 20, floorY + 12 + windowHeight, space.Bounds.Min.z + 12),
                    },
                };
                exclusions[index + 1] = new DecorationExclusion
                {
                    Kind = DecorationExclusionKind.Gameplay,
                    Bounds = new DecorationBounds
                    {
                        Min = new int3(x - 4, floorY + 10, space.Bounds.MaxExclusive.z - 12),
                        MaxExclusive = new int3(x + 20, floorY + 12 + windowHeight, space.Bounds.MaxExclusive.z),
                    },
                };
            }

            // Fireplace and mantel retained from the legacy bedchamber during the first migration.
            exclusions[8] = new DecorationExclusion
            {
                Kind = DecorationExclusionKind.Gameplay,
                Bounds = new DecorationBounds
                {
                    Min = new int3(keepMin.x + 4, space.Bounds.Min.y, cz + 18),
                    MaxExclusive = new int3(keepMin.x + 28, space.Bounds.MaxExclusive.y, cz + 68),
                },
            };

            // Reading/sitting cluster retained until chair/table scene recipes exist.
            exclusions[9] = new DecorationExclusion
            {
                Kind = DecorationExclusionKind.Gameplay | DecorationExclusionKind.Navigation,
                Bounds = new DecorationBounds
                {
                    Min = new int3(keepMin.x + 34, space.Bounds.Min.y, cz - 2),
                    MaxExclusive = new int3(keepMin.x + 72, space.Bounds.MaxExclusive.y, cz + 56),
                },
            };

            // Two existing wall hangings remain as secondary decoration for now.
            for (int side = -1; side <= 1; side += 2)
            {
                int hangingZ = cz + side * 48;
                int index = side < 0 ? 10 : 11;
                exclusions[index] = new DecorationExclusion
                {
                    Kind = DecorationExclusionKind.Gameplay,
                    Bounds = new DecorationBounds
                    {
                        Min = new int3(space.Bounds.MaxExclusive.x - 12, space.Bounds.Min.y, hangingZ - 15),
                        MaxExclusive = new int3(space.Bounds.MaxExclusive.x, space.Bounds.MaxExclusive.y, hangingZ + 15),
                    },
                };
            }

            return exclusions;
        }

        private static DecorationBounds GrandStairExclusion(
            in CastlePlan plan,
            in DecorationSpace space,
            int3 keepMin)
        {
            int grandX = plan.Centre.x - 68;
            int grandZ = keepMin.z + 28;
            const int grandWidth = 18;
            const int grandRun = 3;
            const int grandRise = 2;
            int grandSteps = plan.FloorHeight / grandRise;
            return new DecorationBounds
            {
                Min = new int3(grandX - 4, space.Bounds.Min.y, grandZ - 4),
                MaxExclusive = new int3(
                    grandX + grandWidth + 4,
                    space.Bounds.MaxExclusive.y,
                    grandZ + grandSteps * grandRun + 8),
            };
        }

        private static DecorationBounds SpiralStairExclusion(
            in DecorationSpace space,
            int3 keepMin)
        {
            int stairX = keepMin.x + 34;
            int stairZ = keepMin.z + 34;
            const int stairRadius = 22;
            const int clearance = 5;
            int radius = stairRadius + clearance;
            return new DecorationBounds
            {
                Min = new int3(stairX - radius, space.Bounds.Min.y, stairZ - radius),
                MaxExclusive = new int3(stairX + radius + 1, space.Bounds.MaxExclusive.y, stairZ + radius + 1),
            };
        }

        private static uint CastleStructureId(in CastlePlan plan)
        {
            unchecked
            {
                uint positionHash = (uint)(plan.Centre.x * 73856093) ^
                                    (uint)(plan.Centre.y * 19349663) ^
                                    (uint)(plan.Centre.z * 83492791);
                return DecorationSeed.Derive(plan.Seed, positionHash ^ 0xCA571Eu);
            }
        }
    }
}
