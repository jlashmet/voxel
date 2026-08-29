using System;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Canonical reference-driven town-architecture catalogue. Programs describe reusable construction
    /// language; voxel/presentation backends decide how to realize the semantic roles.
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

        public static TownArchitectureProgram Resolve(string styleId) => Resolve(styleId, CanonicalSeed(styleId));

        public static TownArchitectureProgram Resolve(string styleId, uint seed)
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
                        seed,
                        TownArchitectureSilhouette.PastoralTimberFrame,
                        TownArchitectureRoofForm.SteepGable,
                        TownArchitectureOpeningStyle.TimberFramed,
                        new TownArchitectureMaterialFamily(
                            "warm-fieldstone-and-cream-plaster",
                            "weathered-wood-shingle",
                            "exposed-oak-timber-frame",
                            "mossy-stone-path",
                            "dark-oak-and-iron",
                            "faded-ochre-red-blue"),
                        new[]
                        {
                            "recessed-window", "projecting-sill-lintel", "mullion", "timber-brace",
                            "timber-joint", "threshold", "door-canopy", "fascia", "ridge-cap",
                            "chimney-cap", "porch-post", "stone-course"
                        },
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
                        seed,
                        TownArchitectureSilhouette.CivicVerticalStone,
                        TownArchitectureRoofForm.TwinGable,
                        TownArchitectureOpeningStyle.OrderedStone,
                        new TownArchitectureMaterialFamily(
                            "dressed-pale-ashlar",
                            "dark-slate",
                            "carved-dark-timber",
                            "formal-stone-paving",
                            "iron-and-clean-plaster",
                            "royal-blue-burgundy-glass"),
                        new[]
                        {
                            "recessed-window", "projecting-sill-lintel", "mullion", "quoins",
                            "stone-course", "formal-door-frame", "threshold", "stone-step",
                            "fascia", "ridge-cap", "balcony-rail", "arched-passage"
                        },
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
                        seed,
                        TownArchitectureSilhouette.MoorlandLowStone,
                        TownArchitectureRoofForm.GableWithLeanTo,
                        TownArchitectureOpeningStyle.DeepWeatheredStone,
                        new TownArchitectureMaterialFamily(
                            "weathered-dark-fieldstone",
                            "coarse-slate-and-thatch",
                            "heavy-aged-timber",
                            "peat-earth-and-rough-stone",
                            "iron-moss-lichen",
                            "heather-purple-warm-amber"),
                        new[]
                        {
                            "deep-window-reveal", "projecting-sill-lintel", "heavy-shutter", "stone-course",
                            "corner-quoin", "threshold", "rough-door-frame", "lean-to-junction",
                            "fascia", "ridge-cap", "chimney-cap", "drainage-channel"
                        },
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
                        seed,
                        TownArchitectureSilhouette.RoyalFortified,
                        TownArchitectureRoofForm.FortifiedParapet,
                        TownArchitectureOpeningStyle.FortifiedReveal,
                        new TownArchitectureMaterialFamily(
                            "cut-stone-masonry",
                            "clay-tile-and-slate",
                            "dark-structural-timber",
                            "clean-civic-paving",
                            "iron-and-brass",
                            "crimson-royal-blue-gold"),
                        new[]
                        {
                            "deep-window-reveal", "arrow-slit-reveal", "projecting-sill-lintel", "stone-course",
                            "corner-quoin", "buttress-cap", "layered-coping", "crenellation",
                            "tower-wall-transition", "gate-frame", "gate-hardware", "access-stair"
                        },
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
                        seed,
                        TownArchitectureSilhouette.OrganicCanopy,
                        TownArchitectureRoofForm.OrganicCanopySpire,
                        TownArchitectureOpeningStyle.OrganicPointed,
                        new TownArchitectureMaterialFamily(
                            "pale-bark-and-light-stone",
                            "leaf-canopy-and-woven-fiber",
                            "root-and-branch-wood",
                            "moss-and-flower-ground",
                            "woven-flower-trim",
                            "aqua-lilac-crystal-glow"),
                        new[]
                        {
                            "pointed-recessed-window", "luminous-surround", "branch-bracket", "woven-rail",
                            "curved-step", "root-buttress", "canopy-rim", "spire-tip",
                            "hanging-lantern", "balcony-rail", "bridge-rail", "flower-corbels"
                        },
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
                        seed,
                        TownArchitectureSilhouette.TribalHeavyTimber,
                        TownArchitectureRoofForm.StockadeJagged,
                        TownArchitectureOpeningStyle.HeavySlit,
                        new TownArchitectureMaterialFamily(
                            "dark-basalt-and-packed-earth",
                            "heavy-plank-and-hide",
                            "rough-hewn-log",
                            "packed-earth-and-black-stone",
                            "blackened-iron-bone-rope",
                            "smoky-red-orange"),
                        new[]
                        {
                            "deep-slit", "heavy-window-surround", "log-brace", "timber-joint",
                            "threshold", "heavy-door-frame", "spike", "stockade-post",
                            "watch-platform", "forge-hood", "rack", "gate-crossbar"
                        },
                        "orc-village.png",
                        "orc-village-armor-shop.png",
                        "orc-village-weapon-shop.png",
                        "orc-village-magic-shop.png",
                        "orc-village-pub.png");

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(styleId), styleId,
                        "WorldBuilder has no registered town-architecture program for this style id.");
            }
        }

        public static uint CanonicalSeed(string styleId)
        {
            switch (styleId)
            {
                case WorldBuilderTownArchitectureIds.Kentridge: return 0x4B454E54u; // KENT
                case WorldBuilderTownArchitectureIds.Hightown: return 0x48494748u; // HIGH
                case WorldBuilderTownArchitectureIds.Moordell: return 0x4D4F4F52u; // MOOR
                case WorldBuilderTownArchitectureIds.Rossdam: return 0x524F5353u; // ROSS
                case WorldBuilderTownArchitectureIds.FairyVillage: return 0x46414952u; // FAIR
                case WorldBuilderTownArchitectureIds.OrcVillage: return 0x4F524353u; // ORCS
                default:
                    throw new ArgumentOutOfRangeException(nameof(styleId), styleId, "Unknown town architecture style.");
            }
        }

        public static string Describe(TownArchitectureProgram program)
        {
            if (program == null) throw new ArgumentNullException(nameof(program));
            return program.DisplayName + " seed=0x" + program.Seed.ToString("X8") +
                   " form=" + program.FormSignature +
                   " detailUnit=" + program.DetailUnitBlocks +
                   " details=" + program.DetailSignature;
        }

        private static TownArchitectureProgram Program(
            string styleId,
            string displayName,
            string sourcePrefix,
            uint seed,
            TownArchitectureSilhouette silhouette,
            TownArchitectureRoofForm roofForm,
            TownArchitectureOpeningStyle openingStyle,
            TownArchitectureMaterialFamily materialFamily,
            string[] detailVocabulary,
            params string[] evidence)
        {
            return new TownArchitectureProgram(
                styleId,
                displayName,
                sourcePrefix,
                seed,
                detailUnitBlocks: 1,
                silhouette,
                roofForm,
                openingStyle,
                in materialFamily,
                (TownArchitectureStructureRole[])s_RequiredRoles.Clone(),
                evidence,
                detailVocabulary);
        }
    }
}
