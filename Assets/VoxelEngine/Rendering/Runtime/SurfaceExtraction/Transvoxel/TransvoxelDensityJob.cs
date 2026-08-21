using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel
{
    internal struct TransvoxelDensityBrick
    {
        // 0 = empty, 1 = uniform, 2 = COW-pinned mixed payload at MixedOffset.
        public byte Kind;
        public byte UniformMaterial;
        public int MixedOffset;
    }

    /// <summary>
    /// Evaluates the 35^3 smooth-field lattice for one 12.8 m Transvoxel chunk.
    ///
    /// The main thread snapshots only compact block kind/offset metadata. Mixed payloads remain in
    /// Storage-owned BrickPool arrays under generation-stamped COW pins, so gameplay edits publish
    /// clones while this job reads the immutable retired version. The job performs no RegionTable
    /// hashing or region-lifetime access and never copies 8^3 mixed payloads into renderer memory.
    /// </summary>
    [BurstCompile]
    internal struct TransvoxelDensityJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<TransvoxelDensityBrick> Bricks;
        [NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<byte> MixedVoxels;
        [NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<ushort> MixedSurfaceSemantics;
        [NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<byte> MixedBoundarySamples;
        public MaterialPaletteView Palette;
        public SurfaceCatalogueView Catalogue;
        public CoatingCatalogueView Coatings;

        [WriteOnly] public NativeArray<float> Density;
        [WriteOnly] public NativeArray<byte> Materials;
        [WriteOnly] public NativeArray<uint> SurfaceSemantics;
        [WriteOnly] public NativeArray<byte> BoundarySamples;

        public int3 ChunkOriginVoxel;
        public int3 BrickCacheOrigin;
        public int BrickCacheEdge;
        public int GridSize;
        public int Padding;
        public int SourceStep;

        public void Execute(int index)
        {
            int gx = index % GridSize;
            int yz = index / GridSize;
            int gy = yz % GridSize;
            int gz = yz / GridSize;

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
            if (packedBoundary != 0)
            {
                // Authored boundaries are already sign-checked against authoritative occupancy by
                // the structure rasteriser and deliberately persist on empty halo cells. Gating
                // them again on a six-neighbour occupancy transition discards valid diagonal SDF
                // samples on curved surfaces and falls back to the voxel-weighted field, which
                // reintroduces isolated voxel-scale bulges on otherwise analytic curves.
                dominantMaterial = centreSolid ? centre : (byte)0;
                dominantSurface = centreSolid ? centreSurface : 0u;
                var boundary = new VoxelBoundarySample { Packed = packedBoundary };
                return boundary.SignedQ3 * 0.125f + CoatingDisplacement(centreSurface);
            }
            ushort style = (ushort)centreSurface;
            SurfaceStyleReadDefinition centreDefinition = Catalogue.Get(style);
            if (centreSolid && (centreDefinition.Reconstruction == SurfaceReconstruction.Planar
                                || centreDefinition.Reconstruction == SurfaceReconstruction.Sharp
                                || centreDefinition.Reconstruction == SurfaceReconstruction.Cubic))
            {
                dominantMaterial = centre;
                dominantSurface = centreSurface;
                return 0.5f + CoatingDisplacement(centreSurface);
            }

            float curvature = CurvatureFactor(centreDefinition);
            float centreWeight = math.lerp(0.55f, 0.40f, curvature);
            float mass = centreSolid ? centreWeight : 0f;
            dominantMaterial = centreSolid ? centre : (byte)0;
            dominantSurface = centreSolid ? centreSurface : 0u;

            mass += Add(p + new int3( 1,0,0), 0.06f * curvature, centreSolid, in centreDefinition, ref dominantMaterial, ref dominantSurface);
            mass += Add(p + new int3(-1,0,0), 0.06f * curvature, centreSolid, in centreDefinition, ref dominantMaterial, ref dominantSurface);
            mass += Add(p + new int3(0, 1,0), 0.06f * curvature, centreSolid, in centreDefinition, ref dominantMaterial, ref dominantSurface);
            mass += Add(p + new int3(0,-1,0), 0.06f * curvature, centreSolid, in centreDefinition, ref dominantMaterial, ref dominantSurface);
            mass += Add(p + new int3(0,0, 1), 0.06f * curvature, centreSolid, in centreDefinition, ref dominantMaterial, ref dominantSurface);
            mass += Add(p + new int3(0,0,-1), 0.06f * curvature, centreSolid, in centreDefinition, ref dominantMaterial, ref dominantSurface);

            mass += Add(p + new int3( 2,0,0), 0.04f * curvature, centreSolid, in centreDefinition, ref dominantMaterial, ref dominantSurface);
            mass += Add(p + new int3(-2,0,0), 0.04f * curvature, centreSolid, in centreDefinition, ref dominantMaterial, ref dominantSurface);
            mass += Add(p + new int3(0, 2,0), 0.04f * curvature, centreSolid, in centreDefinition, ref dominantMaterial, ref dominantSurface);
            mass += Add(p + new int3(0,-2,0), 0.04f * curvature, centreSolid, in centreDefinition, ref dominantMaterial, ref dominantSurface);
            mass += Add(p + new int3(0,0, 2), 0.04f * curvature, centreSolid, in centreDefinition, ref dominantMaterial, ref dominantSurface);
            mass += Add(p + new int3(0,0,-2), 0.04f * curvature, centreSolid, in centreDefinition, ref dominantMaterial, ref dominantSurface);

            return mass - 0.5f + (centreSolid ? CoatingDisplacement(centreSurface) : 0f);
        }

        private float CoatingDisplacement(uint surface)
        {
            byte coating = (byte)(surface >> 16);
            return Coatings.Get(coating).Displacement * (1f / 64f);
        }

        private float Add(int3 p, float weight, bool centreSolid,
                          in SurfaceStyleReadDefinition centreDefinition, ref byte dominantMaterial,
                          ref uint dominantSurface)
        {
            byte material = ReadMaterial(p, out uint surface, out _);
            if (!IsSolidSample(material)) return 0f;
            surface = ResolveSurface(material, surface);
            if (dominantMaterial == 0)
            {
                dominantMaterial = material;
                dominantSurface = surface;
            }
            if (!centreSolid) return weight;

            SurfaceStyleReadDefinition neighbourDefinition = Catalogue.Get((ushort)surface);
            SurfaceJoinReadRule join = Catalogue.GetJoin(centreDefinition.JoinGroup,
                                                     neighbourDefinition.JoinGroup);
            if (join.Compatibility != SurfaceCompatibility.Join
                || join.Continuity == SurfaceContinuity.Discontinuous)
                return weight;

            // Smooth-compatible neighbours share their reconstruction influence. This is the
            // pairwise rule that lets curvature propagate without allowing a style to decide
            // unilaterally how a neighbour is rebuilt.
            float neighbourCurvature = CurvatureFactor(neighbourDefinition);
            return weight * math.lerp(1f, neighbourCurvature,
                math.saturate(join.BlendWidth * 0.5f));
        }

        /// <summary>
        /// Whether a material contributes solid surface. Internal rather than private so the GPU
        /// oracle can compare against this predicate instead of restating it — the two excluded
        /// materials are presentation-only and easy to forget.
        /// </summary>
        internal static bool IsSolidSample(byte material) =>
            material != 0 && material != 11 && material != 16;

        private static float CurvatureFactor(in SurfaceStyleReadDefinition definition)
        {
            if (definition.Reconstruction == SurfaceReconstruction.Planar
                || definition.Reconstruction == SurfaceReconstruction.Sharp
                || definition.Reconstruction == SurfaceReconstruction.Cubic) return 0f;
            return definition.Curvature / 255f;
        }

        private uint ResolveSurface(byte material, uint surface)
        {
            ushort style = (ushort)surface;
            if (style == SurfaceStyles.MaterialDefault)
                style = Palette.GetDefaultSurfaceStyle(material);
            if (style == SurfaceStyles.MaterialDefault)
                style = SurfaceStyles.Smooth;
            return (surface & 0xFFFF0000u) | style;
        }

        private byte ReadMaterial(int3 p, out uint surface, out byte boundary)
        {
            // Arithmetic right shift gives floor division for negative world coordinates.
            int3 worldBrick = new int3(p.x >> 3, p.y >> 3, p.z >> 3);
            int3 localBrick = worldBrick - BrickCacheOrigin;
            if ((uint)localBrick.x >= (uint)BrickCacheEdge
                || (uint)localBrick.y >= (uint)BrickCacheEdge
                || (uint)localBrick.z >= (uint)BrickCacheEdge)
            {
                surface = 0;
                boundary = 0;
                return 0;
            }

            int brickIndex = localBrick.x
                           + BrickCacheEdge * (localBrick.y + BrickCacheEdge * localBrick.z);
            TransvoxelDensityBrick brick = Bricks[brickIndex];
            if (brick.Kind == 0)
            {
                surface = 0;
                boundary = 0;
                return 0;
            }
            if (brick.Kind == 1)
            {
                surface = 0;
                boundary = 0;
                return brick.UniformMaterial;
            }

            int vx = p.x & 7;
            int vy = p.y & 7;
            int vz = p.z & 7;
            int voxelIndex = vx | (vy << 3) | (vz << 6);
            surface = VoxelSurfaceSemantics.FromStorage(
                MixedSurfaceSemantics[brick.MixedOffset + voxelIndex]).Packed;
            boundary = MixedBoundarySamples[brick.MixedOffset + voxelIndex];
            return MixedVoxels[brick.MixedOffset + voxelIndex];
        }

    }
}