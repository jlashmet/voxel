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

    /// <summary>Client-local visual transition adapter such as a fade to or from black.</summary>
    public interface ICutsceneTransitionCueRuntime
    {
        ICutsceneOperation Execute(CutsceneCueId cue);
    }

    /// <summary>Client-local actor presentation cue such as a one-shot attack animation.</summary>
    public interface ICutsceneActorCueRuntime
    {
        ICutsceneOperation Execute(CutsceneActorId actor, CutsceneCueId cue);
    }

    /// <summary>Additive presentation capability for authored visual transitions.</summary>
    public interface ICutsceneTransitionPresentation
    {
        ICutsceneOperation PlayTransition(CutsceneCueId cue);
    }

    /// <summary>Additive presentation capability for authored actor animation/presentation cues.</summary>
    public interface ICutsceneActorCuePresentation
    {
        ICutsceneOperation PlayActorCue(CutsceneActorId actor, CutsceneCueId cue);
    }
}
