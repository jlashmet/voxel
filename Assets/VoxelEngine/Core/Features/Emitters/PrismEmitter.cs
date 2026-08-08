using Unity.Mathematics;

namespace VoxelEngine.Core.Features.Emitters
{
    /// <summary>Extruded profiles: gable roofs, shed roofs, arched openings.</summary>
    public static class PrismEmitter
    {
        public static Primitive Prism(int3 min, int3 size, PrismProfile profile, byte material,
                                      PrimitiveMode mode, int order)
        {
            return new Primitive
            {
                Shape = PrimitiveShape.Prism,
                Mode = mode,
                Material = material,
                Profile = profile,
                Order = order,
                A = min,
                B = min + math.max(size, new int3(1, 1, 1)) - 1,
            };
        }

        public static bool Contains(in Primitive p, int3 voxel)
        {
            if (!BoxEmitter.BoxContains(in p, voxel)) return false;

            int width = p.B.x - p.A.x + 1;
            int height = p.B.y - p.A.y + 1;

            int x = voxel.x - p.A.x;
            int y = voxel.y - p.A.y;

            switch (p.Profile)
            {
                case PrismProfile.Gable:
                {
                    // Symmetric peak. Distance from the centre line decides the roof height, so
                    // both slopes are the same shape rather than one being a voxel longer.
                    int fromCentre = math.abs(2 * x - (width - 1));
                    int allowed = height - ((fromCentre * height) / width);
                    return y < allowed;
                }

                case PrismProfile.Shed:
                {
                    int allowed = ((x + 1) * height) / width;
                    return y < allowed;
                }

                case PrismProfile.Arch:
                {
                    // Half-ellipse: everything under the curve is solid, the opening is carved by
                    // using this primitive in Carve mode.
                    int halfWidth = width / 2;
                    if (halfWidth == 0) return true;

                    int dx = x - halfWidth;
                    long lhs = (long)dx * dx * height * height;
                    long rhs = (long)halfWidth * halfWidth * (height - y) * (height - y);
                    return lhs <= rhs;
                }

                default:
                    return true;
            }
        }
    }
}
