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
    /// Temporary diagnostic. The cylinder fixture shows the one-owner rule frees the cap rim, but
    /// the same rule barely moves the real arch. This walks the arch's front cap layer -- the one
    /// place never sampled -- and reports authored coverage and greedy ownership around the rim.
    /// </summary>
    public sealed class ArchCapLayerDiagnosticTests
    {
        [Test]
        public void ReportAuthoredCoverageAcrossDepthAtTheOpeningRim()
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
                int depth = bay.Metadata.Footprint.z;

                Debug.Log($"[cap] footprint={bay.Metadata.Footprint} centre={centre} r={radius}");

                Debug.Log($"[cap] primitives={primitives.Length}");
                var modes = new System.Collections.Generic.Dictionary<string,int>();
                for (int i = 0; i < primitives.Length; i++)
                {
                    string k = $"{primitives[i].Shape}/{primitives[i].Mode}";
                    modes.TryGetValue(k, out int n); modes[k] = n + 1;
                }
                foreach (var kv in modes) Debug.Log($"[cap] prim {kv.Key} x{kv.Value}");

                // Exact lattice ray along +X from the centre: no trig, no rounding, so every
                // sample is a distinct cell and the ramp can be read without aliasing.
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append("[cap] +X lattice ray z=6:");
                    for (int x = radius - 5; x <= radius + 5; x++)
                    {
                        VoxelCell c = VoxelAccess.GetCell(
                            ref table, in pool, new int3(centre.x + x, centre.y, 6));
                        string tag = c.BaseMaterialId == 0 ? "e" : "S";
                        sb.Append(c.Boundary.IsAuthored
                            ? $" x{x}:{tag}{c.Boundary.SignedQ3}"
                            : $" x{x}:{tag}.");
                    }
                    Debug.Log(sb.ToString());
                }
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append("[cap] +Y lattice ray z=6:");
                    for (int y = radius - 5; y <= radius + 5; y++)
                    {
                        VoxelCell c = VoxelAccess.GetCell(
                            ref table, in pool, new int3(centre.x, centre.y + y, 6));
                        string tag = c.BaseMaterialId == 0 ? "e" : "S";
                        sb.Append(c.Boundary.IsAuthored
                            ? $" y{y}:{tag}{c.Boundary.SignedQ3}"
                            : $" y{y}:{tag}.");
                    }
                    Debug.Log(sb.ToString());
                }

                // Radial ray at 45 degrees, mid-depth. The cylinder fixture produces a clean
                // analytic ramp here; if the arch does not, the field is the problem.
                for (int step = 30; step <= 60; step += 15)
                {
                    double a = step * math.PI / 180.0;
                    var sb = new System.Text.StringBuilder();
                    sb.Append($"[cap] ray {step}deg z=6 q3 by radius:");
                    for (int r = -4; r <= 4; r++)
                    {
                        int x = centre.x + (int)math.round(math.cos(a) * (radius + r));
                        int y = centre.y + (int)math.round(math.sin(a) * (radius + r));
                        VoxelCell c = VoxelAccess.GetCell(ref table, in pool, new int3(x, y, 6));
                        string tag = c.BaseMaterialId == 0 ? "e" : "S";
                        sb.Append(c.Boundary.IsAuthored
                            ? $" {tag}{c.Boundary.SignedQ3,3}"
                            : $" {tag}  .");
                    }
                    Debug.Log(sb.ToString());
                }
                for (int z = origin.z - 1; z < origin.z + depth; z++)
                {
                    // Walk the rim: for each angle take the innermost solid cell outside the
                    // opening, i.e. the cell whose -radial face lines the hole.
                    int solid = 0, authored = 0, facetedZ = 0, planar = 0;
                    for (int step = 0; step <= 180; step += 3)
                    {
                        double a = step * math.PI / 180.0;
                        for (int r = 0; r < 4; r++)
                        {
                            int x = centre.x + (int)math.round(math.cos(a) * (radius + r));
                            int y = centre.y + (int)math.round(math.sin(a) * (radius + r));
                            VoxelCell cell = VoxelAccess.GetCell(
                                ref table, in pool, new int3(x, y, z));
                            if (cell.BaseMaterialId == 0) continue;
                            solid++;
                            if (cell.Boundary.IsAuthored) authored++;
                            SurfaceStyleReadDefinition style =
                                view.Get((ushort)cell.Surface.Packed);
                            if (style.Reconstruction == Recon.Planar) planar++;
                            if (style.Reconstruction == Recon.Planar
                                && !cell.Boundary.AppliesAlong(2)) facetedZ++;
                            break;
                        }
                    }
                    Debug.Log($"[cap] z={z,3} rimSolid={solid,3} authored={authored,3} " +
                              $"planar={planar,3} greedyZ={facetedZ,3}");
                }
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
