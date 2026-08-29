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

    /// <summary>Audio presentation adapter for one authored cue.</summary>
    public interface ICutsceneSoundCueRuntime
    {
        ICutsceneOperation Execute(CutsceneCueId cue);
    }
}
