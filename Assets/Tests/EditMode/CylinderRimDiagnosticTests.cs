using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;
using Recon = VoxelEngine.Storage.Api.SurfaceReconstruction;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Simplified reproduction, part two. Runs the real continuous contour over the same solid slab
    /// with one cylindrical hole and measures every vertex on the cap layer against the analytic
    /// circle. If the contour hugs the circle, the rim staircase belongs to some other path; if it
    /// quantizes, the contour itself is the cause.
    /// </summary>
    public sealed class CylinderRimDiagnosticTests
    {
        const int CellsPerAxis = 16;
        const int Padding = 2;
        const int GridSize = CellsPerAxis + 2 * Padding + 1;
        const byte Stone = 9;
        const float Radius = 6.5f;
        const int ZMin = 4, ZMax = 12;
        const float VoxelSize = 1f;

        static int Index(int3 p) => p.x + GridSize * (p.y + GridSize * p.z);
        static float2 Centre => new(GridSize * 0.5f, GridSize * 0.5f);

        [Test]
        public void ContourVerticesOnTheCapLayerFollowTheAnalyticCircle()
        {
            int samples = GridSize * GridSize * GridSize;
            int cells = CellsPerAxis * CellsPerAxis * CellsPerAxis;

            var density = new NativeArray<float>(samples, Allocator.TempJob);
            var materials = new NativeArray<byte>(samples, Allocator.TempJob);
            var semantics = new NativeArray<uint>(samples, Allocator.TempJob);
            var boundaries = new NativeArray<byte>(samples, Allocator.TempJob);
            var stream = new NativeStream(cells, Allocator.TempJob);
            var tables = new TransvoxelLookupTables();
            SurfaceCatalogue catalogue = SurfaceCatalogue.CreateBuiltIns();
            CoatingCatalogue coatings = CoatingCatalogue.CreateBuiltIns();
            try
            {
                for (int z = 0; z < GridSize; z++)
                for (int y = 0; y < GridSize; y++)
                for (int x = 0; x < GridSize; x++)
                {
                    int i = Index(new int3(x, y, z));
                    float radial = math.distance(new float2(x, y), Centre);
                    bool insideHole = radial < Radius && z >= ZMin && z < ZMax;
                    bool solid = !insideHole;

                    materials[i] = solid ? Stone : (byte)0;
                    semantics[i] = solid ? SurfaceStyles.MasonryJoint : 0u;

                    float toWall = radial - Radius;
                    float toCap = math.min(z - (ZMin - 0.5f), (ZMax - 0.5f) - z);
                    float signed = math.min(toWall, toCap);
                    if (!solid) signed = -math.abs(signed);
                    bool authored = math.abs(signed) <= 2f;
                    var sample = authored
                        ? VoxelBoundarySample.FromSignedQ4(
                            (int)math.round(math.clamp(signed * 16f, -127f, 127f)), 2)
                        : default;
                    boundaries[i] = sample.Packed;

                    // Reproduce TransvoxelDensityJob.SampleField for MasonryJoint (Planar,
                    // curvature 0, no coating) with the sign-agreement gate this branch installs.
                    density[i] = authored && solid == sample.SignedQ3 >= 0
                        ? sample.SignedQ3 * 0.125f
                        : solid ? 0.5f : -0.5f;
                }

                var topology = new TransvoxelTopologyJob
                {
                    Density = density,
                    Materials = materials,
                    SurfaceSemantics = semantics,
                    BoundarySamples = boundaries,
                    CellClass = tables.RegularCellClass,
                    GeometryCounts = tables.RegularGeometryCounts,
                    CellVertexIndices = tables.RegularCellVertexIndices,
                    EdgeCodes = tables.RegularEdgeCodes,
                    Catalogue = catalogue,
                    Coatings = coatings,
                    ChunkOriginVoxel = int3.zero,
                    CellsPerAxis = CellsPerAxis,
                    GridSize = GridSize,
                    Padding = Padding,
                    SourceStep = 1,
                    VoxelSize = VoxelSize,
                    Output = stream.AsWriter(),
                };
                for (int cell = 0; cell < cells; cell++) topology.Execute(cell);

                var verts = new List<float3>();
                NativeStream.Reader reader = stream.AsReader();
                for (int cell = 0; cell < cells; cell++)
                {
                    if (reader.BeginForEachIndex(cell) <= 0) { reader.EndForEachIndex(); continue; }
                    byte status = reader.Read<byte>();
                    byte vc = reader.Read<byte>();
                    byte ic = reader.Read<byte>();
                    if (status != 0 || vc == 0 || ic == 0) { reader.EndForEachIndex(); continue; }
                    for (int v = 0; v < vc; v++)
                        verts.Add((float3)(Vector3)reader.Read<SmoothSurfaceVertex>().Position);
                    for (int i = 0; i < ic; i++) reader.Read<byte>();
                    reader.EndForEachIndex();
                }

                Debug.Log($"[rim] contour produced {verts.Count} vertices");

                // The cap plane sits at z = ZMin - 0.5 in voxel-index space, which the topology job
                // publishes at (index + 0.5) * VoxelSize. Collect vertices on that plane and report
                // how far each sits from the analytic circle of radius 6.5.
                var zhist = new SortedDictionary<int, int>();
                foreach (float3 v in verts)
                {
                    int b = (int)math.round(v.z * 4f);
                    zhist.TryGetValue(b, out int c); zhist[b] = c + 1;
                }
                foreach (var kv in zhist)
                    Debug.Log($"[rim] z={kv.Key / 4f,6:F2} : {kv.Value,4} vertices");

                // grid index g maps to cell g-Padding, published at (cell + 0.5) * VoxelSize.
                float capPlane = (ZMin - 0.5f - Padding + 0.5f) * VoxelSize;
                float worst = 0f;
                int onCap = 0;
                var errs = new List<float>();
                foreach (float3 v in verts)
                {
                    if (math.abs(v.z - capPlane) > 0.01f) continue;
                    float2 p = new(v.x / VoxelSize + Padding - 0.5f, v.y / VoxelSize + Padding - 0.5f);
                    float radial = math.distance(p, Centre);
                    if (radial < Radius - 2f || radial > Radius + 2f) continue;
                    onCap++;
                    float err = radial - Radius;
                    errs.Add(err);
                    worst = math.max(worst, math.abs(err));
                }
                errs.Sort();
                Debug.Log($"[rim] cap-plane rim vertices: {onCap}, worst radial error " +
                          $"{worst:F3} voxels");
                if (errs.Count > 0)
                    Debug.Log($"[rim] radial error min={errs[0]:F3} " +
                              $"median={errs[errs.Count / 2]:F3} max={errs[errs.Count - 1]:F3}");

                // Now the barrel: vertices at mid-depth, which the greedy path never touches.
                // This is the surface seen against the sky through the opening.
                var berr = new List<float>();
                foreach (float3 v in verts)
                {
                    float zc = v.z / VoxelSize;
                    if (zc < 3.4f || zc > 8.6f) continue;         // strictly inside the barrel
                    float2 p2 = new(v.x / VoxelSize + Padding - 0.5f,
                                    v.y / VoxelSize + Padding - 0.5f);
                    float rad = math.distance(p2, Centre);
                    if (rad < Radius - 2f || rad > Radius + 2f) continue;
                    berr.Add(rad - Radius);
                }
                berr.Sort();
                if (berr.Count > 0)
                    Debug.Log($"[rim] BARREL vertices={berr.Count} radial error " +
                              $"min={berr[0]:F3} median={berr[berr.Count / 2]:F3} " +
                              $"max={berr[berr.Count - 1]:F3}");
                var bh = new SortedDictionary<int, int>();
                foreach (float e in berr)
                {
                    int b = (int)math.round(e * 8f);
                    bh.TryGetValue(b, out int c); bh[b] = c + 1;
                }
                foreach (var kv in bh)
                    Debug.Log($"[rim] BARREL error {kv.Key / 8f,6:F3} : {kv.Value,3} vertices");

                // A lattice-quantized rim shows up as errors clustering on discrete steps.
                var hist = new SortedDictionary<int, int>();
                foreach (float e in errs)
                {
                    int b = (int)math.round(e * 8f);
                    hist.TryGetValue(b, out int c);
                    hist[b] = c + 1;
                }
                foreach (var kv in hist)
                    Debug.Log($"[rim] error {kv.Key / 8f,6:F3} voxels : {kv.Value,3} vertices");
            }
            finally
            {
                tables.Dispose();
                stream.Dispose();
                boundaries.Dispose();
                semantics.Dispose();
                materials.Dispose();
                density.Dispose();
            }
        }
    }
}
