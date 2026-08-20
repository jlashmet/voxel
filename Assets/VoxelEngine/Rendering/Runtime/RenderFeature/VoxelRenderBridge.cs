using System;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime
{
    /// <summary>
    /// A read-only snapshot of the world for the render pass.
    ///
    /// Storage is exposed only through borrowed read contracts. The render pass never receives
    /// the physical region table, brick pool, allocator identity, pool slots, or mutable structure
    /// authoring stores. The direction remains one-way: rendering consumes authoritative reads and
    /// produces pixels.
    /// </summary>
    public struct VoxelWorldView
    {
        public IRegionReadSource Storage;
        public MaterialPaletteView Palette;
        public SurfaceCatalogueView SurfaceCatalogueView;
        public CoatingCatalogueView CoatingCatalogueView;
        public IProfileBlockReadSource ProfileBlocks;

        public bool IsValid => Storage != null
            && SurfaceCatalogueView.CatalogueHash != 0 && CoatingCatalogueView.CatalogueHash != 0;
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
        public static IVoxelChangeSource Changes;

        // The URP renderer feature outlives individual application worlds. A world owner must be
        // able to synchronously retire renderer-side jobs/pins before disposing the Storage that
        // backs them. Keep the callback private to Rendering.Runtime; Composition gets only the
        // release operation, not scheduler ownership.
        private static event System.Action s_WorldReleasing;
        private static VoxelRenderPass s_ActivePass;

        /// <summary>
        /// The render pass that most recently executed through URP. This is diagnostics-only:
        /// tests may inspect production-visible entries without trying to discover renderer-data
        /// assets through Resources, but cannot replace or drive scheduler ownership.
        /// </summary>
        internal static VoxelRenderPass ActivePass => s_ActivePass;

        internal static void RegisterActivePass(VoxelRenderPass pass) =>
            s_ActivePass = pass;

        internal static void UnregisterActivePass(VoxelRenderPass pass)
        {
            if (ReferenceEquals(s_ActivePass, pass)) s_ActivePass = null;
        }

        internal static void RegisterWorldReleaseHandler(System.Action handler) =>
            s_WorldReleasing += handler;

        internal static void UnregisterWorldReleaseHandler(System.Action handler) =>
            s_WorldReleasing -= handler;

        public static void ReleaseWorldResources()
        {
            s_WorldReleasing?.Invoke();
            SurfaceMetrics = default;
        }

        /// <summary>
        /// Read-only diagnostics from the most recent production surface pass. Offline captures,
        /// telemetry and tests may observe convergence; they never drive extraction through this
        /// value or acquire ownership of scheduler state.
        /// </summary>
        public static VoxelSurfaceMetrics SurfaceMetrics { get; internal set; }

        public static int RenderFeatureEnqueueCount { get; internal set; }
        public static int SurfacePassRecordCount { get; internal set; }

        /// <summary>
        /// Per-LOD residency, on demand. The ring bands are recomputed every frame from the
        /// streamed radius, so a truncated ring's real band appears nowhere in the static layout
        /// and cannot be reasoned about from the source alone. Formatting allocates: call this
        /// from a diagnostic, never per frame from gameplay.
        /// </summary>
        public static Func<string> DescribeRings { get; internal set; }
        public static string LastSurfacePassState { get; internal set; } = "not-recorded";
        /// <summary>
        /// Full human-readable per-frame diagnostic strings allocate. Keep them disabled in
        /// gameplay; structured SurfaceMetrics carries the same data without formatting garbage.
        /// </summary>
        public static bool VerboseSurfaceDiagnostics;

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
        // Renderer-wide admission/publication controls. These are shared across every
        // LOD ring and worker; adding workers must never multiply the frame budget.
        //
        // These are the steady-state numbers, spent every frame once the view is complete. They are
        // deliberately small: with the view converged there is nothing on screen waiting on them, so
        // anything spent here is taken straight out of the frame for no visible gain.
        //
        // The millisecond deadlines are the frame-time guard — extraction and publication both stop
        // the moment their slice is spent, however much work is outstanding. The byte counters bound
        // how much a frame may attempt inside that slice, and want to be large enough to publish a
        // whole chunk (~476 KB in production) rather than splitting one across frames.
        public static double SolidBuildBudgetMs = 0.50;
        public static int SolidUploadBudgetBytes = 2 * 1024 * 1024;
        public static int SolidUploadSliceBytes = 1024 * 1024;
        public static int SolidUploadWorkerBudget = 4;
        public static double SolidUploadBudgetMs = 0.25;

        /// <summary>
        /// Multiplier applied to the budgets above while chunks inside the frustum still have no
        /// geometry — that is, while the player can see a hole.
        ///
        /// Convergence and steady-state framerate pull in opposite directions on a single budget.
        /// Sized for a converged view, filling a cold ~1.5 K chunk view takes thousands of frames and
        /// the world visibly assembles itself around the player. Sized for convergence, the scheduler
        /// keeps spending milliseconds of main-thread time every frame long after there is anything
        /// left to publish, which is what turns up in a profile as SchedulerPrepare/WorkerAdmission.
        /// Scaling by visible incompleteness pays the cost exactly while it buys something.
        /// </summary>
        /// <summary>
        /// Per-frame budget for turning resident regions into known surface chunks. Unlike the
        /// build and upload budgets this had no channel at all, so it could not be raised for a
        /// world small enough to discover in one go.
        /// </summary>
        public static double SurfaceDiscoveryBudgetMs = 0.10;

        public static double SurfaceConvergenceBudgetScale = 8.0;

        /// <summary>
        /// Chunk builds allowed in flight while the frustum still contains geometry-less chunks.
        /// </summary>
        /// <summary>
        /// Outer limit of voxel-meshed rings, in metres. Worlds set this from the radius they
        /// actually stream; rings beyond it have no resident regions and would render holes.
        /// </summary>
        public static float SurfaceMaxVoxelRingRadiusMetres =
            VoxelEngine.Rendering.Runtime.SurfaceExtraction.VoxelSurfaceScheduler
                       .MaxVoxelRingRadiusMetresDefault;

        /// <summary>Whether coarse voxel LOD rings are used. False meshes everything at the
        /// finest step, which only suits a world small enough to afford it.</summary>
        public static bool SurfaceLodEnabled = true;

        /// <summary>
        /// Draws the exposed-water surface. Diagnostic only: turning it off is how a flat sheet
        /// standing where terrain should be gets attributed to the water cache rather than to a
        /// hole in the solid surface.
        /// </summary>
        public static bool WaterRenderEnabled = true;

        public static int SurfaceMaxResidentChunksPerRing = 4096;

        public static int SurfaceMaxConcurrentBuildsConverging = 12;

        /// <summary>
        /// Chunk builds allowed in flight once the view is complete.
        ///
        /// This bounds prefetch, which is the work that keeps a moving player from walking into
        /// unmeshed ground. Set low it protects a stationary frame; set low for too long it starves
        /// the shell, and the cost reappears as a stall the moment the player moves.
        /// </summary>
        public static int SurfaceMaxConcurrentBuildsConverged = 1;
        /// <summary>
        /// Soft cap for active solid arena leases. The default does not constrain the fixed
        /// arena; tests/debugging may lower it to exercise real backpressure without reallocating
        /// GPU buffers or changing the arena's committed byte size.
        /// </summary>
        public static int SolidArenaMaxActiveLeases = int.MaxValue;

        /// <summary>
        /// Overrides the surface geometry arena's byte budget when positive. Diagnostic: the
        /// driver cost of writing into a ComputeBuffer the GPU is reading can scale with the
        /// buffer's size rather than the bytes written, and that is only visible by changing the
        /// size while holding everything else fixed.
        /// </summary>
        public static long SurfaceArenaBudgetBytesOverride;
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
        public static Color SkyHorizon = new(0.55f, 0.70f, 0.92f);
        // A deeper, more saturated zenith. The previous value sat close enough to the
        // horizon colour that the dome read as flat haze rather than sky.
        public static Color SkyZenith = new(0.16f, 0.40f, 0.82f);

        // -- clouds ---------------------------------------------------------------
        /// <summary>Size of the cloud deck's cells. Larger values make smaller clouds.</summary>
        public static float CloudScale = 0.55f;
        /// <summary>Density threshold a cell must exceed to be cloud. Lower means more sky
        /// covered; 0.5 is a broken fair-weather deck.</summary>
        public static float CloudCoverage = 0.50f;
        /// <summary>Drift rate across the deck, in arbitrary units per second.</summary>
        public static float CloudDriftSpeed = 0.006f;
        /// <summary>How strongly cloud replaces sky where it is present.</summary>
        public static float CloudOpacity = 0.92f;
        /// <summary>Sunlit cloud face.</summary>
        public static Color CloudColour = new(1.00f, 0.99f, 0.97f);
        /// <summary>Shaded underside. Kept blue-grey rather than neutral so cloud bases pick
        /// up skylight instead of reading as dirty smudges.</summary>
        public static Color CloudShadowColour = new(0.58f, 0.63f, 0.72f);

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
