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
        // VoxelSurfaceSemantics storage persists only flag bits 0-1. Flag bit 2 is therefore a
        // renderer-only transient channel on this density lattice. FacetedMaskJob needs the exact
        // authoritative centre occupancy because Materials[] intentionally carries presentation
        // identity onto nearby air-centred smooth samples. Strip this bit before vertex publication.
        internal const uint AuthoritativeSolidBit = 1u << 26;

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
                p, out byte material, out uint surface, out byte boundary,
                out bool authoritativeSolid);
            Materials[index] = material;
            SurfaceSemantics[index] = WithAuthoritativeOccupancy(surface, authoritativeSolid);
            BoundarySamples[index] = boundary;
        }

        internal static bool IsAuthoritativelySolid(uint surface) =>
            (surface & AuthoritativeSolidBit) != 0;

        internal static uint WithAuthoritativeOccupancy(uint surface, bool solid) =>
            solid ? surface | AuthoritativeSolidBit : surface & ~AuthoritativeSolidBit;

        internal static uint StripAuthoritativeOccupancy(uint surface) =>
            surface & ~AuthoritativeSolidBit;

        private float SampleField(int3 p, out byte dominantMaterial, out uint dominantSurface,
                                  out byte dominantBoundary, out bool authoritativeSolid)
        {
            byte centre = ReadMaterial(p, out uint centreSurface, out byte packedBoundary);
            dominantBoundary = packedBoundary;
            bool centreSolid = IsSolidSample(centre);
            authoritativeSolid = centreSolid;
            centreSurface = ResolveSurface(centre, centreSurface);
            var boundary = new VoxelBoundarySample { Packed = packedBoundary };
            // An authored sample refines where the surface sits inside this cell; occupancy decides
            // which side of it the cell is on. Trust the analytic distance whenever the two agree.
            //
            // The previous rule instead demanded a six-neighbour occupancy transition, which is a
            // proxy for "near the surface" and a bad one on a curve: a cell one voxel inside a
            // smooth intrados has no empty axis neighbour, so it failed, fell through to the Planar
            // branch below, and returned a constant 0.5. With no sub-voxel information on the solid
            // side of an edge the crossing collapses onto the lattice, which is the staircased
            // intrados. Dropping the guard outright is not the answer either: authored samples that
            // contradict occupancy then produce surface where the world is empty, which tears holes
            // in the surrounding wall. Sign agreement keeps the precision and rejects the garbage.
            if (packedBoundary != 0 && centreSolid == boundary.SignedQ3 >= 0)
            {
                dominantMaterial = centreSolid ? centre : (byte)0;
                dominantSurface = centreSolid ? centreSurface : 0u;
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

            float density = mass - 0.5f;
            int nearestCrossingDistance = SourceStep;

            // A coarse lattice point can sit several voxels below the actual surface. The 13-tap
            // smoothing kernel is intentionally evaluated at authoritative-voxel scale, but simply
            // interpolating its values between SourceStep-spaced samples nearly erases where a
            // crossing falls inside that coarse edge. A one-voxel terrain rise at SourceStep 2 then
            // moves the reconstructed surface only a small fraction of a voxel, producing contour
            // rings. Preserve the smoothing decision (including removal/fill of tiny features) by
            // changing magnitude only when its sign agrees with authoritative centre occupancy, and
            // only when a coarse axis ray really reaches the opposite occupancy. The nearest fine
            // transition supplies a signed-distance-like magnitude with the missing sub-step phase.
            if (SourceStep > 1)
            {
                if (centreSolid)
                {
                    nearestCrossingDistance = PreferNearestCrossingSurfaceMaterial(
                        p, centre, centreSurface, ref dominantMaterial, ref dominantSurface);
                }
                else
                {
                    nearestCrossingDistance = FindNearestCrossingDistance(p, centreSolid);
                }

                bool densitySignMatchesOccupancy = centreSolid ? density >= 0f : density < 0f;
                if (nearestCrossingDistance < SourceStep && densitySignMatchesOccupancy)
                {
                    float phase = (nearestCrossingDistance + 0.5f) / SourceStep;
                    density = centreSolid ? phase : -phase;
                }
            }

            return density + (centreSolid ? CoatingDisplacement(centreSurface) : 0f);
        }

        private int PreferNearestCrossingSurfaceMaterial(
            int3 p, byte centreMaterial, uint centreSurface,
            ref byte dominantMaterial, ref uint dominantSurface)
        {
            int bestDistance = SourceStep;
            int bestMaterialDistance = SourceStep;
            bool hasVisibleTopMaterial = false;

            ConsiderCrossingRay(p, new int3( 1, 0, 0), false, centreMaterial, centreSurface,
                ref bestDistance, ref bestMaterialDistance, ref hasVisibleTopMaterial,
                ref dominantMaterial, ref dominantSurface);
            ConsiderCrossingRay(p, new int3(-1, 0, 0), false, centreMaterial, centreSurface,
                ref bestDistance, ref bestMaterialDistance, ref hasVisibleTopMaterial,
                ref dominantMaterial, ref dominantSurface);
            ConsiderCrossingRay(p, new int3(0,  1, 0), true, centreMaterial, centreSurface,
                ref bestDistance, ref bestMaterialDistance, ref hasVisibleTopMaterial,
                ref dominantMaterial, ref dominantSurface);
            ConsiderCrossingRay(p, new int3(0, -1, 0), false, centreMaterial, centreSurface,
                ref bestDistance, ref bestMaterialDistance, ref hasVisibleTopMaterial,
                ref dominantMaterial, ref dominantSurface);
            ConsiderCrossingRay(p, new int3(0, 0,  1), false, centreMaterial, centreSurface,
                ref bestDistance, ref bestMaterialDistance, ref hasVisibleTopMaterial,
                ref dominantMaterial, ref dominantSurface);
            ConsiderCrossingRay(p, new int3(0, 0, -1), false, centreMaterial, centreSurface,
                ref bestDistance, ref bestMaterialDistance, ref hasVisibleTopMaterial,
                ref dominantMaterial, ref dominantSurface);
            return bestDistance;
        }

        private void ConsiderCrossingRay(
            int3 p, int3 direction, bool preferVisibleTopMaterial,
            byte centreMaterial, uint centreSurface,
            ref int bestDistance, ref int bestMaterialDistance, ref bool hasVisibleTopMaterial,
            ref byte dominantMaterial, ref uint dominantSurface)
        {
            byte farMaterial = ReadMaterial(p + direction * SourceStep, out _, out _);
            if (IsSolidSample(farMaterial)) return;

            byte lastMaterial = centreMaterial;
            uint lastSurface = centreSurface;
            for (int distance = 1; distance < SourceStep; distance++)
            {
                byte material = ReadMaterial(
                    p + direction * distance, out uint surface, out _);
                if (!IsSolidSample(material))
                {
                    ConsiderExposedMaterial(
                        distance - 1, preferVisibleTopMaterial, lastMaterial, lastSurface,
                        ref bestDistance, ref bestMaterialDistance, ref hasVisibleTopMaterial,
                        ref dominantMaterial, ref dominantSurface);
                    return;
                }

                lastMaterial = material;
                lastSurface = ResolveSurface(material, surface);
            }

            // The far coarse endpoint is air, so if every intermediate voxel was solid then the
            // final intermediate voxel is the exposed one immediately before that endpoint.
            ConsiderExposedMaterial(
                SourceStep - 1, preferVisibleTopMaterial, lastMaterial, lastSurface,
                ref bestDistance, ref bestMaterialDistance, ref hasVisibleTopMaterial,
                ref dominantMaterial, ref dominantSurface);
        }

        private static void ConsiderExposedMaterial(
            int exposedDistance, bool preferVisibleTopMaterial,
            byte material, uint surface,
            ref int bestDistance, ref int bestMaterialDistance, ref bool hasVisibleTopMaterial,
            ref byte dominantMaterial, ref uint dominantSurface)
        {
            bestDistance = math.min(bestDistance, exposedDistance);

            // Geometry and material answer different questions. Geometry needs the nearest crossing
            // on any axis so the coarse scalar field keeps its fine-voxel phase. Terrain material,
            // however, is vertically layered: grass/snow is stored only on the exposed cap while a
            // nearby side crossing can legitimately expose dirt/rock. If this sample has a +Y cap
            // inside the same coarse step, use that cap for colour without moving the crossing.
            // Walls, ceilings and cave sides have no +Y cap and retain the old nearest-crossing rule.
            bool shouldUseMaterial = preferVisibleTopMaterial
                || (!hasVisibleTopMaterial && exposedDistance < bestMaterialDistance);
            if (!shouldUseMaterial) return;

            if (preferVisibleTopMaterial) hasVisibleTopMaterial = true;
            bestMaterialDistance = exposedDistance;
            dominantMaterial = material;
            dominantSurface = surface;
        }

        private int FindNearestCrossingDistance(int3 p, bool centreSolid)
        {
            int bestDistance = SourceStep;
            ConsiderPhaseCrossingRay(p, new int3( 1, 0, 0), centreSolid, ref bestDistance);
            ConsiderPhaseCrossingRay(p, new int3(-1, 0, 0), centreSolid, ref bestDistance);
            ConsiderPhaseCrossingRay(p, new int3(0,  1, 0), centreSolid, ref bestDistance);
            ConsiderPhaseCrossingRay(p, new int3(0, -1, 0), centreSolid, ref bestDistance);
            ConsiderPhaseCrossingRay(p, new int3(0, 0,  1), centreSolid, ref bestDistance);
            ConsiderPhaseCrossingRay(p, new int3(0, 0, -1), centreSolid, ref bestDistance);
            return bestDistance;
        }

        private void ConsiderPhaseCrossingRay(
            int3 p, int3 direction, bool centreSolid, ref int bestDistance)
        {
            byte farMaterial = ReadMaterial(p + direction * SourceStep, out _, out _);
            if (IsSolidSample(farMaterial) == centreSolid) return;

            for (int distance = 1; distance < SourceStep; distance++)
            {
                byte material = ReadMaterial(p + direction * distance, out _, out _);
                if (IsSolidSample(material) == centreSolid) continue;
                bestDistance = math.min(bestDistance, distance - 1);
                return;
            }

            bestDistance = math.min(bestDistance, SourceStep - 1);
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
