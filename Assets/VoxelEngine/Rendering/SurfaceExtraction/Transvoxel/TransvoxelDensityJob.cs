using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Core.Features;

namespace VoxelEngine.Rendering.SurfaceExtraction.Transvoxel
{
    [BurstCompile]
    public struct TransvoxelDensityJob : IJobParallelFor
    {
        [ReadOnly] public RegionTable Table;
        [ReadOnly] public BrickPool Pool;
        [ReadOnly] public SurfaceCatalogue Catalogue;
        public int3 ChunkOriginVoxel;
        public int ChunkEdge;
        public int SourceStep;
        public int Padding;

        [WriteOnly] public NativeArray<float> Density;
        [WriteOnly] public NativeArray<byte> Materials;
        [WriteOnly] public NativeArray<uint> SurfaceSemantics;
        [WriteOnly] public NativeArray<byte> BoundarySamples;

        public void Execute(int index)
        {
            int paddedEdge = ChunkEdge + Padding * 2;
            int plane = paddedEdge * paddedEdge;
            int gz = index / plane;
            int remainder = index - gz * plane;
            int gy = remainder / paddedEdge;
            int gx = remainder - gy * paddedEdge;
            int3 p = ChunkOriginVoxel
                   + (new int3(gx, gy, gz) - Padding) * SourceStep;

            Density[index] = SampleField(
                p, out byte material, out uint surface, out byte boundary);
            Materials[index] = material;
            SurfaceSemantics[index] = surface;
            BoundarySamples[index] = boundary;
        }

        private float SampleField(int3 p, out byte dominantMaterial, out uint dominantSurface,
                                  out byte dominantBoundary)
        {
            byte centre = ReadMaterial(p, out uint centreSurface, out byte packedBoundary);
            dominantBoundary = packedBoundary;
            bool centreSolid = IsSolidSample(centre);
            centreSurface = ResolveSurface(centre, centreSurface);
            if (packedBoundary != 0 && HasOppositeOccupancyNeighbour(p, centreSolid))
            {
                dominantMaterial = centreSolid ? centre : (byte)0;
                dominantSurface = centreSolid ? centreSurface : 0u;
                var boundary = new VoxelBoundarySample { Packed = packedBoundary };
                return boundary.SignedQ3 * 0.125f + CoatingDisplacement(centreSurface);
            }
            ushort style = (ushort)centreSurface;
            SurfaceStyleDefinition centreDefinition = Catalogue.Get(style);
            if (centreSolid && (centreDefinition.Reconstruction == SurfaceReconstruction.Planar
                                || centreDefinition.Reconstruction == SurfaceReconstruction.Sharp
                                || centreDefinition.Reconstruction == SurfaceReconstruction.Cubic))
            {
                dominantMaterial = centre;
                dominantSurface = centreSurface;
                return 0.5f + CoatingDisplacement(centreSurface);
            }

            float curvature = CurvatureFactor(centreDefinition);
            float centreValue = centreSolid ? 0.5f : -0.5f;
            float sum = centreValue * 4f;
            float weight = 4f;
            byte bestMaterial = centre;
            uint bestSurface = centreSurface;
            float bestContribution = centreSolid ? 4f : -4f;

            SampleNeighbour(p + new int3(1, 0, 0), 1f, ref sum, ref weight,
                            ref bestContribution, ref bestMaterial, ref bestSurface);
            SampleNeighbour(p + new int3(-1, 0, 0), 1f, ref sum, ref weight,
                            ref bestContribution, ref bestMaterial, ref bestSurface);
            SampleNeighbour(p + new int3(0, 1, 0), 1f, ref sum, ref weight,
                            ref bestContribution, ref bestMaterial, ref bestSurface);
            SampleNeighbour(p + new int3(0, -1, 0), 1f, ref sum, ref weight,
                            ref bestContribution, ref bestMaterial, ref bestSurface);
            SampleNeighbour(p + new int3(0, 0, 1), 1f, ref sum, ref weight,
                            ref bestContribution, ref bestMaterial, ref bestSurface);
            SampleNeighbour(p + new int3(0, 0, -1), 1f, ref sum, ref weight,
                            ref bestContribution, ref bestMaterial, ref bestSurface);

            dominantMaterial = bestContribution > 0f ? bestMaterial : (byte)0;
            dominantSurface = bestContribution > 0f ? bestSurface : 0u;
            return math.lerp(centreValue, sum / weight, curvature)
                 + CoatingDisplacement(centreSurface);
        }

        private void SampleNeighbour(
            int3 p,
            float neighbourWeight,
            ref float sum,
            ref float weight,
            ref float bestContribution,
            ref byte bestMaterial,
            ref uint bestSurface)
        {
            byte material = ReadMaterial(p, out uint surface, out _);
            bool solid = IsSolidSample(material);
            surface = ResolveSurface(material, surface);
            float signed = solid ? 0.5f : -0.5f;
            sum += signed * neighbourWeight;
            weight += neighbourWeight;
            float contribution = signed * neighbourWeight;
            if (contribution <= bestContribution) return;
            bestContribution = contribution;
            bestMaterial = material;
            bestSurface = surface;
        }

        private bool HasOppositeOccupancyNeighbour(int3 p, bool centreSolid)
        {
            return IsSolidSample(ReadMaterial(p + new int3(1, 0, 0), out _, out _)) != centreSolid
                || IsSolidSample(ReadMaterial(p + new int3(-1, 0, 0), out _, out _)) != centreSolid
                || IsSolidSample(ReadMaterial(p + new int3(0, 1, 0), out _, out _)) != centreSolid
                || IsSolidSample(ReadMaterial(p + new int3(0, -1, 0), out _, out _)) != centreSolid
                || IsSolidSample(ReadMaterial(p + new int3(0, 0, 1), out _, out _)) != centreSolid
                || IsSolidSample(ReadMaterial(p + new int3(0, 0, -1), out _, out _)) != centreSolid;
        }

        private byte ReadMaterial(int3 worldVoxel, out uint surface, out byte boundary)
        {
            int3 regionCoord = worldVoxel >> VoxelDimensions.RegionVoxelEdgeLog2;
            if (!Table.TryGetRegion(regionCoord, out Region region))
            {
                surface = 0;
                boundary = 0;
                return VoxelDimensions.MaterialEmpty;
            }

            int3 localVoxel = worldVoxel & VoxelDimensions.RegionVoxelEdgeMask;
            int3 brickCoord = localVoxel >> VoxelDimensions.BrickEdgeLog2;
            int brickIndex = Region.BrickIndex(brickCoord.x, brickCoord.y, brickCoord.z);
            BrickRef brick = region.BrickRefs[brickIndex];
            if (brick.IsUniform)
            {
                surface = 0;
                boundary = 0;
                return brick.UniformMaterial;
            }

            int3 voxelInBrick = localVoxel & VoxelDimensions.BrickEdgeMask;
            int voxelIndex = VoxelDimensions.VoxelIndex(
                voxelInBrick.x, voxelInBrick.y, voxelInBrick.z);
            surface = Pool.GetSurfaceSemantics(brick.PoolIndex, voxelIndex).Storage;
            boundary = Pool.GetBoundarySample(brick.PoolIndex, voxelIndex).Packed;
            return Pool.GetVoxel(brick.PoolIndex, voxelIndex);
        }

        private uint ResolveSurface(byte material, uint stored)
        {
            if (stored != 0) return stored;
            if (material == VoxelDimensions.MaterialEmpty) return 0;
            return Catalogue.DefaultForMaterial(material).Storage;
        }

        private static bool IsSolidSample(byte material) =>
            material != VoxelDimensions.MaterialEmpty;

        private float CurvatureFactor(SurfaceStyleDefinition definition)
        {
            switch (definition.Reconstruction)
            {
                case SurfaceReconstruction.Smooth:
                    return 0.82f;
                case SurfaceReconstruction.Organic:
                    return 0.94f;
                default:
                    return 0f;
            }
        }

        private float CoatingDisplacement(uint surface)
        {
            if (surface == 0) return 0f;
            VoxelSurfaceSemantics semantics = VoxelSurfaceSemantics.FromStorage((ushort)surface);
            return semantics.Coating != 0 ? 0.025f : 0f;
        }
    }
}
