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

    public enum TownArchitectureStructureRole : byte
    {
        Residential = 0,
        Commercial = 1,
        CivicCommunal = 2,
        LandmarkInfrastructure = 3,
    }

    /// <summary>
    /// High-level massing contract used by shared realizers. It intentionally describes construction
    /// language rather than captured-scene coordinates.
    /// </summary>
    public enum TownArchitectureSilhouette : byte
    {
        PastoralTimberFrame = 0,
        CivicVerticalStone = 1,
        MoorlandLowStone = 2,
        RoyalFortified = 3,
        OrganicCanopy = 4,
        TribalHeavyTimber = 5,
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
    /// scenes only choose a style, seed/origin and a material-ID mapping.
    /// </summary>
    public sealed class TownArchitectureProgram
    {
        private readonly TownArchitectureStructureRole[] _requiredRoles;
        private readonly string[] _referenceScreenshots;

        public string StyleId { get; }
        public string DisplayName { get; }
        public string SourcePrefix { get; }
        public TownArchitectureSilhouette Silhouette { get; }
        public TownArchitectureMaterialFamily MaterialFamily { get; }
        public IReadOnlyList<TownArchitectureStructureRole> RequiredRoles => _requiredRoles;
        public IReadOnlyList<string> ReferenceScreenshots => _referenceScreenshots;

        internal TownArchitectureProgram(
            string styleId,
            string displayName,
            string sourcePrefix,
            TownArchitectureSilhouette silhouette,
            in TownArchitectureMaterialFamily materialFamily,
            TownArchitectureStructureRole[] requiredRoles,
            string[] referenceScreenshots)
        {
            StyleId = Require(styleId, nameof(styleId));
            DisplayName = Require(displayName, nameof(displayName));
            SourcePrefix = Require(sourcePrefix, nameof(sourcePrefix));
            Silhouette = silhouette;
            MaterialFamily = materialFamily;
            _requiredRoles = requiredRoles ?? throw new ArgumentNullException(nameof(requiredRoles));
            _referenceScreenshots = referenceScreenshots ?? throw new ArgumentNullException(nameof(referenceScreenshots));

            if (_requiredRoles.Length == 0)
                throw new ArgumentException("A town architecture program requires structure roles.", nameof(requiredRoles));
            if (_referenceScreenshots.Length == 0)
                throw new ArgumentException("A reference-driven town program requires screenshot evidence.", nameof(referenceScreenshots));
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

        private static string Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Town architecture identity values cannot be blank.", name);
            return value;
        }
    }
}
