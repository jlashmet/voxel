using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
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
            byte magic = region.IsWellFormed && region.MagicMaterial != GameMaterialIds.Empty
                ? region.MagicMaterial
                : GameMaterialIds.LitWindow;

            switch (prototype.SpatialPlan.ShellStyle)
            {
                case GuildHouseShellStyle.Tower:
                    AuthorTower(authoring, in prototype.SpatialPlan, primary, secondary, accent, magic);
                    break;
                case GuildHouseShellStyle.Lodge:
                    AuthorLodge(authoring, in prototype.SpatialPlan, primary, secondary, accent, magic);
                    break;
                default:
                    AuthorHall(authoring, in prototype.SpatialPlan, primary, secondary, accent, magic);
                    break;
            }

            GuildHouseSecretAccessAuthoring.Author(authoring, in prototype.SpatialPlan, prototype.Region);
            GuildHouseExteriorAuthoring.Author(authoring, in prototype);
        }

        private static void AuthorHall(IStructureAuthoringSession authoring, in GuildHouseSpatialPlan plan,
            byte primary, byte secondary, byte accent, byte magic)
        {
            for (int floor = 0; floor < plan.FloorCount; floor++)
            {
                int y = plan.Origin.y + floor * plan.FloorHeight;
                AuthorFloor(authoring, plan.Origin.x, y, plan.Origin.z, plan.Width, plan.Depth, secondary);
                AuthorPerimeter(authoring, plan.Origin.x, y, plan.Origin.z,
                    plan.Width, plan.Depth, plan.FloorHeight, primary, floor == 0);
            }
            int roofY = plan.Origin.y + plan.FloorCount * plan.FloorHeight;
            authoring.Box(new int3(plan.Origin.x, roofY, plan.Origin.z), new int3(plan.Width, 3, plan.Depth), secondary);

            // Public hall/church-like guild houses need a readable roof silhouette in every consumer,
            // not a showcase-only cap. Hidden dens stay intentionally plain and towers own crenels.
            if (plan.ShellStyle == GuildHouseShellStyle.Hall ||
                plan.ShellStyle == GuildHouseShellStyle.ChapelHouse)
                AuthorSteppedGable(authoring, in plan, roofY + 3, primary, accent);

            if (plan.ShellStyle != GuildHouseShellStyle.HiddenDen)
                AuthorFacadeArticulation(authoring, in plan, secondary, accent, magic);
            AuthorEntranceSign(authoring, in plan, accent);
        }

        private static void AuthorTower(IStructureAuthoringSession authoring, in GuildHouseSpatialPlan plan,
            byte primary, byte secondary, byte accent, byte magic)
        {
            AuthorHall(authoring, in plan, primary, secondary, accent, magic);
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

        private static void AuthorLodge(IStructureAuthoringSession authoring, in GuildHouseSpatialPlan plan,
            byte primary, byte secondary, byte accent, byte magic)
        {
            int y = plan.Origin.y;
            AuthorFloor(authoring, plan.Origin.x, y, plan.Origin.z, plan.Width, plan.Depth, secondary);
            AuthorPerimeter(authoring, plan.Origin.x, y, plan.Origin.z,
                plan.Width, plan.Depth, plan.FloorHeight, primary, true);

            int gap = 18;
            int roofY = y + plan.FloorHeight;
            int left = (plan.Width - gap) / 2;
            authoring.Box(new int3(plan.Origin.x, roofY, plan.Origin.z), new int3(left, 3, plan.Depth), primary);
            authoring.Box(new int3(plan.Origin.x + left + gap, roofY, plan.Origin.z),
                new int3(plan.Width - left - gap, 3, plan.Depth), primary);

            for (int z = plan.Origin.z + 10; z < plan.Origin.z + plan.Depth - 8; z += 18)
            {
                authoring.Box(new int3(plan.Origin.x + 5, y + 1, z), new int3(6, plan.FloorHeight - 2, 6), accent);
                authoring.Box(new int3(plan.Origin.x + plan.Width - 11, y + 1, z), new int3(6, plan.FloorHeight - 2, 6), accent);
            }
            AuthorFacadeArticulation(authoring, in plan, secondary, accent, magic);
            AuthorEntranceSign(authoring, in plan, accent);
        }

        private static void AuthorSteppedGable(IStructureAuthoringSession authoring,
            in GuildHouseSpatialPlan plan, int baseY, byte roofMaterial, byte ridgeMaterial)
        {
            int depth = math.max(12, plan.Depth - 4);
            int inset = 2;
            int tier = 0;
            while (tier < 14)
            {
                int width = plan.Width - inset * 2;
                if (width < 12) break;
                authoring.Box(
                    new int3(plan.Origin.x + inset, baseY + tier * 2, plan.Origin.z + 2),
                    new int3(width, 2, depth),
                    roofMaterial);
                inset += 4;
                tier++;
            }

            int ridgeY = baseY + math.max(0, tier - 1) * 2;
            authoring.Box(
                new int3(plan.Origin.x + plan.Width / 2 - 2, ridgeY + 2, plan.Origin.z + 2),
                new int3(4, 3, depth),
                ridgeMaterial);
        }

        private static void AuthorFacadeArticulation(IStructureAuthoringSession authoring,
            in GuildHouseSpatialPlan plan, byte frameMaterial, byte accentMaterial, byte windowMaterial)
        {
            const int doorWidth = 12;
            const int doorHeight = 24;
            int wallY = plan.Origin.y + 2;
            int frontZ = plan.Origin.z - 2;
            int doorLeft = plan.Origin.x + (plan.Width - doorWidth) / 2;

            // Door surround and shallow canopy sit outside the perimeter, preserving the production
            // walk-through opening while giving the front elevation a clear visual anchor.
            authoring.Box(new int3(doorLeft - 4, wallY, frontZ),
                new int3(4, doorHeight, 2), frameMaterial);
            authoring.Box(new int3(doorLeft + doorWidth, wallY, frontZ),
                new int3(4, doorHeight, 2), frameMaterial);
            authoring.Box(new int3(doorLeft - 4, wallY + doorHeight, frontZ),
                new int3(doorWidth + 8, 4, 2), frameMaterial);
            authoring.Box(new int3(doorLeft - 7, wallY + doorHeight + 3, plan.Origin.z - 7),
                new int3(doorWidth + 14, 2, 9), accentMaterial);

            // Region-driven lit/magic panels make floor count and facade scale legible from the
            // exterior without carving new openings or inventing showcase-only materials.
            int windowWidth = math.max(6, math.min(10, plan.Width / 10));
            int leftX = plan.Origin.x + plan.Width / 4 - windowWidth / 2;
            int rightX = plan.Origin.x + (plan.Width * 3) / 4 - windowWidth / 2;
            for (int floor = 0; floor < plan.FloorCount; floor++)
            {
                int windowY = plan.Origin.y + floor * plan.FloorHeight + 10;
                int windowHeight = math.max(6, math.min(10, plan.FloorHeight - 14));
                authoring.Box(new int3(leftX, windowY, plan.Origin.z - 1),
                    new int3(windowWidth, windowHeight, 1), windowMaterial);
                authoring.Box(new int3(rightX, windowY, plan.Origin.z - 1),
                    new int3(windowWidth, windowHeight, 1), windowMaterial);
            }
        }

        private static void AuthorFloor(IStructureAuthoringSession authoring,
            int x, int y, int z, int width, int depth, byte material)
        {
            authoring.Box(new int3(x, y, z), new int3(width, 2, depth), material);
        }

        private static void AuthorPerimeter(IStructureAuthoringSession authoring,
            int x, int y, int z, int width, int depth, int height, byte material, bool entrance)
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

            // The character is 1.8 m tall and 0.6 m wide, and a voxel is 10 cm. A door of exactly
            // 18 was therefore exactly head height — no clearance at all, and the collision test
            // samples the capsule's top voxel, so walking in depended on standing on the right
            // side of a rounding boundary. 24 x 12 is a door with room to walk through.
            int doorWidth = 12;
            int doorHeight = math.min(24, wallH - 2);
            int leftWidth = (width - doorWidth) / 2;
            authoring.Box(new int3(x, wallY, z), new int3(leftWidth, wallH, 3), material);
            authoring.Box(new int3(x + leftWidth + doorWidth, wallY, z),
                new int3(width - leftWidth - doorWidth, wallH, 3), material);
            authoring.Box(new int3(x + leftWidth, wallY + doorHeight, z),
                new int3(doorWidth, wallH - doorHeight, 3), material);
        }

        private static void AuthorEntranceSign(IStructureAuthoringSession authoring,
            in GuildHouseSpatialPlan plan, byte material)
        {
            if (plan.ShellStyle == GuildHouseShellStyle.HiddenDen)
                return;
            int x = plan.Origin.x + plan.Width / 2 - 6;
            int y = plan.Origin.y + 21;
            int z = plan.Origin.z - 1;
            authoring.Box(new int3(x, y, z), new int3(12, 7, 2), material);
        }
    }
}