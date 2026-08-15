using System.Collections.Generic;
using Game.Composition.WorldBuilderWorldGen;

namespace Game.Composition.Kentridge.Api
{
    /// <summary>
    /// Gameplay-secret adapter for one realized Kentridge campaign. The implementation owns runtime
    /// interactables/containers and voxel-edit wiring; Composition supplies the exact generated room,
    /// entrance, and container geometry as one batch so the host can validate/register it atomically.
    /// </summary>
    public interface IKentridgeCampaignSecretHost
    {
        void PrepareSecrets(IReadOnlyList<ResolvedSecretWorldGeometry> secrets);
    }
}
