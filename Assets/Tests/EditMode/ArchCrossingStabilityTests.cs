using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using Recon = VoxelEngine.Storage.Api.SurfaceReconstruction;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Measures the quantity the staircase actually is: where the isosurface crossing lands, for
    /// every edge on the intrados, as a radial distance from the arch centre. Iterates the lattice
    /// directly so no cell is sampled twice -- the trig-ray version aliased and produced a defect
    /// that was not there.
    /// </summary>
    public sealed class ArchCrossingStabilityTests
    {
        [Test]
        public void IntradosCrossingsFollowTheAnalyticCircle()
        {
            var arch = new ArchFeatureDefinition
            {
                ClearSpan = 32, PierHeight = 40, RingThickness = 7, Depth = 12,
                VoussoirCount = 12, JointRecessDepth = 1,
                ProfileJointHalfWidthQ4 = 6, ProfileBevelQ4 = 4,
                ProfileProjectionQ4 = 6, ProfileDepthQ4 = 8,
                StoneMaterial = 9,
                PierStyle = SurfaceStyles.MasonryJoint,
                RingStyle = SurfaceStyles.MasonryJoint,
            };
            var bay = new ArchBayFeatureDefinition
            {
                Arch = arch, ShoulderWidth = 10, TopMargin = 8, FaceRecess = 1,
                PlinthHeight = 4, ImpostHeight = 3,
                Damage = ArchRuinDamage.Intact, DamageSeed = 0xA341u, DamageScale = 2,
            };

            var table = new RegionTable(8, Allocator.Temp);
            var pool = new BrickPool(24_000, Allocator.Temp);
            var profileStore = new ProfileBlockStore();
            int3 origin = new(-bay.Width / 2, 0, 0);
            var primitives = new NativeList<Primitive>(
                bay.Metadata.MaxPrimitives, Allocator.Temp);
            SurfaceCatalogue catalogue = SurfaceCatalogue.CreateBuiltIns();
            try
            {
                Assert.True(bay.Emit(origin, primitives, profileStore));
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                PrimitiveRasteriser.Rasterise(
                    primitives.AsArray(), origin, origin + bay.Metadata.Footprint,
                    reads, mutations);

                SurfaceCatalogueView view = catalogue;
                int3 centre = new(0, arch.PierHeight, origin.z);
                int radius = arch.ClearSpan / 2;
                const int z = 6;

                float Density(int3 v)
                {
                    VoxelCell c = VoxelAccess.GetCell(ref table, in pool, v);
                    bool solid = c.BaseMaterialId != 0;
                    var b = c.Boundary;
                    // TransvoxelDensityJob.SampleField for MasonryJoint: Planar, curvature 0,
                    // no coating, with this branch's sign-agreement gate.
                    if (b.IsAuthored && solid == b.SignedQ3 >= 0) return b.SignedQ3 * 0.125f;
                    return solid ? 0.5f : -0.5f;
                }

                var errors = new List<(float angle, float err)>();
                for (int y = -radius - 4; y <= radius + 4; y++)
                for (int x = -radius - 4; x <= radius + 4; x++)
                {
                    if (y < 0) continue;                       // semicircular arch only
                    int3 a = new(centre.x + x, centre.y + y, z);
                    bool aSolid = VoxelAccess.GetCell(ref table, in pool, a).BaseMaterialId != 0;
                    if (!aSolid) continue;

                    // Only in-plane edges: these carry the intrados silhouette.
                    var steps = new[] { new int2(1, 0), new int2(-1, 0),
                                        new int2(0, 1), new int2(0, -1) };
                    foreach (int2 s in steps)
                    {
                        int3 b = new(a.x + s.x, a.y + s.y, z);
                        if (VoxelAccess.GetCell(ref table, in pool, b).BaseMaterialId != 0) continue;

                        float d0 = Density(a), d1 = Density(b);
                        if (math.abs(d1 - d0) < 1e-7f) continue;
                        float t0 = d1 / (d1 - d0);
                        float2 p = new float2(x, y) * t0
                                 + new float2(x + s.x, y + s.y) * (1f - t0);
                        float rad = math.length(p);
                        if (rad < radius - 3f || rad > radius + 3f) continue;
                        float ang = math.degrees(math.atan2(p.y, p.x));
                        errors.Add((ang, rad - radius));
                    }
                }

                errors.Sort((l, r) => l.angle.CompareTo(r.angle));
                float worst = 0f, sum = 0f;
                foreach (var e in errors) { worst = math.max(worst, math.abs(e.err)); sum += e.err; }
                Debug.Log($"[xing] intrados crossings={errors.Count} " +
                          $"mean={sum / math.max(1, errors.Count):F3} worst={worst:F3} voxels");

                // Print the sweep so any angular pattern is visible.
                for (int i = 0; i < errors.Count; i += math.max(1, errors.Count / 24))
                    Debug.Log($"[xing] angle {errors[i].angle,7:F1} deg  error {errors[i].err,7:F3}");
            }
            finally
            {
                primitives.Dispose();
                pool.Dispose();
                table.Dispose();
            }
        }
    }
}
