using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    /// <summary>Stable semantic ids for reusable town-architecture programs.</summary>
    public static class WorldBuilderTownArchitectureIds
    {
        public const string Kentridge = "kentridge";
        public const string Hightown = "hightown";
        public const string Moordell = "moordell";
        public const string Rossdam = "rossdam";
        public const string FairyVillage = "fairy-village";
        public const string OrcVillage = "orc-village";
    }

    /// <summary>
    /// Bounded semantic footprint reserved by the shared town-architecture authoring contract.
    /// Backends may author less geometry, but must stay within these limits so callers can place districts safely.
    /// </summary>
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

    /// <summary>High-level construction language. Backends must preserve these as distinct physical forms.</summary>
    public enum TownArchitectureSilhouette : byte
    {
        PastoralTimberFrame = 0,
        CivicVerticalStone = 1,
        MoorlandLowStone = 2,
        RoyalFortified = 3,
        OrganicCanopy = 4,
        TribalHeavyTimber = 5,
    }

    /// <summary>Roof/form intent that must survive the production realization path.</summary>
    public enum TownArchitectureRoofForm : byte
    {
        SteepGable = 0,
        TwinGable = 1,
        GableWithLeanTo = 2,
        FortifiedParapet = 3,
        OrganicCanopySpire = 4,
        StockadeJagged = 5,
    }

    /// <summary>Reusable player-scale facade construction vocabulary.</summary>
    public enum TownArchitectureOpeningStyle : byte
    {
        TimberFramed = 0,
        OrderedStone = 1,
        DeepWeatheredStone = 2,
        FortifiedReveal = 3,
        OrganicPointed = 4,
        HeavySlit = 5,
    }

    /// <summary>Semantic material family selected by a town style before a renderer maps it to IDs.</summary>
    public readonly struct TownArchitectureMaterialFamily
    {
        public string Wall { get; }
        public string Roof { get; }
        public string Structure { get; }
        public string Ground { get; }
        public string Trim { get; }
        public string Accent { get; }

        public string Signature =>
            Wall + "|" + Roof + "|" + Structure + "|" + Ground + "|" + Trim + "|" + Accent;

        public TownArchitectureMaterialFamily(
            string wall,
            string roof,
            string structure,
            string ground,
            string trim,
            string accent)
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
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Town architecture material roles require semantic names.", name);
            return value;
        }
    }

    /// <summary>
    /// Reusable semantic program for one town's architecture. Geometry backends consume this contract;
    /// scenes only choose placement/material mapping/evidence framing and may override the deterministic seed.
    /// </summary>
    public sealed class TownArchitectureProgram
    {
        private readonly TownArchitectureStructureRole[] _requiredRoles;
        private readonly string[] _referenceScreenshots;
        private readonly string[] _detailVocabulary;

        public string StyleId { get; }
        public string DisplayName { get; }
        public string SourcePrefix { get; }
        public uint Seed { get; }
        public int DetailUnitBlocks { get; }
        public TownArchitectureSilhouette Silhouette { get; }
        public TownArchitectureRoofForm RoofForm { get; }
        public TownArchitectureOpeningStyle OpeningStyle { get; }
        public TownArchitectureMaterialFamily MaterialFamily { get; }
        public IReadOnlyList<TownArchitectureStructureRole> RequiredRoles => _requiredRoles;
        public IReadOnlyList<string> ReferenceScreenshots => _referenceScreenshots;
        public IReadOnlyList<string> DetailVocabulary => _detailVocabulary;

        public string FormSignature => Silhouette + "/" + RoofForm + "/" + OpeningStyle;
        public string DetailSignature => string.Join("|", _detailVocabulary);
        public string DeterministicSignature =>
            StyleId + ":" + Seed.ToString("X8") + ":" + DetailUnitBlocks + ":" + FormSignature + ":" + DetailSignature;

        internal TownArchitectureProgram(
            string styleId,
            string displayName,
            string sourcePrefix,
            uint seed,
            int detailUnitBlocks,
            TownArchitectureSilhouette silhouette,
            TownArchitectureRoofForm roofForm,
            TownArchitectureOpeningStyle openingStyle,
            in TownArchitectureMaterialFamily materialFamily,
            TownArchitectureStructureRole[] requiredRoles,
            string[] referenceScreenshots,
            string[] detailVocabulary)
        {
            StyleId = Require(styleId, nameof(styleId));
            DisplayName = Require(displayName, nameof(displayName));
            SourcePrefix = Require(sourcePrefix, nameof(sourcePrefix));
            if (detailUnitBlocks <= 0)
                throw new ArgumentOutOfRangeException(nameof(detailUnitBlocks), "Detail unit must be at least one voxel.");
            if (!FormMatchesSilhouette(silhouette, roofForm))
                throw new ArgumentException(
                    $"Roof/form intent {roofForm} does not match physical silhouette {silhouette}.", nameof(roofForm));

            Seed = seed;
            DetailUnitBlocks = detailUnitBlocks;
            Silhouette = silhouette;
            RoofForm = roofForm;
            OpeningStyle = openingStyle;
            MaterialFamily = materialFamily;
            _requiredRoles = requiredRoles ?? throw new ArgumentNullException(nameof(requiredRoles));
            _referenceScreenshots = referenceScreenshots ?? throw new ArgumentNullException(nameof(referenceScreenshots));
            _detailVocabulary = detailVocabulary ?? throw new ArgumentNullException(nameof(detailVocabulary));

            if (_requiredRoles.Length == 0)
                throw new ArgumentException("A town architecture program requires structure roles.", nameof(requiredRoles));
            if (_referenceScreenshots.Length == 0)
                throw new ArgumentException("A reference-driven town program requires screenshot evidence.", nameof(referenceScreenshots));
            if (_detailVocabulary.Length == 0)
                throw new ArgumentException("A town architecture program requires reusable construction detail.", nameof(detailVocabulary));
        }

        public bool IncludesRole(TownArchitectureStructureRole role)
        {
            for (int i = 0; i < _requiredRoles.Length; i++)
            {
                if (_requiredRoles[i] == role)
                    return true;
            }
            return false;
        }

        public bool IncludesDetail(string detailId)
        {
            if (string.IsNullOrWhiteSpace(detailId)) return false;
            for (int i = 0; i < _detailVocabulary.Length; i++)
            {
                if (string.Equals(_detailVocabulary[i], detailId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool FormMatchesSilhouette(
            TownArchitectureSilhouette silhouette,
            TownArchitectureRoofForm roofForm)
        {
            switch (silhouette)
            {
                case TownArchitectureSilhouette.PastoralTimberFrame:
                    return roofForm == TownArchitectureRoofForm.SteepGable;
                case TownArchitectureSilhouette.CivicVerticalStone:
                    return roofForm == TownArchitectureRoofForm.TwinGable;
                case TownArchitectureSilhouette.MoorlandLowStone:
                    return roofForm == TownArchitectureRoofForm.GableWithLeanTo;
                case TownArchitectureSilhouette.RoyalFortified:
                    return roofForm == TownArchitectureRoofForm.FortifiedParapet;
                case TownArchitectureSilhouette.OrganicCanopy:
                    return roofForm == TownArchitectureRoofForm.OrganicCanopySpire;
                case TownArchitectureSilhouette.TribalHeavyTimber:
                    return roofForm == TownArchitectureRoofForm.StockadeJagged;
                default:
                    return false;
            }
        }

        private static string Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Town architecture identity values cannot be blank.", name);
            return value;
        }
    }
}