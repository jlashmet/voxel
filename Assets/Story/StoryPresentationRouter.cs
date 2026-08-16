using System;

namespace MountingForce.Story
{
    public interface IStoryCueRuntime
    {
        IStoryOperation Execute(StoryCueId cue);
    }

    public sealed class StoryPresentationRouter : IStoryPresentation
    {
        private readonly IStoryCueRuntime _camera;
        private readonly IStoryCueRuntime _dialogue;
        private readonly IStoryCueRuntime _sound;

        public StoryPresentationRouter(IStoryCueRuntime camera, IStoryCueRuntime dialogue, IStoryCueRuntime sound)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            _sound = sound ?? throw new ArgumentNullException(nameof(sound));
        }

        public IStoryOperation SetCamera(StoryCueId cue) => _camera.Execute(cue);
        public IStoryOperation ShowDialogue(StoryCueId cue) => _dialogue.Execute(cue);
        public IStoryOperation PlaySound(StoryCueId cue) => _sound.Execute(cue);
    }
}
