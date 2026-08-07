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

        /// <summary>Direction light points *from* the surface toward the sun.</summary>
        public static Vector3 SunDirection = new Vector3(0.45f, 0.78f, 0.43f).normalized;

        public static Color SkyHorizon = new(0.62f, 0.70f, 0.80f);
        public static Color SkyZenith = new(0.25f, 0.44f, 0.72f);

        /// <summary>Material colours by index. Element 0 is empty and never shaded.</summary>
        public static Vector4[] MaterialColours =
        {
            new(1f, 0f, 1f, 1f),
            new(0.52f, 0.53f, 0.56f, 1f),   // stone
            new(0.46f, 0.29f, 0.14f, 1f),   // wood
            new(0.82f, 0.72f, 0.46f, 1f),   // sand
            new(0.52f, 0.76f, 0.84f, 1f),   // glass
            new(0.15f, 0.15f, 0.17f, 1f),   // bedrock
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
