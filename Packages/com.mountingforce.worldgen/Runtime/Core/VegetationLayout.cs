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
        public readonly int RoadDirectionX;
        public readonly int RoadDirectionZ;
        public readonly int RoadWidth;
        public readonly int WallThickness;
        public readonly int WallHeight;
        public readonly int GateWidth;
        public readonly int GateHeight;
        public readonly int CourtyardRadius;
        public readonly int TowerCount;
        public readonly int KeepWidth;
        public readonly int KeepDepth;
        public readonly int KeepHeight;

        public CastleVegetationContext(
            uint planSeed,
            int centreX,
            int centreZ,
            int topY,
            int roadDirectionX,
            int roadDirectionZ,
            int roadWidth,
            int wallThickness,
            int wallHeight,
            int gateWidth,
            int gateHeight,
            int courtyardRadius,
            int towerCount,
            int keepWidth,
            int keepDepth,
            int keepHeight)
        {
            PlanSeed = planSeed;
            CentreX = centreX;
            CentreZ = centreZ;
            TopY = topY;
            RoadDirectionX = roadDirectionX;
            RoadDirectionZ = roadDirectionZ;
            RoadWidth = roadWidth;
            WallThickness = wallThickness;
            WallHeight = wallHeight;
            GateWidth = gateWidth;
            GateHeight = gateHeight;
            CourtyardRadius = courtyardRadius;
            TowerCount = towerCount;
            KeepWidth = keepWidth;
            KeepDepth = keepDepth;
            KeepHeight = keepHeight;
        }
    }

    public static class CastleVegetationLayout
    {
        private const int MinimumHeightUnits = 80;
        private const int MaximumHeightUnits = 260;

        public static void Build(
            in CastleVegetationContext context,
            ICollection<VegetationCandidate> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));

            int spacing = Math.Max(5, context.WallThickness + 3);
            int ring = Math.Max(context.CourtyardRadius + 3,
                                Math.Max(context.KeepWidth, context.KeepDepth) / 2 + 4);
            int ordinal = 0;

            int[][] directions =
            {
                new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 },
                new[] { 1, 1 }, new[] { 1, -1 }, new[] { -1, 1 }, new[] { -1, -1 },
            };

            foreach (int[] direction in directions)
            {
                int x = context.CentreX + direction[0] * ring;
                int z = context.CentreZ + direction[1] * ring;
                if (NearRoad(in context, x, z, spacing)) continue;

                uint h = Hash(context.PlanSeed, x, z, ordinal);
                SemanticTreeSpecies species = (h & 3u) switch
                {
                    0 => SemanticTreeSpecies.Oak,
                    1 => SemanticTreeSpecies.Pine,
                    2 => SemanticTreeSpecies.Birch,
                    _ => SemanticTreeSpecies.Maple,
                };
                int height = MinimumHeightUnits
                           + (int)((h >> 8) % (MaximumHeightUnits - MinimumHeightUnits + 1));
                output.Add(VegetationCandidate.Surface(
                    x, z, height, species,
                    context.TopY + context.KeepHeight,
                    context.TopY - context.WallHeight,
                    ordinal++));
            }

            int flank = Math.Max(context.RoadWidth + context.WallThickness + 4, spacing);
            for (int step = 1; step <= 3; step++)
            {
                int forwardX = context.CentreX + context.RoadDirectionX * (ring + step * spacing);
                int forwardZ = context.CentreZ + context.RoadDirectionZ * (ring + step * spacing);
                int lateralX = -context.RoadDirectionZ * flank;
                int lateralZ = context.RoadDirectionX * flank;

                AddRoadside(in context, forwardX + lateralX, forwardZ + lateralZ,
                            ordinal++, output);
                AddRoadside(in context, forwardX - lateralX, forwardZ - lateralZ,
                            ordinal++, output);
            }
        }

        private static void AddRoadside(
            in CastleVegetationContext context,
            int x,
            int z,
            int ordinal,
            ICollection<VegetationCandidate> output)
        {
            uint h = Hash(context.PlanSeed ^ 0x4f1bbcdcu, x, z, ordinal);
            int height = MinimumHeightUnits + (int)((h >> 7) % 121u);
            SemanticTreeSpecies species = (h & 1u) == 0
                ? SemanticTreeSpecies.Oak
                : SemanticTreeSpecies.Pine;
            output.Add(VegetationCandidate.Surface(
                x, z, height, species,
                context.TopY + context.KeepHeight,
                context.TopY - context.WallHeight,
                ordinal));
        }

        private static bool NearRoad(
            in CastleVegetationContext context,
            int x,
            int z,
            int margin)
        {
            int dx = x - context.CentreX;
            int dz = z - context.CentreZ;
            int cross = Math.Abs(dx * context.RoadDirectionZ - dz * context.RoadDirectionX);
            return cross <= context.RoadWidth + margin;
        }

        private static uint Hash(uint seed, int x, int z, int ordinal)
        {
            uint h = seed ^ 0x9e3779b9u;
            h ^= unchecked((uint)x) * 0x85ebca6bu;
            h = (h << 13) | (h >> 19);
            h ^= unchecked((uint)z) * 0xc2b2ae35u;
            h ^= unchecked((uint)ordinal) * 0x27d4eb2du;
            h ^= h >> 16;
            h *= 0x7feb352du;
            h ^= h >> 15;
            h *= 0x846ca68bu;
            h ^= h >> 16;
            return h;
        }
    }
}