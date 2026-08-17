using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public struct DecorationContentMeshRequest
    {
        public GeneratedPropId Id;
        public DecorationContentKind Kind;
        public DecorationBounds Bounds;
        public int3 Facing;
        public uint Variant;
    }

    public struct DecorationContentThinSurfaceRequest
    {
        public GeneratedPropId Id;
        public DecorationContentKind Kind;
        public DecorationBounds Bounds;
        public int3 Facing;
        public uint Variant;
    }

    /// <summary>
    /// Shared authoring grammar for catalog archetypes. A new archetype should normally select one
    /// of these shapes plus a recipe rather than adding another bespoke geometry emitter.
    /// </summary>
    public static class DecorationContentAuthoringEmitter
    {
        public static DecorationContentMeshRequest[] CollectMeshRequests(DecorationPlacement[] placements)
        {
            if (placements == null)
                return new DecorationContentMeshRequest[0];

            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (IsContentBackend(in placements[i], DecorationRenderBackend.ProceduralMesh))
                    count++;

            var requests = new DecorationContentMeshRequest[count];
            int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement placement = placements[i];
                if (!IsContentBackend(in placement, DecorationRenderBackend.ProceduralMesh))
                    continue;
                requests[output++] = new DecorationContentMeshRequest
                {
                    Id = placement.Id,
                    Kind = DecorationContentVariants.KindOf(placement.Variant),
                    Bounds = placement.Bounds,
                    Facing = placement.Facing,
                    Variant = placement.Variant,
                };
            }
            return requests;
        }

        public static DecorationContentThinSurfaceRequest[] CollectThinSurfaceRequests(DecorationPlacement[] placements)
        {
            if (placements == null)
                return new DecorationContentThinSurfaceRequest[0];

            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (IsContentBackend(in placements[i], DecorationRenderBackend.ThinSurface))
                    count++;

            var requests = new DecorationContentThinSurfaceRequest[count];
            int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement placement = placements[i];
                if (!IsContentBackend(in placement, DecorationRenderBackend.ThinSurface))
                    continue;
                requests[output++] = new DecorationContentThinSurfaceRequest
                {
                    Id = placement.Id,
                    Kind = DecorationContentVariants.KindOf(placement.Variant),
                    Bounds = placement.Bounds,
                    Facing = placement.Facing,
                    Variant = placement.Variant,
                };
            }
            return requests;
        }

        public static bool TryAuthorGeometry(
            IStructureAuthoringSession authoring,
            DecorationPlacement[] placements,
            in DecorationContext context)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!context.IsWellFormed || placements == null)
                return false;

            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement placement = placements[i];
                if (!placement.IsWellFormed || !DecorationContentVariants.IsContent(placement.Variant))
                    continue;
                if (placement.Backend == DecorationRenderBackend.ProceduralMesh ||
                    placement.Backend == DecorationRenderBackend.ThinSurface)
                    continue;

                DecorationContentKind kind = DecorationContentVariants.KindOf(placement.Variant);
                DecorationContentRecipe recipe = DecorationContentCatalog.Recipe(kind);
                if (!recipe.IsWellFormed)
                    return false;

                switch (recipe.Shape)
                {
                    case DecorationContentShape.WorkSurface:
                        AuthorWorkSurface(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.Machine:
                        AuthorMachine(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.Hearth:
                        AuthorHearth(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.WheelMachine:
                        AuthorWheelMachine(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.Tub:
                    case DecorationContentShape.Trough:
                        AuthorTrough(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.WallRack:
                        AuthorWallRack(authoring, in placement.Bounds, in placement.Facing, in profile);
                        break;
                    case DecorationContentShape.Counter:
                        AuthorCounter(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.Rack:
                        AuthorRack(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.Stack:
                        AuthorStack(authoring, in placement.Bounds, in profile, placement.Variant);
                        break;
                    case DecorationContentShape.Coffin:
                        AuthorCoffin(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.Pedestal:
                        AuthorPedestal(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.Monument:
                        AuthorMonument(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.Stall:
                        AuthorStall(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.Post:
                        AuthorPost(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.Restraint:
                        AuthorRestraint(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.Cage:
                        AuthorCage(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.Well:
                        AuthorWell(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.Fountain:
                        AuthorFountain(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.LampPost:
                        AuthorLampPost(authoring, in placement.Bounds, in profile);
                        break;
                    case DecorationContentShape.Cart:
                        AuthorCart(authoring, in placement.Bounds, in profile);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        private static bool IsContentBackend(in DecorationPlacement placement, DecorationRenderBackend backend) =>
            placement.IsWellFormed && placement.Backend == backend && DecorationContentVariants.IsContent(placement.Variant);

        private static void AuthorWorkSurface(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int top = math.max(2, b.Size.y / 4);
            int leg = math.max(2, math.min(b.Size.x, b.Size.z) / 5);
            a.Box(new int3(b.Min.x, b.MaxExclusive.y - top, b.Min.z), new int3(b.Size.x, top, b.Size.z), p.PrimaryMaterial);
            a.Box(b.Min, new int3(leg, b.Size.y - top, leg), p.PrimaryMaterial);
            a.Box(new int3(b.MaxExclusive.x - leg, b.Min.y, b.Min.z), new int3(leg, b.Size.y - top, leg), p.PrimaryMaterial);
            a.Box(new int3(b.Min.x, b.Min.y, b.MaxExclusive.z - leg), new int3(leg, b.Size.y - top, leg), p.PrimaryMaterial);
            a.Box(new int3(b.MaxExclusive.x - leg, b.Min.y, b.MaxExclusive.z - leg), new int3(leg, b.Size.y - top, leg), p.PrimaryMaterial);
        }

        private static void AuthorMachine(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int baseH = math.max(2, b.Size.y / 3);
            a.Box(b.Min, new int3(b.Size.x, baseH, b.Size.z), p.PrimaryMaterial);
            int3 innerSize = new int3(math.max(2, b.Size.x - 4), math.max(2, b.Size.y - baseH), math.max(2, b.Size.z - 4));
            a.Box(new int3(b.Min.x + 2, b.Min.y + baseH, b.Min.z + 2), innerSize, p.SoftMaterial);
            int handle = math.max(1, b.Size.x / 6);
            a.Box(new int3(b.MaxExclusive.x - handle, b.MaxExclusive.y - 2, b.Min.z), new int3(handle, 2, b.Size.z), p.AccentMaterial);
        }

        private static void AuthorHearth(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int wall = math.max(2, math.min(b.Size.x, b.Size.z) / 5);
            a.Box(b.Min, new int3(b.Size.x, math.max(3, b.Size.y / 4), b.Size.z), p.PrimaryMaterial);
            int3 fireMin = new int3(b.Min.x + wall, b.Min.y + 2, b.Min.z + wall);
            int3 fireSize = new int3(math.max(2, b.Size.x - wall * 2), math.max(2, b.Size.y / 3), math.max(2, b.Size.z - wall * 2));
            a.Box(fireMin, fireSize, p.EmitsLight ? p.EmissiveMaterial : GameMaterialIds.DarkStone);
            a.Box(new int3(b.Min.x, b.MaxExclusive.y - wall, b.Min.z), new int3(b.Size.x, wall, b.Size.z), p.AccentMaterial);
        }

        private static void AuthorWheelMachine(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int standH = math.max(3, b.Size.y / 3);
            a.Box(b.Min, new int3(b.Size.x, standH, b.Size.z), p.PrimaryMaterial);
            int cx = (b.Min.x + b.MaxExclusive.x) / 2;
            int cy = b.Min.y + standH + (b.Size.y - standH) / 2;
            int cz = (b.Min.z + b.MaxExclusive.z) / 2;
            int spoke = math.max(1, math.min(b.Size.x, b.Size.z) / 6);
            a.Box(new int3(b.Min.x, cy - spoke / 2, cz - spoke / 2), new int3(b.Size.x, spoke, spoke), p.AccentMaterial);
            a.Box(new int3(cx - spoke / 2, b.Min.y + standH, cz - spoke / 2), new int3(spoke, b.Size.y - standH, spoke), p.AccentMaterial);
        }

        private static void AuthorTrough(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int wall = math.max(1, math.min(3, math.min(b.Size.x, b.Size.z) / 4));
            a.Box(b.Min, new int3(b.Size.x, wall, b.Size.z), p.PrimaryMaterial);
            a.Box(new int3(b.Min.x, b.Min.y + wall, b.Min.z), new int3(b.Size.x, b.Size.y - wall, wall), p.PrimaryMaterial);
            a.Box(new int3(b.Min.x, b.Min.y + wall, b.MaxExclusive.z - wall), new int3(b.Size.x, b.Size.y - wall, wall), p.PrimaryMaterial);
            a.Box(new int3(b.Min.x, b.Min.y + wall, b.Min.z + wall), new int3(wall, b.Size.y - wall, math.max(1, b.Size.z - wall * 2)), p.PrimaryMaterial);
            a.Box(new int3(b.MaxExclusive.x - wall, b.Min.y + wall, b.Min.z + wall), new int3(wall, b.Size.y - wall, math.max(1, b.Size.z - wall * 2)), p.PrimaryMaterial);
        }

        private static void AuthorWallRack(
            IStructureAuthoringSession a, in DecorationBounds b, in int3 facing, in DecorationPresentationProfile p)
        {
            int rail = math.max(1, math.min(2, math.min(b.Size.x, b.Size.z)));
            int midY = b.Min.y + b.Size.y / 2;
            a.Box(new int3(b.Min.x, midY, b.Min.z), new int3(b.Size.x, rail, b.Size.z), p.PrimaryMaterial);

            int count = math.clamp(math.max(b.Size.x, b.Size.z) / 5, 2, 6);
            for (int i = 0; i < count; i++)
            {
                if (math.abs(facing.x) == 1)
                {
                    int z = math.min(b.MaxExclusive.z - 1, b.Min.z + 1 + i * math.max(1, b.Size.z / count));
                    a.Box(new int3(b.Min.x, b.Min.y + 1, z), new int3(b.Size.x, math.max(2, b.Size.y - 2), 1), p.AccentMaterial);
                }
                else
                {
                    int x = math.min(b.MaxExclusive.x - 1, b.Min.x + 1 + i * math.max(1, b.Size.x / count));
                    a.Box(new int3(x, b.Min.y + 1, b.Min.z), new int3(1, math.max(2, b.Size.y - 2), b.Size.z), p.AccentMaterial);
                }
            }
        }

        private static void AuthorCounter(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int top = math.max(2, b.Size.y / 5);
            int inset = math.max(1, math.min(3, math.min(b.Size.x, b.Size.z) / 5));
            a.Box(new int3(b.Min.x + inset, b.Min.y, b.Min.z + inset),
                new int3(math.max(1, b.Size.x - inset * 2), b.Size.y - top, math.max(1, b.Size.z - inset * 2)), p.PrimaryMaterial);
            a.Box(new int3(b.Min.x, b.MaxExclusive.y - top, b.Min.z), new int3(b.Size.x, top, b.Size.z), p.AccentMaterial);
        }

        private static void AuthorRack(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int post = math.max(1, math.min(2, math.min(b.Size.x, b.Size.z) / 4));
            a.Box(b.Min, new int3(post, b.Size.y, post), p.PrimaryMaterial);
            a.Box(new int3(b.MaxExclusive.x - post, b.Min.y, b.Min.z), new int3(post, b.Size.y, post), p.PrimaryMaterial);
            a.Box(new int3(b.Min.x, b.Min.y, b.MaxExclusive.z - post), new int3(post, b.Size.y, post), p.PrimaryMaterial);
            a.Box(new int3(b.MaxExclusive.x - post, b.Min.y, b.MaxExclusive.z - post), new int3(post, b.Size.y, post), p.PrimaryMaterial);
            int shelfCount = math.clamp(b.Size.y / 6, 2, 4);
            for (int i = 1; i <= shelfCount; i++)
            {
                int y = b.Min.y + i * b.Size.y / (shelfCount + 1);
                a.Box(new int3(b.Min.x, y, b.Min.z), new int3(b.Size.x, post, b.Size.z), p.AccentMaterial);
            }
        }

        private static void AuthorStack(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p, uint variant)
        {
            int layers = math.clamp(b.Size.y / 3, 2, 4);
            int layerH = math.max(1, b.Size.y / layers);
            for (int i = 0; i < layers; i++)
            {
                int inset = (i + (int)(variant & 1u)) % 2;
                int3 min = new int3(b.Min.x + inset, b.Min.y + i * layerH, b.Min.z + inset);
                int height = i == layers - 1 ? b.MaxExclusive.y - min.y : layerH;
                a.Box(min, new int3(math.max(1, b.Size.x - inset * 2), height, math.max(1, b.Size.z - inset * 2)),
                    i % 2 == 0 ? p.PrimaryMaterial : p.SoftMaterial);
            }
        }

        private static void AuthorCoffin(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int lid = math.max(2, b.Size.y / 4);
            a.Box(b.Min, new int3(b.Size.x, b.Size.y - lid, b.Size.z), p.PrimaryMaterial);
            a.Box(new int3(b.Min.x - 0, b.MaxExclusive.y - lid, b.Min.z), new int3(b.Size.x, lid, b.Size.z), p.AccentMaterial);
            if (p.Ornamentation >= 4)
            {
                int cx = (b.Min.x + b.MaxExclusive.x) / 2;
                a.Box(new int3(cx, b.MaxExclusive.y - lid, b.Min.z + 2), new int3(1, lid, math.max(1, b.Size.z - 4)), p.SoftMaterial);
            }
        }

        private static void AuthorPedestal(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int baseH = math.max(2, b.Size.y / 4);
            int topH = math.max(2, b.Size.y / 4);
            int insetX = math.max(1, b.Size.x / 5);
            int insetZ = math.max(1, b.Size.z / 5);
            a.Box(b.Min, new int3(b.Size.x, baseH, b.Size.z), p.PrimaryMaterial);
            a.Box(new int3(b.Min.x + insetX, b.Min.y + baseH, b.Min.z + insetZ),
                new int3(math.max(1, b.Size.x - insetX * 2), math.max(1, b.Size.y - baseH - topH), math.max(1, b.Size.z - insetZ * 2)), p.PrimaryMaterial);
            a.Box(new int3(b.Min.x, b.MaxExclusive.y - topH, b.Min.z), new int3(b.Size.x, topH, b.Size.z), p.AccentMaterial);
        }

        private static void AuthorMonument(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int baseH = math.max(2, b.Size.y / 6);
            a.Box(b.Min, new int3(b.Size.x, baseH, b.Size.z), p.AccentMaterial);
            int inset = math.max(1, math.min(3, b.Size.x / 5));
            a.Box(new int3(b.Min.x + inset, b.Min.y + baseH, b.Min.z + inset),
                new int3(math.max(1, b.Size.x - inset * 2), b.Size.y - baseH, math.max(1, b.Size.z - inset * 2)), p.PrimaryMaterial);
        }

        private static void AuthorStall(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int post = math.max(2, math.min(3, math.min(b.Size.x, b.Size.z) / 5));
            int counterY = b.Min.y + math.max(6, b.Size.y / 2);
            a.Box(new int3(b.Min.x, b.Min.y, b.Min.z), new int3(post, b.Size.y, post), p.PrimaryMaterial);
            a.Box(new int3(b.MaxExclusive.x - post, b.Min.y, b.Min.z), new int3(post, b.Size.y, post), p.PrimaryMaterial);
            a.Box(new int3(b.Min.x, b.Min.y, b.MaxExclusive.z - post), new int3(post, b.Size.y, post), p.PrimaryMaterial);
            a.Box(new int3(b.MaxExclusive.x - post, b.Min.y, b.MaxExclusive.z - post), new int3(post, b.Size.y, post), p.PrimaryMaterial);
            a.Box(new int3(b.Min.x, counterY, b.Min.z), new int3(b.Size.x, post, b.Size.z), p.AccentMaterial);
            a.Box(new int3(b.Min.x, b.MaxExclusive.y - post, b.Min.z), new int3(b.Size.x, post, b.Size.z), p.SoftMaterial);
        }

        private static void AuthorPost(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int cx = (b.Min.x + b.MaxExclusive.x) / 2;
            int cz = (b.Min.z + b.MaxExclusive.z) / 2;
            int post = math.max(2, math.min(b.Size.x, b.Size.z) / 3);
            a.Box(new int3(cx - post / 2, b.Min.y, cz - post / 2), new int3(post, b.Size.y, post), p.PrimaryMaterial);
            int railY = b.Min.y + b.Size.y * 2 / 3;
            a.Box(new int3(b.Min.x, railY, cz - post / 2), new int3(b.Size.x, post, post), p.AccentMaterial);
        }

        private static void AuthorRestraint(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int plank = math.max(2, b.Size.y / 5);
            a.Box(new int3(b.Min.x, b.Min.y + plank, b.Min.z), new int3(b.Size.x, plank, b.Size.z), p.PrimaryMaterial);
            a.Box(new int3(b.Min.x, b.Min.y, b.Min.z), new int3(plank, b.Size.y, plank), p.PrimaryMaterial);
            a.Box(new int3(b.MaxExclusive.x - plank, b.Min.y, b.Min.z), new int3(plank, b.Size.y, plank), p.PrimaryMaterial);
            a.Box(new int3(b.Min.x, b.MaxExclusive.y - plank, b.Min.z), new int3(b.Size.x, plank, b.Size.z), p.AccentMaterial);
        }

        private static void AuthorCage(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int bar = math.max(1, math.min(2, math.min(b.Size.x, b.Size.z) / 6));
            int stepX = math.max(4, b.Size.x / 4);
            int stepZ = math.max(4, b.Size.z / 4);
            for (int x = b.Min.x; x < b.MaxExclusive.x; x += stepX)
            {
                a.Box(new int3(x, b.Min.y, b.Min.z), new int3(bar, b.Size.y, bar), p.AccentMaterial);
                a.Box(new int3(x, b.Min.y, b.MaxExclusive.z - bar), new int3(bar, b.Size.y, bar), p.AccentMaterial);
            }
            for (int z = b.Min.z; z < b.MaxExclusive.z; z += stepZ)
            {
                a.Box(new int3(b.Min.x, b.Min.y, z), new int3(bar, b.Size.y, bar), p.AccentMaterial);
                a.Box(new int3(b.MaxExclusive.x - bar, b.Min.y, z), new int3(bar, b.Size.y, bar), p.AccentMaterial);
            }
            a.Box(new int3(b.Min.x, b.MaxExclusive.y - bar, b.Min.z), new int3(b.Size.x, bar, b.Size.z), p.AccentMaterial);
        }

        private static void AuthorWell(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int radius = math.max(2, math.min(b.Size.x, b.Size.z) / 2);
            int wallH = math.max(4, b.Size.y / 3);
            int cx = (b.Min.x + b.MaxExclusive.x) / 2;
            int cz = (b.Min.z + b.MaxExclusive.z) / 2;
            a.Cylinder(cx, b.Min.y, cz, radius, wallH, p.PrimaryMaterial);
            int post = math.max(2, radius / 4);
            a.Box(new int3(b.Min.x, b.Min.y + wallH, cz - post / 2), new int3(post, b.Size.y - wallH, post), p.PrimaryMaterial);
            a.Box(new int3(b.MaxExclusive.x - post, b.Min.y + wallH, cz - post / 2), new int3(post, b.Size.y - wallH, post), p.PrimaryMaterial);
            a.Box(new int3(b.Min.x, b.MaxExclusive.y - post, cz - post / 2), new int3(b.Size.x, post, post), p.AccentMaterial);
        }

        private static void AuthorFountain(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int radius = math.max(3, math.min(b.Size.x, b.Size.z) / 2);
            int cx = (b.Min.x + b.MaxExclusive.x) / 2;
            int cz = (b.Min.z + b.MaxExclusive.z) / 2;
            int basinH = math.max(3, b.Size.y / 4);
            a.Cylinder(cx, b.Min.y, cz, radius, basinH, p.PrimaryMaterial);
            int columnRadius = math.max(2, radius / 4);
            a.Cylinder(cx, b.Min.y + basinH, cz, columnRadius, math.max(3, b.Size.y - basinH), p.AccentMaterial);
            if (p.DamageLevel <= 2)
                a.Cylinder(cx, b.Min.y + basinH, cz, math.max(1, columnRadius - 1), math.max(2, b.Size.y / 3), GameMaterialIds.Water);
        }

        private static void AuthorLampPost(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int cx = (b.Min.x + b.MaxExclusive.x) / 2;
            int cz = (b.Min.z + b.MaxExclusive.z) / 2;
            int post = math.max(1, math.min(2, math.min(b.Size.x, b.Size.z) / 2));
            int lampH = math.max(4, b.Size.y / 4);
            a.Box(new int3(cx - post / 2, b.Min.y, cz - post / 2), new int3(post, b.Size.y - lampH, post), p.AccentMaterial);
            int lampSize = math.max(3, math.min(b.Size.x, b.Size.z));
            a.Box(new int3(cx - lampSize / 2, b.MaxExclusive.y - lampH, cz - lampSize / 2),
                new int3(lampSize, lampH, lampSize), p.EmitsLight ? p.EmissiveMaterial : GameMaterialIds.DarkStone);
        }

        private static void AuthorCart(
            IStructureAuthoringSession a, in DecorationBounds b, in DecorationPresentationProfile p)
        {
            int wheelH = math.max(3, b.Size.y / 3);
            int bodyY = b.Min.y + wheelH;
            a.Box(new int3(b.Min.x + 2, bodyY, b.Min.z + 2),
                new int3(math.max(2, b.Size.x - 4), math.max(2, b.Size.y - wheelH), math.max(2, b.Size.z - 4)), p.PrimaryMaterial);
            int wheel = math.max(2, math.min(4, b.Size.x / 4));
            a.Box(new int3(b.Min.x, b.Min.y, b.Min.z + 3), new int3(wheel, wheelH, wheel), p.AccentMaterial);
            a.Box(new int3(b.MaxExclusive.x - wheel, b.Min.y, b.Min.z + 3), new int3(wheel, wheelH, wheel), p.AccentMaterial);
            a.Box(new int3(b.Min.x, b.Min.y, b.MaxExclusive.z - wheel - 3), new int3(wheel, wheelH, wheel), p.AccentMaterial);
            a.Box(new int3(b.MaxExclusive.x - wheel, b.Min.y, b.MaxExclusive.z - wheel - 3), new int3(wheel, wheelH, wheel), p.AccentMaterial);
        }
    }
}
