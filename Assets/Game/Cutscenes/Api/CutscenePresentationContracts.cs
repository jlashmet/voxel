namespace Game.Cutscenes.Api
{
    /// <summary>Camera presentation adapter for one authored cue.</summary>
    public interface ICutsceneCameraCueRuntime
    {
        ICutsceneOperation Execute(CutsceneCueId cue);
    }

    /// <summary>
    /// Dialogue presentation adapter. A default speaker preserves legacy cues that own speaker
    /// assignment internally; an explicit speaker lets newer authored content bind presentation to a
    /// semantic cutscene actor without giving presentation authority over gameplay actor state.
    /// </summary>
    public interface ICutsceneDialogueCueRuntime
    {
        ICutsceneOperation Execute(CutsceneActorId speaker, CutsceneCueId cue);
    }

    /// <summary>
    /// Read-only client presentation seam for the currently visible dialogue line. Gameplay and
    /// authored cutscene state stay independent from whichever Unity/UI view renders the text.
    /// </summary>
    public interface IActiveCutsceneDialogue
    {
        string ActiveDialogue { get; }
    }

    /// <summary>Audio presentation adapter for one authored cue.</summary>
    public interface ICutsceneSoundCueRuntime
    {
        ICutsceneOperation Execute(CutsceneCueId cue);
    }
}
