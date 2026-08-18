using Unity.Mathematics;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Camera-relative horizontal residency geometry for the showcase world.
    ///
    /// Region coordinates alone are not enough to decide whether a column belongs to a physical
    /// radius: the camera can sit anywhere inside its current 51.2 m region. Testing the region's
    /// AABB against the requested circle keeps the streamed voxel footprint aligned with the
    /// camera-centred near/far handoff instead of lagging by up to one region diagonal.
    /// </summary>
    public static class ShowcaseResidencyFootprint
    {
        public static bool ColumnIntersectsRadius(
            float3 cameraMetres, int regionX, int regionZ, float radiusMetres)
        {
            float minX = regionX * ShowcaseWorld.RegionMetres;
            float maxX = minX + ShowcaseWorld.RegionMetres;
            float minZ = regionZ * ShowcaseWorld.RegionMetres;
            float maxZ = minZ + ShowcaseWorld.RegionMetres;

            float dx = cameraMetres.x < minX ? minX - cameraMetres.x
                     : cameraMetres.x > maxX ? cameraMetres.x - maxX
                     : 0f;
            float dz = cameraMetres.z < minZ ? minZ - cameraMetres.z
                     : cameraMetres.z > maxZ ? cameraMetres.z - maxZ
                     : 0f;
            float radius = math.max(0f, radiusMetres);
            return dx * dx + dz * dz <= radius * radius;
        }
    }
}
