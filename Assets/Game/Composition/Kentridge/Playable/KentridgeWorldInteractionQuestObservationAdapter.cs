using System;
using Game.Quests.Api;
using Game.WorldObjects.Api;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Production Kentridge cross-domain adapter from successful semantic WorldObjects interactions
    /// into the campaign's semantic quest observation stream. The adapter owns no quest policy:
    /// stable WorldObjectId values become interaction subject ids and the Quest runtime decides
    /// whether any active objective matches.
    /// </summary>
    public sealed class KentridgeWorldInteractionQuestObservationAdapter : IWorldInteractionFactSink
    {
        private readonly Action<QuestObservation> _observe;

        public KentridgeWorldInteractionQuestObservationAdapter(Action<QuestObservation> observe)
        {
            _observe = observe ?? throw new ArgumentNullException(nameof(observe));
        }

        public void Publish(WorldInteractionFact fact)
        {
            if (!fact.ObjectId.IsValid)
                throw new InvalidOperationException("World interaction fact requires a stable object id.");
            _observe(QuestObservation.Interacted(fact.ObjectId.Value));
        }
    }
}
