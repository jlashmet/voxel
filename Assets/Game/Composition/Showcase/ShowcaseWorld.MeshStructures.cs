using System;
using System.Diagnostics;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Showcase
{
    public readonly struct MeshStructurePlacementResult
    {
        public readonly int VoxelsRequested;
        public readonly long VoxelsWritten;
        public readonly int RegionsPrepared;
        public readonly double PlacementMilliseconds;

        public MeshStructurePlacementResult(
            int voxelsRequested,
            long voxelsWritten,
            int regionsPrepared,
            double placementMilliseconds)
        {
            VoxelsRequested = voxelsRequested;
            VoxelsWritten = voxelsWritten;
            RegionsPrepared = regionsPrepared;
            PlacementMilliseconds = placementMilliseconds;
        }
    }

    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// Places an already-baked sparse mesh structure into the authoritative showcase voxel
        /// store. The source triangle mesh is deliberately absent from this API: ordinary runtime
        /// only decodes/replays discrete authored cells, so collision, edits, rendering and
        /// destruction all consume the same storage truth.
        /// </summary>
        public MeshStructurePlacementResult PlaceBakedMeshStructure(
            BakedVoxelStructure bake,
            int3 worldOrigin)
        {
            if (bake == null) throw new ArgumentNullException(nameof(bake));
            if (bake.Cells.Length == 0)
                throw new ArgumentException("A mesh structure bake must contain authored voxels.", nameof(bake));

            var stopwatch = Stopwatch.StartNew();
            int3 minVoxel = worldOrigin;
            int3 maxVoxelInclusive = worldOrigin + bake.Size - 1;
            int3 firstRegion = VoxelToRegion(minVoxel);
            int3 lastRegion = VoxelToRegion(maxVoxelInclusive);
            int regionsPrepared = 0;

            for (int y = firstRegion.y; y <= lastRegion.y; y++)
            for (int z = firstRegion.z; z <= lastRegion.z; z++)
            for (int x = firstRegion.x; x <= lastRegion.x; x++)
            {
                GenerateRegionBlocking(new int3(x, y, z));
                regionsPrepared++;
            }

            // Sparse mesh cells are slow-path writes by design, but the bake is bounded far below
            // the engine default authoring ceiling. Size the session to this exact artifact with a
            // small guard so a malformed/corrupt decode cannot silently turn into an unbounded pass.
            int writeBudget = checked(bake.Cells.Length + 64);
            IStructureAuthoringSession authoring =
                VoxelEngineBootstrap.CreateStructureAuthoring(_storage, writeBudget);
            bake.ReplayTo(authoring, worldOrigin);
            if (authoring.BudgetExceeded)
                throw new InvalidOperationException("Mesh structure placement exceeded its write budget.");

            _storage.PublishAllResidentRegions();
            stopwatch.Stop();
            return new MeshStructurePlacementResult(
                bake.Cells.Length,
                authoring.TotalVoxelsWritten,
                regionsPrepared,
                stopwatch.Elapsed.TotalMilliseconds);
        }

        private static int3 VoxelToRegion(int3 voxel) =>
            (int3)math.floor((float3)voxel / RegionVoxelEdge);
    }
}
