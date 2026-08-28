using System;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Canonical reference-driven town-architecture catalogue. These programs describe reusable
    /// construction language; voxel/presentation backends decide how to realize the semantic roles.
    /// </summary>
    public static class WorldBuilderTownArchitecture
    {
        private static readonly TownArchitectureStructureRole[] s_RequiredRoles =
        {
            TownArchitectureStructureRole.Residential,
            TownArchitectureStructureRole.Commercial,
            TownArchitectureStructureRole.CivicCommunal,
            TownArchitectureStructureRole.LandmarkInfrastructure,
        };

        public static string[] AllStyleIds => new[]
        {
            WorldBuilderTownArchitectureIds.Kentridge,
            WorldBuilderTownArchitectureIds.Hightown,
            WorldBuilderTownArchitectureIds.Moordell,
            WorldBuilderTownArchitectureIds.Rossdam,
            WorldBuilderTownArchitectureIds.FairyVillage,
            WorldBuilderTownArchitectureIds.OrcVillage,
        };

        public static TownArchitectureProgram Resolve(string styleId)
        {
            if (string.IsNullOrWhiteSpace(styleId))
                throw new ArgumentException("A town architecture style id is required.", nameof(styleId));

            switch (styleId)
            {
                case WorldBuilderTownArchitectureIds.Kentridge:
                    return Program(
                        styleId,
                        "Kentridge",
                        "kentridge",
                        TownArchitectureSilhouette.PastoralTimberFrame,
                        new TownArchitectureMaterialFamily(
                            "warm-fieldstone-and-cream-plaster",
                            "weathered-wood-shingle",
                            "exposed-oak-timber-frame",
                            "mossy-stone-path",
                            "dark-oak-and-iron",
                            "faded-ochre-red-blue"),
                        "kentridge.png",
                        "kentridge-church.png",
                        "kentridge-well.png",
                        "kentridge-warehouse.png",
                        "kentridge-inn.png");

                case WorldBuilderTownArchitectureIds.Hightown:
                    return Program(
                        styleId,
                        "Hightown",
                        "hightown",
                        TownArchitectureSilhouette.CivicVerticalStone,
                        new TownArchitectureMaterialFamily(
                            "dressed-pale-ashlar",
                            "dark-slate",
                            "carved-dark-timber",
                            "formal-stone-paving",
                            "iron-and-clean-plaster",
                            "royal-blue-burgundy-glass"),
                        "hightown.png",
                        "hightown-church.png",
                        "hightown-mayor-house.png",
                        "hightown-under-church.png",
                        "hightown-cave.png");

                case WorldBuilderTownArchitectureIds.Moordell:
                    return Program(
                        styleId,
                        "Moordell",
                        "moordell",
                        TownArchitectureSilhouette.MoorlandLowStone,
                        new TownArchitectureMaterialFamily(
                            "weathered-dark-fieldstone",
                            "coarse-slate-and-thatch",
                            "heavy-aged-timber",
                            "peat-earth-and-rough-stone",
                            "iron-moss-lichen",
                            "heather-purple-warm-amber"),
                        "moordell.png",
                        "moordell-building1.png",
                        "moordell-inn.png",
                        "moordell-grave.png",
                        "moordell-pub.png");

                case WorldBuilderTownArchitectureIds.Rossdam:
                    return Program(
                        styleId,
                        "Rossdam",
                        "rossdam",
                        TownArchitectureSilhouette.RoyalFortified,
                        new TownArchitectureMaterialFamily(
                            "cut-stone-masonry",
                            "clay-tile-and-slate",
                            "dark-structural-timber",
                            "clean-civic-paving",
                            "iron-and-brass",
                            "crimson-royal-blue-gold"),
                        "rossdam.png",
                        "rossdam-king-chamber.png",
                        "rossdam-armor-shop.png",
                        "rossdam-magic-shop.png",
                        "rossdam-weapon-shop.png");

                case WorldBuilderTownArchitectureIds.FairyVillage:
                    return Program(
                        styleId,
                        "Fairy Village",
                        "fairy-village",
                        TownArchitectureSilhouette.OrganicCanopy,
                        new TownArchitectureMaterialFamily(
                            "pale-bark-and-light-stone",
                            "leaf-canopy-and-woven-fiber",
                            "root-and-branch-wood",
                            "moss-and-flower-ground",
                            "woven-flower-trim",
                            "aqua-lilac-crystal-glow"),
                        "fairy-village.png",
                        "fairy-village-treehouse.png",
                        "fairy-village-cave.png",
                        "fairy-village-inn.png",
                        "fairy-village-pub.png");

                case WorldBuilderTownArchitectureIds.OrcVillage:
                    return Program(
                        styleId,
                        "Orc Village",
                        "orc-village",
                        TownArchitectureSilhouette.TribalHeavyTimber,
                        new TownArchitectureMaterialFamily(
                            "dark-basalt-and-packed-earth",
                            "heavy-plank-and-hide",
                            "rough-hewn-log",
                            "packed-earth-and-black-stone",
                            "blackened-iron-bone-rope",
                            "smoky-red-orange"),
                        "orc-village.png",
                        "orc-village-armor-shop.png",
                        "orc-village-weapon-shop.png",
                        "orc-village-magic-shop.png",
                        "orc-village-pub.png");

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(styleId),
                        styleId,
                        "WorldBuilder has no registered town-architecture program for this style id.");
            }
        }

        private static TownArchitectureProgram Program(
            string styleId,
            string displayName,
            string sourcePrefix,
            TownArchitectureSilhouette silhouette,
            TownArchitectureMaterialFamily materialFamily,
            params string[] evidence)
        {
            return new TownArchitectureProgram(
                styleId,
                displayName,
                sourcePrefix,
                silhouette,
                in materialFamily,
                (TownArchitectureStructureRole[])s_RequiredRoles.Clone(),
                evidence);
        }
    }
}
