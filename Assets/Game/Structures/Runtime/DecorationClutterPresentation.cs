using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public struct DecorationClutterMeshRequest
    {
        public GeneratedPropId Id;
        public GeneratedPropId ParentId;
        public DecorationClutterKind Kind;
        public DecorationBounds Bounds;
        public uint Variant;
    }

    public static class DecorationClutterPresentation
    {
        public static DecorationClutterMeshRequest[] CollectMeshRequests(
            DecorationClutterInstance[] items)
        {
            if (items == null)
                return new DecorationClutterMeshRequest[0];

            int count = 0;
            for (int i = 0; i < items.Length; i++)
                if (items[i].IsWellFormed && items[i].Backend == DecorationRenderBackend.ProceduralMesh)
                    count++;

            var requests = new DecorationClutterMeshRequest[count];
            int output = 0;
            for (int i = 0; i < items.Length; i++)
            {
                DecorationClutterInstance item = items[i];
                if (!item.IsWellFormed || item.Backend != DecorationRenderBackend.ProceduralMesh)
                    continue;
                requests[output++] = new DecorationClutterMeshRequest
                {
                    Id = item.Id,
                    ParentId = item.ParentId,
                    Kind = item.Kind,
                    Bounds = item.Bounds,
                    Variant = item.Variant,
                };
            }
            return requests;
        }

        /// <summary>Authors only the deliberately box-based tiny clutter; mesh items remain batched requests.</summary>
        public static bool TryAuthorBoxAssemblies(
            IStructureAuthoringSession authoring,
            DecorationClutterInstance[] items,
            in DecorationContext context)
        {
            if (authoring == null)
                throw new System.ArgumentNullException(nameof(authoring));
            if (items == null || !context.IsWellFormed)
                return false;

            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);
            for (int i = 0; i < items.Length; i++)
            {
                DecorationClutterInstance item = items[i];
                if (!item.IsWellFormed)
                    return false;
                if (item.Backend != DecorationRenderBackend.BoxAssembly)
                    continue;

                switch (item.Kind)
                {
                    case DecorationClutterKind.Book:
                        AuthorBook(authoring, in item, in profile);
                        break;
                    case DecorationClutterKind.Container:
                        AuthorContainer(authoring, in item, in profile);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        private static void AuthorBook(
            IStructureAuthoringSession authoring,
            in DecorationClutterInstance item,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds b = item.Bounds;
            authoring.Box(b.Min, b.Size, profile.SoftMaterial);
            if (b.Size.x >= 4)
                authoring.Box(new int3(b.Min.x, b.MaxExclusive.y - 1, b.Min.z),
                    new int3(b.Size.x, 1, b.Size.z), profile.AccentMaterial);
        }

        private static void AuthorContainer(
            IStructureAuthoringSession authoring,
            in DecorationClutterInstance item,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds b = item.Bounds;
            authoring.Box(b.Min, b.Size, profile.PrimaryMaterial);
            authoring.Box(new int3(b.Min.x, b.MaxExclusive.y - 1, b.Min.z),
                new int3(b.Size.x, 1, b.Size.z), profile.AccentMaterial);
        }
    }
}
