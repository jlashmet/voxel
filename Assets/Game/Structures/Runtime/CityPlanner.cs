using Game.Structures.Api;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum CityCandidateResult : byte
    {
        Placed = 0,
        Plaza = 1,
        OpenSpace = 2,
        DensityRejected = 3,
        OccupancyRejected = 4,
        NoFittingArchetype = 5,
    }

    /// <summary>
    /// Stateless region-local city planner. Every candidate can be resolved independently from its
    /// stable index, which keeps generation order and region scheduling from affecting placement.
    /// </summary>
    public static class CityPlanner
    {
        private const ulong CandidateSalt = 0x9E3779B97F4A7C15ul;
        private const ulong LotWidthSalt = 0xDB4F0B9175AE2165ul;
        private const ulong LotDepthSalt = 0xBBE0563303A4615Ful;
        private const ulong FrontageSalt = 0xD1B54A32D192ED03ul;
        private const ulong OpenSpaceSalt = 0x94D049BB133111EBul;
        private const ulong DensitySalt = 0xBF58476D1CE4E5B9ul;
        private const ulong OccupancySalt = 0xA24BAED4963EE407ul;
        private const ulong PaletteSalt = 0x9FB21C651E98DF25ul;
        private const ulong LandmarkSalt = 0xC6BC279692B5CC83ul;

        public static CityCandidateResult ResolveCandidate(
            in CityConfig config,
            ulong citySeed,
            int3 cityOrigin,
            int candidateIndex,
            out CityPlacement placement)
        {
            placement = default;
            if (!config.IsWellFormed)
                throw new System.ArgumentException("City configuration is invalid.", nameof(config));
            if (candidateIndex < 0 || candidateIndex >= config.CandidateCount)
                throw new System.ArgumentOutOfRangeException(nameof(candidateIndex));

            int gx = candidateIndex % config.BlocksX;
            int gz = candidateIndex / config.BlocksX;
            int cx2 = gx * 2 - (config.BlocksX - 1);
            int cz2 = gz * 2 - (config.BlocksZ - 1);
            ulong identity = StableIdentity(citySeed, gx, gz);
            CityRoadFrontage frontage = (CityRoadFrontage)(Hash(identity ^ FrontageSalt) & 3ul);
            CityDistrict district = ResolveDistrict(in config, cx2, cz2, identity);
            int2 lotSize = ResolveLotSize(in config, identity);
            int3 lotOrigin = ResolveLotOrigin(in config, cityOrigin, gx, gz, lotSize);
            int2 buildableSize = ResolveBuildableSize(in config, frontage, lotSize);
            int3 buildableOrigin = ResolveBuildableOrigin(in config, lotOrigin, frontage);

            FillCommon(ref placement, candidateIndex, identity, gx, gz, lotOrigin, lotSize,
                buildableOrigin, buildableSize, frontage, district);

            if (InsidePlaza(in config, cx2, cz2))
                return CityCandidateResult.Plaza;
            if (Roll(identity ^ OpenSpaceSalt) < config.OpenSpacePermille)
                return CityCandidateResult.OpenSpace;
            if (Roll(identity ^ DensitySalt) >= DensityFor(in config, district))
                return CityCandidateResult.DensityRejected;
            if (Roll(identity ^ OccupancySalt) >= config.Lot.OccupancyPermille)
                return CityCandidateResult.OccupancyRejected;

            if (TryResolveLandmark(in config, identity, district, buildableSize,
                    out CityStructureArchetype landmarkArchetype,
                    out CityStructurePresetId landmarkPreset))
            {
                placement.Archetype = landmarkArchetype;
                placement.PresetId = landmarkPreset;
                placement.IsLandmark = true;
                return CityCandidateResult.Placed;
            }

            if (!TryResolvePalette(in config, identity, district, buildableSize,
                    out CityStructureArchetype archetype, out CityStructurePresetId preset))
                return CityCandidateResult.NoFittingArchetype;

            placement.Archetype = archetype;
            placement.PresetId = preset;
            return CityCandidateResult.Placed;
        }

        public static int CountPlacements(in CityConfig config, ulong citySeed, int3 cityOrigin)
        {
            if (!config.IsWellFormed)
                throw new System.ArgumentException("City configuration is invalid.", nameof(config));
            int count = 0;
            for (int i = 0; i < config.CandidateCount; i++)
                if (ResolveCandidate(in config, citySeed, cityOrigin, i, out _) == CityCandidateResult.Placed)
                    count++;
            return count;
        }

        public static int CollectPlacements(
            in CityConfig config,
            ulong citySeed,
            int3 cityOrigin,
            ref NativeList<CityPlacement> output)
        {
            if (!config.IsWellFormed)
                throw new System.ArgumentException("City configuration is invalid.", nameof(config));
            int before = output.Length;
            for (int i = 0; i < config.CandidateCount; i++)
                if (ResolveCandidate(in config, citySeed, cityOrigin, i, out CityPlacement placement) ==
                    CityCandidateResult.Placed)
                    output.Add(placement);
            return output.Length - before;
        }

        private static void FillCommon(
            ref CityPlacement placement,
            int candidateIndex,
            ulong identity,
            int gx,
            int gz,
            int3 lotOrigin,
            int2 lotSize,
            int3 buildableOrigin,
            int2 buildableSize,
            CityRoadFrontage frontage,
            CityDistrict district)
        {
            placement.CandidateIndex = candidateIndex;
            placement.StableIdentity = identity;
            placement.Grid = new int2(gx, gz);
            placement.LotOrigin = lotOrigin;
            placement.LotSize = lotSize;
            placement.BuildableSize = buildableSize;
            placement.StructureOrigin = new int3(
                buildableOrigin.x + buildableSize.x / 2,
                lotOrigin.y,
                buildableOrigin.z + buildableSize.y / 2);
            placement.Frontage = frontage;
            placement.Facing = FacingFor(frontage);
            placement.District = district;
            placement.IsLandmark = false;
        }

        private static int2 ResolveLotSize(in CityConfig config, ulong identity)
        {
            int widthRange = config.Lot.MaximumWidth - config.Lot.MinimumWidth + 1;
            int depthRange = config.Lot.MaximumDepth - config.Lot.MinimumDepth + 1;
            int width = config.Lot.MinimumWidth + (int)(Hash(identity ^ LotWidthSalt) % (ulong)widthRange);
            int depth = config.Lot.MinimumDepth + (int)(Hash(identity ^ LotDepthSalt) % (ulong)depthRange);
            return new int2(width, depth);
        }

        private static int3 ResolveLotOrigin(
            in CityConfig config,
            int3 origin,
            int gx,
            int gz,
            int2 lotSize)
        {
            int totalX = config.BlocksX * config.BlockPitchX - config.StreetWidth - config.Lot.MinimumSpacing;
            int totalZ = config.BlocksZ * config.BlockPitchZ - config.StreetWidth - config.Lot.MinimumSpacing;
            int slotX = origin.x - totalX / 2 + gx * config.BlockPitchX;
            int slotZ = origin.z - totalZ / 2 + gz * config.BlockPitchZ;
            return new int3(
                slotX + (config.Lot.MaximumWidth - lotSize.x) / 2,
                origin.y,
                slotZ + (config.Lot.MaximumDepth - lotSize.y) / 2);
        }

        private static int2 ResolveBuildableSize(
            in CityConfig config,
            CityRoadFrontage frontage,
            int2 lotSize)
        {
            bool sideFrontage = frontage == CityRoadFrontage.East || frontage == CityRoadFrontage.West;
            return sideFrontage
                ? new int2(
                    lotSize.x - config.Lot.FrontSetback - config.Lot.RearSetback,
                    lotSize.y - config.Lot.SideSetback * 2)
                : new int2(
                    lotSize.x - config.Lot.SideSetback * 2,
                    lotSize.y - config.Lot.FrontSetback - config.Lot.RearSetback);
        }

        private static int3 ResolveBuildableOrigin(
            in CityConfig config,
            int3 lotOrigin,
            CityRoadFrontage frontage)
        {
            switch (frontage)
            {
                case CityRoadFrontage.North:
                    return new int3(lotOrigin.x + config.Lot.SideSetback, lotOrigin.y,
                        lotOrigin.z + config.Lot.FrontSetback);
                case CityRoadFrontage.South:
                    return new int3(lotOrigin.x + config.Lot.SideSetback, lotOrigin.y,
                        lotOrigin.z + config.Lot.RearSetback);
                case CityRoadFrontage.East:
                    return new int3(lotOrigin.x + config.Lot.RearSetback, lotOrigin.y,
                        lotOrigin.z + config.Lot.SideSetback);
                default:
                    return new int3(lotOrigin.x + config.Lot.FrontSetback, lotOrigin.y,
                        lotOrigin.z + config.Lot.SideSetback);
            }
        }

        private static bool InsidePlaza(in CityConfig config, int cx2, int cz2)
        {
            if (config.PlazaRadiusLots <= 0) return false;
            int radius2 = config.PlazaRadiusLots * 2;
            return math.abs(cx2) <= radius2 && math.abs(cz2) <= radius2;
        }

        private static CityDistrict ResolveDistrict(in CityConfig config, int cx2, int cz2, ulong identity)
        {
            int radial = math.max(math.abs(cx2), math.abs(cz2));
            int half = math.max(config.BlocksX, config.BlocksZ);
            if (radial <= math.max(2, config.PlazaRadiusLots * 2 + 2))
                return (Hash(identity ^ LandmarkSalt) & 1ul) == 0 ? CityDistrict.Civic : CityDistrict.Sacred;
            if (radial >= math.max(2, half - 2))
                return CityDistrict.Residential;
            return CityDistrict.Mixed;
        }

        private static int DensityFor(in CityConfig config, CityDistrict district)
        {
            switch (district)
            {
                case CityDistrict.Civic:
                case CityDistrict.Sacred:
                    return config.CivicDensityPermille;
                case CityDistrict.Mixed:
                    return config.MixedDensityPermille;
                default:
                    return config.ResidentialDensityPermille;
            }
        }

        private static bool TryResolveLandmark(
            in CityConfig config,
            ulong identity,
            CityDistrict district,
            int2 buildable,
            out CityStructureArchetype archetype,
            out CityStructurePresetId preset)
        {
            archetype = default;
            preset = default;
            int bestPriority = int.MinValue;
            bool found = false;
            CityDistrictMask districtBit = MaskFor(district);
            for (int i = 0; i < config.Landmarks.Length; i++)
            {
                CityLandmarkRule rule = config.Landmarks[i];
                if (rule.EveryNthEligibleLot <= 0 || (rule.Districts & districtBit) == 0 ||
                    buildable.x < rule.MinimumBuildableWidth || buildable.y < rule.MinimumBuildableDepth)
                    continue;
                ulong roll = Hash(identity ^ LandmarkSalt ^ (ulong)(uint)i * CandidateSalt);
                if (roll % (ulong)rule.EveryNthEligibleLot != 0 || rule.Priority < bestPriority)
                    continue;
                bestPriority = rule.Priority;
                archetype = rule.Archetype;
                preset = rule.PresetId;
                found = true;
            }
            return found;
        }

        private static bool TryResolvePalette(
            in CityConfig config,
            ulong identity,
            CityDistrict district,
            int2 buildable,
            out CityStructureArchetype archetype,
            out CityStructurePresetId preset)
        {
            archetype = default;
            preset = default;
            CityDistrictMask districtBit = MaskFor(district);
            int totalWeight = 0;
            for (int i = 0; i < config.Palette.Length; i++)
            {
                CityPaletteEntry entry = config.Palette[i];
                if ((entry.Districts & districtBit) == 0 || buildable.x < entry.MinimumBuildableWidth ||
                    buildable.y < entry.MinimumBuildableDepth)
                    continue;
                totalWeight += entry.Weight;
            }
            if (totalWeight <= 0) return false;

            int selected = (int)(Hash(identity ^ PaletteSalt) % (ulong)totalWeight);
            for (int i = 0; i < config.Palette.Length; i++)
            {
                CityPaletteEntry entry = config.Palette[i];
                if ((entry.Districts & districtBit) == 0 || buildable.x < entry.MinimumBuildableWidth ||
                    buildable.y < entry.MinimumBuildableDepth)
                    continue;
                if (selected < entry.Weight)
                {
                    archetype = entry.Archetype;
                    preset = entry.PresetId;
                    return true;
                }
                selected -= entry.Weight;
            }
            return false;
        }

        private static CityDistrictMask MaskFor(CityDistrict district) =>
            (CityDistrictMask)(1 << (int)district);

        private static Facing FacingFor(CityRoadFrontage frontage)
        {
            switch (frontage)
            {
                case CityRoadFrontage.North: return Facing.North;
                case CityRoadFrontage.East: return Facing.East;
                case CityRoadFrontage.South: return Facing.South;
                default: return Facing.West;
            }
        }

        private static ulong StableIdentity(ulong citySeed, int gx, int gz)
        {
            ulong packed = ((ulong)(uint)gx << 32) | (uint)gz;
            return Hash(citySeed ^ packed ^ CandidateSalt);
        }

        private static int Roll(ulong value) => (int)(Hash(value) % 1000ul);

        private static ulong Hash(ulong value)
        {
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9ul;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBul;
            value ^= value >> 31;
            return value;
        }
    }
}
