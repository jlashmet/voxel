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

Core API/runtime, mapping, dedupe, production Kentridge cutscene/gameplay sharing, Unity backend, current-state loop reconciliation, headless tests, cleanup audit and module validation scene are implemented on `fixes/agent-3`. Exact run `33880673110` on feature `eb8540369ab1a5d992b7a6ff209c828acc68133a` proved request resolution, planner derivation and the standalone SceneIssue replay, while its Unity test batch exited `0`; automatic module validation then failed before Audio player execution because `AudioValidation.player-scenario.json` used `runSeconds: 8` below the shared validator's `10..300` contract. The scenario now uses the minimum valid `10` seconds. T21-016 remains blocked on the not-yet-merged system 23 preference API. Remaining executable work is exact-SHA automatic module + standalone SceneIssue validation, evidence review and validation-dependent checklist updates; if system 23 lands, merge current master, bind the real preference seam, and exact-SHA revalidate before closure. Final closure requires every checkbox, followed by PR + auto-merge per repository rules.
