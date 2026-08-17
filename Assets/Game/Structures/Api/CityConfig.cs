using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    public enum CityDistrict : byte
    {
        Residential = 0,
        Mixed = 1,
        Civic = 2,
        Sacred = 3,
        Defensive = 4,
    }

    [System.Flags]
    public enum CityDistrictMask : byte
    {
        None = 0,
        Residential = 1 << 0,
        Mixed = 1 << 1,
        Civic = 1 << 2,
        Sacred = 1 << 3,
        Defensive = 1 << 4,
        All = Residential | Mixed | Civic | Sacred | Defensive,
    }

    public enum CityStructureArchetype : byte
    {
        House = 0,
        Shed = 1,
        Church = 2,
        Cathedral = 3,
        Temple = 4,
        Castle = 5,
        Civic = 6,
    }

    public enum CityStructurePresetId : byte
    {
        CompactCabin = 0,
        Farmhouse = 1,
        StorageShed = 2,
        WorkshopShed = 3,
        Chapel = 4,
        ParishChurch = 5,
        SimpleCathedral = 6,
        GothicCathedral = 7,
        ClassicalTemple = 8,
        CourtyardTemple = 9,
        KeepCastle = 10,
        WalledCastle = 11,
        CivicHall = 12,
    }

    public enum CityRoadFrontage : byte
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    /// <summary>
    /// Bounded lot envelope. Candidate lots choose deterministic integer dimensions inside these
    /// ranges; the grid pitch uses maxima so neighbouring candidates never overlap as sizes vary.
    /// </summary>
    public struct CityLotConfig
    {
        public int MinimumWidth;
        public int MaximumWidth;
        public int MinimumDepth;
        public int MaximumDepth;
        public int FrontSetback;
        public int RearSetback;
        public int SideSetback;
        public int MinimumSpacing;
        public int OccupancyPermille;

        public bool IsWellFormed =>
            MinimumWidth >= 8 && MaximumWidth >= MinimumWidth &&
            MinimumDepth >= 8 && MaximumDepth >= MinimumDepth &&
            FrontSetback >= 0 && RearSetback >= 0 && SideSetback >= 0 &&
            MinimumSpacing >= 0 && OccupancyPermille >= 0 && OccupancyPermille <= 1000 &&
            MinimumWidth - SideSetback * 2 >= 4 &&
            MinimumDepth - FrontSetback - RearSetback >= 4 &&
            MinimumWidth - FrontSetback - RearSetback >= 4 &&
            MinimumDepth - SideSetback * 2 >= 4;
    }

    public struct CityPaletteEntry
    {
        public CityStructureArchetype Archetype;
        public CityStructurePresetId PresetId;
        public CityDistrictMask Districts;
        public int Weight;
        public int MinimumBuildableWidth;
        public int MinimumBuildableDepth;

        public bool IsWellFormed =>
            Districts != CityDistrictMask.None && Weight > 0 &&
            MinimumBuildableWidth > 0 && MinimumBuildableDepth > 0 &&
            CityStructurePresetLibrary.MatchesArchetype(Archetype, PresetId);
    }

    public struct CityLandmarkRule
    {
        public CityStructureArchetype Archetype;
        public CityStructurePresetId PresetId;
        public CityDistrictMask Districts;
        public int MinimumBuildableWidth;
        public int MinimumBuildableDepth;
        public int EveryNthEligibleLot;
        public int Priority;

        public bool IsWellFormed =>
            Districts != CityDistrictMask.None && MinimumBuildableWidth > 0 &&
            MinimumBuildableDepth > 0 && EveryNthEligibleLot >= 0 && Priority >= 0 &&
            CityStructurePresetLibrary.MatchesArchetype(Archetype, PresetId);
    }

    public struct CityConfig
    {
        public const int MaximumBlocksPerAxis = 32;
        public const int MaximumCandidateCount = MaximumBlocksPerAxis * MaximumBlocksPerAxis;

        public int BlocksX;
        public int BlocksZ;
        public int StreetWidth;
        public int PlazaRadiusLots;
        public int OpenSpacePermille;
        public int ResidentialDensityPermille;
        public int MixedDensityPermille;
        public int CivicDensityPermille;
        public CityLotConfig Lot;
        public FixedList512Bytes<CityPaletteEntry> Palette;
        public FixedList512Bytes<CityLandmarkRule> Landmarks;

        public int CandidateCount => BlocksX * BlocksZ;
        public int BlockPitchX => Lot.MaximumWidth + StreetWidth + Lot.MinimumSpacing;
        public int BlockPitchZ => Lot.MaximumDepth + StreetWidth + Lot.MinimumSpacing;

        public bool IsWellFormed
        {
            get
            {
                if (BlocksX <= 0 || BlocksX > MaximumBlocksPerAxis ||
                    BlocksZ <= 0 || BlocksZ > MaximumBlocksPerAxis ||
                    StreetWidth <= 0 || PlazaRadiusLots < 0 ||
                    OpenSpacePermille < 0 || OpenSpacePermille > 1000 ||
                    ResidentialDensityPermille < 0 || ResidentialDensityPermille > 1000 ||
                    MixedDensityPermille < 0 || MixedDensityPermille > 1000 ||
                    CivicDensityPermille < 0 || CivicDensityPermille > 1000 ||
                    !Lot.IsWellFormed || Palette.Length == 0)
                    return false;

                for (int i = 0; i < Palette.Length; i++)
                    if (!Palette[i].IsWellFormed)
                        return false;
                for (int i = 0; i < Landmarks.Length; i++)
                    if (!Landmarks[i].IsWellFormed)
                        return false;
                return true;
            }
        }
    }

    public struct CityPlacement
    {
        public int CandidateIndex;
        public ulong StableIdentity;
        public int2 Grid;
        public int3 LotOrigin;
        public int2 LotSize;
        public int3 StructureOrigin;
        public int2 BuildableSize;
        public CityRoadFrontage Frontage;
        public Facing Facing;
        public CityDistrict District;
        public CityStructureArchetype Archetype;
        public CityStructurePresetId PresetId;
        public bool IsLandmark;
    }

    public static class CityPresets
    {
        public static CityConfig MixedTown()
        {
            CityConfig config = new CityConfig
            {
                BlocksX = 10,
                BlocksZ = 10,
                StreetWidth = 8,
                PlazaRadiusLots = 1,
                OpenSpacePermille = 90,
                ResidentialDensityPermille = 900,
                MixedDensityPermille = 850,
                CivicDensityPermille = 760,
                Lot = new CityLotConfig
                {
                    MinimumWidth = 96,
                    MaximumWidth = 112,
                    MinimumDepth = 88,
                    MaximumDepth = 104,
                    FrontSetback = 8,
                    RearSetback = 8,
                    SideSetback = 8,
                    MinimumSpacing = 4,
                    OccupancyPermille = 940,
                },
            };

            AddPalette(ref config, CityStructureArchetype.House, CityStructurePresetId.CompactCabin,
                CityDistrictMask.Residential | CityDistrictMask.Mixed, 5, 48, 40);
            AddPalette(ref config, CityStructureArchetype.House, CityStructurePresetId.Farmhouse,
                CityDistrictMask.Residential | CityDistrictMask.Mixed, 3, 96, 72);
            AddPalette(ref config, CityStructureArchetype.Shed, CityStructurePresetId.WorkshopShed,
                CityDistrictMask.Residential | CityDistrictMask.Mixed, 2, 40, 32);
            AddPalette(ref config, CityStructureArchetype.Church, CityStructurePresetId.Chapel,
                CityDistrictMask.Civic | CityDistrictMask.Sacred, 3, 48, 80);
            AddPalette(ref config, CityStructureArchetype.Church, CityStructurePresetId.ParishChurch,
                CityDistrictMask.Sacred | CityDistrictMask.Civic, 2, 96, 80);

            config.Landmarks.Add(new CityLandmarkRule
            {
                Archetype = CityStructureArchetype.Cathedral,
                PresetId = CityStructurePresetId.SimpleCathedral,
                Districts = CityDistrictMask.Sacred | CityDistrictMask.Civic,
                MinimumBuildableWidth = 88,
                MinimumBuildableDepth = 80,
                EveryNthEligibleLot = 7,
                Priority = 20,
            });
            config.Landmarks.Add(new CityLandmarkRule
            {
                Archetype = CityStructureArchetype.Temple,
                PresetId = CityStructurePresetId.ClassicalTemple,
                Districts = CityDistrictMask.Sacred,
                MinimumBuildableWidth = 88,
                MinimumBuildableDepth = 80,
                EveryNthEligibleLot = 5,
                Priority = 10,
            });
            config.Landmarks.Add(new CityLandmarkRule
            {
                Archetype = CityStructureArchetype.Castle,
                PresetId = CityStructurePresetId.KeepCastle,
                Districts = CityDistrictMask.Residential,
                MinimumBuildableWidth = 96,
                MinimumBuildableDepth = 80,
                EveryNthEligibleLot = 31,
                Priority = 30,
            });
            return config;
        }

        private static void AddPalette(ref CityConfig config, CityStructureArchetype archetype,
            CityStructurePresetId preset, CityDistrictMask districts, int weight, int width, int depth)
        {
            config.Palette.Add(new CityPaletteEntry
            {
                Archetype = archetype,
                PresetId = preset,
                Districts = districts,
                Weight = weight,
                MinimumBuildableWidth = width,
                MinimumBuildableDepth = depth,
            });
        }
    }
}
