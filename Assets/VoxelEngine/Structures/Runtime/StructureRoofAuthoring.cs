using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Shared bounded integer roof emission for archetype composition. RoofConfig remains the public
    /// policy contract; this authorer is the single runtime implementation for flat, shed, gable,
    /// and the existing hip approximation.
    /// </summary>
    public static class StructureRoofAuthoring
    {
        public static void Author(
            IStructureAuthoringSession authoring,
            int3 footprintMin,
            int width,
            int depth,
            int baseY,
            in RoofConfig roof,
            byte material)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (width <= 0 || depth <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(width));
            if (!roof.IsWellFormed)
                throw new System.ArgumentException("Roof configuration is invalid.", nameof(roof));

            int eave = roof.EaveOverhang;
            int minX = footprintMin.x - eave;
            int minZ = footprintMin.z - eave;
            int roofWidth = width + eave * 2;
            int roofDepth = depth + eave * 2;

            switch (roof.Style)
            {
                case RoofStyle.Flat:
                    authoring.Box(
                        new int3(minX, baseY, minZ),
                        new int3(roofWidth, roof.Thickness, roofDepth),
                        material);
                    break;

                case RoofStyle.Gable:
                {
                    int slopeSpan = roof.RidgeAxis == RoofAxis.X ? roofDepth : roofWidth;
                    int halfSpan = math.max(1, slopeSpan / 2);
                    int roofHeight = math.max(
                        roof.Thickness,
                        (halfSpan * roof.PitchRise + roof.PitchRun - 1) / roof.PitchRun);
                    authoring.Gable(
                        new int3(minX, baseY, minZ),
                        new int3(roofWidth, roofHeight, roofDepth),
                        roof.RidgeAxis == RoofAxis.X,
                        material);
                    break;
                }

                case RoofStyle.Shed:
                    AuthorShed(authoring, minX, baseY, minZ, roofWidth, roofDepth,
                        in roof, material);
                    break;

                case RoofStyle.Hip:
                    AuthorHip(authoring, minX, baseY, minZ, roofWidth, roofDepth,
                        in roof, material);
                    break;

                default:
                    throw new System.ArgumentOutOfRangeException(nameof(roof.Style));
            }
        }

        private static void AuthorShed(
            IStructureAuthoringSession authoring,
            int minX,
            int baseY,
            int minZ,
            int width,
            int depth,
            in RoofConfig roof,
            byte material)
        {
            if (roof.RidgeAxis == RoofAxis.X)
            {
                for (int z = 0; z < depth; z++)
                {
                    int rise = z * roof.PitchRise / roof.PitchRun;
                    authoring.Box(
                        new int3(minX, baseY + rise, minZ + z),
                        new int3(width, roof.Thickness, 1),
                        material);
                }
            }
            else
            {
                for (int x = 0; x < width; x++)
                {
                    int rise = x * roof.PitchRise / roof.PitchRun;
                    authoring.Box(
                        new int3(minX + x, baseY + rise, minZ),
                        new int3(1, roof.Thickness, depth),
                        material);
                }
            }
        }

        private static void AuthorHip(
            IStructureAuthoringSession authoring,
            int minX,
            int baseY,
            int minZ,
            int width,
            int depth,
            in RoofConfig roof,
            byte material)
        {
            int maxInset = math.max(0, math.min(width, depth) / 2 - 1);
            int roofHeight = math.max(
                roof.Thickness,
                (maxInset * roof.PitchRise + roof.PitchRun - 1) / roof.PitchRun);

            for (int y = 0; y < roofHeight; y++)
            {
                int inset = math.min(maxInset, y * roof.PitchRun / roof.PitchRise);
                int layerWidth = width - inset * 2;
                int layerDepth = depth - inset * 2;
                if (layerWidth <= 0 || layerDepth <= 0)
                    break;

                authoring.Box(
                    new int3(minX + inset, baseY + y, minZ + inset),
                    new int3(layerWidth, roof.Thickness, layerDepth),
                    material);
            }
        }
    }
}
