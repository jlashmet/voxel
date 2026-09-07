using System;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Adapter from the shared architecture contract to the existing production guild-house planner
    /// and authoring path. It owns no duplicate geometry or materials.
    /// </summary>
    public sealed class GuildHouseArchitectureProvider : IArchitectureProvider
    {
        public const string ProviderKey = "guild-house";
        private const int DefaultWidth = 128;
        private const int DefaultDepth = 128;

        private static readonly ArchitectureParameterSupport Supported =
            ArchitectureParameterSupport.Footprint |
            ArchitectureParameterSupport.RoomCount;

        private static readonly ArchitecturePresentationCapabilities Capabilities =
            ArchitecturePresentationCapabilities.ProductionVoxelOccupancy |
            ArchitecturePresentationCapabilities.ProductionMaterials |
            ArchitecturePresentationCapabilities.ProductionMaterialTextures |
            ArchitecturePresentationCapabilities.CollisionFromOccupancy |
            ArchitecturePresentationCapabilities.InteriorShell |
            ArchitecturePresentationCapabilities.TraversableOpenings;

        private readonly ArchitectureProviderDescriptor _descriptor;

        public GuildHouseArchitectureProvider()
        {
            GuildHouseDescriptor[] houses = GuildHouseCatalogQuery.Houses();
            var keys = new string[houses.Length];
            for (int i = 0; i < houses.Length; i++)
                keys[i] = houses[i].Key;

            _descriptor = new ArchitectureProviderDescriptor(
                ProviderKey,
                "Procedural Guild House",
                ArchitectureStructureClass.GuildHall,
                keys,
                Supported,
                Capabilities);
        }

        public ArchitectureProviderDescriptor Descriptor => _descriptor;

        public bool TryPlan(
            in ArchitectureGenerationRequest request,
            out ArchitectureGenerationResult result,
            out string error)
        {
            result = default;
            if (!TryBuild(in request, out GuildHousePrototype prototype, out error))
                return false;

            result = BuildResult(in request, in prototype);
            if (!result.IsWellFormed)
            {
                error = "Guild-house architecture result was not well formed.";
                result = default;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryAuthor(
            IStructureAuthoringSession authoring,
            in ArchitectureGenerationRequest request,
            out ArchitectureGenerationResult result,
            out string error)
        {
            result = default;
            if (authoring == null)
            {
                error = "A production structure-authoring session is required.";
                return false;
            }

            if (!TryBuild(in request, out GuildHousePrototype prototype, out error))
                return false;

            try
            {
                GuildHousePrototypeAuthoring.Author(authoring, in prototype);
            }
            catch (Exception ex)
            {
                error = $"Guild-house production authoring failed: {ex.Message}";
                return false;
            }

            if (authoring.BudgetExceeded)
            {
                error = "Guild-house production authoring exceeded its voxel budget.";
                return false;
            }

            result = BuildResult(in request, in prototype);
            error = result.IsWellFormed ? string.Empty : "Guild-house architecture result was not well formed.";
            return result.IsWellFormed;
        }

        private static bool TryBuild(
            in ArchitectureGenerationRequest request,
            out GuildHousePrototype prototype,
            out string error)
        {
            prototype = default;
            if (!request.IsWellFormed)
            {
                error = "Architecture request is not well formed.";
                return false;
            }
            if (!string.Equals(request.ProviderKey, ProviderKey, StringComparison.Ordinal))
            {
                error = $"Provider '{request.ProviderKey}' is not '{ProviderKey}'.";
                return false;
            }
            if (!ArchitectureRegistry.TryResolveLegacyRegion(request.StyleProfileKey, out DecorationRegionTheme region))
            {
                error = $"Style profile '{request.StyleProfileKey}' is not supported by the guild-house provider.";
                return false;
            }
            if (!TryResolveHouse(request.ArchetypeKey, out GuildHouseDescriptor house))
            {
                error = $"Unknown guild-house archetype '{request.ArchetypeKey}'.";
                return false;
            }
            if (!TryValidateParameters(in request.Parameters, in house, out int width, out int depth, out int rooms, out error))
                return false;

            prototype = GuildHousePrototypeComposition.Build(
                house.Kind,
                region,
                request.Seed,
                request.StructureId,
                request.Origin,
                width,
                depth,
                rooms);
            if (!prototype.IsWellFormed)
            {
                error = "Production guild-house composition returned an invalid prototype.";
                prototype = default;
                return false;
            }

            return true;
        }

        private static bool TryValidateParameters(
            in ArchitectureGenerationParameters parameters,
            in GuildHouseDescriptor house,
            out int width,
            out int depth,
            out int rooms,
            out string error)
        {
            width = parameters.Width == 0 ? DefaultWidth : parameters.Width;
            depth = parameters.Depth == 0 ? DefaultDepth : parameters.Depth;
            rooms = parameters.RoomCount == 0 ? house.PreferredRooms : parameters.RoomCount;

            int minimumWidth = house.Kind == GuildHouseKind.Druids ? 84 : 64;
            int minimumDepth = house.Kind == GuildHouseKind.Druids ? 72 : 64;
            if (width < minimumWidth || depth < minimumDepth)
            {
                error = $"{house.DisplayName} requires at least {minimumWidth}x{minimumDepth} voxels.";
                return false;
            }
            if (rooms < house.MinimumRooms)
            {
                error = $"{house.DisplayName} requires at least {house.MinimumRooms} rooms.";
                return false;
            }
            if (parameters.StoreyCount != 0 || parameters.FloorHeight != 0 ||
                parameters.RoofForm != ArchitectureRoofForm.ProviderDefault ||
                parameters.Foundation != ArchitectureFoundationForm.ProviderDefault ||
                parameters.Openings != ArchitectureOpeningPattern.ProviderDefault ||
                parameters.TrimLevel != 0 || parameters.DetailLevel != 0)
            {
                error = "Guild-house provider currently supports explicit footprint and room-count parameters only; other non-default parameters are rejected.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryResolveHouse(string key, out GuildHouseDescriptor house)
        {
            GuildHouseDescriptor[] houses = GuildHouseCatalogQuery.Houses();
            for (int i = 0; i < houses.Length; i++)
            {
                if (!string.Equals(houses[i].Key, key, StringComparison.Ordinal))
                    continue;
                house = houses[i];
                return true;
            }

            house = default;
            return false;
        }

        private static ArchitectureGenerationResult BuildResult(
            in ArchitectureGenerationRequest request,
            in GuildHousePrototype prototype)
        {
            GuildHouseSpatialPlan plan = prototype.SpatialPlan;
            int roofAllowance = plan.ShellStyle == GuildHouseShellStyle.Tower ? 10 : 31;
            int3 maxExclusive = plan.Origin + new int3(
                plan.Width,
                plan.FloorCount * plan.FloorHeight + roofAllowance,
                plan.Depth);
            int windowCount = plan.ShellStyle == GuildHouseShellStyle.HiddenDen
                ? 0
                : plan.FloorCount * 2;
            ArchitecturePresentationCapabilities capabilities = Capabilities;
            if (plan.FloorCount > 1)
                capabilities |= ArchitecturePresentationCapabilities.MultiStoreyCirculation;

            var summary = new ArchitectureRealizationSummary(
                plan.Origin,
                maxExclusive,
                plan.FloorCount,
                plan.FloorHeight,
                doorCount: 1,
                windowCount: windowCount,
                stairCount: math.max(0, plan.FloorCount - 1),
                hasInteriorShell: true,
                capabilities: capabilities);

            ulong requestHash = ArchitectureIdentityHasher.HashRequest(in request);
            ulong realizationHash = ArchitectureIdentityHasher.HashPrototype(in prototype);
            return new ArchitectureGenerationResult(
                new ArchitectureGenerationIdentity(requestHash, realizationHash),
                summary);
        }
    }

    internal static class ArchitectureIdentityHasher
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong HashRequest(in ArchitectureGenerationRequest request)
        {
            ulong hash = Offset;
            Add(ref hash, request.StyleProfileKey);
            Add(ref hash, request.ProviderKey);
            Add(ref hash, request.ArchetypeKey);
            Add(ref hash, request.Seed);
            Add(ref hash, request.StructureId);
            Add(ref hash, request.Origin.x);
            Add(ref hash, request.Origin.y);
            Add(ref hash, request.Origin.z);
            ArchitectureGenerationParameters p = request.Parameters;
            Add(ref hash, p.Width);
            Add(ref hash, p.Depth);
            Add(ref hash, p.StoreyCount);
            Add(ref hash, p.FloorHeight);
            Add(ref hash, p.RoomCount);
            Add(ref hash, (byte)p.RoofForm);
            Add(ref hash, (byte)p.Foundation);
            Add(ref hash, (byte)p.Openings);
            Add(ref hash, p.TrimLevel);
            Add(ref hash, p.DetailLevel);
            return NonZero(hash);
        }

        public static ulong HashPrototype(in GuildHousePrototype prototype)
        {
            ulong hash = Offset;
            GuildHouseSpatialPlan plan = prototype.SpatialPlan;
            Add(ref hash, (byte)plan.Kind);
            Add(ref hash, (byte)plan.ShellStyle);
            Add(ref hash, plan.Origin.x);
            Add(ref hash, plan.Origin.y);
            Add(ref hash, plan.Origin.z);
            Add(ref hash, plan.Width);
            Add(ref hash, plan.Depth);
            Add(ref hash, plan.FloorHeight);
            Add(ref hash, plan.FloorCount);
            Add(ref hash, prototype.Rooms.Length);
            for (int i = 0; i < prototype.Rooms.Length; i++)
            {
                GuildHouseSpatialRoom room = prototype.Rooms[i].SpatialRoom;
                Add(ref hash, (byte)room.Node.Room.Role);
                Add(ref hash, room.FloorIndex);
                Add(ref hash, room.CellIndex);
                Add(ref hash, room.Min.x);
                Add(ref hash, room.Min.y);
                Add(ref hash, room.Min.z);
                Add(ref hash, room.Size.x);
                Add(ref hash, room.Size.y);
                Add(ref hash, room.Size.z);
            }
            return NonZero(hash);
        }

        private static void Add(ref ulong hash, string value)
        {
            if (value == null)
            {
                Add(ref hash, 0u);
                return;
            }
            for (int i = 0; i < value.Length; i++)
            {
                ushort c = value[i];
                AddByte(ref hash, (byte)c);
                AddByte(ref hash, (byte)(c >> 8));
            }
            AddByte(ref hash, 0xFF);
        }

        private static void Add(ref ulong hash, int value) => Add(ref hash, unchecked((uint)value));
        private static void Add(ref ulong hash, byte value) => AddByte(ref hash, value);
        private static void Add(ref ulong hash, uint value)
        {
            AddByte(ref hash, (byte)value);
            AddByte(ref hash, (byte)(value >> 8));
            AddByte(ref hash, (byte)(value >> 16));
            AddByte(ref hash, (byte)(value >> 24));
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash *= Prime;
        }

        private static ulong NonZero(ulong hash) => hash == 0 ? 1UL : hash;
    }
}
