using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen
{
    [Flags]
    public enum SettlementDistrictMask : byte
    {
        None = 0,
        Civic = 1 << 0,
        Market = 1 << 1,
        Residential = 1 << 2,
        Working = 1 << 3,
        Noble = 1 << 4,
        All = Civic | Market | Residential | Working | Noble,
    }

    [Flags]
    public enum SettlementFrontageMask : byte
    {
        None = 0,
        South = 1 << 0,
        West = 1 << 1,
        North = 1 << 2,
        East = 1 << 3,
        Cardinal = South | West | North | East,
    }

    [Flags]
    public enum SettlementArchetypeMask : ushort
    {
        None = 0,
        Townhouse = 1 << 0,
        WideHouse = 1 << 1,
        Shop = 1 << 2,
        Inn = 1 << 3,
        Warehouse = 1 << 4,
        Mansion = 1 << 5,
        Church = 1 << 6,
        Well = 1 << 7,
        All = Townhouse | WideHouse | Shop | Inn | Warehouse | Mansion | Church | Well,
    }

    public enum SettlementLandmarkKind : byte
    {
        Church = 0,
        Cathedral = 1,
        Temple = 2,
        Castle = 3,
        Civic = 4,
    }

    public enum SettlementPlanningScope : byte
    {
        RegionLocal = 0,
        Global = 1,
    }

    /// <summary>Inclusive deterministic integer range used by semantic lot policy.</summary>
    public readonly struct SettlementIntRange
    {
        public readonly int Min;
        public readonly int Max;

        public SettlementIntRange(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public bool IsWellFormed => Min >= 0 && Max >= Min;

        public int Resolve(uint seed, int candidateId, uint salt)
        {
            if (!IsWellFormed)
                throw new InvalidOperationException("Settlement range is not well formed.");
            if (Max == Min) return Min;

            uint h = SettlementDeterminism.StableHash(seed, candidateId, (int)salt, 0);
            uint span = checked((uint)(Max - Min + 1));
            return Min + (int)(h % span);
        }
    }

    /// <summary>
    /// Bounded lot policy expressed relative to its public frontage. Width is the street-parallel
    /// span and depth is the street-normal span, regardless of cardinal orientation.
    /// </summary>
    public readonly struct SettlementLotConfig
    {
        public readonly SettlementIntRange WidthDm;
        public readonly SettlementIntRange DepthDm;
        public readonly int FrontSetbackDm;
        public readonly int RearSetbackDm;
        public readonly int SideSetbackDm;
        public readonly int MinSpacingDm;
        public readonly SettlementFrontageMask AllowedFrontages;
        public readonly bool RequireRoadFrontage;
        public readonly int MaxBuildingCoveragePercent;

        public SettlementLotConfig(
            SettlementIntRange widthDm,
            SettlementIntRange depthDm,
            int frontSetbackDm,
            int rearSetbackDm,
            int sideSetbackDm,
            int minSpacingDm,
            SettlementFrontageMask allowedFrontages,
            bool requireRoadFrontage,
            int maxBuildingCoveragePercent)
        {
            WidthDm = widthDm;
            DepthDm = depthDm;
            FrontSetbackDm = frontSetbackDm;
            RearSetbackDm = rearSetbackDm;
            SideSetbackDm = sideSetbackDm;
            MinSpacingDm = minSpacingDm;
            AllowedFrontages = allowedFrontages;
            RequireRoadFrontage = requireRoadFrontage;
            MaxBuildingCoveragePercent = maxBuildingCoveragePercent;
        }

        public bool IsWellFormed =>
            WidthDm.IsWellFormed && WidthDm.Min > 0 &&
            DepthDm.IsWellFormed && DepthDm.Min > 0 &&
            FrontSetbackDm >= 0 && RearSetbackDm >= 0 && SideSetbackDm >= 0 &&
            MinSpacingDm >= 0 && AllowedFrontages != SettlementFrontageMask.None &&
            MaxBuildingCoveragePercent > 0 && MaxBuildingCoveragePercent <= 100;

        public bool Allows(FrontageDirection frontage) =>
            (AllowedFrontages & FrontageMask(frontage)) != 0;

        public Int2 ResolveSize(uint seed, int candidateId) =>
            new Int2(
                WidthDm.Resolve(seed, candidateId, 0x51A7u),
                DepthDm.Resolve(seed, candidateId, 0xD37Fu));

        public void ValidatePlacement(
            uint seed,
            int candidateId,
            FrontageDirection frontage,
            PlannedSiteAccess access,
            Int3 footprintDm)
        {
            if (!IsWellFormed)
                throw new InvalidOperationException("Settlement lot configuration is not well formed.");
            if (!Allows(frontage))
                throw new InvalidOperationException("Settlement lot frontage is not allowed by policy.");
            if (RequireRoadFrontage && access.Kind != SiteAccessKind.Street)
                throw new InvalidOperationException("Settlement lot requires explicit street frontage.");
            if (footprintDm.X <= 0 || footprintDm.Z <= 0)
                throw new ArgumentOutOfRangeException(nameof(footprintDm));

            Int2 lot = ResolveSize(seed, candidateId);
            bool northSouth = frontage == FrontageDirection.North || frontage == FrontageDirection.South;
            int structureWidth = northSouth ? footprintDm.X : footprintDm.Z;
            int structureDepth = northSouth ? footprintDm.Z : footprintDm.X;
            int requiredWidth = checked(structureWidth + 2 * SideSetbackDm);
            int requiredDepth = checked(structureDepth + FrontSetbackDm + RearSetbackDm);
            if (lot.X < requiredWidth || lot.Y < requiredDepth)
                throw new InvalidOperationException("Structure footprint does not fit the resolved settlement lot.");

            long structureArea = (long)structureWidth * structureDepth;
            long lotArea = (long)lot.X * lot.Y;
            if (structureArea * 100L > lotArea * MaxBuildingCoveragePercent)
                throw new InvalidOperationException("Structure footprint exceeds lot occupancy policy.");
        }

        private static SettlementFrontageMask FrontageMask(FrontageDirection frontage)
        {
            switch (frontage)
            {
                case FrontageDirection.South: return SettlementFrontageMask.South;
                case FrontageDirection.West: return SettlementFrontageMask.West;
                case FrontageDirection.North: return SettlementFrontageMask.North;
                case FrontageDirection.East: return SettlementFrontageMask.East;
                default: return SettlementFrontageMask.None;
            }
        }
    }

    /// <summary>
    /// One weighted reusable structure/preset option. PresetId is intentionally opaque to Core;
    /// the voxel/application layer resolves it to its ordinary shared structure preset factory.
    /// </summary>
    public readonly struct SettlementPaletteEntry
    {
        public readonly string PresetId;
        public readonly SettlementArchetypeMask Archetypes;
        public readonly SettlementDistrictMask Districts;
        public readonly int Weight;
        public readonly bool LandmarkOnly;

        public SettlementPaletteEntry(
            string presetId,
            SettlementArchetypeMask archetypes,
            SettlementDistrictMask districts,
            int weight,
            bool landmarkOnly = false)
        {
            PresetId = presetId;
            Archetypes = archetypes;
            Districts = districts;
            Weight = weight;
            LandmarkOnly = landmarkOnly;
        }

        public bool IsWellFormed =>
            !string.IsNullOrWhiteSpace(PresetId) &&
            Archetypes != SettlementArchetypeMask.None &&
            Districts != SettlementDistrictMask.None &&
            Weight > 0;
    }

    /// <summary>Order-independent weighted preset selection keyed by stable candidate identity.</summary>
    public sealed class SettlementStructurePalette
    {
        private readonly SettlementPaletteEntry[] _entries;
        public IReadOnlyList<SettlementPaletteEntry> Entries => _entries;

        public SettlementStructurePalette(params SettlementPaletteEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
                throw new ArgumentException("Settlement structure palette requires entries.", nameof(entries));

            _entries = new SettlementPaletteEntry[entries.Length];
            Array.Copy(entries, _entries, entries.Length);
            for (int i = 0; i < _entries.Length; i++)
                if (!_entries[i].IsWellFormed)
                    throw new ArgumentException("Settlement palette contains an invalid entry.", nameof(entries));
        }

        public string SelectPreset(
            uint seed,
            int candidateId,
            StructureArchetype archetype,
            DistrictKind district,
            bool landmark = false)
        {
            SettlementArchetypeMask archetypeMask = ArchetypeMask(archetype);
            SettlementDistrictMask districtMask = DistrictMask(district);
            int totalWeight = 0;
            for (int i = 0; i < _entries.Length; i++)
            {
                SettlementPaletteEntry entry = _entries[i];
                if (entry.LandmarkOnly != landmark ||
                    (entry.Archetypes & archetypeMask) == 0 ||
                    (entry.Districts & districtMask) == 0)
                    continue;
                totalWeight = checked(totalWeight + entry.Weight);
            }

            if (totalWeight <= 0)
                throw new InvalidOperationException("No settlement preset matches this candidate.");

            uint h = SettlementDeterminism.StableHash(
                seed, candidateId, (int)archetype, (int)district);
            int pick = (int)(h % (uint)totalWeight);
            for (int i = 0; i < _entries.Length; i++)
            {
                SettlementPaletteEntry entry = _entries[i];
                if (entry.LandmarkOnly != landmark ||
                    (entry.Archetypes & archetypeMask) == 0 ||
                    (entry.Districts & districtMask) == 0)
                    continue;
                if (pick < entry.Weight) return entry.PresetId;
                pick -= entry.Weight;
            }

            throw new InvalidOperationException("Weighted settlement preset selection failed.");
        }

        private static SettlementDistrictMask DistrictMask(DistrictKind district) =>
            (SettlementDistrictMask)(1 << (int)district);

        private static SettlementArchetypeMask ArchetypeMask(StructureArchetype archetype) =>
            (SettlementArchetypeMask)(1 << (int)archetype);
    }

    /// <summary>
    /// Explicit landmark policy. A landmark candidate is deterministic and bounded; MaxPerPlan keeps
    /// a caller from turning rare civic content into an unbounded global search problem.
    /// </summary>
    public readonly struct SettlementLandmarkRule
    {
        public readonly SettlementLandmarkKind Kind;
        public readonly string PresetId;
        public readonly SettlementDistrictMask Districts;
        public readonly int RarityDenominator;
        public readonly int MaxPerPlan;
        public readonly int MinSpacingDm;
        public readonly bool PreferOpenSpace;

        public SettlementLandmarkRule(
            SettlementLandmarkKind kind,
            string presetId,
            SettlementDistrictMask districts,
            int rarityDenominator,
            int maxPerPlan,
            int minSpacingDm,
            bool preferOpenSpace)
        {
            Kind = kind;
            PresetId = presetId;
            Districts = districts;
            RarityDenominator = rarityDenominator;
            MaxPerPlan = maxPerPlan;
            MinSpacingDm = minSpacingDm;
            PreferOpenSpace = preferOpenSpace;
        }

        public bool IsWellFormed =>
            !string.IsNullOrWhiteSpace(PresetId) &&
            Districts != SettlementDistrictMask.None &&
            RarityDenominator > 0 && MaxPerPlan > 0 && MinSpacingDm >= 0;

        public bool IsCandidate(uint seed, int candidateId, DistrictKind district)
        {
            if (!IsWellFormed) return false;
            SettlementDistrictMask districtMask = (SettlementDistrictMask)(1 << (int)district);
            if ((Districts & districtMask) == 0) return false;
            uint h = SettlementDeterminism.StableHash(
                seed, candidateId, (int)Kind, 0x4C4D);
            return h % (uint)RarityDenominator == 0u;
        }
    }

    /// <summary>Reserved open space/plaza hook used by density and landmark policy.</summary>
    public readonly struct SettlementOpenSpaceRule
    {
        public readonly string Id;
        public readonly Int2 CentreDm;
        public readonly Int2 SizeDm;
        public readonly int ClearanceDm;

        public SettlementOpenSpaceRule(string id, Int2 centreDm, Int2 sizeDm, int clearanceDm)
        {
            Id = id;
            CentreDm = centreDm;
            SizeDm = sizeDm;
            ClearanceDm = clearanceDm;
        }

        public bool IsWellFormed =>
            !string.IsNullOrWhiteSpace(Id) &&
            SizeDm.X > 0 && SizeDm.Y > 0 && ClearanceDm >= 0;
    }

    /// <summary>Finite city-density envelope. Global/unbounded optimization is deliberately rejected.</summary>
    public readonly struct SettlementDensityPolicy
    {
        public readonly int OccupancyPercent;
        public readonly int MinSpacingDm;
        public readonly int MaxCandidatesPerRegion;
        public readonly int MaxPlanningSpanDm;
        public readonly SettlementPlanningScope PlanningScope;

        public SettlementDensityPolicy(
            int occupancyPercent,
            int minSpacingDm,
            int maxCandidatesPerRegion,
            int maxPlanningSpanDm,
            SettlementPlanningScope planningScope)
        {
            OccupancyPercent = occupancyPercent;
            MinSpacingDm = minSpacingDm;
            MaxCandidatesPerRegion = maxCandidatesPerRegion;
            MaxPlanningSpanDm = maxPlanningSpanDm;
            PlanningScope = planningScope;
        }

        public bool IsWellFormed =>
            OccupancyPercent >= 0 && OccupancyPercent <= 100 &&
            MinSpacingDm >= 0 && MaxCandidatesPerRegion > 0 && MaxPlanningSpanDm > 0 &&
            PlanningScope == SettlementPlanningScope.RegionLocal;

        public bool AcceptCandidate(uint seed, int candidateId)
        {
            if (!IsWellFormed)
                throw new InvalidOperationException("Settlement density policy is not bounded/region-local.");
            if (OccupancyPercent == 0) return false;
            if (OccupancyPercent == 100) return true;
            uint h = SettlementDeterminism.StableHash(seed, candidateId, 0x444E, 0x5354);
            return h % 100u < (uint)OccupancyPercent;
        }
    }

    /// <summary>
    /// Explicit composition policy passed alongside a settlement definition. It contains no global
    /// registry and validates that all candidate/open-space work is finite and region-local.
    /// </summary>
    public sealed class SettlementCompositionPolicy
    {
        private readonly SettlementLandmarkRule[] _landmarks;
        private readonly SettlementOpenSpaceRule[] _openSpaces;

        public SettlementLotConfig DefaultLot { get; }
        public SettlementStructurePalette Palette { get; }
        public SettlementDensityPolicy Density { get; }
        public IReadOnlyList<SettlementLandmarkRule> Landmarks => _landmarks;
        public IReadOnlyList<SettlementOpenSpaceRule> OpenSpaces => _openSpaces;

        public SettlementCompositionPolicy(
            SettlementLotConfig defaultLot,
            SettlementStructurePalette palette,
            SettlementDensityPolicy density,
            SettlementLandmarkRule[] landmarks,
            SettlementOpenSpaceRule[] openSpaces)
        {
            DefaultLot = defaultLot;
            Palette = palette ?? throw new ArgumentNullException(nameof(palette));
            Density = density;
            _landmarks = landmarks == null
                ? Array.Empty<SettlementLandmarkRule>()
                : (SettlementLandmarkRule[])landmarks.Clone();
            _openSpaces = openSpaces == null
                ? Array.Empty<SettlementOpenSpaceRule>()
                : (SettlementOpenSpaceRule[])openSpaces.Clone();
            ValidateBounded();
        }

        public void ValidateBounded()
        {
            if (!DefaultLot.IsWellFormed)
                throw new InvalidOperationException("Settlement default lot configuration is invalid.");
            if (!Density.IsWellFormed)
                throw new InvalidOperationException(
                    "Settlement composition must declare finite region-local density bounds.");
            if (DefaultLot.WidthDm.Max > Density.MaxPlanningSpanDm ||
                DefaultLot.DepthDm.Max > Density.MaxPlanningSpanDm)
                throw new InvalidOperationException("Settlement lot exceeds its bounded planning span.");

            for (int i = 0; i < _landmarks.Length; i++)
            {
                if (!_landmarks[i].IsWellFormed)
                    throw new InvalidOperationException("Settlement landmark rule is invalid.");
                if (_landmarks[i].MaxPerPlan > Density.MaxCandidatesPerRegion)
                    throw new InvalidOperationException("Landmark rule exceeds candidate budget.");
            }

            for (int i = 0; i < _openSpaces.Length; i++)
            {
                SettlementOpenSpaceRule open = _openSpaces[i];
                if (!open.IsWellFormed)
                    throw new InvalidOperationException("Settlement open-space rule is invalid.");
                if (open.SizeDm.X > Density.MaxPlanningSpanDm ||
                    open.SizeDm.Y > Density.MaxPlanningSpanDm)
                    throw new InvalidOperationException("Open space exceeds bounded planning span.");
            }
        }
    }

    public static class SettlementDeterminism
    {
        /// <summary>Stable integer hash for candidate-local selection; independent of traversal order.</summary>
        public static uint StableHash(uint seed, int candidateId, int semanticA, int semanticB)
        {
            uint h = seed
                   ^ ((uint)(candidateId + 1) * 0x9E3779B9u)
                   ^ ((uint)(semanticA + 7) * 0x85EBCA6Bu)
                   ^ ((uint)(semanticB + 11) * 0xC2B2AE35u);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }
}
