using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public struct MineCaveMeshRequest
    {
        public GeneratedPropId Id;
        public MineCaveDecorationKind Kind;
        public DecorationBounds Bounds;
        public uint Variant;
    }

    public struct MineCaveLightRequest
    {
        public GeneratedPropId Id;
        public float3 PositionVoxels;
        public uint Variant;
    }

    public static class MineCaveDecorationPresentation
    {
        public static MineCaveMeshRequest[] CollectMeshRequests(MineCaveDecorationInstance[] instances)
        {
            if (instances == null) return new MineCaveMeshRequest[0];
            int count = 0;
            for (int i = 0; i < instances.Length; i++)
                if (instances[i].IsWellFormed && instances[i].Backend == DecorationRenderBackend.ProceduralMesh)
                    count++;

            var requests = new MineCaveMeshRequest[count];
            int output = 0;
            for (int i = 0; i < instances.Length; i++)
            {
                MineCaveDecorationInstance instance = instances[i];
                if (!instance.IsWellFormed || instance.Backend != DecorationRenderBackend.ProceduralMesh)
                    continue;
                requests[output++] = new MineCaveMeshRequest
                {
                    Id = instance.Id,
                    Kind = instance.Kind,
                    Bounds = instance.Bounds,
                    Variant = instance.Variant,
                };
            }
            return requests;
        }

        public static MineCaveLightRequest[] CollectLightRequests(
            MineCaveDecorationInstance[] instances,
            in DecorationContext context)
        {
            if (instances == null || !context.IsWellFormed ||
                !DecorationContextProfiles.ResolvePresentation(in context).EmitsLight)
                return new MineCaveLightRequest[0];

            int count = 0;
            for (int i = 0; i < instances.Length; i++)
                if (instances[i].IsWellFormed && instances[i].Kind == MineCaveDecorationKind.Lantern)
                    count++;

            var lights = new MineCaveLightRequest[count];
            int output = 0;
            for (int i = 0; i < instances.Length; i++)
            {
                MineCaveDecorationInstance instance = instances[i];
                if (!instance.IsWellFormed || instance.Kind != MineCaveDecorationKind.Lantern)
                    continue;
                lights[output++] = new MineCaveLightRequest
                {
                    Id = instance.Id,
                    PositionVoxels = ((float3)instance.Bounds.Min + (float3)instance.Bounds.MaxExclusive) * 0.5f,
                    Variant = instance.Variant,
                };
            }
            return lights;
        }

        public static bool TryAuthorGeometry(
            IStructureAuthoringSession authoring,
            MineCaveDecorationInstance[] instances,
            in DecorationContext context)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (instances == null || !context.IsWellFormed) return false;
            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);

            for (int i = 0; i < instances.Length; i++)
            {
                MineCaveDecorationInstance instance = instances[i];
                if (!instance.IsWellFormed) return false;
                if (instance.Backend == DecorationRenderBackend.ProceduralMesh) continue;

                switch (instance.Kind)
                {
                    case MineCaveDecorationKind.SupportBeam:
                        AuthorSupport(authoring, in instance);
                        break;
                    case MineCaveDecorationKind.Rail:
                        AuthorRail(authoring, in instance);
                        break;
                    case MineCaveDecorationKind.MineCart:
                        AuthorCart(authoring, in instance);
                        break;
                    case MineCaveDecorationKind.Lantern:
                        AuthorLantern(authoring, in instance, in profile);
                        break;
                    case MineCaveDecorationKind.Crate:
                        AuthorCrate(authoring, in instance);
                        break;
                    case MineCaveDecorationKind.ToolRack:
                        AuthorToolRack(authoring, in instance);
                        break;
                    case MineCaveDecorationKind.Ladder:
                        AuthorLadder(authoring, in instance);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        private static void AuthorSupport(IStructureAuthoringSession a, in MineCaveDecorationInstance instance)
        {
            DecorationBounds b = instance.Bounds;
            int post = math.min(3, math.max(2, math.min(b.Size.x, b.Size.z) / 5));
            if (math.abs(instance.Facing.x) == 1)
            {
                a.Box(new int3(b.Min.x, b.Min.y, b.Min.z), new int3(b.Size.x, b.Size.y, post), GameMaterialIds.Wood);
                a.Box(new int3(b.Min.x, b.Min.y, b.MaxExclusive.z - post), new int3(b.Size.x, b.Size.y, post), GameMaterialIds.Wood);
                a.Box(new int3(b.Min.x, b.MaxExclusive.y - post, b.Min.z), new int3(b.Size.x, post, b.Size.z), GameMaterialIds.Wood);
            }
            else
            {
                a.Box(new int3(b.Min.x, b.Min.y, b.Min.z), new int3(post, b.Size.y, b.Size.z), GameMaterialIds.Wood);
                a.Box(new int3(b.MaxExclusive.x - post, b.Min.y, b.Min.z), new int3(post, b.Size.y, b.Size.z), GameMaterialIds.Wood);
                a.Box(new int3(b.Min.x, b.MaxExclusive.y - post, b.Min.z), new int3(b.Size.x, post, b.Size.z), GameMaterialIds.Wood);
            }
        }

        private static void AuthorRail(IStructureAuthoringSession a, in MineCaveDecorationInstance instance)
        {
            DecorationBounds b = instance.Bounds;
            if (math.abs(instance.Facing.x) == 1)
            {
                int zA = b.Min.z + 1;
                int zB = b.MaxExclusive.z - 2;
                a.Box(new int3(b.Min.x, b.Min.y, zA), new int3(b.Size.x, 1, 1), GameMaterialIds.Gold);
                a.Box(new int3(b.Min.x, b.Min.y, zB), new int3(b.Size.x, 1, 1), GameMaterialIds.Gold);
                for (int x = b.Min.x; x < b.MaxExclusive.x; x += 5)
                    a.Box(new int3(x, b.Min.y, b.Min.z), new int3(1, 1, b.Size.z), GameMaterialIds.Wood);
            }
            else
            {
                int xA = b.Min.x + 1;
                int xB = b.MaxExclusive.x - 2;
                a.Box(new int3(xA, b.Min.y, b.Min.z), new int3(1, 1, b.Size.z), GameMaterialIds.Gold);
                a.Box(new int3(xB, b.Min.y, b.Min.z), new int3(1, 1, b.Size.z), GameMaterialIds.Gold);
                for (int z = b.Min.z; z < b.MaxExclusive.z; z += 5)
                    a.Box(new int3(b.Min.x, b.Min.y, z), new int3(b.Size.x, 1, 1), GameMaterialIds.Wood);
            }
        }

        private static void AuthorCart(IStructureAuthoringSession a, in MineCaveDecorationInstance instance)
        {
            DecorationBounds b = instance.Bounds;
            int bodyHeight = math.max(3, b.Size.y - 2);
            a.Box(new int3(b.Min.x, b.Min.y + 2, b.Min.z),
                new int3(b.Size.x, bodyHeight, b.Size.z), GameMaterialIds.Wood);
            if (math.abs(instance.Facing.x) == 1)
            {
                a.Box(new int3(b.Min.x + 2, b.Min.y, b.Min.z), new int3(2, 2, b.Size.z), GameMaterialIds.Gold);
                a.Box(new int3(b.MaxExclusive.x - 4, b.Min.y, b.Min.z), new int3(2, 2, b.Size.z), GameMaterialIds.Gold);
            }
            else
            {
                a.Box(new int3(b.Min.x, b.Min.y, b.Min.z + 2), new int3(b.Size.x, 2, 2), GameMaterialIds.Gold);
                a.Box(new int3(b.Min.x, b.Min.y, b.MaxExclusive.z - 4), new int3(b.Size.x, 2, 2), GameMaterialIds.Gold);
            }
        }

        private static void AuthorLantern(IStructureAuthoringSession a, in MineCaveDecorationInstance instance,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds b = instance.Bounds;
            int cx = (b.Min.x + b.MaxExclusive.x) / 2;
            int cz = (b.Min.z + b.MaxExclusive.z) / 2;
            byte glow = profile.EmitsLight ? profile.EmissiveMaterial : GameMaterialIds.DarkStone;
            a.Box(new int3(cx - 1, b.Min.y + 1, cz - 1),
                new int3(3, math.max(2, b.Size.y - 2), 3), glow);
            a.Box(new int3(cx, b.Min.y, cz), new int3(1, b.Size.y, 1), GameMaterialIds.Gold);
        }

        private static void AuthorCrate(IStructureAuthoringSession a, in MineCaveDecorationInstance instance)
        {
            DecorationBounds b = instance.Bounds;
            a.Box(b.Min, b.Size, GameMaterialIds.Wood);
            a.Box(new int3(b.Min.x, b.MaxExclusive.y - 2, b.Min.z),
                new int3(b.Size.x, 2, b.Size.z), GameMaterialIds.Gold);
        }

        private static void AuthorToolRack(IStructureAuthoringSession a, in MineCaveDecorationInstance instance)
        {
            DecorationBounds b = instance.Bounds;
            int midY = b.Min.y + b.Size.y / 2;
            a.Box(new int3(b.Min.x, midY, b.Min.z), new int3(b.Size.x, 2, b.Size.z), GameMaterialIds.Wood);
            int count = math.clamp(b.Size.x / 5, 2, 5);
            for (int i = 0; i < count; i++)
            {
                int x = math.min(b.MaxExclusive.x - 1, b.Min.x + 2 + i * math.max(2, (b.Size.x - 4) / count));
                a.Box(new int3(x, b.Min.y + 1, b.Min.z),
                    new int3(1, math.max(3, b.Size.y - 2), b.Size.z), GameMaterialIds.Gold);
            }
        }

        private static void AuthorLadder(IStructureAuthoringSession a, in MineCaveDecorationInstance instance)
        {
            DecorationBounds b = instance.Bounds;
            if (math.abs(instance.Facing.x) == 1)
            {
                a.Box(new int3(b.Min.x, b.Min.y, b.Min.z + 1), new int3(b.Size.x, b.Size.y, 1), GameMaterialIds.Wood);
                a.Box(new int3(b.Min.x, b.Min.y, b.MaxExclusive.z - 2), new int3(b.Size.x, b.Size.y, 1), GameMaterialIds.Wood);
                for (int y = b.Min.y + 2; y < b.MaxExclusive.y; y += 4)
                    a.Box(new int3(b.Min.x, y, b.Min.z + 1), new int3(b.Size.x, 1, math.max(1, b.Size.z - 2)), GameMaterialIds.Wood);
            }
            else
            {
                a.Box(new int3(b.Min.x + 1, b.Min.y, b.Min.z), new int3(1, b.Size.y, b.Size.z), GameMaterialIds.Wood);
                a.Box(new int3(b.MaxExclusive.x - 2, b.Min.y, b.Min.z), new int3(1, b.Size.y, b.Size.z), GameMaterialIds.Wood);
                for (int y = b.Min.y + 2; y < b.MaxExclusive.y; y += 4)
                    a.Box(new int3(b.Min.x + 1, y, b.Min.z), new int3(math.max(1, b.Size.x - 2), 1, b.Size.z), GameMaterialIds.Wood);
            }
        }
    }
}
