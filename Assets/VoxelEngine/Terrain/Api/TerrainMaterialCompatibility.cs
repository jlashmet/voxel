using System;

namespace VoxelEngine.Terrain.Api
{
    /// <summary>
    /// Transitional binding used only by legacy terrain entry points that predate explicit
    /// <see cref="TerrainMaterialSet"/> parameters. The engine stores opaque roles here; the
    /// application decides which semantic material indices occupy them.
    /// </summary>
    public static class TerrainMaterialCompatibility
    {
        private static TerrainMaterialSet s_materials;
        private static bool s_configured;

        public static bool IsConfigured => s_configured;

        public static void Configure(in TerrainMaterialSet materials)
        {
            s_materials = materials;
            s_configured = true;
        }

        public static void Reset()
        {
            s_materials = default;
            s_configured = false;
        }

        public static TerrainMaterialSet RequireConfigured()
        {
            if (!s_configured)
                throw new InvalidOperationException(
                    "Terrain material roles have not been configured by the application composition root. " +
                    "Pass TerrainMaterialSet explicitly or configure the compatibility binding first.");
            return s_materials;
        }
    }
}
