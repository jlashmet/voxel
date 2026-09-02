using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Order-independent physical composition for one contiguous terrain-corridor generation stage.
    /// Bounded corridor primitives remain storage/execution partitions; for each horizontal column
    /// this compositor first chooses the same kind of best closest-point candidate used by semantic
    /// road influence, then delegates the actual density/material mutation to the existing generic
    /// <see cref="TerrainCorridorRasteriser"/>. No endpoint fade or road-specific policy lives here.
    /// </summary>
    public static class ContinuousTerrainCorridorRasteriser
    {
        public static RasterResult Rasterise(
            NativeArray<Primitive> corridors,
            int3 subVolumeMin,
            int3 subVolumeMax,
            IRegionReadSource reads,
            IRegionMutationStore mutations)
        {
            var result = new RasterResult();
            if (corridors.Length == 0 || math.any(subVolumeMin >= subVolumeMax))
                return result;

            bool rasterisedAny = false;
            for (int z = subVolumeMin.z; z < subVolumeMax.z; z++)
            for (int x = subVolumeMin.x; x < subVolumeMax.x; x++)
            {
                if (!TryChoose(corridors, x, z, out Primitive winner))
                    continue;

                int3 columnMin = new int3(x, subVolumeMin.y, z);
                int3 columnMax = new int3(x + 1, subVolumeMax.y, z + 1);
                RasterResult column = TerrainCorridorRasteriser.Rasterise(
                    in winner, columnMin, columnMax, reads, mutations);
                result.VoxelsWritten += column.VoxelsWritten;
                rasterisedAny |= column.PrimitivesRasterised > 0;
            }

            // A continuous batch is one physical composition operation. Callers that need authored
            // primitive counts already have them before rasterisation; this flag only communicates
            // whether the clipped batch touched the requested volume.
            result.PrimitivesRasterised = rasterisedAny ? 1 : 0;
            return result;
        }

        public static bool TryChoose(
            NativeArray<Primitive> corridors,
            int worldX,
            int worldZ,
            out Primitive winner)
        {
            bool found = false;
            Primitive bestPrimitive = default;
            TerrainCorridorSample bestSample = default;

            for (int i = 0; i < corridors.Length; i++)
            {
                Primitive candidatePrimitive = corridors[i];
                if (candidatePrimitive.Shape != PrimitiveShape.TerrainCorridor
                    || candidatePrimitive.Mode != PrimitiveMode.TerrainCorridor)
                    continue;
                if (!TerrainCorridorRasteriser.TrySample(
                        in candidatePrimitive, worldX, worldZ,
                        out TerrainCorridorSample candidateSample))
                    continue;

                if (!found || Better(
                        in candidateSample, in candidatePrimitive,
                        in bestSample, in bestPrimitive))
                {
                    found = true;
                    bestSample = candidateSample;
                    bestPrimitive = candidatePrimitive;
                }
            }

            winner = bestPrimitive;
            return found;
        }

        private static bool Better(
            in TerrainCorridorSample candidate,
            in Primitive candidatePrimitive,
            in TerrainCorridorSample best,
            in Primitive bestPrimitive)
        {
            // Visible corridor influence is the semantic road/network discriminator. Outside the
            // authored surface, use the broader grading influence, then exact closest distance.
            if (candidate.SurfaceCoverage31 != best.SurfaceCoverage31)
                return candidate.SurfaceCoverage31 > best.SurfaceCoverage31;
            if (candidate.Coverage31 != best.Coverage31)
                return candidate.Coverage31 > best.Coverage31;
            if (candidate.DistanceDm != best.DistanceDm)
                return candidate.DistanceDm < best.DistanceDm;

            // Exact ties must still be independent of catalogue/write order. Route seeds are stable
            // for existing road consumers; endpoint comparison makes the fallback total even when
            // two callers intentionally share a seed.
            uint candidateSeed = unchecked((uint)candidatePrimitive.D.y);
            uint bestSeed = unchecked((uint)bestPrimitive.D.y);
            if (candidateSeed != bestSeed) return candidateSeed < bestSeed;

            int compare = ComparePoint(candidatePrimitive.A, bestPrimitive.A);
            if (compare != 0) return compare < 0;
            compare = ComparePoint(candidatePrimitive.B, bestPrimitive.B);
            if (compare != 0) return compare < 0;
            if (candidatePrimitive.InnerRadius != bestPrimitive.InnerRadius)
                return candidatePrimitive.InnerRadius < bestPrimitive.InnerRadius;
            if (candidatePrimitive.Radius != bestPrimitive.Radius)
                return candidatePrimitive.Radius < bestPrimitive.Radius;
            return candidatePrimitive.Material < bestPrimitive.Material;
        }

        private static int ComparePoint(int3 a, int3 b)
        {
            if (a.x != b.x) return a.x < b.x ? -1 : 1;
            if (a.y != b.y) return a.y < b.y ? -1 : 1;
            if (a.z != b.z) return a.z < b.z ? -1 : 1;
            return 0;
        }
    }
}
