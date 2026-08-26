using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Mathematics;
using VoxelEngine.Vegetation.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Kentridge-specific adapter from semantic settlement geometry to the generic lightweight
    /// vegetation rules. Tree composition remains in KentridgeVegetationLayoutPlanner; this pass is
    /// deliberately limited to ground plants, moss and vines.
    /// </summary>
    public static class KentridgeDecorativeVegetationPlanner
    {
        private const float DecimetreMetres = 0.1f;

        public static List<VegetationInstance> BuildAnalytic(
            uint seed,
            int voxelsPerDecimetre = 1,
            float density = 0.72f)
        {
            // Editor-preview overload: no VoxelWorldGenSettings here, so this path stays
            // Kentridge-specific rather than inventing a settlement to preview.
            SettlementPlan plan = KentridgeDefinition.Build(seed);
            List<VegetationSurfaceSample> samples = BuildSurfaceSamples(
                plan, seed, math.max(1, voxelsPerDecimetre));

            VegetationPlacementSettings settings = VegetationPlacementSettings.Default(seed);
            settings.Density = density;
            settings.MinScale = 0.72f;
            settings.MaxScale = 1.30f;
            settings.MaxGroundSlopeDegrees = 48f;

            var result = new List<VegetationInstance>(samples.Count);
            VegetationPlacement.Generate(samples, settings, result);
            return result;
        }

        /// <summary>
        /// Exposed for tests and alternate render backends. Samples are semantic attachment points,
        /// not rendered instances, so biome rules remain owned by VoxelEngine.Vegetation.
        /// </summary>
        public static List<VegetationSurfaceSample> BuildSurfaceSamples(
            SettlementPlan plan,
            uint seed,
            int voxelsPerDecimetre = 1)
        {
            var samples = new List<VegetationSurfaceSample>(plan.Plots.Count * 7);
            int scale = math.max(1, voxelsPerDecimetre);

            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                Int3 footprint = SettlementFootprints.For(plan, plot.Archetype);
                AddGardenSamples(samples, plot, footprint, seed, scale);
                AddWallSamples(samples, plot, footprint, seed, scale);
            }

            AddMarketEdgeSamples(samples, plan, seed, scale);
            return samples;
        }

        private static void AddGardenSamples(
            List<VegetationSurfaceSample> samples,
            in BuildingPlot plot,
            in Int3 footprint,
            uint seed,
            int scale)
        {
            if (plot.District == DistrictKind.Civic || plot.District == DistrictKind.Market)
                return;

            int margin = plot.District == DistrictKind.Noble ? 18 : 10;
            int centreX = plot.PositionDm.X + footprint.X / 2;
            int centreZ = plot.PositionDm.Y + footprint.Z / 2;

            (int x, int z)[] points =
            {
                (plot.PositionDm.X - margin, centreZ),
                (plot.PositionDm.X + footprint.X + margin, centreZ),
                (centreX, plot.PositionDm.Y - margin),
                (centreX, plot.PositionDm.Y + footprint.Z + margin),
            };

            for (int p = 0; p < points.Length; p++)
            {
                int hash = StableHash(seed, plot.RoleId * 17 + p);
                float moisture = 0.25f + Positive01(hash) * 0.60f;
                float shade = plot.District == DistrictKind.Residential
                    ? 0.30f + Positive01(hash >> 5) * 0.45f
                    : 0.15f + Positive01(hash >> 5) * 0.35f;

                samples.Add(GroundSample(points[p].x, points[p].z,
                                         seed, scale, moisture, shade));
            }
        }

        private static void AddWallSamples(
            List<VegetationSurfaceSample> samples,
            in BuildingPlot plot,
            in Int3 footprint,
            uint seed,
            int scale)
        {
            WallFrame(plot, footprint,
                      out int backX, out int backZ, out float3 backNormal,
                      out int sideX, out int sideZ, out float3 sideNormal);

            float baseY = SurfaceMetres(backX, backZ, seed, scale);
            int hash = StableHash(seed ^ 0x51A7u, plot.RoleId);
            float wet = plot.District == DistrictKind.Working ? 0.82f : 0.58f;
            float shade = 0.48f + Positive01(hash) * 0.45f;

            samples.Add(new VegetationSurfaceSample
            {
                PositionMetres = new float3(backX * DecimetreMetres,
                                            baseY + 1.2f,
                                            backZ * DecimetreMetres),
                Normal = backNormal,
                Surface = WallSurface(plot),
                Moisture = wet,
                Shade = shade,
            });

            baseY = SurfaceMetres(sideX, sideZ, seed, scale);
            samples.Add(new VegetationSurfaceSample
            {
                PositionMetres = new float3(sideX * DecimetreMetres,
                                            baseY + 1.8f,
                                            sideZ * DecimetreMetres),
                Normal = sideNormal,
                Surface = WallSurface(plot),
                Moisture = math.saturate(wet + 0.10f),
                Shade = math.saturate(shade + 0.08f),
            });
        }

        private static void AddMarketEdgeSamples(
            List<VegetationSurfaceSample> samples,
            SettlementPlan plan,
            uint seed,
            int scale)
        {
            PlannedPlaza plaza = plan.Plaza;
            int halfX = plaza.SizeDm.X / 2;
            int halfZ = plaza.SizeDm.Y / 2;
            int x0 = plaza.CentreDm.X - halfX - 14;
            int x1 = plaza.CentreDm.X + halfX + 14;
            int z0 = plaza.CentreDm.Y - halfZ - 14;
            int z1 = plaza.CentreDm.Y + halfZ + 14;

            (int x, int z)[] points =
            {
                (x0, plaza.CentreDm.Y - halfZ / 2),
                (x0, plaza.CentreDm.Y + halfZ / 2),
                (x1, plaza.CentreDm.Y - halfZ / 2),
                (x1, plaza.CentreDm.Y + halfZ / 2),
                (plaza.CentreDm.X - halfX / 2, z0),
                (plaza.CentreDm.X + halfX / 2, z1),
            };

            for (int i = 0; i < points.Length; i++)
            {
                int hash = StableHash(seed ^ 0xB10Fu, i);
                samples.Add(GroundSample(points[i].x, points[i].z, seed, scale,
                                         0.30f + Positive01(hash) * 0.25f,
                                         0.08f + Positive01(hash >> 6) * 0.20f));
            }
        }

        private static VegetationSurfaceSample GroundSample(
            int xDm, int zDm, uint seed, int scale, float moisture, float shade)
        {
            return new VegetationSurfaceSample
            {
                PositionMetres = new float3(xDm * DecimetreMetres,
                                            SurfaceMetres(xDm, zDm, seed, scale) + 0.02f,
                                            zDm * DecimetreMetres),
                Normal = new float3(0f, 1f, 0f),
                Surface = VegetationSurface.Ground,
                Moisture = math.saturate(moisture),
                Shade = math.saturate(shade),
            };
        }

        private static float SurfaceMetres(int xDm, int zDm, uint seed, int scale)
        {
            int y = KentridgeVerticalProfile.SurfaceYAtDm(xDm, zDm, seed, scale);
            return y * (DecimetreMetres / scale);
        }

        private static VegetationSurface WallSurface(in BuildingPlot plot)
        {
            return plot.Archetype == StructureArchetype.Warehouse
                ? VegetationSurface.Wood
                : VegetationSurface.Masonry;
        }

        private static void WallFrame(
            in BuildingPlot plot,
            in Int3 footprint,
            out int backX, out int backZ, out float3 backNormal,
            out int sideX, out int sideZ, out float3 sideNormal)
        {
            int minX = plot.PositionDm.X;
            int maxX = minX + footprint.X;
            int minZ = plot.PositionDm.Y;
            int maxZ = minZ + footprint.Z;
            int cx = (minX + maxX) / 2;
            int cz = (minZ + maxZ) / 2;

            switch (plot.Frontage)
            {
                case FrontageDirection.North:
                    backX = cx; backZ = minZ;
                    backNormal = new float3(0f, 0f, -1f);
                    sideX = minX; sideZ = cz;
                    sideNormal = new float3(-1f, 0f, 0f);
                    break;
                case FrontageDirection.West:
                    backX = maxX; backZ = cz;
                    backNormal = new float3(1f, 0f, 0f);
                    sideX = cx; sideZ = maxZ;
                    sideNormal = new float3(0f, 0f, 1f);
                    break;
                case FrontageDirection.East:
                    backX = minX; backZ = cz;
                    backNormal = new float3(-1f, 0f, 0f);
                    sideX = cx; sideZ = minZ;
                    sideNormal = new float3(0f, 0f, -1f);
                    break;
                case FrontageDirection.South:
                default:
                    backX = cx; backZ = maxZ;
                    backNormal = new float3(0f, 0f, 1f);
                    sideX = maxX; sideZ = cz;
                    sideNormal = new float3(1f, 0f, 0f);
                    break;
            }
        }

        private static int StableHash(uint seed, int value)
        {
            uint x = seed ^ ((uint)value * 0x9E3779B9u);
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return unchecked((int)x);
        }

        private static float Positive01(int value)
        {
            return ((uint)value & 0x00FFFFFFu) / 16777216f;
        }
    }
}
