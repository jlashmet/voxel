using UnityEngine;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering.SurfaceExtraction;

namespace VoxelEngine.Rendering
{
    /// <summary>
    /// A read-only snapshot of the world for the render pass.
    ///
    /// <see cref="RegionTable"/> and <see cref="BrickPool"/> are handle-like: copying them copies
    /// native container handles, not the data, so the pass reads exactly what the simulation
    /// holds. The direction is one-way by construction — the pass consumes the brickmap and
    /// produces pixels, never the reverse (Constitution Principle I).
    /// </summary>
    public struct VoxelWorldView
    {
        public RegionTable Table;
        public BrickPool Pool;
        public MaterialPalette Palette;
        public SurfaceCatalogue SurfaceCatalogue;
        public CoatingCatalogue CoatingCatalogue;
        public ProfileBlockStore ProfileBlocks;

        public bool IsValid => Table.IsCreated && Pool.IsCreated
            && SurfaceCatalogue.CatalogueHash != 0 && CoatingCatalogue.CatalogueHash != 0;
    }

    /// <summary>
    /// Registration point between whoever owns the world and the render feature.
    ///
    /// A renderer feature is a project asset, instantiated by URP, with no constructor the game
    /// can reach — so the world has to hand itself in rather than be injected. A static is the
    /// honest shape for that; the alternative is a singleton MonoBehaviour that does the same
    /// thing with more ceremony.
    /// </summary>
    public static class VoxelRenderBridge
    {
        /// <summary>Supplies the current world. Null when nothing is driving the engine.</summary>
        public static System.Func<VoxelWorldView> Source;

        /// <summary>Versioned changes consumed independently by every derived render domain.</summary>
        public static VoxelChangeJournal Changes;

        /// <summary>
        /// Read-only diagnostics from the most recent production surface pass. Offline captures,
        /// telemetry and tests may observe convergence; they never drive extraction through this
        /// value or acquire ownership of scheduler state.
        /// </summary>
        public static VoxelSurfaceMetrics SurfaceMetrics { get; internal set; }

        public static int RenderFeatureEnqueueCount { get; internal set; }
        public static int SurfacePassRecordCount { get; internal set; }
        public static string LastSurfacePassState { get; internal set; } = "not-recorded";

        public static void ResetSurfacePassDiagnostics(string state = "not-recorded")
        {
            RenderFeatureEnqueueCount = 0;
            SurfacePassRecordCount = 0;
            LastSurfacePassState = state;
        }

        /// <summary>
        /// CPU extraction budgets per rendered frame. Runtime defaults remain conservative;
        /// loading screens, offline captures and photo modes may temporarily spend more to reach
        /// convergence without changing geometry semantics or introducing another extractor.
        /// </summary>
        // Four asynchronous worker shards each receive this bounded admission/publication slice.
        // 0.50 ms keeps worst-case render-thread orchestration near 2 ms while Burst topology
        // proceeds off-thread; 0.20 ms fell below useful post-job granularity on Apple silicon.
        public static double SolidBuildBudgetMs = 0.50;
        public static double WaterBuildBudgetMs = 0.15;
        public static bool SurfaceBuildEnabled = true;

        /// <summary>
        /// Diagnostic tint for continuous extracted geometry. White is production; fixed-view
        /// tests can use a loud colour to prove which pixels are owned by the solid extractor.
        /// </summary>
        public static Color SurfaceDebugTint = Color.white;

        /// <summary>World seed, so the far field can evaluate the same terrain the CPU generates.</summary>
        public static uint TerrainSeed;
        public static uint FarBaseHeight;
        public static bool FarFieldEnabled;

        /// <summary>
        /// Presentation-only rectangular clip volume in world-voxel coordinates. Used by fixed
        /// section views to expose generated rooms without mutating authoritative storage.
        /// </summary>
        public static bool CutawayEnabled;
        public static Vector3 CutawayMinVoxel;
        public static Vector3 CutawayMaxVoxel;

        /// <summary>Direction light points *from* the surface toward the sun.</summary>
        /// <remarks>
        /// Roughly 50 degrees of elevation, matching the reference board's midday key. The
        /// previous 34 degrees put long raking shadows across the whole approach and, combined
        /// with the dusk sky below, was most of why the castle read as flat and mauve rather than
        /// as sunlit stone.
        /// </remarks>
        public static Vector3 SunDirection = new Vector3(-0.48f, 0.76f, -0.44f).normalized;

        // Bright open daylight: a pale, slightly hazy horizon under a saturated zenith.
        public static Color SkyHorizon = new(0.66f, 0.75f, 0.85f);
        public static Color SkyZenith = new(0.24f, 0.45f, 0.76f);

        /// <summary>
        /// Presentation lights consumed directly by the voxel surface shader. xyz is world metres
        /// and w is radius; matching colour xyz is linear tint and w is intensity.
        /// </summary>
        public static Vector4[] LocalLights = System.Array.Empty<Vector4>();
        public static Vector4[] LocalLightColours = System.Array.Empty<Vector4>();

        /// <summary>
        /// Camera-mounted spotlight consumed by the voxel surface shader.
        /// </summary>
        public static bool FlashlightEnabled;
        public static Vector3 FlashlightPosition;
        public static Vector3 FlashlightDirection = Vector3.forward;
        public static Color FlashlightColour = new(1.00f, 0.91f, 0.72f, 1f);
        public static float FlashlightRange = 34f;
        public static float FlashlightIntensity = 2.4f;
        public static float FlashlightInnerCos = 0.94f;
        public static float FlashlightOuterCos = 0.78f;

        public static bool TryGetWorld(out VoxelWorldView view)
        {
            view = default;
            if (Source == null) return false;

            view = Source();
            return view.IsValid;
        }
    }
}
