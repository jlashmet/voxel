using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// One analytic vertical mountain mass. The voxel compiler consumes these exact descriptors and
    /// <see cref="MountainLandformSurface"/> evaluates their exact frustum envelope for queries, so
    /// road resolution and physical realization cannot drift onto different terrain truths.
    /// </summary>
    public readonly struct MountainLandformMass
    {
        public int CentreXdm { get; }
        public int BaseYdm { get; }
        public int CentreZdm { get; }
        public int HeightDm { get; }
        public int BaseRadiusDm { get; }
        public int TopRadiusDm { get; }

        public int TopYdm => BaseYdm + HeightDm - 1;

        internal MountainLandformMass(
            int centreXdm,
            int baseYdm,
            int centreZdm,
            int heightDm,
            int baseRadiusDm,
            int topRadiusDm)
        {
            CentreXdm = centreXdm;
            BaseYdm = baseYdm;
            CentreZdm = centreZdm;
            HeightDm = Math.Max(1, heightDm);
            BaseRadiusDm = Math.Max(1, baseRadiusDm);
            TopRadiusDm = Clamp(topRadiusDm, 0, BaseRadiusDm);
        }

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : value > max ? max : value;
    }

    /// <summary>
    /// Deterministic integer mountain surface authority.
    ///
    /// A compact ridged multi-octave value-noise field establishes macro ridge energy around the
    /// mountain. A bounded thermal-relaxation pass transfers excess angular relief toward adjacent
    /// sectors (the same qualitative effect as talus erosion), then the result is compiled into a
    /// small union of exact analytic frusta. Massif cores use vertically joined frustum bands whose
    /// seam radii and heights are shared exactly; that preserves one terrain truth while avoiding a
    /// single giant planar slope. Aspect shoulders, ridges and roughness remain bounded additions.
    /// </summary>
    public sealed class MountainLandformSurface : IWorldRoadTerrain
    {
        private const int SectorCount = 16;
        private const int DirectionScale = 1024;

        private static readonly int[] DirectionX =
        {
            1024, 946, 724, 392, 0, -392, -724, -946,
            -1024, -946, -724, -392, 0, 392, 724, 946,
        };

        private static readonly int[] DirectionZ =
        {
            0, 392, 724, 946, 1024, 946, 724, 392,
            0, -392, -724, -946, -1024, -946, -724, -392,
        };

        private readonly MountainLandformMass[] _masses;
        private readonly int[] _ridgeProfilePermille;

        public MountainLandformSpec Spec { get; }
        public int MassCount => _masses.Length;

        public MountainLandformSurface(in MountainLandformSpec spec)
        {
            Spec = spec;
            _ridgeProfilePermille = BuildRidgeProfile(in spec);
            _masses = BuildMasses(in spec, _ridgeProfilePermille);
        }

        public MountainLandformMass GetMass(int index)
        {
            if ((uint)index >= (uint)_masses.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return _masses[index];
        }

        public int HeightAtDm(int xdm, int zdm)
        {
            int highest = Spec.OriginYdm;
            for (int i = 0; i < _masses.Length; i++)
            {
                int candidate = SurfaceHeight(in _masses[i], xdm, zdm);
                if (candidate > highest) highest = candidate;
            }
            return highest;
        }

        public WorldRoadTerrainFlags FlagsAtDm(int xdm, int zdm) => WorldRoadTerrainFlags.None;

        private static MountainLandformMass[] BuildMasses(
            in MountainLandformSpec spec,
            int[] ridgeProfile)
        {
            var masses = new List<MountainLandformMass>(48);
            int minRadius = Math.Min(spec.RadiusXdm, spec.RadiusZdm);
            int majorRadius = Math.Max(spec.RadiusXdm, spec.RadiusZdm);
            bool majorAlongX = spec.RadiusXdm >= spec.RadiusZdm;

            int summitShiftLimit = Math.Max(0, (minRadius - spec.SummitRadiusDm) / 3);
            int summitX = spec.OriginXdm
                + summitShiftLimit * spec.AsymmetryXPermille / 500;
            int summitZ = spec.OriginZdm
                + summitShiftLimit * spec.AsymmetryZPermille / 500;

            int macroBasePermille;
            switch (spec.MacroShape)
            {
                case MountainMacroShape.Massif: macroBasePermille = 1000; break;
                case MountainMacroShape.Pyramidal: macroBasePermille = 900; break;
                default: macroBasePermille = 860; break;
            }

            int summitTopPermille;
            switch (spec.SummitCharacter)
            {
                case MountainSummitCharacter.Broad: summitTopPermille = 1000; break;
                case MountainSummitCharacter.Rounded: summitTopPermille = 720; break;
                default: summitTopPermille = 480; break;
            }

            int coreRadius = Math.Max(spec.SummitRadiusDm + 1, minRadius * macroBasePermille / 1000);
            int coreTop = Math.Max(1, spec.SummitRadiusDm * summitTopPermille / 1000);
            AddCoreEnvelope(masses, in spec, summitX, summitZ, coreRadius, coreTop);

            AddAspectShoulders(masses, in spec, summitX, summitZ, minRadius, majorRadius, majorAlongX);
            AddRidges(masses, in spec, ridgeProfile, summitX, summitZ, minRadius);
            AddRoughnessMasses(masses, in spec, ridgeProfile, summitX, summitZ, minRadius);
            AddSummitCharacter(masses, in spec, summitX, summitZ, minRadius);

            return masses.ToArray();
        }

        private static void AddCoreEnvelope(
            List<MountainLandformMass> masses,
            in MountainLandformSpec spec,
            int summitX,
            int summitZ,
            int coreRadius,
            int coreTop)
        {
            int radialRun = coreRadius - coreTop;
            if (spec.MacroShape != MountainMacroShape.Massif
                || radialRun < 8
                || spec.HeightDm < 8)
            {
                masses.Add(new MountainLandformMass(
                    summitX, spec.OriginYdm, summitZ,
                    spec.HeightDm, coreRadius, coreTop));
                return;
            }

            // A single full-height frustum produces one enormous constant-slope face at landmark
            // scale. These four vertically joined bands describe the same semantic massif with a
            // continuous radial profile: broad/gentle foothills transition through steeper upper
            // mountain and finish at the authored summit radius. Adjacent bands overlap exactly on
            // one seam layer (same Y and radius), so there is no floating support or horizontal
            // terrace for roads, collision, or voxel realization to disagree about.
            int totalRise = spec.HeightDm - 1;
            int[] cumulativeRunPermille = { 390, 670, 860, 1000 };
            int[] cumulativeRisePermille = { 230, 480, 755, 1000 };
            int baseY = spec.OriginYdm;
            int baseRadius = coreRadius;

            for (int i = 0; i < cumulativeRunPermille.Length; i++)
            {
                int topRadius = i == cumulativeRunPermille.Length - 1
                    ? coreTop
                    : coreRadius - radialRun * cumulativeRunPermille[i] / 1000;
                int topY = i == cumulativeRisePermille.Length - 1
                    ? spec.OriginYdm + totalRise
                    : spec.OriginYdm + totalRise * cumulativeRisePermille[i] / 1000;

                topRadius = Math.Max(coreTop, Math.Min(baseRadius - 1, topRadius));
                topY = Math.Max(baseY + 1, topY);
                masses.Add(new MountainLandformMass(
                    summitX,
                    baseY,
                    summitZ,
                    topY - baseY + 1,
                    baseRadius,
                    topRadius));

                baseY = topY;
                baseRadius = topRadius;
            }
        }

        private static void AddAspectShoulders(
            List<MountainLandformMass> masses,
            in MountainLandformSpec spec,
            int summitX,
            int summitZ,
            int minRadius,
            int majorRadius,
            bool majorAlongX)
        {
            int excess = Math.Max(0, majorRadius - minRadius);
            if (excess <= minRadius / 8) return;

            int offset = Math.Max(1, excess / 2);
            int radius = Math.Max(spec.SummitRadiusDm + 1, minRadius * 3 / 4);
            int height = Math.Max(2, spec.HeightDm * 7 / 10);
            int top = Math.Max(1, radius / 5);

            masses.Add(new MountainLandformMass(
                summitX + (majorAlongX ? offset : 0), spec.OriginYdm,
                summitZ + (majorAlongX ? 0 : offset), height, radius, top));
            masses.Add(new MountainLandformMass(
                summitX - (majorAlongX ? offset : 0), spec.OriginYdm,
                summitZ - (majorAlongX ? 0 : offset), height * 9 / 10, radius, top));
        }

        private static void AddRidges(
            List<MountainLandformMass> masses,
            in MountainLandformSpec spec,
            int[] ridgeProfile,
            int summitX,
            int summitZ,
            int minRadius)
        {
            if (spec.RidgeCount <= 0 || spec.RidgeStrengthPermille <= 0) return;

            bool[] selected = SelectRidgeSectors(ridgeProfile, spec.RidgeCount);
            for (int sector = 0; sector < SectorCount; sector++)
            {
                if (!selected[sector]) continue;

                int energy = ridgeProfile[sector] * spec.RidgeStrengthPermille / 1000;
                int macroBoost = spec.MacroShape == MountainMacroShape.Ridged
                    ? 1200
                    : spec.MacroShape == MountainMacroShape.Pyramidal ? 1050 : 900;
                energy = Clamp(energy * macroBoost / 1000, 0, 1000);

                int dx = DirectionX[sector];
                int dz = DirectionZ[sector];
                int firstDistance = minRadius * (32 + energy * 10 / 1000) / 100;
                int secondDistance = minRadius * (58 + energy * 14 / 1000) / 100;
                int thirdDistance = minRadius * (76 + energy * 10 / 1000) / 100;

                AddRidgeMass(
                    masses, in spec, summitX, summitZ, dx, dz,
                    firstDistance,
                    spec.HeightDm * (62 + energy * 22 / 1000) / 100,
                    minRadius * (30 + energy * 10 / 1000) / 100,
                    minRadius * 11 / 100);
                AddRidgeMass(
                    masses, in spec, summitX, summitZ, dx, dz,
                    secondDistance,
                    spec.HeightDm * (42 + energy * 20 / 1000) / 100,
                    minRadius * (24 + energy * 8 / 1000) / 100,
                    minRadius * 7 / 100);
                AddRidgeMass(
                    masses, in spec, summitX, summitZ, dx, dz,
                    thirdDistance,
                    spec.HeightDm * (24 + energy * 14 / 1000) / 100,
                    minRadius * (18 + energy * 6 / 1000) / 100,
                    minRadius * 4 / 100);
            }
        }

        private static void AddRidgeMass(
            List<MountainLandformMass> masses,
            in MountainLandformSpec spec,
            int summitX,
            int summitZ,
            int directionX,
            int directionZ,
            int distance,
            int height,
            int baseRadius,
            int topRadius)
        {
            int centreX = summitX + directionX * distance / DirectionScale;
            int centreZ = summitZ + directionZ * distance / DirectionScale;
            masses.Add(new MountainLandformMass(
                centreX, spec.OriginYdm, centreZ,
                Math.Max(2, height), Math.Max(2, baseRadius), Math.Max(1, topRadius)));
        }

        private static void AddRoughnessMasses(
            List<MountainLandformMass> masses,
            in MountainLandformSpec spec,
            int[] ridgeProfile,
            int summitX,
            int summitZ,
            int minRadius)
        {
            if (spec.RoughnessAmplitudeDm <= 0) return;

            int count = Clamp((minRadius * 2) / spec.RoughnessScaleDm, 4, 12);
            int baseRadius = Clamp(spec.RoughnessScaleDm / 2, Math.Max(2, minRadius / 14), Math.Max(3, minRadius / 4));
            for (int i = 0; i < count; i++)
            {
                uint h = Hash(spec.Seed ^ 0xA2C79B3Du, i, 0);
                int sector = (int)(h % SectorCount);
                int radialPermille = 280 + (int)((h >> 8) % 500u);
                int distance = minRadius * radialPermille / 1000;
                int jitter = SignedHash(spec.Seed ^ 0x6B5F4A13u, i, sector);
                int relief = spec.RoughnessAmplitudeDm * jitter / 1000;
                int ridgeLift = spec.RoughnessAmplitudeDm * ridgeProfile[sector] / 2000;
                int baseline = spec.HeightDm * (620 - radialPermille / 2) / 1000;
                int height = Clamp(
                    baseline + relief + ridgeLift,
                    Math.Max(2, spec.HeightDm / 8),
                    Math.Max(2, spec.HeightDm * 3 / 4));

                int dx = DirectionX[sector];
                int dz = DirectionZ[sector];
                int tangentX = -dz;
                int tangentZ = dx;
                int tangentJitter = jitter * Math.Max(1, spec.RoughnessScaleDm / 3) / 1000;
                int centreX = summitX + dx * distance / DirectionScale + tangentX * tangentJitter / DirectionScale;
                int centreZ = summitZ + dz * distance / DirectionScale + tangentZ * tangentJitter / DirectionScale;
                int localRadius = Clamp(
                    baseRadius + baseRadius * ridgeProfile[sector] / 2500,
                    2,
                    Math.Max(3, minRadius / 3));

                masses.Add(new MountainLandformMass(
                    centreX, spec.OriginYdm, centreZ,
                    height, localRadius, Math.Max(1, localRadius / 5)));
            }
        }

        private static void AddSummitCharacter(
            List<MountainLandformMass> masses,
            in MountainLandformSpec spec,
            int summitX,
            int summitZ,
            int minRadius)
        {
            if (spec.SummitCharacter != MountainSummitCharacter.Craggy) return;

            int cragRadius = Math.Max(2, spec.SummitRadiusDm / 3);
            int offset = Math.Max(1, spec.SummitRadiusDm / 2);
            for (int i = 0; i < 3; i++)
            {
                int sector = (int)(Hash(spec.Seed ^ 0xC3E17A55u, i, 3) % SectorCount);
                int heightDrop = spec.RoughnessAmplitudeDm > 0
                    ? (int)(Hash(spec.Seed, i, 7) % (uint)(spec.RoughnessAmplitudeDm + 1))
                    : 0;
                int height = Math.Max(2, spec.HeightDm - heightDrop / 2);
                masses.Add(new MountainLandformMass(
                    summitX + DirectionX[sector] * offset / DirectionScale,
                    spec.OriginYdm,
                    summitZ + DirectionZ[sector] * offset / DirectionScale,
                    height, cragRadius, Math.Max(1, cragRadius / 3)));
            }
        }

        private static int[] BuildRidgeProfile(in MountainLandformSpec spec)
        {
            var profile = new int[SectorCount];
            for (int sector = 0; sector < SectorCount; sector++)
            {
                int octave2 = Ridged(CircularValueNoise(spec.Seed, sector, 2));
                int octave4 = Ridged(CircularValueNoise(spec.Seed ^ 0x9E3779B9u, sector, 4));
                int octave8 = Ridged(CircularValueNoise(spec.Seed ^ 0x85EBCA6Bu, sector, 8));
                int value = (octave2 * 4 + octave4 * 2 + octave8) / 7;

                if (spec.MacroShape == MountainMacroShape.Massif)
                    value = 350 + value * 550 / 1000;
                else if (spec.MacroShape == MountainMacroShape.Pyramidal)
                    value = 180 + value * 760 / 1000;
                else
                    value = 80 + value * 920 / 1000;

                profile[sector] = Clamp(value, 0, 1000);
            }

            ThermalRelax(profile, spec.ErosionStrengthPermille);
            return profile;
        }

        private static void ThermalRelax(int[] values, int strengthPermille)
        {
            if (strengthPermille <= 0) return;

            int passes = 1 + strengthPermille * 3 / 1000;
            int talus = 300 - strengthPermille * 180 / 1000;
            var delta = new int[SectorCount];
            for (int pass = 0; pass < passes; pass++)
            {
                Array.Clear(delta, 0, delta.Length);
                for (int i = 0; i < SectorCount; i++)
                {
                    int next = (i + 1) % SectorCount;
                    int difference = values[i] - values[next];
                    int magnitude = Math.Abs(difference);
                    if (magnitude <= talus) continue;

                    int transfer = (magnitude - talus) * strengthPermille / 4000;
                    transfer = Math.Max(1, transfer);
                    if (difference > 0)
                    {
                        delta[i] -= transfer;
                        delta[next] += transfer;
                    }
                    else
                    {
                        delta[i] += transfer;
                        delta[next] -= transfer;
                    }
                }

                for (int i = 0; i < SectorCount; i++)
                    values[i] = Clamp(values[i] + delta[i], 0, 1000);
            }
        }

        private static bool[] SelectRidgeSectors(int[] profile, int requested)
        {
            var selected = new bool[SectorCount];
            int count = Math.Min(requested, SectorCount);
            int minimumSpacing = count <= 1 ? 0 : Math.Max(1, SectorCount / (count * 2));

            for (int selection = 0; selection < count; selection++)
            {
                int best = -1;
                int bestValue = int.MinValue;
                for (int sector = 0; sector < SectorCount; sector++)
                {
                    if (selected[sector] || IsNearSelected(selected, sector, minimumSpacing)) continue;
                    if (profile[sector] <= bestValue) continue;
                    best = sector;
                    bestValue = profile[sector];
                }

                if (best < 0)
                {
                    for (int sector = 0; sector < SectorCount; sector++)
                    {
                        if (selected[sector] || profile[sector] <= bestValue) continue;
                        best = sector;
                        bestValue = profile[sector];
                    }
                }

                if (best >= 0) selected[best] = true;
            }

            return selected;
        }

        private static bool IsNearSelected(bool[] selected, int sector, int spacing)
        {
            if (spacing <= 0) return false;
            for (int i = 0; i < SectorCount; i++)
            {
                if (!selected[i]) continue;
                int distance = Math.Abs(i - sector);
                distance = Math.Min(distance, SectorCount - distance);
                if (distance <= spacing) return true;
            }
            return false;
        }

        private static int CircularValueNoise(uint seed, int sector, int anchorCount)
        {
            int scaled = sector * anchorCount;
            int anchor = scaled / SectorCount;
            int remainder = scaled % SectorCount;
            int a = SignedHash(seed, anchor, anchorCount);
            int b = SignedHash(seed, (anchor + 1) % anchorCount, anchorCount);
            int tPermille = remainder * 1000 / SectorCount;
            return a + (b - a) * tPermille / 1000;
        }

        private static int Ridged(int signedNoise) => 1000 - Math.Abs(Clamp(signedNoise, -1000, 1000));

        private static int SignedHash(uint seed, int a, int b) =>
            (int)(Hash(seed, a, b) % 2001u) - 1000;

        private static uint Hash(uint seed, int a, int b)
        {
            uint h = seed ^ ((uint)a * 0x9E3779B9u) ^ ((uint)b * 0x85EBCA6Bu) ^ 0xC2B2AE35u;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }

        private static int SurfaceHeight(in MountainLandformMass mass, int xdm, int zdm)
        {
            long dx = (long)xdm - mass.CentreXdm;
            long dz = (long)zdm - mass.CentreZdm;
            long distanceSquared = dx * dx + dz * dz;
            if (distanceSquared > (long)mass.BaseRadiusDm * mass.BaseRadiusDm)
                return int.MinValue;

            int length = Math.Max(1, mass.HeightDm - 1);
            int low = 0;
            int high = mass.HeightDm - 1;
            int best = 0;
            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                int radius = mass.BaseRadiusDm
                    + (mass.TopRadiusDm - mass.BaseRadiusDm) * mid / length;
                if (distanceSquared <= (long)radius * radius)
                {
                    best = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return mass.BaseYdm + best;
        }

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : value > max ? max : value;
    }
}
