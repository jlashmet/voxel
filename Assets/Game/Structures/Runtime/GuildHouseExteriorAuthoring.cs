using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Baseline guild-specific exterior dressing. These are deliberately simple voxel compositions;
    /// later settlement/exterior adapters can replace them with richer yards, gardens and street seams.
    /// Secretive guilds intentionally avoid loud public heraldry.
    /// </summary>
    public static class GuildHouseExteriorAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, in GuildHousePrototype prototype)
        {
            if (authoring == null || !prototype.IsWellFormed) return;
            DecorationRegionProfile profile = DecorationRegionProfiles.Resolve(prototype.Region);
            byte primary = profile.IsWellFormed ? profile.PrimaryMaterial : GameMaterialIds.Wood;
            byte secondary = profile.IsWellFormed ? profile.SecondaryMaterial : GameMaterialIds.MasonrySmall;
            byte accent = profile.IsWellFormed ? profile.AccentMaterial : GameMaterialIds.Cloth;
            byte magic = profile.IsWellFormed ? profile.MagicMaterial : GameMaterialIds.LitWindow;
            GuildHouseSpatialPlan plan = prototype.SpatialPlan;
            int cx = plan.Origin.x + plan.Width / 2;
            int frontZ = plan.Origin.z - 5;
            int y = plan.Origin.y + 2;

            switch (plan.Kind)
            {
                case GuildHouseKind.Adventurers:
                    // Exterior quest board and supply stack.
                    authoring.Box(new int3(cx - 20, y + 6, frontZ), new int3(14, 12, 2), primary);
                    authoring.Box(new int3(cx + 13, y, frontZ + 1), new int3(9, 7, 9), secondary);
                    break;

                case GuildHouseKind.Wizards:
                    // Crystal-lit entry pylons and a small floating-looking seal block.
                    Pylon(authoring, cx - 15, y, frontZ, primary, magic);
                    Pylon(authoring, cx + 12, y, frontZ, primary, magic);
                    authoring.Box(new int3(cx - 3, y + 19, plan.Origin.z - 2), new int3(6, 6, 2), magic);
                    break;

                case GuildHouseKind.Knights:
                    // Chivalric heraldry and horse hitching, not modern military infrastructure.
                    BannerPost(authoring, cx - 18, y, frontZ, primary, accent);
                    BannerPost(authoring, cx + 15, y, frontZ, primary, accent);
                    authoring.Box(new int3(cx - 24, y, frontZ + 10), new int3(48, 3, 3), primary);
                    break;

                case GuildHouseKind.Assassins:
                    // Deliberately mundane: plain lintel and ordinary crates, no guild crest.
                    authoring.Box(new int3(cx - 8, y + 16, plan.Origin.z - 1), new int3(16, 3, 2), primary);
                    authoring.Box(new int3(cx + 22, y, plan.Origin.z + 3), new int3(8, 7, 8), primary);
                    authoring.Box(new int3(cx + 30, y, plan.Origin.z + 5), new int3(6, 5, 6), primary);
                    break;

                case GuildHouseKind.Druids:
                    // Standing-stone threshold with living/enchanted accents.
                    authoring.Box(new int3(cx - 18, y, frontZ), new int3(7, 17, 7), secondary);
                    authoring.Box(new int3(cx + 11, y, frontZ), new int3(7, 17, 7), secondary);
                    authoring.Box(new int3(cx - 13, y + 13, frontZ), new int3(26, 5, 7), primary);
                    authoring.Box(new int3(cx - 2, y + 18, frontZ + 2), new int3(4, 4, 3), magic);
                    break;

                case GuildHouseKind.Thieves:
                    // Low-key entry: crooked trade sign and stacked goods rather than formal heraldry.
                    authoring.Box(new int3(cx - 22, y + 8, frontZ), new int3(10, 7, 2), accent);
                    authoring.Box(new int3(cx + 16, y, frontZ + 2), new int3(12, 6, 8), primary);
                    break;

                case GuildHouseKind.Clerics:
                    Pylon(authoring, cx - 16, y, frontZ, secondary, accent);
                    Pylon(authoring, cx + 13, y, frontZ, secondary, accent);
                    authoring.Box(new int3(cx - 4, y, frontZ + 2), new int3(8, 6, 8), secondary);
                    break;

                case GuildHouseKind.Rangers:
                    // Hitching rail and rough field-gear rack.
                    authoring.Box(new int3(cx - 26, y + 5, frontZ + 3), new int3(52, 3, 3), primary);
                    for (int x = cx - 24; x <= cx + 24; x += 24)
                        authoring.Box(new int3(x, y, frontZ + 3), new int3(3, 10, 3), primary);
                    authoring.Box(new int3(cx + 27, y, frontZ + 3), new int3(9, 12, 5), secondary);
                    break;

                case GuildHouseKind.Bards:
                    // Textile marquee and notice/song board.
                    authoring.Box(new int3(cx - 22, y + 17, plan.Origin.z - 3), new int3(44, 3, 8), accent);
                    authoring.Box(new int3(cx - 20, y, frontZ), new int3(3, 19, 3), primary);
                    authoring.Box(new int3(cx + 17, y, frontZ), new int3(3, 19, 3), primary);
                    authoring.Box(new int3(cx + 25, y + 7, frontZ), new int3(12, 10, 2), accent);
                    break;

                case GuildHouseKind.Alchemists:
                    // Tall exhaust/chimney stack plus reagent delivery shelf.
                    authoring.Box(new int3(plan.Origin.x + plan.Width - 12, y, plan.Origin.z + 8),
                        new int3(7, plan.FloorHeight + 18, 7), secondary);
                    authoring.Box(new int3(cx + 18, y, frontZ + 2), new int3(14, 9, 7), primary);
                    break;
            }
        }

        private static void Pylon(IStructureAuthoringSession a, int x, int y, int z, byte baseMat, byte glow)
        {
            a.Box(new int3(x, y, z), new int3(6, 16, 6), baseMat);
            a.Box(new int3(x + 1, y + 16, z + 1), new int3(4, 5, 4), glow);
        }

        private static void BannerPost(IStructureAuthoringSession a, int x, int y, int z, byte wood, byte cloth)
        {
            a.Box(new int3(x, y, z), new int3(3, 22, 3), wood);
            a.Box(new int3(x + 3, y + 12, z), new int3(10, 9, 2), cloth);
        }
    }
}
