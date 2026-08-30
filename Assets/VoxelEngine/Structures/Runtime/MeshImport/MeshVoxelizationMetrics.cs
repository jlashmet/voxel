using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Runtime.MeshImport
{
    /// <summary>Structural/cost facts derived from a sparse baked voxel structure.</summary>
    public readonly struct BakedVoxelStructureStats
    {
        public readonly int CellCount;
        public readonly int SurfaceCellCount;
        public readonly int ConnectedComponentCount;
        public readonly int MaterialCount;
        public readonly int SparseBrickCount;

        public BakedVoxelStructureStats(
            int cellCount,
            int surfaceCellCount,
            int connectedComponentCount,
            int materialCount,
            int sparseBrickCount)
        {
            CellCount = cellCount;
            SurfaceCellCount = surfaceCellCount;
            ConnectedComponentCount = connectedComponentCount;
            MaterialCount = materialCount;
            SparseBrickCount = sparseBrickCount;
        }
    }

    /// <summary>
    /// Deterministic sampled fidelity report. Distances are measured in output-voxel units and
    /// silhouettes are rasterized from the same sampled source/bake surfaces into fixed views.
    /// This is supplemental evidence for authored assets; it never participates in runtime truth.
    /// </summary>
    public readonly struct MeshVoxelFidelityReport
    {
        public readonly int SourceSampleCount;
        public readonly int VoxelSampleCount;
        public readonly float SymmetricP95Voxels;
        public readonly float FrontSilhouetteIoU;
        public readonly float SideSilhouetteIoU;
        public readonly float TopSilhouetteIoU;

        public MeshVoxelFidelityReport(
            int sourceSampleCount,
            int voxelSampleCount,
            float symmetricP95Voxels,
            float frontSilhouetteIoU,
            float sideSilhouetteIoU,
            float topSilhouetteIoU)
        {
            SourceSampleCount = sourceSampleCount;
            VoxelSampleCount = voxelSampleCount;
            SymmetricP95Voxels = symmetricP95Voxels;
            FrontSilhouetteIoU = frontSilhouetteIoU;
            SideSilhouetteIoU = sideSilhouetteIoU;
            TopSilhouetteIoU = topSilhouetteIoU;
        }

        public float MinPrimarySilhouetteIoU =>
            math.min(FrontSilhouetteIoU, math.min(SideSilhouetteIoU, TopSilhouetteIoU));
    }

    /// <summary>
    /// Reusable offline analysis for mesh-to-voxel bakes. Work is deliberately bounded by a
    /// caller-selected sample cap so a detailed source can be measured without an O(triangles ×
    /// voxels) authoring pass. Sampling is stable by triangle/cell order for exact-SHA evidence.
    /// </summary>
    public static class MeshVoxelizationMetrics
    {
        private static readonly int3[] Neighbours =
        {
            new int3(1, 0, 0), new int3(-1, 0, 0),
            new int3(0, 1, 0), new int3(0, -1, 0),
            new int3(0, 0, 1), new int3(0, 0, -1),
        };

        public static BakedVoxelStructureStats Analyze(
            BakedVoxelStructure bake,
            int brickEdgeVoxels = 8)
        {
            if (bake == null) throw new ArgumentNullException(nameof(bake));
            if (brickEdgeVoxels <= 0) throw new ArgumentOutOfRangeException(nameof(brickEdgeVoxels));

            var occupied = new HashSet<int3>();
            var materials = new HashSet<byte>();
            var bricks = new HashSet<int3>();
            for (int i = 0; i < bake.Cells.Length; i++)
            {
                BakedVoxelCell cell = bake.Cells[i];
                occupied.Add(cell.Position);
                materials.Add(cell.Material);
                bricks.Add(cell.Position / brickEdgeVoxels);
            }

            int surfaceCount = 0;
            foreach (int3 p in occupied)
            {
                if (IsSurface(p, occupied)) surfaceCount++;
            }

            var visited = new HashSet<int3>();
            var queue = new Queue<int3>();
            int components = 0;
            foreach (int3 start in occupied)
            {
                if (!visited.Add(start)) continue;
                components++;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int3 p = queue.Dequeue();
                    for (int n = 0; n < Neighbours.Length; n++)
                    {
                        int3 q = p + Neighbours[n];
                        if (occupied.Contains(q) && visited.Add(q)) queue.Enqueue(q);
                    }
                }
            }

            return new BakedVoxelStructureStats(
                bake.Cells.Length,
                surfaceCount,
                components,
                materials.Count,
                bricks.Count);
        }

        /// <summary>Returns sparse surface cells in the bake's stable source-control order.</summary>
        public static BakedVoxelCell[] ExtractSurfaceCells(BakedVoxelStructure bake)
        {
            if (bake == null) throw new ArgumentNullException(nameof(bake));
            var occupied = new HashSet<int3>();
            for (int i = 0; i < bake.Cells.Length; i++) occupied.Add(bake.Cells[i].Position);

            var result = new List<BakedVoxelCell>();
            for (int i = 0; i < bake.Cells.Length; i++)
            {
                BakedVoxelCell cell = bake.Cells[i];
                if (IsSurface(cell.Position, occupied)) result.Add(cell);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Measures transformed source triangles against the baked surface. Source points are
        /// converted to the bake's local grid before comparison, so the result is directly in
        /// voxels regardless of source units, translation, rotation, or scale.
        /// </summary>
        public static MeshVoxelFidelityReport Measure(
            in MeshVoxelizationSource source,
            BakedVoxelStructure bake,
            int maxSamplesPerSurface = 2048,
            int silhouetteResolution = 192)
        {
            if (bake == null) throw new ArgumentNullException(nameof(bake));
            if (source.Vertices == null || source.Triangles == null || source.Triangles.Length == 0)
                throw new ArgumentException("Source must contain indexed triangles.", nameof(source));
            ValidateMeasurementSettings(maxSamplesPerSurface, silhouetteResolution);

            float3[] sourceSamples = SampleSource(in source, bake, maxSamplesPerSurface);
            float3[] voxelSamples = SampleBakeSurface(bake, maxSamplesPerSurface);
            return MeasurePointClouds(sourceSamples, voxelSamples, silhouetteResolution);
        }

        /// <summary>
        /// Point-cloud form used by authoring tests and by tools that already have surface samples.
        /// Coordinates must use the same voxel-space frame.
        /// </summary>
        public static MeshVoxelFidelityReport MeasurePointClouds(
            float3[] sourceSurfaceSamples,
            float3[] voxelSurfaceSamples,
            int silhouetteResolution = 192)
        {
            if (sourceSurfaceSamples == null) throw new ArgumentNullException(nameof(sourceSurfaceSamples));
            if (voxelSurfaceSamples == null) throw new ArgumentNullException(nameof(voxelSurfaceSamples));
            if (sourceSurfaceSamples.Length == 0 || voxelSurfaceSamples.Length == 0)
                throw new ArgumentException("Both surfaces require at least one sample.");
            if (silhouetteResolution < 16 || silhouetteResolution > 2048)
                throw new ArgumentOutOfRangeException(nameof(silhouetteResolution));
            ValidateFinite(sourceSurfaceSamples, nameof(sourceSurfaceSamples));
            ValidateFinite(voxelSurfaceSamples, nameof(voxelSurfaceSamples));

            float[] distances = new float[sourceSurfaceSamples.Length + voxelSurfaceSamples.Length];
            int write = 0;
            for (int i = 0; i < sourceSurfaceSamples.Length; i++)
                distances[write++] = NearestDistance(sourceSurfaceSamples[i], voxelSurfaceSamples);
            for (int i = 0; i < voxelSurfaceSamples.Length; i++)
                distances[write++] = NearestDistance(voxelSurfaceSamples[i], sourceSurfaceSamples);
            Array.Sort(distances);
            int p95Index = math.clamp((int)math.ceil(distances.Length * 0.95f) - 1, 0, distances.Length - 1);

            return new MeshVoxelFidelityReport(
                sourceSurfaceSamples.Length,
                voxelSurfaceSamples.Length,
                distances[p95Index],
                SilhouetteIoU(sourceSurfaceSamples, voxelSurfaceSamples, 0, 1, silhouetteResolution),
                SilhouetteIoU(sourceSurfaceSamples, voxelSurfaceSamples, 2, 1, silhouetteResolution),
                SilhouetteIoU(sourceSurfaceSamples, voxelSurfaceSamples, 0, 2, silhouetteResolution));
        }

        private static float3[] SampleSource(
            in MeshVoxelizationSource source,
            BakedVoxelStructure bake,
            int maxSamples)
        {
            int triangleBudget = math.max(1, maxSamples / 4);
            int stride = math.max(1, (source.Triangles.Length + triangleBudget - 1) / triangleBudget);
            var points = new List<float3>(math.min(maxSamples, source.Triangles.Length * 4));
            for (int i = 0; i < source.Triangles.Length && points.Count < maxSamples; i += stride)
            {
                MeshVoxelTriangle triangle = source.Triangles[i];
                if ((uint)triangle.A >= (uint)source.Vertices.Length
                    || (uint)triangle.B >= (uint)source.Vertices.Length
                    || (uint)triangle.C >= (uint)source.Vertices.Length)
                    throw new ArgumentException($"Triangle {i} contains an invalid vertex index.", nameof(source));

                float3 a = ToBakeGrid(math.transform(source.Transform, source.Vertices[triangle.A]), bake);
                float3 b = ToBakeGrid(math.transform(source.Transform, source.Vertices[triangle.B]), bake);
                float3 c = ToBakeGrid(math.transform(source.Transform, source.Vertices[triangle.C]), bake);
                Add(points, (a + b + c) / 3f, maxSamples);
                Add(points, a, maxSamples);
                Add(points, b, maxSamples);
                Add(points, c, maxSamples);
            }
            return points.ToArray();
        }

        private static float3[] SampleBakeSurface(BakedVoxelStructure bake, int maxSamples)
        {
            BakedVoxelCell[] surface = ExtractSurfaceCells(bake);
            if (surface.Length == 0) throw new ArgumentException("Bake has no occupied surface cells.", nameof(bake));
            int stride = math.max(1, (surface.Length + maxSamples - 1) / maxSamples);
            var points = new List<float3>(math.min(maxSamples, surface.Length));
            for (int i = 0; i < surface.Length && points.Count < maxSamples; i += stride)
                points.Add((float3)surface[i].Position + 0.5f);
            return points.ToArray();
        }

        private static float3 ToBakeGrid(float3 sourcePoint, BakedVoxelStructure bake) =>
            sourcePoint / bake.VoxelSize - (float3)bake.GridOrigin;

        private static void Add(List<float3> points, float3 value, int maxSamples)
        {
            if (points.Count < maxSamples) points.Add(value);
        }

        private static bool IsSurface(int3 p, HashSet<int3> occupied)
        {
            for (int i = 0; i < Neighbours.Length; i++)
                if (!occupied.Contains(p + Neighbours[i])) return true;
            return false;
        }

        private static float NearestDistance(float3 point, float3[] candidates)
        {
            float bestSq = float.PositiveInfinity;
            for (int i = 0; i < candidates.Length; i++)
                bestSq = math.min(bestSq, math.lengthsq(point - candidates[i]));
            return math.sqrt(bestSq);
        }

        private static float SilhouetteIoU(
            float3[] source,
            float3[] voxel,
            int axisU,
            int axisV,
            int resolution)
        {
            float2 min = new float2(float.PositiveInfinity);
            float2 max = new float2(float.NegativeInfinity);
            AccumulateProjectedBounds(source, axisU, axisV, ref min, ref max);
            AccumulateProjectedBounds(voxel, axisU, axisV, ref min, ref max);
            float2 extent = math.max(max - min, new float2(1e-5f));

            var sourceMask = new bool[resolution * resolution];
            var voxelMask = new bool[resolution * resolution];
            RasterizePoints(source, sourceMask, axisU, axisV, min, extent, resolution);
            RasterizePoints(voxel, voxelMask, axisU, axisV, min, extent, resolution);

            int intersection = 0;
            int union = 0;
            for (int i = 0; i < sourceMask.Length; i++)
            {
                bool a = sourceMask[i];
                bool b = voxelMask[i];
                if (a && b) intersection++;
                if (a || b) union++;
            }
            return union == 0 ? 1f : (float)intersection / union;
        }

        private static void AccumulateProjectedBounds(
            float3[] points,
            int axisU,
            int axisV,
            ref float2 min,
            ref float2 max)
        {
            for (int i = 0; i < points.Length; i++)
            {
                float2 p = new float2(Component(points[i], axisU), Component(points[i], axisV));
                min = math.min(min, p);
                max = math.max(max, p);
            }
        }

        private static void RasterizePoints(
            float3[] points,
            bool[] mask,
            int axisU,
            int axisV,
            float2 min,
            float2 extent,
            int resolution)
        {
            for (int i = 0; i < points.Length; i++)
            {
                float2 p = new float2(Component(points[i], axisU), Component(points[i], axisV));
                float2 normalized = math.saturate((p - min) / extent);
                int x = (int)math.round(normalized.x * (resolution - 1));
                int y = (int)math.round(normalized.y * (resolution - 1));

                // Point samples represent finite patches of the continuous surface. A one-pixel
                // footprint avoids making IoU depend on subpixel rounding while remaining much
                // smaller than the silhouette features this metric is intended to catch.
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = x + dx;
                    int py = y + dy;
                    if ((uint)px >= (uint)resolution || (uint)py >= (uint)resolution) continue;
                    mask[py * resolution + px] = true;
                }
            }
        }

        private static float Component(float3 value, int axis)
        {
            switch (axis)
            {
                case 0: return value.x;
                case 1: return value.y;
                case 2: return value.z;
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }

        private static void ValidateMeasurementSettings(int maxSamples, int silhouetteResolution)
        {
            if (maxSamples < 32 || maxSamples > 16384)
                throw new ArgumentOutOfRangeException(nameof(maxSamples));
            if (silhouetteResolution < 16 || silhouetteResolution > 2048)
                throw new ArgumentOutOfRangeException(nameof(silhouetteResolution));
        }

        private static void ValidateFinite(float3[] points, string parameter)
        {
            for (int i = 0; i < points.Length; i++)
            {
                float3 p = points[i];
                if (!math.all(math.isfinite(p)))
                    throw new ArgumentException($"Surface sample {i} is non-finite.", parameter);
            }
        }
    }
}
