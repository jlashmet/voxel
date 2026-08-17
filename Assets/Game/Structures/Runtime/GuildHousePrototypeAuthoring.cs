using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Baseline voxel authoring for guild-house shells. The first goal is to make the building-scale
    /// programs visible and furnishable; signature roofs/curves can be upgraded independently later.
    /// </summary>
    public static class GuildHousePrototypeAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, in GuildHousePrototype prototype)
        {
            if (authoring == null)
                throw new System.ArgumentNullException(nameof(authoring));
            if (!prototype.IsWellFormed)
                throw new System.ArgumentException("Guild-house prototype is not well formed.", nameof(prototype));

            DecorationRegionProfile region = DecorationRegionProfiles.Resolve(prototype.Region);
            byte primary = region.IsWellFormed ? region.PrimaryMaterial : GameMaterialIds.Wood;
            byte secondary = region.IsWellFormed ? region.SecondaryMaterial : GameMaterialIds.MasonrySmall;
            byte accent = region.IsWellFormed ? region.AccentMaterial : GameMaterialIds.Cloth;

            switch (prototype.SpatialPlan.ShellStyle)
            {
                case GuildHouseShellStyle.Tower:
                    AuthorTower(authoring, in prototype.SpatialPlan, primary, secondary, accent);
                    break;
                case GuildHouseShellStyle.Lodge:
                    AuthorLodge(authoring, in prototype.SpatialPlan, primary, secondary, accent);
                    break;
                default:
                    AuthorHall(authoring, in prototype.SpatialPlan, primary, secondary, accent);
                    break;
            }

            GuildHouseSecretAccessAuthoring.Author(authoring, in prototype.SpatialPlan, prototype.Region);
        }

        private static void AuthorHall(
            IStructureAuthoringSession authoring,
            in GuildHouseSpatialPlan plan,
            byte primary,
            byte secondary,
            byte accent)
        {
            for (int floor = 0; floor < plan.FloorCount; floor++)
            {
                int y = plan.Origin.y + floor * plan.FloorHeight;
                AuthorFloor(authoring, plan.Origin.x, y, plan.Origin.z, plan.Width, plan.Depth, secondary);
                AuthorPerimeter(authoring, plan.Origin.x, y, plan.Origin.z,
                    plan.Width, plan.Depth, plan.FloorHeight, primary, floor == 0);
            }
            int roofY = plan.Origin.y + plan.FloorCount * plan.FloorHeight;
            authoring.Box(new int3(plan.Origin.x, roofY, plan.Origin.z),
                new int3(plan.Width, 3, plan.Depth), secondary);
            AuthorEntranceSign(authoring, in plan, accent);
        }

        private static void AuthorTower(
            IStructureAuthoringSession authoring,
            in GuildHouseSpatialPlan plan,
            byte primary,
            byte secondary,
            byte accent)
        {
            AuthorHall(authoring, in plan, primary, secondary, accent);
            int top = plan.Origin.y + plan.FloorCount * plan.FloorHeight + 3;
            const int crenel = 5;
            for (int x = plan.Origin.x; x < plan.Origin.x + plan.Width; x += 12)
            {
                authoring.Box(new int3(x, top, plan.Origin.z), new int3(crenel, 7, 4), accent);
                authoring.Box(new int3(x, top, plan.Origin.z + plan.Depth - 4), new int3(crenel, 7, 4), accent);
            }
            for (int z = plan.Origin.z + 8; z < plan.Origin.z + plan.Depth - 8; z += 12)
            {
                authoring.Box(new int3(plan.Origin.x, top, z), new int3(4, 7, crenel), accent);
                authoring.Box(new int3(plan.Origin.x + plan.Width - 4, top, z), new int3(4, 7, crenel), accent);
            }
        }

        private static void AuthorLodge(
            IStructureAuthoringSession authoring,
            in GuildHouseSpatialPlan plan,
            byte primary,
            byte secondary,
            byte accent)
        {
            // Druid lodge: broad low shell with a deliberately open central roof/courtyard strip.
            int y = plan.Origin.y;
            AuthorFloor(authoring, plan.Origin.x, y, plan.Origin.z, plan.Width, plan.Depth, secondary);
            AuthorPerimeter(authoring, plan.Origin.x, y, plan.Origin.z,
                plan.Width, plan.Depth, plan.FloorHeight, primary, true);

            int gap = 18;
            int roofY = y + plan.FloorHeight;
            int left = (plan.Width - gap) / 2;
            authoring.Box(new int3(plan.Origin.x, roofY, plan.Origin.z),
                new int3(left, 3, plan.Depth), primary);
            authoring.Box(new int3(plan.Origin.x + left + gap, roofY, plan.Origin.z),
                new int3(plan.Width - left - gap, 3, plan.Depth), primary);

            // Living-root/tree posts are intentionally chunky for the breadth-first pass.
            for (int z = plan.Origin.z + 10; z < plan.Origin.z + plan.Depth - 8; z += 18)
            {
                authoring.Box(new int3(plan.Origin.x + 5, y + 1, z), new int3(6, plan.FloorHeight - 2, 6), accent);
                authoring.Box(new int3(plan.Origin.x + plan.Width - 11, y + 1, z), new int3(6, plan.FloorHeight - 2, 6), accent);
            }
            AuthorEntranceSign(authoring, in plan, accent);
        }

        private static void AuthorFloor(
            IStructureAuthoringSession authoring,
            int x,
            int y,
            int z,
            int width,
            int depth,
            byte material)
        {
            authoring.Box(new int3(x, y, z), new int3(width, 2, depth), material);
        }

        private static void AuthorPerimeter(
            IStructureAuthoringSession authoring,
            int x,
            int y,
            int z,
            int width,
            int depth,
            int height,
            byte material,
            bool entrance)
        {
            int wallY = y + 2;
            int wallH = height - 2;
            authoring.Box(new int3(x, wallY, z), new int3(3, wallH, depth), material);
            authoring.Box(new int3(x + width - 3, wallY, z), new int3(3, wallH, depth), material);
            authoring.Box(new int3(x, wallY, z + depth - 3), new int3(width, wallH, 3), material);

            if (!entrance)
            {
                authoring.Box(new int3(x, wallY, z), new int3(width, wallH, 3), material);
                return;
            }

            int doorWidth = 10;
            int doorHeight = math.min(18, wallH - 2);
            int leftWidth = (width - doorWidth) / 2;
            authoring.Box(new int3(x, wallY, z), new int3(leftWidth, wallH, 3), material);
            authoring.Box(new int3(x + leftWidth + doorWidth, wallY, z),
                new int3(width - leftWidth - doorWidth, wallH, 3), material);
            authoring.Box(new int3(x + leftWidth, wallY + doorHeight, z),
                new int3(doorWidth, wallH - doorHeight, 3), material);
        }

        private static void AuthorEntranceSign(
            IStructureAuthoringSession authoring,
            in GuildHouseSpatialPlan plan,
            byte material)
        {
            int x = plan.Origin.x + plan.Width / 2 - 6;
            int y = plan.Origin.y + 21;
            int z = plan.Origin.z - 1;
            authoring.Box(new int3(x, y, z), new int3(12, 7, 2), material);
        }
    }
}
