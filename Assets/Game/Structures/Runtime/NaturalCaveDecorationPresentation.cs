using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public struct NaturalCaveMeshRequest
    {
        public GeneratedPropId Id;
        public NaturalCaveDecorationKind Kind;
        public DecorationBounds Bounds;
        public uint Variant;
    }

    public struct NaturalCaveThinSurfaceRequest
    {
        public GeneratedPropId Id;
        public DecorationBounds Bounds;
        public uint Variant;
    }

    public static class NaturalCaveDecorationPresentation
    {
        public static NaturalCaveMeshRequest[] CollectMeshRequests(
            NaturalCaveDecorationInstance[] instances)
        {
            if (instances == null) return new NaturalCaveMeshRequest[0];
            int count = 0;
            for (int i = 0; i < instances.Length; i++)
                if (instances[i].IsWellFormed && instances[i].Backend == DecorationRenderBackend.ProceduralMesh)
                    count++;

            var requests = new NaturalCaveMeshRequest[count];
            int output = 0;
            for (int i = 0; i < instances.Length; i++)
            {
                NaturalCaveDecorationInstance instance = instances[i];
                if (!instance.IsWellFormed || instance.Backend != DecorationRenderBackend.ProceduralMesh)
                    continue;
                requests[output++] = new NaturalCaveMeshRequest
                {
                    Id = instance.Id,
                    Kind = instance.Kind,
                    Bounds = instance.Bounds,
                    Variant = instance.Variant,
                };
            }
            return requests;
        }

        public static NaturalCaveThinSurfaceRequest[] CollectThinSurfaces(
            NaturalCaveDecorationInstance[] instances)
        {
            if (instances == null) return new NaturalCaveThinSurfaceRequest[0];
            int count = 0;
            for (int i = 0; i < instances.Length; i++)
                if (instances[i].IsWellFormed && instances[i].Backend == DecorationRenderBackend.ThinSurface)
                    count++;

            var requests = new NaturalCaveThinSurfaceRequest[count];
            int output = 0;
            for (int i = 0; i < instances.Length; i++)
            {
                NaturalCaveDecorationInstance instance = instances[i];
                if (!instance.IsWellFormed || instance.Backend != DecorationRenderBackend.ThinSurface)
                    continue;
                requests[output++] = new NaturalCaveThinSurfaceRequest
                {
                    Id = instance.Id,
                    Bounds = instance.Bounds,
                    Variant = instance.Variant,
                };
            }
            return requests;
        }

        public static bool TryAuthorVoxelStamps(
            IStructureAuthoringSession authoring,
            NaturalCaveDecorationInstance[] instances)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (instances == null) return false;

            for (int i = 0; i < instances.Length; i++)
            {
                NaturalCaveDecorationInstance instance = instances[i];
                if (!instance.IsWellFormed) return false;
                if (instance.Backend != DecorationRenderBackend.VoxelStamp) continue;

                DecorationBounds b = instance.Bounds;
                int cx = (b.Min.x + b.MaxExclusive.x) / 2;
                int cz = (b.Min.z + b.MaxExclusive.z) / 2;
                int radius = math.max(1, math.min(b.Size.x, b.Size.z) / 2);
                switch (instance.Kind)
                {
                    case NaturalCaveDecorationKind.Stone:
                        authoring.Cylinder(cx, b.Min.y, cz, radius, b.Size.y, GameMaterialIds.DarkStone);
                        break;
                    case NaturalCaveDecorationKind.Crystal:
                        authoring.Cone(cx, b.Min.y, cz, radius, b.Size.y, GameMaterialIds.Crystal);
                        break;
                    case NaturalCaveDecorationKind.Stalagmite:
                        authoring.Cone(cx, b.Min.y, cz, radius, b.Size.y, GameMaterialIds.DarkStone);
                        break;
                    case NaturalCaveDecorationKind.Stalactite:
                        authoring.HangingCone(cx, b.MaxExclusive.y, cz, radius, b.Size.y, GameMaterialIds.DarkStone);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }
    }
}
