using System;
using System.Collections.Generic;

namespace MountingForce.Story
{
    public sealed class StoryActorRegistry : IStoryActorController
    {
        private readonly Dictionary<StoryActorId, IStoryActorRuntime> _actors =
            new Dictionary<StoryActorId, IStoryActorRuntime>();

        public void Register(StoryActorId id, IStoryActorRuntime actor)
        {
            _actors[id] = actor ?? throw new ArgumentNullException(nameof(actor));
        }

        public bool Unregister(StoryActorId id) => _actors.Remove(id);
        public bool Contains(StoryActorId id) => _actors.ContainsKey(id);

        public void PlaceAt(StoryActorId actor, StoryStagePoint destination)
            => Resolve(actor).PlaceAt(destination);

        public IStoryOperation MoveTo(StoryActorId actor, StoryStagePoint destination, int durationHintMilliseconds)
            => Resolve(actor).MoveTo(destination, durationHintMilliseconds);

        public IStoryOperation FaceActor(StoryActorId actor, StoryActorId target)
            => Resolve(actor).FaceTowards(Resolve(target).Position);

        public IStoryOperation FacePoint(StoryActorId actor, StoryStagePoint target)
            => Resolve(actor).FaceTowards(target.Position);

        private IStoryActorRuntime Resolve(StoryActorId id)
        {
            if (_actors.TryGetValue(id, out IStoryActorRuntime actor)) return actor;
            throw new KeyNotFoundException("Story actor '" + id + "' is not registered.");
        }
    }
}
