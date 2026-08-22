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

                for (int step = 0; step <= 60; step += 30)
                {
                    double a = step * math.PI / 180.0;
                    var sb = new System.Text.StringBuilder();
                    sb.Append($"[csg] {label,-12} {step,2}deg q3:");
                    for (int r = -4; r <= 4; r++)
                    {
                        int x = centre.x + (int)math.round(math.cos(a) * (hole + r));
                        int y = centre.y + (int)math.round(math.sin(a) * (hole + r));
                        VoxelCell c = VoxelAccess.GetCell(ref t2, in p2, new int3(x, y, 6));
                        string tag = c.BaseMaterialId == 0 ? "e" : "S";
                        sb.Append(c.Boundary.IsAuthored
                            ? $" {tag}{c.Boundary.SignedQ3,3}"
                            : $" {tag}  .");
                    }
                    Debug.Log(sb.ToString());
                }
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
