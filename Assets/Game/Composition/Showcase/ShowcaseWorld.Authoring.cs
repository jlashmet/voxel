using System;
using Unity.Mathematics;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        private const int MaxAuthoredBoxVoxels = 262_144;

        /// <summary>
        /// Authors one bounded axis-aligned voxel box through the world's ordinary Storage.Api
        /// mutation path. This is intentionally material-agnostic: validation scenes and game
        /// composition may choose semantic material IDs, while storage, change publication and
        /// rendering discovery remain identical to normal voxel edits.
        /// </summary>
        public int AuthorVoxelBox(int3 minCorner, int3 size, byte material)
        {
            if (size.x <= 0 || size.y <= 0 || size.z <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Voxel box dimensions must be positive.");
            if (minCorner.y < 0)
                throw new ArgumentOutOfRangeException(nameof(minCorner), "Voxel authoring below y=0 is not supported by the showcase world.");

            long volume = (long)size.x * size.y * size.z;
            if (volume > MaxAuthoredBoxVoxels)
                throw new ArgumentOutOfRangeException(nameof(size),
                    $"Voxel box volume {volume} exceeds the bounded authoring limit {MaxAuthoredBoxVoxels}.");

            int changed = 0;
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            for (int z = 0; z < size.z; z++)
            {
                int3 voxel = minCorner + new int3(x, y, z);
                if (!SetMaterialApi(voxel, material))
                    continue;

                MarkDirty(voxel);
                changed++;
            }

            return changed;
        }
    }
}
