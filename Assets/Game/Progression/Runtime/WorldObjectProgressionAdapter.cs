using System;
using Game.Progression.Api;
using Game.WorldObjects.Api;

namespace Game.Progression.Runtime
{
    public sealed class WorldObjectProgressionAdapter : IWorldInteractionFactSink
    {
        private readonly IProgressionFactSink _progression;

        public WorldObjectProgressionAdapter(IProgressionFactSink progression)
        {
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
        }

        public void Publish(WorldInteractionFact fact)
        {
            _progression.Publish(new ProgressionFact(
                fact.Sequence,
                "world-object-interaction",
                fact.ActorId.ToString(),
                fact.ObjectId.Value,
                fact.StateCode));
        }
    }
}
