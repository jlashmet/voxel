using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Structures.Runtime.Emitters;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Minimal CSG case. One box fill, then one cylindrical carve through it -- the arch opening
    /// with everything else stripped away. Dumps the authored scalar along a radial ray so the
    /// composite field can be compared against the true distance to the carve surface.
    /// </summary>
    public sealed class CsgFieldCoherenceTests
    {
        [Test]
        public void CompositeFieldAlongARadialRay()
        {
            var table = new RegionTable(4, Allocator.Temp);
            var pool = new BrickPool(8_000, Allocator.Temp);
            try
            {
                int3 centre = new(24, 24, 6);
                const int hole = 10;              // carve radius

                Run(ref table, ref pool, centre, hole, carve: false, label: "fill only");
                Run(ref table, ref pool, centre, hole, carve: true, label: "fill + carve");
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
            }
        }

        static void Run(ref RegionTable table, ref BrickPool pool,
                        int3 centre, int hole, bool carve, string label)
        {
            var t2 = new RegionTable(4, Allocator.Temp);
            var p2 = new BrickPool(8_000, Allocator.Temp);
            var prims = new NativeList<Primitive>(4, Allocator.Temp);
            try
            {
                prims.Add(BoxEmitter.Box(
                    new int3(0, 0, 0), new int3(48, 48, 12),
                    9, PrimitiveMode.Fill, 0, SurfaceStyles.MasonryJoint));
                if (carve)
                    prims.Add(CurvedPrimitiveEmitter.Annulus(
                        centre, hole, 0, 12, 2, false,
                        9, SurfaceStyles.MasonryJoint, PrimitiveMode.Carve, 1));

                var reads = new RegionReadSource(in t2, in p2);
                var mutations = new RegionMutationStore(in t2, in p2);
                PrimitiveRasteriser.Rasterise(
                    prims.AsArray(), int3.zero, new int3(48, 48, 12), reads, mutations);

                if (!carve) return;

                // Same alias-free crossing measurement as the arch: iterate the lattice, take every
                // in-plane solid->empty edge near the hole, and record where the crossing lands.
                float Density(int3 v)
                {
                    VoxelCell c = VoxelAccess.GetCell(ref t2, in p2, v);
                    bool sol = c.BaseMaterialId != 0;
                    var b = c.Boundary;
                    if (b.IsAuthored && sol == b.SignedQ3 >= 0) return b.SignedQ3 * 0.125f;
                    return sol ? 0.5f : -0.5f;
                }

                var rads = new System.Collections.Generic.List<float>();
                for (int y = -hole - 4; y <= hole + 4; y++)
                for (int x = -hole - 4; x <= hole + 4; x++)
                {
                    int3 a0 = new(centre.x + x, centre.y + y, 6);
                    if (VoxelAccess.GetCell(ref t2, in p2, a0).BaseMaterialId == 0) continue;
                    var steps = new[] { new int2(1, 0), new int2(-1, 0),
                                        new int2(0, 1), new int2(0, -1) };
                    foreach (int2 st in steps)
                    {
                        int3 b0 = new(a0.x + st.x, a0.y + st.y, 6);
                        if (VoxelAccess.GetCell(ref t2, in p2, b0).BaseMaterialId != 0) continue;
                        float d0 = Density(a0), d1 = Density(b0);
                        if (math.abs(d1 - d0) < 1e-7f) continue;
                        float t0 = d1 / (d1 - d0);
                        float2 pp = new float2(x, y) * t0
                                  + new float2(x + st.x, y + st.y) * (1f - t0);
                        float rad = math.length(pp);
                        if (rad < hole - 3f || rad > hole + 3f) continue;
                        rads.Add(rad);
                    }
                }
                rads.Sort();
                if (rads.Count > 0)
                    Debug.Log($"[csg] {label}: crossings={rads.Count} " +
                              $"min={rads[0]:F3} max={rads[rads.Count - 1]:F3} " +
                              $"SPREAD={rads[rads.Count - 1] - rads[0]:F3} voxels");
            }
            finally
            {
                prims.Dispose();
                p2.Dispose();
                t2.Dispose();
            }
        }
    }
}
