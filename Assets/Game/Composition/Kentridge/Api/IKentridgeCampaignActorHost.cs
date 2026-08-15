using System.Collections.Generic;
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
        /// Atomically materialize or reposition the campaign NPC set at its resolved generated-world
        /// locations. After this returns, TryResolveNpc must succeed for every supplied NpcRef.
        /// </summary>
        void PrepareNpcs(IReadOnlyList<ResolvedNpcWorldPlacement> placements);
    }
}
