using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    /// <summary>Stable ids for the six reference-backed baseline programs. Registries may contain any additional id.</summary>
    public static class WorldBuilderTownArchitectureIds
    {
        public const string Kentridge = "kentridge";
        public const string Hightown = "hightown";
        public const string Moordell = "moordell";
        public const string Rossdam = "rossdam";
        public const string FairyVillage = "fairy-village";
        public const string OrcVillage = "orc-village";
    }

    public static class TownArchitectureDistrictBounds
    {
        public const int HalfWidthVoxels = 82;
        public const int HalfDepthVoxels = 66;
        public const int EstimatedMaxHeightVoxels = 78;
        public const int WidthVoxels = HalfWidthVoxels * 2;
        public const int DepthVoxels = HalfDepthVoxels * 2;
    }

    public enum TownArchitectureStructureRole : byte
    {
        Residential = 0,
        Commercial = 1,
        CivicCommunal = 2,
        LandmarkInfrastructure = 3,
    }

    /// <summary>Semantic identity metadata. Realization is controlled by per-role composition recipes, not this enum.</summary>
    public enum TownArchitectureSilhouette : byte
    {
        PastoralTimberFrame = 0,
        CivicVerticalStone = 1,
        MoorlandLowStone = 2,
        RoyalFortified = 3,
        OrganicCanopy = 4,
        TribalHeavyTimber = 5,
    }

    public enum TownArchitectureRoofForm : byte
    {
        SteepGable = 0,
        TwinGable = 1,
        GableWithLeanTo = 2,
        FortifiedParapet = 3,
        OrganicCanopySpire = 4,
        StockadeJagged = 5,
    }

    public enum TownArchitectureOpeningStyle : byte
    {
        TimberFramed = 0,
        OrderedStone = 1,
        DeepWeatheredStone = 2,
        FortifiedReveal = 3,
        OrganicPointed = 4,
        HeavySlit = 5,
    }

    /// <summary>Reusable massing capabilities. New styles compose these; they are not town identities.</summary>
    public enum TownArchitectureMassing : byte
    {
        GabledFrame = 0,
        StoneGabled = 1,
        LowStoneLeanTo = 2,
        FortifiedParapet = 3,
        OrganicCanopy = 4,
        HeavyStockade = 5,
    }

    [Flags]
    public enum TownArchitectureDetailFeatures : ushort
    {
        None = 0,
        TimberFrame = 1 << 0,
        MasonryCourses = 1 << 1,
        Balcony = 1 << 2,
        Awning = 1 << 3,
        CivicArch = 1 << 4,
        Chimney = 1 << 5,
        Buttress = 1 << 6,
        Crenellation = 1 << 7,
        Canopy = 1 << 8,
        Stockade = 1 << 9,
        Spikes = 1 << 10,
        LeanTo = 1 << 11,
    }

    public readonly struct TownArchitectureMaterialFamily
    {
        public string Wall { get; }
        public string Roof { get; }
        public string Structure { get; }
        public string Ground { get; }
        public string Trim { get; }
        public string Accent { get; }

        public string Signature => Wall + "|" + Roof + "|" + Structure + "|" + Ground + "|" + Trim + "|" + Accent;

        public TownArchitectureMaterialFamily(string wall, string roof, string structure, string ground, string trim, string accent)
        {
            Wall = Require(wall, nameof(wall));
            Roof = Require(roof, nameof(roof));
            Structure = Require(structure, nameof(structure));
            Ground = Require(ground, nameof(ground));
            Trim = Require(trim, nameof(trim));
            Accent = Require(accent, nameof(accent));
        }

        private static string Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Town architecture material roles require semantic names.", name);
            return value;
        }
    }

    /// <summary>One structure role assembled from reusable WorldBuilder massing/opening/detail capabilities.</summary>
    public readonly struct TownArchitectureRoleRecipe
    {
        public TownArchitectureStructureRole Role { get; }
        public TownArchitectureMassing Massing { get; }
        public TownArchitectureRoofForm RoofForm { get; }
        public TownArchitectureOpeningStyle OpeningStyle { get; }
        public TownArchitectureDetailFeatures Features { get; }
        public int Width { get; }
        public int Depth { get; }
        public int WallHeight { get; }
        public int RoofHeight { get; }

        public TownArchitectureRoleRecipe(
            TownArchitectureStructureRole role,
            TownArchitectureMassing massing,
            TownArchitectureRoofForm roofForm,
            TownArchitectureOpeningStyle openingStyle,
            TownArchitectureDetailFeatures features,
            int width,
            int depth,
            int wallHeight,
            int roofHeight)
        {
            if (width < 16 || depth < 14 || wallHeight < 10 || roofHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Town role dimensions are outside reusable authoring bounds.");
            Role = role;
            Massing = massing;
            RoofForm = roofForm;
            OpeningStyle = openingStyle;
            Features = features;
            Width = width;
            Depth = depth;
            WallHeight = wallHeight;
            RoofHeight = roofHeight;
        }

        public string Signature => Role + ":" + Massing + ":" + RoofForm + ":" + OpeningStyle + ":" + Features +
                                   ":" + Width + "x" + Depth + "x" + WallHeight + "+" + RoofHeight;
    }

    /// <summary>Validated four-role composition. No style id or town name is encoded in the realization recipe.</summary>
    public sealed class TownArchitectureComposition
    {
        private readonly TownArchitectureRoleRecipe[] _roles;
        public IReadOnlyList<TownArchitectureRoleRecipe> Roles => _roles;
        public string Signature { get; }

        public TownArchitectureComposition(params TownArchitectureRoleRecipe[] roles)
        {
            if (roles == null) throw new ArgumentNullException(nameof(roles));
            if (roles.Length != 4) throw new ArgumentException("A town composition requires exactly four structure roles.", nameof(roles));
            _roles = (TownArchitectureRoleRecipe[])roles.Clone();
            foreach (TownArchitectureStructureRole required in Enum.GetValues(typeof(TownArchitectureStructureRole)))
            {
                int matches = 0;
                for (int i = 0; i < _roles.Length; i++) if (_roles[i].Role == required) matches++;
                if (matches != 1) throw new ArgumentException("A town composition must define each required role exactly once.", nameof(roles));
            }
            var signatures = new string[_roles.Length];
            for (int i = 0; i < _roles.Length; i++) signatures[i] = _roles[i].Signature;
            Signature = string.Join("|", signatures);
        }

        public TownArchitectureRoleRecipe RecipeFor(TownArchitectureStructureRole role)
        {
            for (int i = 0; i < _roles.Length; i++) if (_roles[i].Role == role) return _roles[i];
            throw new ArgumentOutOfRangeException(nameof(role), role, "Town composition has no recipe for role.");
        }
    }

    /// <summary>Registry-ready immutable style definition. Callers may create/register additional definitions at runtime.</summary>
    public sealed class TownArchitectureDefinition
    {
        private readonly string[] _referenceScreenshots;
        private readonly string[] _detailVocabulary;
        public string StyleId { get; }
        public string DisplayName { get; }
        public string SourcePrefix { get; }
        public uint CanonicalSeed { get; }
        public int DetailUnitBlocks { get; }
        public TownArchitectureSilhouette Silhouette { get; }
        public TownArchitectureRoofForm RoofForm { get; }
        public TownArchitectureOpeningStyle OpeningStyle { get; }
        public TownArchitectureMaterialFamily MaterialFamily { get; }
        public TownArchitectureComposition Composition { get; }
        public IReadOnlyList<string> ReferenceScreenshots => _referenceScreenshots;
        public IReadOnlyList<string> DetailVocabulary => _detailVocabulary;

        public TownArchitectureDefinition(
            string styleId, string displayName, string sourcePrefix, uint canonicalSeed, int detailUnitBlocks,
            TownArchitectureSilhouette silhouette, TownArchitectureRoofForm roofForm,
            TownArchitectureOpeningStyle openingStyle, in TownArchitectureMaterialFamily materialFamily,
            TownArchitectureComposition composition, string[] referenceScreenshots, string[] detailVocabulary)
        {
            StyleId = Require(styleId, nameof(styleId));
            DisplayName = Require(displayName, nameof(displayName));
            SourcePrefix = Require(sourcePrefix, nameof(sourcePrefix));
            if (detailUnitBlocks <= 0) throw new ArgumentOutOfRangeException(nameof(detailUnitBlocks));
            CanonicalSeed = canonicalSeed;
            DetailUnitBlocks = detailUnitBlocks;
            Silhouette = silhouette;
            RoofForm = roofForm;
            OpeningStyle = openingStyle;
            MaterialFamily = materialFamily;
            Composition = composition ?? throw new ArgumentNullException(nameof(composition));
            _referenceScreenshots = referenceScreenshots == null ? Array.Empty<string>() : (string[])referenceScreenshots.Clone();
            _detailVocabulary = detailVocabulary == null ? throw new ArgumentNullException(nameof(detailVocabulary)) : (string[])detailVocabulary.Clone();
            if (_detailVocabulary.Length == 0) throw new ArgumentException("A town architecture definition requires reusable detail vocabulary.", nameof(detailVocabulary));
        }

        public TownArchitectureProgram CreateProgram(uint seed) => new TownArchitectureProgram(this, seed);

        private static string Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Town architecture identity values cannot be blank.", name);
            return value;
        }
    }

    public sealed class TownArchitectureProgram
    {
        private static readonly TownArchitectureStructureRole[] s_RequiredRoles =
        {
            TownArchitectureStructureRole.Residential, TownArchitectureStructureRole.Commercial,
            TownArchitectureStructureRole.CivicCommunal, TownArchitectureStructureRole.LandmarkInfrastructure,
        };
        private readonly TownArchitectureDefinition _definition;

        public string StyleId => _definition.StyleId;
        public string DisplayName => _definition.DisplayName;
        public string SourcePrefix => _definition.SourcePrefix;
        public uint Seed { get; }
        public int DetailUnitBlocks => _definition.DetailUnitBlocks;
        public TownArchitectureSilhouette Silhouette => _definition.Silhouette;
        public TownArchitectureRoofForm RoofForm => _definition.RoofForm;
        public TownArchitectureOpeningStyle OpeningStyle => _definition.OpeningStyle;
        public TownArchitectureMaterialFamily MaterialFamily => _definition.MaterialFamily;
        public TownArchitectureComposition Composition => _definition.Composition;
        public IReadOnlyList<TownArchitectureStructureRole> RequiredRoles => s_RequiredRoles;
        public IReadOnlyList<string> ReferenceScreenshots => _definition.ReferenceScreenshots;
        public IReadOnlyList<string> DetailVocabulary => _definition.DetailVocabulary;
        public string FormSignature => Silhouette + "/" + RoofForm + "/" + OpeningStyle;
        public string DetailSignature => string.Join("|", _definition.DetailVocabulary);
        public string DeterministicSignature => StyleId + ":" + Seed.ToString("X8") + ":" + DetailUnitBlocks + ":" +
                                                FormSignature + ":" + Composition.Signature + ":" + DetailSignature;

        internal TownArchitectureProgram(TownArchitectureDefinition definition, uint seed)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Seed = seed;
        }

        public bool IncludesRole(TownArchitectureStructureRole role) =>
            role >= TownArchitectureStructureRole.Residential && role <= TownArchitectureStructureRole.LandmarkInfrastructure;

        public bool IncludesDetail(string detailId)
        {
            if (string.IsNullOrWhiteSpace(detailId)) return false;
            for (int i = 0; i < _definition.DetailVocabulary.Count; i++)
                if (string.Equals(_definition.DetailVocabulary[i], detailId, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
