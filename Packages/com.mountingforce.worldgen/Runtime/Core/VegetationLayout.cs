using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen
{
    public enum SemanticTreeSpecies : byte
    {
        Oak,
        Pine,
        Birch,
        Maple,
        Sakura,
        Willow,
        Dead
    }

    public enum VegetationHeightMode : byte
    {
        Fixed,
        SampleSurface
    }

    /// <summary>
    /// Engine-independent semantic vegetation candidate. Coordinates are authored in the worldgen
    /// integer grid; a realization adapter is responsible for sampling terrain and translating the
    /// species into the runtime vegetation model.
    /// </summary>
    public readonly struct VegetationCandidate
    {
        public readonly int X;
        public readonly int Z;
        public readonly int HeightUnits;
        public readonly SemanticTreeSpecies Species;
        public readonly VegetationHeightMode HeightMode;
        public readonly int FixedRootY;
        public readonly int SurfaceMaxY;
        public readonly int SurfaceMinY;
        public readonly int Ordinal;

        private VegetationCandidate(int x, int z, int heightUnits,
                                    SemanticTreeSpecies species,
                                    VegetationHeightMode heightMode,
                                    int fixedRootY, int surfaceMaxY, int surfaceMinY,
                                    int ordinal)
        {
            X = x;
            Z = z;
            HeightUnits = heightUnits;
            Species = species;
            HeightMode = heightMode;
            FixedRootY = fixedRootY;
            SurfaceMaxY = surfaceMaxY;
            SurfaceMinY = surfaceMinY;
            Ordinal = ordinal;
        }

        public static VegetationCandidate Fixed(int x, int y, int z, int heightUnits,
                                                SemanticTreeSpecies species, int ordinal) =>
            new(x, z, heightUnits, species, VegetationHeightMode.Fixed,
                y, 0, 0, ordinal);

        public static VegetationCandidate Surface(int x, int z, int heightUnits,
                                                  SemanticTreeSpecies species,
                                                  int maxY, int minY, int ordinal) =>
            new(x, z, heightUnits, species, VegetationHeightMode.SampleSurface,
                0, maxY, minY, ordinal);
    }

    /// <summary>
    /// Primitive-only context needed by the castle vegetation grammar. Keeping this separate from
    /// CastlePlan prevents semantic layout rules from depending on voxel storage or Unity types.
    /// </summary>
    public readonly struct CastleVegetationContext
    {
        public readonly uint PlanSeed;
        public readonly int CentreX;
        public readonly int CentreZ;
        public readonly int TopY;
        public readonly int GateZ;
        public readonly int PlateauRadius;
        public readonly int BaileyHalfX;
        public readonly int BaileyHalfZ;
        public readonly int TowerRadius;
        public readonly int WaterfallStreamX;
        public readonly int WaterfallLipZ;
        public readonly int LowerRiverZ;

        public CastleVegetationContext(uint planSeed,
                                       int centreX, int centreZ, int topY, int gateZ,
                                       int plateauRadius, int baileyHalfX, int baileyHalfZ,
                                       int towerRadius, int waterfallStreamX,
                                       int waterfallLipZ, int lowerRiverZ)
        {
            PlanSeed = planSeed;
            CentreX = centreX;
            CentreZ = centreZ;
            TopY = topY;
            GateZ = gateZ;
            PlateauRadius = plateauRadius;
            BaileyHalfX = baileyHalfX;
            BaileyHalfZ = baileyHalfZ;
            TowerRadius = towerRadius;
            WaterfallStreamX = waterfallStreamX;
            WaterfallLipZ = waterfallLipZ;
            LowerRiverZ = lowerRiverZ;
        }
    }

    /// <summary>
    /// Pure deterministic placement grammar. It knows nothing about voxels, Unity, rendering, or
    /// gameplay state; callers receive semantic candidates and decide how/where to realize them.
    /// </summary>
    public static class CastleVegetationLayoutPlanner
    {
        public static List<VegetationCandidate> Build(in CastleVegetationContext context)
        {
            var result = new List<VegetationCandidate>(40);
            int ordinal = 0;
            AddTreeBelt(in context, result, ref ordinal);
            AddApproachTrees(in context, result, ref ordinal);
            AddForegroundCopse(in context, result, ref ordinal);
            AddWaterfallTrees(in context, result, ref ordinal);
            return result;
        }

        private static void AddTreeBelt(in CastleVegetationContext c,
                                        List<VegetationCandidate> result, ref int ordinal)
        {
            var rng = new StableRandom(c.PlanSeed ^ 0x7EE5u);
            int built = 0;
            for (int attempt = 0; attempt < 96 && built < 22; attempt++)
            {
                double angle = rng.NextDouble() * Math.PI * 2.0;
                double radius = Lerp(c.PlateauRadius * 0.74,
                                     c.PlateauRadius - 26.0, rng.NextDouble());
                int ox = (int)Math.Round(Math.Cos(angle) * radius);
                int oz = (int)Math.Round(Math.Sin(angle) * radius);

                bool outsideWalls = Math.Abs(ox) > c.BaileyHalfX + c.TowerRadius + 16
                                 || Math.Abs(oz) > c.BaileyHalfZ + c.TowerRadius + 16;
                bool blocksGate = oz < -c.BaileyHalfZ && Math.Abs(ox) < 105;
                int waterfallOffsetX = c.WaterfallStreamX - c.CentreX;
                int waterfallOffsetZ = c.WaterfallLipZ - c.CentreZ;
                bool nearWaterfall = Math.Abs(ox - waterfallOffsetX) < 125
                                  && Math.Abs(oz - waterfallOffsetZ) < 165;
                if (!outsideWalls || blocksGate || nearWaterfall) continue;

                int height = rng.NextInt(34, 58);
                result.Add(VegetationCandidate.Fixed(
                    c.CentreX + ox, c.TopY + 1, c.CentreZ + oz,
                    height, BroadleafSpecies(built), ordinal++));
                built++;
            }
        }

        private static void AddApproachTrees(in CastleVegetationContext c,
                                             List<VegetationCandidate> result, ref int ordinal)
        {
            (int x, int z)[] offsets =
            {
                (-178, -92), (168, -78), (-235, -105), (235, -110),
                (-154, 42), (184, 62),
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                int x = c.CentreX + offsets[i].x;
                int z = c.GateZ + offsets[i].z;
                SemanticTreeSpecies species = (i & 1) == 0
                    ? SemanticTreeSpecies.Pine : BroadleafSpecies(i + 3);
                int height = (i & 1) == 0 ? 58 + (i % 3) * 8 : 44 + (i % 3) * 6;
                result.Add(VegetationCandidate.Surface(
                    x, z, height, species, c.TopY + 20, c.TopY - 170, ordinal++));
            }
        }

        private static void AddForegroundCopse(in CastleVegetationContext c,
                                               List<VegetationCandidate> result, ref int ordinal)
        {
            (int x, int z)[] offsets =
            {
                (-260, -82), (-282, -48), (266, -62), (292, -30),
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                result.Add(VegetationCandidate.Surface(
                    c.CentreX + offsets[i].x,
                    c.GateZ + offsets[i].z,
                    44 + i * 5,
                    SemanticTreeSpecies.Pine,
                    c.TopY + 18, c.TopY - 120, ordinal++));
            }
        }

        private static void AddWaterfallTrees(in CastleVegetationContext c,
                                              List<VegetationCandidate> result, ref int ordinal)
        {
            int poolX = c.WaterfallStreamX;
            int poolZ = c.LowerRiverZ + 27;
            (int x, int z)[] offsets = { (-88, 58), (92, 72), (-105, -28), (108, -18) };

            for (int i = 0; i < offsets.Length; i++)
            {
                SemanticTreeSpecies species = (i & 1) == 0
                    ? (i == 0 ? SemanticTreeSpecies.Sakura : SemanticTreeSpecies.Willow)
                    : SemanticTreeSpecies.Pine;
                int height = (i & 1) == 0 ? 40 + i * 3 : 45 + i * 3;
                result.Add(VegetationCandidate.Surface(
                    poolX + offsets[i].x, poolZ + offsets[i].z,
                    height, species, c.TopY + 24, c.TopY - 180, ordinal++));
            }
        }

        private static SemanticTreeSpecies BroadleafSpecies(int index)
        {
            switch (index & 7)
            {
                case 0: return SemanticTreeSpecies.Oak;
                case 1: return SemanticTreeSpecies.Sakura;
                case 2: return SemanticTreeSpecies.Birch;
                case 3: return SemanticTreeSpecies.Maple;
                case 4: return SemanticTreeSpecies.Willow;
                case 5: return SemanticTreeSpecies.Oak;
                case 6: return SemanticTreeSpecies.Sakura;
                default: return SemanticTreeSpecies.Dead;
            }
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private struct StableRandom
        {
            private uint _state;

            public StableRandom(uint seed) => _state = seed == 0 ? 0xA341316Cu : seed;

            private uint NextUInt()
            {
                uint x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x == 0 ? 0xA341316Cu : x;
                return _state;
            }

            public double NextDouble() => (NextUInt() >> 8) * (1.0 / 16777216.0);

            public int NextInt(int minInclusive, int maxExclusive)
            {
                if (maxExclusive <= minInclusive) return minInclusive;
                uint span = (uint)(maxExclusive - minInclusive);
                return minInclusive + (int)(NextUInt() % span);
            }
        }
    }
}
