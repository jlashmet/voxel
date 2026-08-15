using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Terrain.Api;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    public static partial class KentridgeTerraceSurfaceCorrectionCatalogue
    {
        private static void ResolveBounds(Patch patch, uint seed, int scale,
                                          out int3 position, out int3 footprint)
        {
            int minX = patch.XDm - patch.ShoulderDm;
            int maxX = patch.XDm + patch.WidthDm + patch.ShoulderDm;
            int minZ = patch.ZDm - patch.ShoulderDm;
            int maxZ = patch.ZDm + patch.DepthDm + patch.ShoulderDm;
            int target = KentridgeVerticalProfile.SurfaceYAtDm(
                patch.AnchorXDm, patch.AnchorZDm, seed, scale);
            int minY = target, maxY = target;
            Sample(minX, minZ, seed, scale, ref minY, ref maxY);
            Sample(maxX, minZ, seed, scale, ref minY, ref maxY);
            Sample(minX, maxZ, seed, scale, ref minY, ref maxY);
            Sample(maxX, maxZ, seed, scale, ref minY, ref maxY);
            Sample((minX + maxX) / 2, (minZ + maxZ) / 2,
                   seed, scale, ref minY, ref maxY);

            int pad = VerticalPaddingDm * scale;
            int baseY = Math.Max(0, minY - pad);
            int topY = Math.Min(TerrainQuery.MaxHeight, maxY + pad);
            position = new int3(minX * scale, baseY, minZ * scale);
            footprint = new int3(
                Math.Max(1, (maxX - minX) * scale),
                Math.Max(1, topY - baseY + 1),
                Math.Max(1, (maxZ - minZ) * scale));
        }

        private static void Sample(int xDm, int zDm, uint seed, int scale,
                                   ref int minY, ref int maxY)
        {
            int y = TerrainQuery.HeightAt(xDm * scale, zDm * scale, seed);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        private static int[] Program(Patch patch, int3 footprint,
                                     VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte moss = settings.Materials.Resolve(MaterialRole.Moss);
            byte paving = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            var b = new ProgramBuilder();
            b.Box(0, 0, 0, footprint.x, footprint.y, footprint.z,
                  stone, PrimitiveMode.PaintSolid);
            b.Box(0, 0, 0, footprint.x, footprint.y, footprint.z,
                  moss, PrimitiveMode.PaintSurface);
            if (patch.UrbanCore)
                b.Box(patch.ShoulderDm * s, 0, patch.ShoulderDm * s,
                      patch.WidthDm * s, footprint.y, patch.DepthDm * s,
                      paving, PrimitiveMode.PaintSurface);
            return b.Finish();
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new();

            public void Box(int x, int y, int z, int sx, int sy, int sz,
                            byte material, PrimitiveMode mode)
            {
                _code.Add((int)ShapeOp.EmitBox); _code.Add(0);
                _code.Add(x); _code.Add(y); _code.Add(z);
                _code.Add(sx); _code.Add(sy); _code.Add(sz);
                _code.Add(material); _code.Add(0); _code.Add(0); _code.Add((int)mode);
            }

            public int[] Finish()
            {
                _code.Add((int)ShapeOp.End); _code.Add(0);
                return _code.ToArray();
            }
        }
    }
}
