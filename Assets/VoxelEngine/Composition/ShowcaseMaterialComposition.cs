using VoxelEngine.Composition.Api;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Application-to-showcase material-role handoff. The game installs semantic choices before
    /// scene load; ShowcaseWorld consumes only the resulting opaque role set.
    /// </summary>
    public static class ShowcaseMaterialComposition
    {
        private static ShowcaseMaterialSet s_Roles;
        private static bool s_Configured;

        public static void Configure(in ShowcaseMaterialSet roles)
        {
            s_Roles = roles;
            s_Configured = true;
        }

        public static bool TryGet(out ShowcaseMaterialSet roles)
        {
            roles = s_Roles;
            return s_Configured;
        }

        /// <summary>Test/editor lifecycle hook. Runtime game bootstrap installs again before scene load.</summary>
        public static void Reset()
        {
            s_Roles = default;
            s_Configured = false;
        }
    }
}
