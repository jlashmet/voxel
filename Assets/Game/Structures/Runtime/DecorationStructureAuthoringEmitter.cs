using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Compatibility renderer for resolved decoration placements using the current structure-authoring
    /// primitives. Placement and scene logic remain backend-independent. Thin surfaces are emitted as
    /// one-voxel sheets here until the presentation layer gains a true sub-voxel surface backend.
    /// </summary>
    public static class DecorationStructureAuthoringEmitter
    {
        public static bool TryAuthor(
            IStructureAuthoringSession authoring,
            DecorationPlacement[] placements)
        {
            if (authoring == null)
                throw new System.ArgumentNullException(nameof(authoring));
            if (placements == null)
                return false;

            for (int i = 0; i < placements.Length; i++)
            {
                if (!TryAuthor(authoring, in placements[i]))
                    return false;
            }

            return true;
        }

        public static bool TryAuthor(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement)
        {
            if (authoring == null)
                throw new System.ArgumentNullException(nameof(authoring));
            if (!placement.IsWellFormed)
                return false;

            switch (placement.Backend)
            {
                case DecorationRenderBackend.BoxAssembly:
                    return TryAuthorBoxAssembly(authoring, in placement);
                case DecorationRenderBackend.ThinSurface:
                    return TryAuthorThinSurfaceCompatibility(authoring, in placement);
                case DecorationRenderBackend.VoxelStamp:
                case DecorationRenderBackend.ProceduralMesh:
                default:
                    return false;
            }
        }

        private static bool TryAuthorBoxAssembly(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement)
        {
            switch (placement.Family)
            {
                case DecorationPropFamily.Bed:
                    AuthorBed(authoring, in placement);
                    return true;
                case DecorationPropFamily.Dresser:
                    AuthorDresser(authoring, in placement);
                    return true;
                case DecorationPropFamily.WallTorch:
                    AuthorWallTorch(authoring, in placement);
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryAuthorThinSurfaceCompatibility(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement)
        {
            switch (placement.Family)
            {
                case DecorationPropFamily.Rug:
                    authoring.Box(placement.Bounds.Min, placement.Bounds.Size, GameMaterialIds.Cloth);
                    return true;
                case DecorationPropFamily.Painting:
                    AuthorPainting(authoring, in placement);
                    return true;
                default:
                    return false;
            }
        }

        private static void AuthorBed(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            int baseHeight = math.min(3, size.y);
            int mattressHeight = math.min(3, math.max(1, size.y - baseHeight));

            authoring.Box(
                bounds.Min,
                new int3(size.x, baseHeight, size.z),
                GameMaterialIds.Wood);

            int insetX = math.min(2, math.max(0, (size.x - 2) / 4));
            int insetZ = math.min(2, math.max(0, (size.z - 2) / 4));
            int mattressWidth = math.max(1, size.x - insetX * 2);
            int mattressDepth = math.max(1, size.z - insetZ * 2);
            authoring.Box(
                new int3(bounds.Min.x + insetX, bounds.Min.y + baseHeight, bounds.Min.z + insetZ),
                new int3(mattressWidth, mattressHeight, mattressDepth),
                GameMaterialIds.Cloth);

            int headboardHeight = math.max(baseHeight + mattressHeight,
                math.min(size.y, 8 + (int)(placement.Variant & 3u)));
            AuthorWallBand(
                authoring,
                in bounds,
                placement.Facing,
                2,
                headboardHeight,
                GameMaterialIds.Wood);

            if (size.y >= 8)
                AuthorBedPosts(authoring, in bounds, math.min(size.y, headboardHeight + 2));
        }

        private static void AuthorBedPosts(
            IStructureAuthoringSession authoring,
            in DecorationBounds bounds,
            int height)
        {
            int3 size = bounds.Size;
            int post = math.max(1, math.min(2, math.min(size.x, size.z) / 4));
            int maxX = bounds.MaxExclusive.x - post;
            int maxZ = bounds.MaxExclusive.z - post;
            authoring.Box(new int3(bounds.Min.x, bounds.Min.y, bounds.Min.z), new int3(post, height, post), GameMaterialIds.Wood);
            authoring.Box(new int3(maxX, bounds.Min.y, bounds.Min.z), new int3(post, height, post), GameMaterialIds.Wood);
            authoring.Box(new int3(bounds.Min.x, bounds.Min.y, maxZ), new int3(post, height, post), GameMaterialIds.Wood);
            authoring.Box(new int3(maxX, bounds.Min.y, maxZ), new int3(post, height, post), GameMaterialIds.Wood);
        }

        private static void AuthorDresser(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            authoring.Box(bounds.Min, size, GameMaterialIds.Wood);

            int trimHeight = math.min(2, size.y);
            authoring.Box(
                new int3(bounds.Min.x, bounds.MaxExclusive.y - trimHeight, bounds.Min.z),
                new int3(size.x, trimHeight, size.z),
                GameMaterialIds.Gold);

            int drawerCount = math.clamp(size.y / 6, 2, 4);
            for (int drawer = 0; drawer < drawerCount; drawer++)
            {
                int handleY = bounds.Min.y + 3 + drawer * math.max(3, (size.y - 4) / drawerCount);
                handleY = math.min(handleY, bounds.MaxExclusive.y - 2);
                AuthorFacingHandle(authoring, in bounds, placement.Facing, handleY);
            }
        }

        private static void AuthorFacingHandle(
            IStructureAuthoringSession authoring,
            in DecorationBounds bounds,
            int3 facing,
            int y)
        {
            int centerX = (bounds.Min.x + bounds.MaxExclusive.x) / 2;
            int centerZ = (bounds.Min.z + bounds.MaxExclusive.z) / 2;
            if (math.abs(facing.x) == 1)
            {
                int x = facing.x > 0 ? bounds.MaxExclusive.x - 1 : bounds.Min.x;
                authoring.Box(new int3(x, y, centerZ - 2), new int3(1, 2, 4), GameMaterialIds.Gold);
            }
            else
            {
                int z = facing.z > 0 ? bounds.MaxExclusive.z - 1 : bounds.Min.z;
                authoring.Box(new int3(centerX - 2, y, z), new int3(4, 2, 1), GameMaterialIds.Gold);
            }
        }

        private static void AuthorPainting(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            authoring.Box(bounds.Min, size, GameMaterialIds.Gold);

            if (math.abs(placement.Facing.x) == 1)
            {
                int innerHeight = size.y - 2;
                int innerWidth = size.z - 2;
                if (innerHeight > 0 && innerWidth > 0)
                {
                    authoring.Box(
                        new int3(bounds.Min.x, bounds.Min.y + 1, bounds.Min.z + 1),
                        new int3(size.x, innerHeight, innerWidth),
                        GameMaterialIds.Cloth);
                }
            }
            else
            {
                int innerHeight = size.y - 2;
                int innerWidth = size.x - 2;
                if (innerHeight > 0 && innerWidth > 0)
                {
                    authoring.Box(
                        new int3(bounds.Min.x + 1, bounds.Min.y + 1, bounds.Min.z),
                        new int3(innerWidth, innerHeight, size.z),
                        GameMaterialIds.Cloth);
                }
            }
        }

        private static void AuthorWallTorch(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            int centerX = (bounds.Min.x + bounds.MaxExclusive.x) / 2;
            int centerZ = (bounds.Min.z + bounds.MaxExclusive.z) / 2;
            int stemHeight = math.max(2, size.y - 3);

            authoring.Box(
                new int3(centerX, bounds.Min.y, centerZ),
                new int3(1, stemHeight, 1),
                GameMaterialIds.Wood);

            int flameY = math.min(bounds.MaxExclusive.y - 2, bounds.Min.y + stemHeight);
            authoring.Box(
                new int3(centerX - 1, flameY, centerZ - 1),
                new int3(3, math.min(2, bounds.MaxExclusive.y - flameY), 3),
                GameMaterialIds.LitWindow);

            int bracketY = bounds.Min.y + math.max(1, stemHeight / 3);
            if (math.abs(placement.Facing.x) == 1)
            {
                int x = placement.Facing.x > 0 ? bounds.Min.x : bounds.MaxExclusive.x - 2;
                authoring.Box(new int3(x, bracketY, centerZ), new int3(2, 2, 1), GameMaterialIds.Gold);
            }
            else
            {
                int z = placement.Facing.z > 0 ? bounds.Min.z : bounds.MaxExclusive.z - 2;
                authoring.Box(new int3(centerX, bracketY, z), new int3(1, 2, 2), GameMaterialIds.Gold);
            }
        }

        private static void AuthorWallBand(
            IStructureAuthoringSession authoring,
            in DecorationBounds bounds,
            int3 facing,
            int thickness,
            int height,
            byte material)
        {
            height = math.min(height, bounds.Size.y);
            if (math.abs(facing.x) == 1)
            {
                int x = facing.x > 0 ? bounds.Min.x : bounds.MaxExclusive.x - thickness;
                authoring.Box(
                    new int3(x, bounds.Min.y, bounds.Min.z),
                    new int3(thickness, height, bounds.Size.z),
                    material);
            }
            else
            {
                int z = facing.z > 0 ? bounds.Min.z : bounds.MaxExclusive.z - thickness;
                authoring.Box(
                    new int3(bounds.Min.x, bounds.Min.y, z),
                    new int3(bounds.Size.x, height, thickness),
                    material);
            }
        }
    }
}
