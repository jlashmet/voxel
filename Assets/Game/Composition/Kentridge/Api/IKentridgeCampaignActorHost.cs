using Game.Composition.Campaign;
using Game.Composition.WorldBuilderWorldGen;

namespace Game.Composition.Kentridge.Api
{
    /// <summary>
    /// Gameplay-character adapter used by the Kentridge composition root. Implementations own the
    /// authoritative player/NPC objects; Composition supplies only deterministic generated-world
    /// placement and the cutscene actor lookup seam.
    /// </summary>
    public interface IKentridgeCampaignActorHost : IWorldBoundCutsceneActorProvider
    {
        /// <summary>
        /// Materialize or reposition one authoritative NPC at its resolved generated-world location.
        /// After this returns, TryResolveNpc for the same NpcRef must succeed.
        /// </summary>
        void PrepareNpc(ResolvedNpcWorldPlacement placement);
    }
}
