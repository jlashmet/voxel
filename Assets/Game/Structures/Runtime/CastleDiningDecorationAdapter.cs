using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Exposes the ground-floor great hall dining zone to the generic decoration system while
    /// reserving existing circulation, fireplace, and throne geometry.
    /// </summary>
    public static class CastleDiningDecorationAdapter
    {
        public const uint SpaceDiscriminator = 0xD1A1CA57u;
        private const int InnerWallOffset = 8;
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
            if (plan.Floors < 1 || plan.FloorHeight <= CeilingReserve + 16)
                return false;

            space = CreateSpace(in plan);
            context = CreateContext(in plan, space.SpaceId);
            exclusions = CreateExclusions(in plan, in space);
            return DiningSceneResolver.TryResolve(
                in space, in context, exclusions, DiningLongAxis.X, out placements);
        }

        public static DecorationSpace CreateSpace(in CastlePlan plan)
        {
            int3 keepMin = CastleKeepCoreAuthoring.Minimum(in plan);
            int3 keepSize = CastleKeepCoreAuthoring.Size(in plan);
            int floorY = plan.Centre.y + plan.PlateauHeight;
            uint structureId = CastleStructureId(in plan);
            return new DecorationSpace
            {
                SpaceId = DecorationSeed.Derive(structureId, SpaceDiscriminator),
                Kind = DecorationSpaceKind.DiningRoom,
                Bounds = new DecorationBounds
                {
                    Min = new int3(
                        keepMin.x + InnerWallOffset,
                        floorY + 1,
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
            uint variation = DecorationSeed.Derive(plan.Seed, 0xD1A1571Eu);
            return new DecorationContext
            {
                WorldSeed = plan.Seed,
                StructureId = CastleStructureId(in plan),
                SpaceId = spaceId,
                StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Courtly, variation),
                StructureKind = DecorationStructureKind.Castle,
                SpaceKind = DecorationSpaceKind.DiningRoom,
                Wealth = DecorationWealthTier.Noble,
                Condition = DecorationConditionTier.Maintained,
                Environment = DecorationEnvironmentTags.Interior,
            };
        }

        public static DecorationExclusion[] CreateExclusions(
            in CastlePlan plan,
            in DecorationSpace space)
        {
            int3 keepMin = CastleKeepCoreAuthoring.Minimum(in plan);
            int3 keepSize = CastleKeepCoreAuthoring.Size(in plan);
            int cx = keepMin.x + keepSize.x / 2;
            int cz = keepMin.z + keepSize.z / 2;
            int grandX = plan.Centre.x - 68;
            int grandZ = keepMin.z + 28;
            const int grandWidth = 18;
            const int grandRun = 3;
            const int grandRise = 2;
            int grandSteps = plan.FloorHeight / grandRise;
            int stairX = keepMin.x + 34;
            int stairZ = keepMin.z + 34;
            const int spiralRadius = 27;

            return new[]
            {
                new DecorationExclusion
                {
                    Kind = DecorationExclusionKind.Door | DecorationExclusionKind.Navigation,
                    Bounds = new DecorationBounds
                    {
                        Min = new int3(cx - 12, space.Bounds.Min.y, space.Bounds.Min.z),
                        MaxExclusive = new int3(cx + 12, space.Bounds.MaxExclusive.y, cz - 26),
                    },
                },
                new DecorationExclusion
                {
                    Kind = DecorationExclusionKind.Stair | DecorationExclusionKind.Navigation,
                    Bounds = new DecorationBounds
                    {
                        Min = new int3(grandX - 4, space.Bounds.Min.y, grandZ - 4),
                        MaxExclusive = new int3(
                            grandX + grandWidth + 4,
                            space.Bounds.MaxExclusive.y,
                            grandZ + grandSteps * grandRun + 8),
                    },
                },
                new DecorationExclusion
                {
                    Kind = DecorationExclusionKind.Stair | DecorationExclusionKind.Navigation,
                    Bounds = new DecorationBounds
                    {
                        Min = new int3(stairX - spiralRadius, space.Bounds.Min.y, stairZ - spiralRadius),
                        MaxExclusive = new int3(stairX + spiralRadius + 1, space.Bounds.MaxExclusive.y, stairZ + spiralRadius + 1),
                    },
                },
                new DecorationExclusion
                {
                    Kind = DecorationExclusionKind.Gameplay | DecorationExclusionKind.Hazard,
                    Bounds = new DecorationBounds
                    {
                        Min = new int3(keepMin.x + 4, space.Bounds.Min.y, cz - 30),
                        MaxExclusive = new int3(keepMin.x + 30, space.Bounds.MaxExclusive.y, cz + 30),
                    },
                },
                new DecorationExclusion
                {
                    Kind = DecorationExclusionKind.Gameplay,
                    Bounds = new DecorationBounds
                    {
                        Min = new int3(cx + 52, space.Bounds.Min.y, cz - 24),
                        MaxExclusive = new int3(cx + 78, space.Bounds.MaxExclusive.y, cz + 24),
                    },
                },
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
