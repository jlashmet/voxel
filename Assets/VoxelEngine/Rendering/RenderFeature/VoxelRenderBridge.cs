using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Storage;

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

        /// <summary>Region the camera is standing in; centres the GPU window.</summary>
        public int3 CameraRegion;

        public bool IsValid => Table.IsCreated && Pool.IsCreated;
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

        /// <summary>Regions whose brick pointers changed and need re-uploading.</summary>
        public static System.Collections.Generic.HashSet<int3> RegionsNeedingUpload;

        /// <summary>
        /// 0 shades normally; 1 and 2 emit traversal state as colour. Kept out of the asset so a
        /// test can flip it at runtime — locating a traversal failure by staring at the shaded
        /// image does not work, as several wrong guesses established.
        /// </summary>
        public static int DebugMode;

        /// <summary>
        /// Diagnostic tint for continuous extracted geometry. White is production; fixed-view
        /// tests can use a loud colour to prove exactly which pixels have left the fallback.
        /// </summary>
        public static Color SurfaceDebugTint = Color.white;

        /// <summary>World seed, so the far field can evaluate the same terrain the CPU generates.</summary>
        public static uint TerrainSeed;

        /// <summary>Base terrain height in voxels, matching the world's generator.</summary>
        public static int FarBaseHeight = 220;

        /// <summary>
        /// How far the procedural horizon extends, in metres. This is not bounded by residency —
        /// the far field holds no data — so it is a shading cost, not a memory one.
        /// </summary>
        public static float FarDistance = 8000f;

        public static bool FarFieldEnabled = true;

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
        /// Presentation lights consumed directly by the compute shader. xyz is world metres and
        /// w is radius; matching colour xyz is linear tint and w is intensity. The raymarch owns
        /// its pixels, so ordinary Unity lights cannot illuminate voxel hits.
        /// </summary>
        public static Vector4[] LocalLights = System.Array.Empty<Vector4>();
        public static Vector4[] LocalLightColours = System.Array.Empty<Vector4>();

        /// <summary>
        /// Camera-mounted spotlight consumed by the voxel compute shader. Unity lights cannot
        /// illuminate the raymarch target, so gameplay equipment must cross this bridge too.
        /// </summary>
        public static bool FlashlightEnabled;
        public static Vector3 FlashlightPosition;
        public static Vector3 FlashlightDirection = Vector3.forward;
        public static Color FlashlightColour = new(1.00f, 0.91f, 0.72f, 1f);
        public static float FlashlightRange = 34f;
        public static float FlashlightIntensity = 2.4f;
        public static float FlashlightInnerCos = 0.94f;
        public static float FlashlightOuterCos = 0.78f;

        /// <summary>Material colours by index. Element 0 is empty and never shaded.</summary>
        public static Vector4[] MaterialColours =
        {
            new(1f, 0f, 1f, 1f),
            new(0.43f, 0.45f, 0.48f, 1f),   // cool weathered limestone
            new(0.46f, 0.29f, 0.14f, 1f),   // wood
            new(0.82f, 0.72f, 0.46f, 1f),   // sand
            new(0.78f, 0.48f, 0.18f, 1f),   // warm lit glass
            new(0.15f, 0.15f, 0.17f, 1f),   // bedrock
            new(0.23f, 0.25f, 0.28f, 1f),   // structural / cave stone
            new(0.24f, 0.26f, 0.32f, 1f),   // slate
            new(0.46f, 0.24f, 0.18f, 1f),   // tile
            new(0.62f, 0.12f, 0.14f, 1f),   // cloth
            new(0.31f, 0.44f, 0.20f, 1f),   // grass
            new(0.10f, 0.43f, 0.56f, 1f),   // water
            new(0.80f, 0.66f, 0.26f, 1f),   // gold
            new(0.38f, 0.31f, 0.24f, 1f),   // dirt
            new(0.32f, 0.40f, 0.24f, 1f),   // moss
            new(0.16f, 0.19f, 0.18f, 1f),   // dark leaded window glass
            new(0.22f, 0.62f, 0.78f, 1f),   // aerated cascade
            new(0.08f, 0.56f, 0.82f, 1f),   // luminous cave crystal
        };

        public static bool TryGetWorld(out VoxelWorldView view)
        {
            view = default;
            if (Source == null) return false;

            view = Source();
            return view.IsValid;
        }
    }
}
