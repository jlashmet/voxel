# 21. Gameplay audio integration & semantic cue presentation

**Status:** Approved

## Purpose

Provide one reusable client-side audio presentation system for gameplay, UI, and authored cutscene sound cues without introducing audio assets, Unity audio objects, or sound-selection policy into authoritative gameplay systems.

The defining rule is:

> Gameplay says what happened; audio presentation decides whether, where, and how that fact sounds.

Conceptually:

```text
authoritative/local semantic gameplay facts
        |
        +--> vitality events
        +--> combat events
        +--> interaction results
        +--> inventory/loot results
        +--> progression events
        +--> encounter/session events
        |
        v
audio presentation adapters / cue policy
        |
        v
AudioCueRef + presentation context
        |
        v
audio playback runtime
        |
        v
Unity audio adapter
```

Cutscenes use the same playback infrastructure through their existing authored sound-cue seam while retaining ownership of cutscene timing and sequencing.

## 1. Gameplay authority does not depend on audio

Systems such as vitality, combat, inventory, quests, encounters, and WorldObject interactions must not directly invoke `AudioSource`, `AudioClip`, sound names, mixer objects, or Unity presentation components.

They continue emitting semantic state transitions and results. Audio presentation consumes those facts independently.

For example, `ActorDefeated(actorId, source, cause)` is a gameplay fact. Which sound represents that fact is presentation policy.

The dependency direction remains:

```text
audio presentation -> gameplay APIs
```

not:

```text
gameplay runtime -> audio runtime
```

## 2. Domain events do not carry sound IDs

Do not change semantic gameplay contracts so they contain sound identity purely for presentation.

Avoid contracts such as:

```text
ActorDefeated(actorId, soundId = "orc_death_03")
InteractionResult.Sound = "gate_open"
```

Instead:

```text
semantic event
    -> audio presentation policy
        -> AudioCueRef
```

The semantic event remains reusable by UI, VFX, story, tests, networking, and headless simulation.

## 3. Small Audio API / Runtime boundary

Follow the repository's API/runtime module convention.

Conceptually:

```text
Game.Audio.Api
    AudioCueRef
    AudioPlaybackRequest
    AudioOrigin
    IAudioPlayback

Game.Audio.Runtime
    cue catalog/resolution
    playback scheduling
    source lifecycle/pooling
    concurrency handling
    Unity audio realization
```

Other modules may depend on `Game.Audio.Api` where an audio adapter is genuinely needed. They must not depend on `Game.Audio.Runtime`.

Gameplay domain modules generally should not need even the Audio API; presentation/composition adapters bridge between the domain APIs and audio API.

## 4. AudioCueRef is presentation identity

Use a stable semantic presentation identifier such as:

```text
AudioCueRef("character.damage.light")
AudioCueRef("world.gate.open")
AudioCueRef("progression.objective.complete")
```

An `AudioCueRef` does not itself contain `AudioClip`, `AudioSource`, mixer objects, GameObjects, prefabs, or Unity resource paths.

It identifies a presentation cue whose realization is provided by audio content/runtime.

## 5. Concrete clip configuration belongs to audio content

Audio content resolves an `AudioCueRef` to one or more concrete clips and the minimal production playback properties actually required, for example:

- spatial versus non-spatial playback;
- playback category/bus;
- volume variation;
- pitch variation;
- priority;
- concurrency/voice limit;
- optional variation selection.

These are presentation details. Do not turn the first implementation into a general-purpose audio middleware clone.

## 6. Variation belongs to presentation

If a semantic cue has several interchangeable recordings, gameplay still emits the same semantic fact. Audio presentation chooses the variation.

Random presentation variation must never affect authoritative simulation. Clients do not need to choose identical variations unless a future requirement demonstrates that need.

## 7. Spatial origin uses semantic identities

For world-space audio, gameplay events should not expose Unity Transforms.

Audio presentation resolves origin through stable semantic identity where possible, for example:

```text
AudioOrigin
    None
    Character(CharacterId)
    WorldObject(WorldObjectId)
    WorldPosition(...)
```

A local presentation resolver translates that identity into the currently realized spatial position.

## 8. Streaming-safe spatial audio

WorldObject or character presentation may unload and later be recreated while authoritative identity persists. Audio cannot assume a permanent AudioSource GameObject per semantic object.

At playback time, stable identity resolves through the current local presentation/spatial resolver. If no meaningful local realization exists, presentation policy may suppress the cue or use another deliberate fallback without changing gameplay authority.

## 9. One-shot sounds come from transitions/events, not snapshots

A snapshot saying that a character is defeated, a gate is open, or an objective is completed establishes current truth. It does not imply that historical death, gate-opening, or completion sounds should replay.

One-shot audio responds to semantic transitions/events. This prevents late join, reconnect, restore, and replication repair from replaying old sounds merely because current state was reconstructed.

## 10. Reconnect and late join do not replay historical audio

When a client reconnects or joins late, current authoritative state is synchronized. Historical one-shot sounds are not replayed.

The same rule applies to:

- reconnect;
- late join;
- scene/presentation reconstruction;
- system-16 restore;
- replication repair.

## 11. Sustained audio derives from current state

Long-running or looping audio is different from a historical one-shot.

If production content requires a sustained sound, derive desired presentation from current semantic state:

```text
machine state = Running
    -> desired audio state = machine-running loop

machine state = Stopped
    -> no machine-running loop
```

Thus reconnect/reconstruction can establish the correct loop without replaying the historical start transition.

Do not persist `AudioSource.isPlaying` as gameplay state.

## 12. Outcome-confirming sounds follow authoritative results

Sounds communicating confirmed gameplay consequences follow the authoritative result, for example:

- hit confirmed;
- damage received;
- actor defeated;
- item successfully acquired;
- inventory transfer succeeded;
- objective completed;
- interaction accepted;
- combat completed.

A client must not play a success cue merely because it requested the operation when that operation can be rejected authoritatively.

## 13. Local anticipation and UI sounds may be immediate

Presentation-only anticipation or interface sounds may happen immediately where incorrect playback carries no gameplay meaning, for example panel/button feedback or an attack-swing whoosh.

Keep the distinction between an attack-attempt sound and an authoritative hit-confirmation sound.

Do not invent additional client prediction machinery merely for audio.

## 14. Predicted and authoritative playback must not double-fire

If a locally predicted presentation cue corresponds to a later authoritative event, explicit presentation policy must define whether:

- anticipation and confirmation are distinct cues;
- the authoritative cue replaces/reconciles the first;
- or the same logical cue is deduplicated.

Do not blindly play both local prediction and replicated confirmation.

## 15. Reuse semantic replication identity for deduplication

Where system 06 transports transient gameplay events for one-shot presentation, use the existing semantic event sequence/revision/tick identity where practical to avoid duplicate presentation.

Do not add an audio-specific distributed event protocol or `AudioEventGuid` to every gameplay operation.

## 16. Vitality integration

System 02 provides a clean first consumer.

Conceptually:

```text
DamageApplied
    -> audio presentation resolver
        -> damage/impact cue
        -> spatial origin = target CharacterId

ActorDefeated
    -> defeat cue policy
        -> spatial origin = defeated CharacterId
```

System 02 never invokes audio. Audio never decides whether damage or defeat occurred.

## 17. Combat integration

Combat-specific audio consumes semantic combat facts rather than inspecting private runtime collections.

Possible demonstrated consumers include combat start, committed actions, authoritative attack results, meaningful turn/round transitions, and combat completion.

Do not put `AudioClip` fields on combat participants or actions.

## 18. World interaction integration

System 13 already returns semantic interaction results rather than sound names. Preserve that.

For example:

```text
character activates lever
    -> system 13 validates
    -> WorldObject executes
    -> semantic state/result
        -> audio presentation
            -> lever activation cue
```

Likewise, a gate `Closed -> Open` transition can feed a gate-open cue. The lever/gate behavior does not know which concrete asset represents it.

## 19. Object/content metadata may refine cue selection

Not every object with semantic action `Open` should sound identical.

Presentation/content may associate audio traits or cue mappings with a WorldObject definition, for example wooden-door-open versus heavy-stone-gate-open.

This mapping belongs with presentation/content configuration, not shared interaction behavior.

## 20. Inventory and loot integration

Systems 09/10 remain authoritative for item movement. Audio may react to semantic pickup, drop, or transfer results.

Do not play success audio from a request alone if the transaction can fail authoritatively.

Pure inventory-screen UI sounds remain local presentation concerns.

## 21. Quest/objective integration

System 11 emits semantic progression transitions. System 21 may independently consume events such as objective activation/completion or quest completion.

System 19 may consume the same event visually. Neither system invokes the other.

```text
ObjectiveCompleted
    +--> system 19 visual presentation
    +--> system 21 audio presentation
```

## 22. UI sounds reuse playback without becoming gameplay authority

Systems 17-20 may request purely local UI cues such as confirm, cancel, or panel-open where actually authored.

These are local presentation intents, not authoritative gameplay events.

The playback runtime can serve both event-derived gameplay audio and local UI audio while keeping those semantics distinct.

## 23. Cutscenes reuse playback but retain sequencing ownership

The cutscene API already owns authored sound steps through `CutsceneStepType.Sound`, `CutsceneCueId`, and `ICutsceneSoundCueRuntime`.

A composition adapter can bridge:

```text
CutsceneCueId
    -> authored audio mapping
        -> Game.Audio.Api playback
```

System 21 must not move cutscene timing into a generic audio manager.

Cutscene runtime decides when an authored sound cue occurs. System 21 decides how the resolved cue is played.

## 24. Avoid duplicate cinematic and gameplay cues

An authored cutscene may stage sound at the same moment a gameplay-like semantic event occurs. Composition must avoid unintentionally playing both when only one presentation is desired.

Selected normal presentation may be suppressed while a cutscene owns that presentation moment, but the underlying gameplay event itself remains unchanged.

## 25. Audio settings belong to system 23

System 21 exposes the categories/configuration necessary for local settings to control playback, but system 23 owns settings UI and start/menu flow.

Only create categories actually required by the final audio design. Do not hardcode a speculative giant mixer hierarchy.

These settings are local user preferences, not authoritative gameplay state and not system-16 session persistence.

## 26. Audio pooling/concurrency are runtime details

Unity realization may need AudioSource reuse, pooling, voice stealing, concurrency limits, spatial attenuation, mixer routing, and clip lifetime management.

Those concerns stay behind the audio runtime/adapter rather than being reimplemented by each feature.

Repeated semantic events may require audio-side priority, concurrency, cooldown, or replacement policy. Do not throttle authoritative gameplay events to manage the soundscape.

## 27. Interest and audibility remain client presentation concerns

The server does not decide which concrete sound a speaker hears.

The server supplies relevant gameplay truth/events through the normal replication architecture. Each client applies local presentation policy using its listener, distance, available emitter, and cue configuration.

Do not create a second network protocol that streams `PlayClip` commands.

## 28. The server never sends clip identity

Multiplayer remains semantic:

```text
server:
    ActorDefeated(CharacterId 12)

client:
    semantic event
        -> local audio mapping
            -> creature defeat clip
```

not:

```text
server:
    Play clip goblin_die_04.wav
```

## 29. Missing audio is not a gameplay failure

If a clip is missing, fails to load, no source can be allocated, or the device is muted, the authoritative gameplay operation remains valid.

Audio playback failure must never roll back combat, interaction, inventory, quest, or session state.

## 30. Restore does not persist playback internals

System 16 does not persist currently allocated AudioSources, one-shot playback offsets, random variation choices, voice-pool contents, mixer internals, or transient cue queues.

After restore, current semantic state may reestablish valid sustained presentation. Historical one-shot cues remain historical.

## 31. GameplayReady governs gameplay-driven one-shots

During initial synchronization or restoration, do not emit normal gameplay one-shot cues merely as snapshot fields arrive.

First establish current authoritative truth. Once GameplayReady is satisfied, new semantic events can drive normal transient audio.

Connection/recovery UI may have independent local audio if desired.

## 32. Audio and VFX remain sibling consumers

System 22 addresses visual effects/feedback. Do not solve both by inventing a giant `FeedbackManager` that becomes a hidden owner of gameplay semantics.

Prefer:

```text
semantic gameplay event
    +--> audio presentation
    +--> VFX presentation
    +--> HUD presentation
```

They may share low-level presentation context where demonstrated useful, but remain independently replaceable consumers.

## 33. No generic global event bus merely for audio

Do not add a global string event protocol such as `Publish("player_hit")` merely so audio can subscribe.

Consume the typed semantic APIs/events already owned by each domain. Composition wires necessary adapters explicitly.

## Suggested module structure

Conceptually:

```text
Assets/Game/Audio/
    Api/
        AudioCueRef
        AudioPlaybackRequest
        AudioOrigin
        IAudioPlayback

    Runtime/
        AudioPlaybackRuntime
        AudioCueCatalog
        AudioEmitterResolver
        UnityAudioPlaybackAdapter

    Content/
        cue definitions / clip mappings
```

Presentation/composition adapters are introduced only for demonstrated consumers, for example vitality, interaction, progression, inventory, or cutscene audio adapters.

## Acceptance / reuse proof

### Character vitality

1. Damage two unrelated characters through system 02.
2. Both use the same vitality-to-audio integration path.
3. Audio resolves each cue from semantic damage state.
4. Spatial origin resolves through each `CharacterId`.
5. Neither character nor vitality runtime contains an AudioSource/AudioClip dependency.

### WorldObject reuse

1. Interact with two unrelated WorldObjects through system 13.
2. Both execute their normal authoritative behavior.
3. Semantic transitions feed system 21.
4. Presentation/content maps each object to appropriate audio.
5. No WorldObject behavior directly invokes Unity audio.

### Quest progression

1. Complete an objective through system 11.
2. System 19 updates visual presentation.
3. System 21 independently plays the configured completion cue.
4. Neither UI nor progression owns the other's behavior.

### Multiplayer remote event

1. Client A causes authoritative damage near client B.
2. The server replicates the semantic result through normal system-06 paths.
3. Client B resolves and plays the appropriate cue locally.
4. No clip name or AudioSource command is transmitted by the server.

### Reconnect

1. Disconnect a client.
2. Several one-shot gameplay events occur.
3. Reconnect and synchronize through systems 08/06.
4. Current gameplay state is reconstructed.
5. Historical one-shot sounds are not replayed.

### Sustained state

For one demonstrated looping case:

1. Establish semantic state requiring a loop.
2. Create/recreate its local presentation.
3. The loop is established from current state.
4. Transition semantic state so the loop is no longer appropriate.
5. The loop ends.
6. No authoritative `AudioSource.isPlaying` state exists.

### Cutscene reuse

1. Execute an authored `CutsceneStepType.Sound`.
2. Existing cutscene sound adapter resolves its `CutsceneCueId`.
3. The adapter delegates concrete playback to system 21.
4. Cutscene timing remains owned entirely by the cutscene runtime.

### Headless independence

Run vitality, interactions, quests, combat, and session progression on a headless server with no system-21 runtime loaded. Authoritative results remain identical.

### Alternate sink

Run semantic audio-presentation tests with a fake `IAudioPlayback` implementation and verify cue/origin selection without Unity audio.

## Out of scope

- gameplay authority of any kind;
- damage/combat rules;
- WorldObject behavior;
- inventory/loot transactions;
- quest progression;
- audio asset production itself;
- voice chat;
- text-to-speech;
- recorded dialogue/localization pipeline;
- speculative adaptive-music system;
- speculative biome ambience system;
- generic music composer/state machine;
- audio settings screens (system 23);
- VFX (system 22);
- haptics;
- server-driven clip playback commands;
- generic global string event bus;
- third-party audio middleware abstraction unless actually adopted;
- persistence of transient playback state.

## Architectural constraints

- Gameplay emits semantic facts; system 21 selects and plays sound.
- Domain APIs do not contain `AudioClip`, `AudioSource`, sound names, mixer objects, or Unity presentation references.
- Domain events do not carry `AudioCueRef` merely for presentation.
- `Game.Audio.Api` remains engine-neutral; concrete playback stays behind Runtime.
- Other modules depend only on Audio API, never Audio Runtime.
- Audio mapping belongs to presentation/content composition.
- Spatial audio resolves stable semantic identities into local presentation positions.
- Snapshots establish truth but do not replay historical one-shot sounds.
- Sustained audio derives from current state where required.
- Outcome-confirming sounds follow authoritative results.
- Local anticipation/UI sounds may be immediate when they convey no authoritative outcome.
- Replicated events are deduplicated through semantic event/replication identity rather than an audio-specific protocol.
- The server never sends clip names or playback commands.
- Playback failure never alters authoritative gameplay.
- Cutscene sequencing remains owned by the existing cutscene runtime while concrete playback is reusable.
- Systems 19, 21, and 22 may react independently to the same semantic event.
- Audio settings remain local presentation preferences.
- Headless gameplay runs identically without system 21.
