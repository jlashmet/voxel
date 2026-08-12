using Unity.Mathematics;

namespace VoxelEngine.Structures
{
    /// <summary>
    /// Reusable authored-surface operations for structural voxel masonry. These operations affect
    /// only the shallow presentation face; continuous structural mass behind the face remains voxel data.
    /// </summary>
    public static class WorldArtVoxelMasonrySurface
    {
        public static void RecessArchivoltJoints(ref VoxelBrush brush,
                                                 in WorldArtVoxelArchSpec spec,
                                                 int stoneCount = 15,
                                                 int recessDepth = 1)
        {
            stoneCount = math.max(5, stoneCount);
            recessDepth = math.max(0, recessDepth);
            if (recessDepth == 0) return;

            int innerRadius = math.max(4, spec.HalfOpening);
            int outerRadius = innerRadius + math.max(3, spec.RingThickness);
            int depth = math.max(4, spec.Depth);
            int frontZ = spec.BaseCentre.z - depth / 2;
            int faceDepth = math.clamp((spec.ArchivoltProjection > 0 ? spec.ArchivoltProjection : 2) + 1,
                                       2, depth - 1);
            int maxRecess = math.max(1, depth - 2);
            recessDepth = math.min(recessDepth, maxRecess);
            byte jointMaterial = spec.JointMaterial == 0 ? spec.StoneMaterial : spec.JointMaterial;

            // The reusable voussoir primitives own the real mass, depth and damage behavior. A dressed
            // arch still needs a tightly keyed front bed: neighboring stones meet at the face and the
            // mortar joint is carved afterward, instead of leaving projection-sized holes between wedges.
            SealArchivoltBackKey(ref brush, in spec, innerRadius, outerRadius,
                frontZ, faceDepth, spec.StoneMaterial);
            SealArchivoltFaceKey(ref brush, in spec, innerRadius, outerRadius,
                frontZ, math.min(2, faceDepth), spec.StoneMaterial);
            SealArchivoltIntrados(ref brush, in spec, innerRadius,
                frontZ, faceDepth, spec.StoneMaterial);
            SealArchivoltBeds(ref brush, in spec, stoneCount, innerRadius, outerRadius,
                frontZ, faceDepth, spec.StoneMaterial);

            // Mortar is the only visible separation. Carve one shallow radial bed after all hidden
            // structural keys are in place, then paint the stone immediately behind it dark.
            for (int boundary = 1; boundary < stoneCount; boundary++)
            {
                float angle = math.PI * boundary / stoneCount;
                float ca = math.cos(angle);
                float sa = math.sin(angle);
                for (int r = innerRadius - 1; r <= outerRadius + 1; r++)
                {
                    int x = (int)math.round(ca * r);
                    int y = (int)math.round(sa * r);
                    int worldX = spec.BaseCentre.x + x;
                    int worldY = spec.BaseCentre.y + math.max(8, spec.PierHeight) + y;
                    RecessPoint(ref brush, worldX, worldY, frontZ, depth, recessDepth,
                        spec.EmptyMaterial, jointMaterial);
                }
            }

            RecessPierJoints(ref brush, in spec, -1, spec.Seed + 101u, recessDepth, jointMaterial);
            RecessPierJoints(ref brush, in spec, 1, spec.Seed + 211u, recessDepth, jointMaterial);
        }

        private static void SealArchivoltBackKey(ref VoxelBrush brush, in WorldArtVoxelArchSpec spec,
                                                  int innerRadius, int outerRadius,
                                                  int frontZ, int faceDepth, byte stoneMaterial)
        {
            int springY = spec.BaseCentre.y + math.max(8, spec.PierHeight);
            int z = frontZ + faceDepth - 1;
            float inner2 = (innerRadius - 0.20f) * (innerRadius - 0.20f);
            float outer2 = (outerRadius + 0.20f) * (outerRadius + 0.20f);
            for (int y = 0; y <= outerRadius + 1; y++)
            for (int x = -outerRadius - 1; x <= outerRadius + 1; x++)
            {
                float d2 = x * x + y * y;
                if (d2 < inner2 || d2 > outer2) continue;
                brush.Set(spec.BaseCentre.x + x, springY + y, z, stoneMaterial);
            }
        }

        private static void SealArchivoltFaceKey(ref VoxelBrush brush, in WorldArtVoxelArchSpec spec,
                                                  int innerRadius, int outerRadius,
                                                  int frontZ, int keyDepth, byte stoneMaterial)
        {
            int springY = spec.BaseCentre.y + math.max(8, spec.PierHeight);
            float inner2 = (innerRadius - 0.06f) * (innerRadius - 0.06f);
            float outer2 = (outerRadius + 0.10f) * (outerRadius + 0.10f);
            int limit = outerRadius + 1;
            for (int y = 0; y <= limit; y++)
            for (int x = -limit; x <= limit; x++)
            {
                float d2 = x * x + y * y;
                if (d2 < inner2 || d2 > outer2) continue;
                for (int d = 0; d < keyDepth; d++)
                    brush.Set(spec.BaseCentre.x + x, springY + y, frontZ + d, stoneMaterial);
            }
        }

        private static void SealArchivoltIntrados(ref VoxelBrush brush, in WorldArtVoxelArchSpec spec,
                                                   int innerRadius, int frontZ, int faceDepth,
                                                   byte stoneMaterial)
        {
            int springY = spec.BaseCentre.y + math.max(8, spec.PierHeight);
            float inner2 = (innerRadius - 0.12f) * (innerRadius - 0.12f);
            float outer2 = (innerRadius + 1.20f) * (innerRadius + 1.20f);
            int limit = innerRadius + 2;
            for (int y = 0; y <= limit; y++)
            for (int x = -limit; x <= limit; x++)
            {
                float d2 = x * x + y * y;
                if (d2 < inner2 || d2 > outer2) continue;
                for (int z = frontZ; z < frontZ + faceDepth; z++)
                    brush.Set(spec.BaseCentre.x + x, springY + y, z, stoneMaterial);
            }
        }

        private static void SealArchivoltBeds(ref VoxelBrush brush, in WorldArtVoxelArchSpec spec,
                                               int stoneCount, int innerRadius, int outerRadius,
                                               int frontZ, int faceDepth, byte stoneMaterial)
        {
            int springY = spec.BaseCentre.y + math.max(8, spec.PierHeight);
            for (int boundary = 1; boundary < stoneCount; boundary++)
            {
                float angle = math.PI * boundary / stoneCount;
                float ca = math.cos(angle);
                float sa = math.sin(angle);
                for (int r = innerRadius - 1; r <= outerRadius + 1; r++)
                {
                    int x = (int)math.round(ca * r);
                    int y = (int)math.round(sa * r);
                    for (int z = frontZ; z < frontZ + faceDepth; z++)
                        brush.Set(spec.BaseCentre.x + x, springY + y, z, stoneMaterial);
                }
            }
        }

        private static void RecessPierJoints(ref VoxelBrush brush, in WorldArtVoxelArchSpec spec,
                                             int side, uint seed, int recessDepth,
                                             byte jointMaterial)
        {
            int halfOpening = math.max(4, spec.HalfOpening);
            int pierWidth = math.max(4, spec.PierWidth);
            int pierHeight = math.max(8, spec.PierHeight);
            int courseHeight = math.max(3, spec.CourseHeight);
            int impostHeight = math.max(2, spec.ImpostHeight);
            int depth = math.max(4, spec.Depth);
            int pierOffset = halfOpening + (pierWidth + 1) / 2;
            int plinthHeight = math.max(3, courseHeight - 1);
            int shaftHeight = math.max(courseHeight * 2, pierHeight - plinthHeight - impostHeight);
            int leftX = spec.BaseCentre.x + side * pierOffset - pierWidth / 2;
            int shaftY = spec.BaseCentre.y + plinthHeight;
            int frontZ = spec.BaseCentre.z - depth / 2;
            int rows = math.max(2, shaftHeight / courseHeight);

            for (int row = 1; row < rows; row++)
            {
                int jointY = shaftY + row * courseHeight;
                if (jointY >= shaftY + shaftHeight - 1) break;
                for (int x = leftX + 1; x < leftX + pierWidth - 1; x++)
                    RecessPoint(ref brush, x, jointY, frontZ, depth, recessDepth,
                        spec.EmptyMaterial, jointMaterial);
            }

            for (int row = 0; row < rows; row++)
            {
                int rowY = shaftY + row * courseHeight;
                int rowTop = math.min(shaftY + shaftHeight, rowY + courseHeight);
                int shift = (row & 1) == 0 ? -1 : 1;
                if ((Hash(seed + (uint)(row * 37)) & 15u) == 0u) shift = 0;
                int seamX = leftX + pierWidth / 2 + shift;
                seamX = math.clamp(seamX, leftX + 3, leftX + pierWidth - 4);
                for (int y = rowY + 1; y < rowTop - 1; y++)
                    RecessPoint(ref brush, seamX, y, frontZ, depth, recessDepth,
                        spec.EmptyMaterial, jointMaterial);
            }
        }

        private static void RecessPoint(ref VoxelBrush brush, int x, int y, int frontZ,
                                        int depth, int recessDepth, byte emptyMaterial,
                                        byte jointMaterial)
        {
            for (int d = 0; d < recessDepth; d++)
                brush.Set(x, y, frontZ + d, emptyMaterial);
            int backZ = frontZ + recessDepth;
            if (backZ < frontZ + depth)
                brush.Set(x, y, backZ, jointMaterial);
        }

        private static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }
    }
}
