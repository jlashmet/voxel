using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Stable semantic handoffs into castle underground content. These anchors describe where
    /// downstream basement/dungeon/crypt/cave composition may attach without exposing the authored
    /// room implementation or requiring consumers to re-derive castle-private coordinates.
    /// </summary>
    public struct CastleUndergroundAttachmentConfig
    {
        public AttachmentAnchorConfig KeepBasement;
        public AttachmentAnchorConfig Dungeon;
        public AttachmentAnchorConfig GatehouseCrypt;
        public AttachmentAnchorConfig Cave;

        public bool IsWellFormed =>
            KeepBasement.Kind == StructureAttachmentKind.Basement &&
            Dungeon.Kind == StructureAttachmentKind.Dungeon &&
            GatehouseCrypt.Kind == StructureAttachmentKind.Crypt &&
            Cave.Kind == StructureAttachmentKind.Cave &&
            KeepBasement.IsWellFormed &&
            Dungeon.IsWellFormed &&
            GatehouseCrypt.IsWellFormed &&
            Cave.IsWellFormed;

        public int3 ResolveKeepBasement(in CastlePlan plan) =>
            plan.Centre + KeepBasement.LocalPosition;

        public int3 ResolveDungeon(in CastlePlan plan) =>
            plan.Centre + Dungeon.LocalPosition;

        public int3 ResolveGatehouseCrypt(in CastlePlan plan) =>
            plan.Centre + GatehouseCrypt.LocalPosition;

        public int3 ResolveCave(in CastlePlan plan) =>
            plan.Centre + Cave.LocalPosition;
    }

    public static class CastleUndergroundAttachmentPresets
    {
        /// <summary>
        /// Maps the historical cellar, deep dungeon, and dungeon-to-cave passage coordinates exactly
        /// and adds a bounded gatehouse crypt extension point below the existing gatehouse without
        /// authoring a new room.
        /// </summary>
        public static CastleUndergroundAttachmentConfig Compatibility(in CastlePlan plan)
        {
            int3 trapdoor = CastleLayout.TrapdoorCentre(in plan);
            int3 trapdoorLocal = trapdoor - plan.Centre;
            int cellarLocalY = plan.PlateauHeight - 46;
            int dungeonLocalY = cellarLocalY - 120;

            return new CastleUndergroundAttachmentConfig
            {
                KeepBasement = new AttachmentAnchorConfig
                {
                    Kind = StructureAttachmentKind.Basement,
                    LocalPosition = new int3(
                        trapdoorLocal.x,
                        cellarLocalY,
                        trapdoorLocal.z),
                    Facing = Facing.Down,
                    SnapToGround = false,
                },
                Dungeon = new AttachmentAnchorConfig
                {
                    Kind = StructureAttachmentKind.Dungeon,
                    LocalPosition = new int3(
                        trapdoorLocal.x,
                        dungeonLocalY,
                        trapdoorLocal.z),
                    Facing = Facing.South,
                    SnapToGround = false,
                },
                GatehouseCrypt = new AttachmentAnchorConfig
                {
                    Kind = StructureAttachmentKind.Crypt,
                    LocalPosition = new int3(
                        0,
                        plan.PlateauHeight - 64,
                        -plan.BaileyHalfZ),
                    Facing = Facing.Down,
                    SnapToGround = false,
                },
                // Legacy CastleDungeonAuthoring used hallMin.z = trapdoor.z - 90,
                // passZ = hallMin.z - 1, then authored 320 passage columns before calling the
                // castle cave at passZ - 320. Preserve that exact handoff as semantic data.
                Cave = new AttachmentAnchorConfig
                {
                    Kind = StructureAttachmentKind.Cave,
                    LocalPosition = new int3(
                        trapdoorLocal.x,
                        dungeonLocalY,
                        trapdoorLocal.z - 411),
                    Facing = Facing.South,
                    SnapToGround = false,
                },
            };
        }
    }
}
