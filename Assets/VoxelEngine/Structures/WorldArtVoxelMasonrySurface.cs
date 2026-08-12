using Unity.Mathematics;

namespace VoxelEngine.Structures
{
    /// <summary>
    /// Reusable authored-surface operations for structural voxel masonry.
    ///
    /// These operations deliberately affect only the shallow presentation face of a component.
    /// The continuous structural mass behind the face is preserved, so destruction still acts on
    /// ordinary authoritative voxels while close-range masonry can have believable mortar depth.
    /// </summary>
    public static class WorldArtVoxelMasonrySurface
    {
        /// <summary>
        /// Recesses the radial mortar channels on the proud face of an arch without cutting through
        /// the structural ring. The first layer is removed and the next layer is painted with the
        /// joint material, producing a real 10 cm reveal with masonry immediately behind it.
        ///
        /// The operation is deterministic and derives every dimension from the reusable arch spec.
        /// It is safe on damaged arches: writing Empty into an already-empty damaged area is a no-op.
        /// </summary>
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
            int maxRecess = math.max(1, depth - 2);
            recessDepth = math.min(recessDepth, maxRecess);
            byte jointMaterial = spec.JointMaterial == 0 ? spec.StoneMaterial : spec.JointMaterial;

            for (int boundary = 1; boundary < stoneCount; boundary++)
            {
                float angle = math.PI * boundary / stoneCount;
                float ca = math.cos(angle);
                float sa = math.sin(angle);

                // Sample slightly beyond the nominal ring radii so smoothing cannot bridge the
                // channel at either arris. These writes only remove/paint material; they never add
                // stone outside the structural component.
                for (int r = innerRadius - 1; r <= outerRadius + 1; r++)
                {
                    int x = (int)math.round(ca * r);
                    int y = (int)math.round(sa * r);
                    int worldX = spec.BaseCentre.x + x;
                    int worldY = spec.BaseCentre.y + math.max(8, spec.PierHeight) + y;

                    for (int d = 0; d < recessDepth; d++)
                        brush.Set(worldX, worldY, frontZ + d, spec.EmptyMaterial);

                    int backZ = frontZ + recessDepth;
                    if (backZ < frontZ + depth)
                        brush.Set(worldX, worldY, backZ, jointMaterial);
                }
            }
        }
    }
}
