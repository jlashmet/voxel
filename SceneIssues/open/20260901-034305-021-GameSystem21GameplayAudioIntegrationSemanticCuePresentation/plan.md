# 21 Gameplay audio integration & semantic cue presentation — implementation plan

**Target module:** `Assets/Game/Audio/Api` / `Runtime` (`Game.Audio.Api`, `Game.Audio.Runtime`).

## Acceptance / observed baseline

Audio is client-local presentation only: gameplay/cutscenes publish semantic identity/current state, Audio maps that to Unity playback, and missing/failed audio never changes authority. Baseline audit on master `aa61895f28d70f35c67d07db6a4fa93beee635eb` found no `Assets/Game/Audio` owner and no indexed production `AudioSource` ownership. `KentridgePlayableSlice.SlicePresentation.PlaySound` was a no-op even though `KentridgeOpeningCutscene` authors `CutsceneStep.Sound(door.open)`. System 23 remains open; its `IUserPreferencesStore`/audio-preference tasks are not implemented, so T21-016 is an external prerequisite and must not be replaced by Audio-owned settings authority.

## Architecture / hypotheses

1. **Selected:** engine-neutral `AudioCueRef`, stable one-shot event identity, semantic origin, and sustained-state descriptors in `Game.Audio.Api`; Unity clip/source mapping, mix, dedupe, origin resolution and current-state loop reconciliation live in `Game.Audio.Runtime`. Confirmed gameplay adapters subscribe to public semantic events. Kentridge composition owns one `KentridgeGameplayAudioIntegration` that implements `ICutsceneSoundCueRuntime`, observes confirmed character-defeat events, and delegates both paths into the same `AudioPresentationRuntime`.
2. **Rejected:** gameplay modules or scenes choose clips/call `AudioSource`, Audio owns gameplay/settings state, or cutscene and gameplay create separate Audio runtimes. Those options leak presentation identity into authority or create duplicate playback ownership.

**Discriminating proof:** EditMode regressions cover mapping failures, predicted+confirmed dedupe, confirmed Vitality events, reconnect/current-state reconstruction, shared cutscene/gameplay service and headless gameplay with Audio absent. `Assets/Game/Audio/Validation/AudioValidation.unity` is the module-owned built-player surface and emits semantic milestones while real Unity `AudioSource` playback runs, including the distinct `world.door.opened` production mapping. Kentridge's authored `door.open` cue now traverses `SlicePresentation -> ICutsceneSoundCueRuntime -> KentridgeGameplayAudioIntegration -> AudioPresentationRuntime`.

## Ownership / blast radius

`Game.Audio.Api` has no Unity or gameplay Runtime dependency. `Game.Audio.Runtime` owns Unity playback only. Kentridge composition attaches presentation adapters but does not receive or transfer gameplay mutation authority. Production combat still owns its normal character-defeat transition; Audio only observes the resulting public semantic event. Current sustained audio is reconstructible from descriptors; one-shot history is deliberately not reconstructed. Unknown cues/origins/backend failures are diagnostic presentation failures.

The runtime default catalog uses locally generated Unity `AudioClip` objects only as self-contained integration assets; semantic identity remains independent of clip/resource paths and authored assets can replace bindings without gameplay changes. Repository audit found no pre-existing indexed `AudioClip`/`AudioSource` gameplay ownership to consolidate beyond the Kentridge no-op sound handoff.

## Current state / remaining gates

Exact-SHA request `2e337f36acded13e7d89ee34bac91ea01ea59f70` validated production feature SHA `a4bb46392a188cc30d3996270d07d46b7fa0c73e` in run `33882411311` with all required gates green. Persistent EditMode results: `Game.Audio.Tests.EditMode` 6/6, `Game.Composition.Kentridge.Playable.Tests` 3/3, `Game.Kentridge.PlayableSlice.Tests.EditMode` 1/1, focused dedupe regression 1/1. Audio module built-player validation ran 10s and emitted all required dedupe, `world.door.opened`, sustained-state reconnect/no-history-replay and stop milestones; Kentridge module player passed; canonical `KentridgePlayableSlice` ran 80s with zero harness assertions and logged the production `door.open -> world.door.opened` handoff; separate SceneIssue replay ran 30s with zero assertions. Artifact `single-test-33882411311` id `9940674133`, digest `sha256:31c27c1f772264b4b6c9c74b2fc4c00e067a723ede1e07c9fd11964376c87f96`.

Prerequisite refresh after fetching origin on 2026-09-04: current master is `d08612dfe2f4a99aff34897717569744565bc642`. System 23 is still in `SceneIssues/open`, repository search finds no `IUserPreferencesStore`, and System23 tasks T23-007 (define the preference contract) and T23-017 (persist/apply audio settings) remain unchecked. The newer master does not supply or supersede the missing preference seam. Therefore T21-016 remains the only external implementation blocker. Keep this SceneIssue open; do not invent Audio-owned settings authority. When system 23 lands, merge then-current master, bind its real preference seam to `AudioPresentationRuntime.ApplyMix`, run exact-SHA validation on that integrated production head, complete T21-016/T21-032, then close and promote by PR + auto-merge.
