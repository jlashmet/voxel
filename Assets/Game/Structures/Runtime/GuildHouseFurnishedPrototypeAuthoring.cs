using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// End-to-end source path for the first guild-house prototypes: author shell, resolve each room
    /// through an existing semantic decoration scene, then emit the corresponding geometry backend.
    /// </summary>
    public static class GuildHouseFurnishedPrototypeAuthoring
    {
        public static bool TryAuthor(IStructureAuthoringSession authoring, in GuildHousePrototype prototype)
        {
            if (authoring == null || !prototype.IsWellFormed)
                return false;

            if (!GuildHouseRoomDecorationResolver.TryResolvePrototype(in prototype, out GuildHouseResolvedRoom[] rooms))
                return false;

            GuildHousePrototypeAuthoring.Author(authoring, in prototype);

            for (int i = 0; i < rooms.Length; i++)
            {
                GuildHouseResolvedRoom room = rooms[i];
                bool ok;
                switch (room.Source)
                {
                    case GuildHouseDecorationSource.Expansion200:
                        ok = DecorationExpansion200AuthoringEmitter.TryAuthorGeometry(
                            authoring, room.Placements, in room.Room.Context);
                        break;
                    case GuildHouseDecorationSource.Expansion260:
                        ok = DecorationExpansion260AuthoringEmitter.TryAuthorGeometry(
                            authoring, room.Placements, in room.Room.Context, prototype.Region);
                        break;
                    case GuildHouseDecorationSource.Expansion320:
                        ok = DecorationExpansion320AuthoringEmitter.TryAuthorGeometry(
                            authoring, room.Placements, in room.Room.Context, prototype.Region);
                        break;
                    case GuildHouseDecorationSource.Expansion380:
                        ok = DecorationExpansion380AuthoringEmitter.TryAuthorGeometry(
                            authoring, room.Placements, in room.Room.Context, prototype.Region);
                        break;
                    default:
                        ok = false;
                        break;
                }

                if (!ok)
                    return false;
            }

            return true;
        }
    }
}
