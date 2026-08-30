using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Extensible catalogue of town-architecture definitions. Built-ins are ordinary registrations;
    /// additional styles can be registered as data without changing this resolver or voxel backend dispatch.
    /// </summary>
    public static class WorldBuilderTownArchitecture
    {
        private static readonly object s_Gate = new();
        private static readonly Dictionary<string, TownArchitectureDefinition> s_Definitions =
            new(StringComparer.Ordinal);
        private static readonly List<string> s_Order = new();

        static WorldBuilderTownArchitecture()
        {
            RegisterBuiltIns();
        }

        public static string[] AllStyleIds
        {
            get
            {
                lock (s_Gate) return s_Order.ToArray();
            }
        }

        public static bool Register(TownArchitectureDefinition definition, bool replace = false)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            lock (s_Gate)
            {
                bool exists = s_Definitions.ContainsKey(definition.StyleId);
                if (exists && !replace) return false;
                s_Definitions[definition.StyleId] = definition;
                if (!exists) s_Order.Add(definition.StyleId);
                return true;
            }
        }

        public static bool Unregister(string styleId)
        {
            if (string.IsNullOrWhiteSpace(styleId)) return false;
            lock (s_Gate)
            {
                if (!s_Definitions.Remove(styleId)) return false;
                s_Order.Remove(styleId);
                return true;
            }
        }

        public static bool IsRegistered(string styleId)
        {
            if (string.IsNullOrWhiteSpace(styleId)) return false;
            lock (s_Gate) return s_Definitions.ContainsKey(styleId);
        }

        public static TownArchitectureProgram Resolve(string styleId) => Resolve(styleId, CanonicalSeed(styleId));

        public static TownArchitectureProgram Resolve(string styleId, uint seed)
        {
            if (string.IsNullOrWhiteSpace(styleId))
                throw new ArgumentException("A town architecture style id is required.", nameof(styleId));
            TownArchitectureDefinition definition;
            lock (s_Gate)
            {
                if (!s_Definitions.TryGetValue(styleId, out definition))
                    throw new ArgumentOutOfRangeException(nameof(styleId), styleId,
                        "WorldBuilder has no registered town-architecture definition for this style id.");
            }
            return definition.CreateProgram(seed);
        }

        public static uint CanonicalSeed(string styleId)
        {
            if (string.IsNullOrWhiteSpace(styleId))
                throw new ArgumentException("A town architecture style id is required.", nameof(styleId));
            lock (s_Gate)
            {
                if (!s_Definitions.TryGetValue(styleId, out TownArchitectureDefinition definition))
                    throw new ArgumentOutOfRangeException(nameof(styleId), styleId, "Unknown town architecture style.");
                return definition.CanonicalSeed;
            }
        }

        public static string Describe(TownArchitectureProgram program)
        {
            if (program == null) throw new ArgumentNullException(nameof(program));
            return program.DisplayName + " seed=0x" + program.Seed.ToString("X8") +
                   " form=" + program.FormSignature +
                   " composition=" + program.Composition.Signature +
                   " detailUnit=" + program.DetailUnitBlocks +
                   " details=" + program.DetailSignature;
        }

        private static TownArchitectureRoleRecipe Role(
            TownArchitectureStructureRole role, TownArchitectureMassing massing,
            TownArchitectureRoofForm roof, TownArchitectureOpeningStyle opening,
            TownArchitectureDetailFeatures features, int width, int depth, int wall, int roofHeight) =>
            new(role, massing, roof, opening, features, width, depth, wall, roofHeight);

        private static TownArchitectureComposition Composition(
            TownArchitectureRoleRecipe residential,
            TownArchitectureRoleRecipe commercial,
            TownArchitectureRoleRecipe civic,
            TownArchitectureRoleRecipe landmark) =>
            new(residential, commercial, civic, landmark);

        private static TownArchitectureDefinition Definition(
            string id, string display, string prefix, uint seed,
            TownArchitectureSilhouette silhouette, TownArchitectureRoofForm roof,
            TownArchitectureOpeningStyle opening, TownArchitectureMaterialFamily materials,
            TownArchitectureComposition composition, string[] details, params string[] evidence) =>
            new(id, display, prefix, seed, 1, silhouette, roof, opening, in materials, composition, evidence, details);

        private static void RegisterBuiltIns()
        {
            Register(Definition(
                WorldBuilderTownArchitectureIds.Kentridge, "Kentridge", "kentridge", 0x4B454E54u,
                TownArchitectureSilhouette.PastoralTimberFrame, TownArchitectureRoofForm.SteepGable,
                TownArchitectureOpeningStyle.TimberFramed,
                new TownArchitectureMaterialFamily(
                    "warm-fieldstone-and-cream-plaster", "weathered-wood-shingle", "exposed-oak-timber-frame",
                    "mossy-stone-path", "dark-oak-and-iron", "faded-ochre-red-blue"),
                Composition(
                    Role(TownArchitectureStructureRole.Residential, TownArchitectureMassing.GabledFrame,
                        TownArchitectureRoofForm.SteepGable, TownArchitectureOpeningStyle.TimberFramed,
                        TownArchitectureDetailFeatures.TimberFrame | TownArchitectureDetailFeatures.Chimney,
                        36, 30, 24, 15),
                    Role(TownArchitectureStructureRole.Commercial, TownArchitectureMassing.GabledFrame,
                        TownArchitectureRoofForm.SteepGable, TownArchitectureOpeningStyle.TimberFramed,
                        TownArchitectureDetailFeatures.TimberFrame | TownArchitectureDetailFeatures.Awning,
                        42, 32, 26, 16),
                    Role(TownArchitectureStructureRole.CivicCommunal, TownArchitectureMassing.StoneGabled,
                        TownArchitectureRoofForm.SteepGable, TownArchitectureOpeningStyle.TimberFramed,
                        TownArchitectureDetailFeatures.TimberFrame | TownArchitectureDetailFeatures.CivicArch,
                        40, 34, 28, 18),
                    Role(TownArchitectureStructureRole.LandmarkInfrastructure, TownArchitectureMassing.GabledFrame,
                        TownArchitectureRoofForm.SteepGable, TownArchitectureOpeningStyle.TimberFramed,
                        TownArchitectureDetailFeatures.TimberFrame | TownArchitectureDetailFeatures.Chimney,
                        34, 30, 24, 17)),
                new[] { "recessed-window", "projecting-sill-lintel", "mullion", "timber-brace", "timber-joint", "threshold", "door-canopy", "fascia", "ridge-cap", "chimney-cap", "porch-post", "stone-course" },
                "kentridge.png", "kentridge-church.png", "kentridge-well.png", "kentridge-warehouse.png", "kentridge-inn.png"));

            Register(Definition(
                WorldBuilderTownArchitectureIds.Hightown, "Hightown", "hightown", 0x48494748u,
                TownArchitectureSilhouette.CivicVerticalStone, TownArchitectureRoofForm.TwinGable,
                TownArchitectureOpeningStyle.OrderedStone,
                new TownArchitectureMaterialFamily(
                    "dressed-pale-ashlar", "dark-slate", "carved-dark-timber", "formal-stone-paving",
                    "iron-and-clean-plaster", "royal-blue-burgundy-glass"),
                Composition(
                    Role(TownArchitectureStructureRole.Residential, TownArchitectureMassing.StoneGabled,
                        TownArchitectureRoofForm.TwinGable, TownArchitectureOpeningStyle.OrderedStone,
                        TownArchitectureDetailFeatures.MasonryCourses | TownArchitectureDetailFeatures.Balcony,
                        34, 28, 34, 17),
                    Role(TownArchitectureStructureRole.Commercial, TownArchitectureMassing.StoneGabled,
                        TownArchitectureRoofForm.TwinGable, TownArchitectureOpeningStyle.OrderedStone,
                        TownArchitectureDetailFeatures.MasonryCourses | TownArchitectureDetailFeatures.Awning,
                        40, 30, 36, 18),
                    Role(TownArchitectureStructureRole.CivicCommunal, TownArchitectureMassing.StoneGabled,
                        TownArchitectureRoofForm.TwinGable, TownArchitectureOpeningStyle.OrderedStone,
                        TownArchitectureDetailFeatures.MasonryCourses | TownArchitectureDetailFeatures.CivicArch | TownArchitectureDetailFeatures.Buttress,
                        46, 36, 40, 20),
                    Role(TownArchitectureStructureRole.LandmarkInfrastructure, TownArchitectureMassing.FortifiedParapet,
                        TownArchitectureRoofForm.FortifiedParapet, TownArchitectureOpeningStyle.OrderedStone,
                        TownArchitectureDetailFeatures.MasonryCourses | TownArchitectureDetailFeatures.CivicArch,
                        38, 34, 42, 0)),
                new[] { "recessed-window", "projecting-sill-lintel", "mullion", "quoins", "stone-course", "formal-door-frame", "threshold", "stone-step", "fascia", "ridge-cap", "balcony-rail", "arched-passage" },
                "hightown.png", "hightown-church.png", "hightown-mayor-house.png", "hightown-under-church.png", "hightown-cave.png"));

            Register(Definition(
                WorldBuilderTownArchitectureIds.Moordell, "Moordell", "moordell", 0x4D4F4F52u,
                TownArchitectureSilhouette.MoorlandLowStone, TownArchitectureRoofForm.GableWithLeanTo,
                TownArchitectureOpeningStyle.DeepWeatheredStone,
                new TownArchitectureMaterialFamily(
                    "weathered-dark-fieldstone", "coarse-slate-and-thatch", "heavy-aged-timber", "peat-earth-and-rough-stone",
                    "iron-moss-lichen", "heather-purple-warm-amber"),
                Composition(
                    Role(TownArchitectureStructureRole.Residential, TownArchitectureMassing.LowStoneLeanTo,
                        TownArchitectureRoofForm.GableWithLeanTo, TownArchitectureOpeningStyle.DeepWeatheredStone,
                        TownArchitectureDetailFeatures.MasonryCourses | TownArchitectureDetailFeatures.LeanTo | TownArchitectureDetailFeatures.Chimney,
                        38, 34, 20, 12),
                    Role(TownArchitectureStructureRole.Commercial, TownArchitectureMassing.LowStoneLeanTo,
                        TownArchitectureRoofForm.GableWithLeanTo, TownArchitectureOpeningStyle.DeepWeatheredStone,
                        TownArchitectureDetailFeatures.LeanTo | TownArchitectureDetailFeatures.Awning,
                        44, 36, 22, 12),
                    Role(TownArchitectureStructureRole.CivicCommunal, TownArchitectureMassing.LowStoneLeanTo,
                        TownArchitectureRoofForm.GableWithLeanTo, TownArchitectureOpeningStyle.DeepWeatheredStone,
                        TownArchitectureDetailFeatures.MasonryCourses | TownArchitectureDetailFeatures.CivicArch | TownArchitectureDetailFeatures.LeanTo,
                        44, 38, 24, 13),
                    Role(TownArchitectureStructureRole.LandmarkInfrastructure, TownArchitectureMassing.StoneGabled,
                        TownArchitectureRoofForm.GableWithLeanTo, TownArchitectureOpeningStyle.DeepWeatheredStone,
                        TownArchitectureDetailFeatures.MasonryCourses | TownArchitectureDetailFeatures.Chimney,
                        34, 34, 26, 14)),
                new[] { "deep-window-reveal", "projecting-sill-lintel", "heavy-shutter", "stone-course", "corner-quoin", "threshold", "rough-door-frame", "lean-to-junction", "fascia", "ridge-cap", "chimney-cap", "drainage-channel" },
                "moordell.png", "moordell-building1.png", "moordell-inn.png", "moordell-grave.png", "moordell-pub.png"));

            Register(Definition(
                WorldBuilderTownArchitectureIds.Rossdam, "Rossdam", "rossdam", 0x524F5353u,
                TownArchitectureSilhouette.RoyalFortified, TownArchitectureRoofForm.FortifiedParapet,
                TownArchitectureOpeningStyle.FortifiedReveal,
                new TownArchitectureMaterialFamily(
                    "cut-stone-masonry", "clay-tile-and-slate", "dark-structural-timber", "clean-civic-paving",
                    "iron-and-brass", "crimson-royal-blue-gold"),
                Composition(
                    Role(TownArchitectureStructureRole.Residential, TownArchitectureMassing.FortifiedParapet,
                        TownArchitectureRoofForm.FortifiedParapet, TownArchitectureOpeningStyle.FortifiedReveal,
                        TownArchitectureDetailFeatures.MasonryCourses | TownArchitectureDetailFeatures.Crenellation,
                        38, 32, 28, 0),
                    Role(TownArchitectureStructureRole.Commercial, TownArchitectureMassing.FortifiedParapet,
                        TownArchitectureRoofForm.FortifiedParapet, TownArchitectureOpeningStyle.FortifiedReveal,
                        TownArchitectureDetailFeatures.MasonryCourses | TownArchitectureDetailFeatures.Awning | TownArchitectureDetailFeatures.Crenellation,
                        44, 34, 30, 0),
                    Role(TownArchitectureStructureRole.CivicCommunal, TownArchitectureMassing.FortifiedParapet,
                        TownArchitectureRoofForm.FortifiedParapet, TownArchitectureOpeningStyle.FortifiedReveal,
                        TownArchitectureDetailFeatures.MasonryCourses | TownArchitectureDetailFeatures.CivicArch | TownArchitectureDetailFeatures.Buttress | TownArchitectureDetailFeatures.Crenellation,
                        48, 38, 34, 0),
                    Role(TownArchitectureStructureRole.LandmarkInfrastructure, TownArchitectureMassing.FortifiedParapet,
                        TownArchitectureRoofForm.FortifiedParapet, TownArchitectureOpeningStyle.FortifiedReveal,
                        TownArchitectureDetailFeatures.Buttress | TownArchitectureDetailFeatures.Crenellation | TownArchitectureDetailFeatures.CivicArch,
                        46, 36, 38, 0)),
                new[] { "deep-window-reveal", "arrow-slit-reveal", "projecting-sill-lintel", "stone-course", "corner-quoin", "buttress-cap", "layered-coping", "crenellation", "tower-wall-transition", "gate-frame", "gate-hardware", "access-stair" },
                "rossdam.png", "rossdam-king-chamber.png", "rossdam-armor-shop.png", "rossdam-magic-shop.png", "rossdam-weapon-shop.png"));

            Register(Definition(
                WorldBuilderTownArchitectureIds.FairyVillage, "Fairy Village", "fairy-village", 0x46414952u,
                TownArchitectureSilhouette.OrganicCanopy, TownArchitectureRoofForm.OrganicCanopySpire,
                TownArchitectureOpeningStyle.OrganicPointed,
                new TownArchitectureMaterialFamily(
                    "pale-bark-and-light-stone", "leaf-canopy-and-woven-fiber", "root-and-branch-wood", "moss-and-flower-ground",
                    "woven-flower-trim", "aqua-lilac-crystal-glow"),
                Composition(
                    Role(TownArchitectureStructureRole.Residential, TownArchitectureMassing.OrganicCanopy,
                        TownArchitectureRoofForm.OrganicCanopySpire, TownArchitectureOpeningStyle.OrganicPointed,
                        TownArchitectureDetailFeatures.Canopy | TownArchitectureDetailFeatures.Balcony,
                        32, 28, 20, 12),
                    Role(TownArchitectureStructureRole.Commercial, TownArchitectureMassing.OrganicCanopy,
                        TownArchitectureRoofForm.OrganicCanopySpire, TownArchitectureOpeningStyle.OrganicPointed,
                        TownArchitectureDetailFeatures.Canopy | TownArchitectureDetailFeatures.Awning,
                        36, 30, 22, 13),
                    Role(TownArchitectureStructureRole.CivicCommunal, TownArchitectureMassing.OrganicCanopy,
                        TownArchitectureRoofForm.OrganicCanopySpire, TownArchitectureOpeningStyle.OrganicPointed,
                        TownArchitectureDetailFeatures.Canopy | TownArchitectureDetailFeatures.CivicArch | TownArchitectureDetailFeatures.Balcony,
                        40, 34, 24, 15),
                    Role(TownArchitectureStructureRole.LandmarkInfrastructure, TownArchitectureMassing.OrganicCanopy,
                        TownArchitectureRoofForm.OrganicCanopySpire, TownArchitectureOpeningStyle.OrganicPointed,
                        TownArchitectureDetailFeatures.Canopy | TownArchitectureDetailFeatures.Spikes,
                        30, 30, 26, 18)),
                new[] { "pointed-recessed-window", "luminous-surround", "branch-bracket", "woven-rail", "curved-step", "root-buttress", "canopy-rim", "spire-tip", "hanging-lantern", "balcony-rail", "bridge-rail", "flower-corbels" },
                "fairy-village.png", "fairy-village-treehouse.png", "fairy-village-cave.png", "fairy-village-inn.png", "fairy-village-pub.png"));

            Register(Definition(
                WorldBuilderTownArchitectureIds.OrcVillage, "Orc Village", "orc-village", 0x4F524353u,
                TownArchitectureSilhouette.TribalHeavyTimber, TownArchitectureRoofForm.StockadeJagged,
                TownArchitectureOpeningStyle.HeavySlit,
                new TownArchitectureMaterialFamily(
                    "dark-basalt-and-packed-earth", "heavy-plank-and-hide", "rough-hewn-log", "packed-earth-and-black-stone",
                    "blackened-iron-bone-rope", "smoky-red-orange"),
                Composition(
                    Role(TownArchitectureStructureRole.Residential, TownArchitectureMassing.HeavyStockade,
                        TownArchitectureRoofForm.StockadeJagged, TownArchitectureOpeningStyle.HeavySlit,
                        TownArchitectureDetailFeatures.Stockade | TownArchitectureDetailFeatures.Spikes,
                        38, 32, 24, 14),
                    Role(TownArchitectureStructureRole.Commercial, TownArchitectureMassing.HeavyStockade,
                        TownArchitectureRoofForm.StockadeJagged, TownArchitectureOpeningStyle.HeavySlit,
                        TownArchitectureDetailFeatures.Stockade | TownArchitectureDetailFeatures.Awning | TownArchitectureDetailFeatures.Spikes,
                        42, 34, 26, 15),
                    Role(TownArchitectureStructureRole.CivicCommunal, TownArchitectureMassing.HeavyStockade,
                        TownArchitectureRoofForm.StockadeJagged, TownArchitectureOpeningStyle.HeavySlit,
                        TownArchitectureDetailFeatures.Stockade | TownArchitectureDetailFeatures.CivicArch | TownArchitectureDetailFeatures.Spikes,
                        46, 38, 28, 16),
                    Role(TownArchitectureStructureRole.LandmarkInfrastructure, TownArchitectureMassing.HeavyStockade,
                        TownArchitectureRoofForm.StockadeJagged, TownArchitectureOpeningStyle.HeavySlit,
                        TownArchitectureDetailFeatures.Stockade | TownArchitectureDetailFeatures.Spikes | TownArchitectureDetailFeatures.Buttress,
                        44, 36, 30, 18)),
                new[] { "deep-slit", "heavy-window-surround", "log-brace", "timber-joint", "threshold", "heavy-door-frame", "spike", "stockade-post", "watch-platform", "forge-hood", "rack", "gate-crossbar" },
                "orc-village.png", "orc-village-armor-shop.png", "orc-village-weapon-shop.png", "orc-village-magic-shop.png", "orc-village-pub.png"));
        }
    }
}
