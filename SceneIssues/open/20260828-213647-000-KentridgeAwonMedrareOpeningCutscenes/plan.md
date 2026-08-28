# Plan — Kentridge Awon + Medrare Opening Cutscenes

## Observed gap and acceptance
The current playable slice does not yet carry the original early Kentridge progression through Weldon visiting his father Awon and then his teacher Medrare. This must be a fidelity port from `jlashmet/mounting-force` plus `References/MountingForce`, using original dialogue and meaningful cutscene cues rather than approximate replacement conversations. The applicable source sequence must be traced through Awon, Medrare, `medrare-to-church`, and `medrare-first-spell` before deciding the slice endpoint.

## Competing hypotheses / first discriminator
1. Existing Story/Campaign/cutscene APIs can express the complete legacy sequence; work is primarily source extraction, authoring, staging, and gating.
2. One or more legacy behaviors (NPC walk/follow, facing, waits, room transitions, conditional sequencing, control lock) are missing as reusable primitives and require shared cutscene/runtime support.

First build a source-to-runtime cue matrix for every applicable legacy event and map each cue to the current production cutscene API. Any unmapped cue is the discriminator for shared-engine work before Kentridge-specific authoring.

## Work
1. Trace the authoritative original event chain beginning after the existing Logan opening. Record exact dialogue, prerequisites, initial actor positions/facing, movement order/destinations, pauses, entrances/exits, transitions, story flags, and repeat behavior.
2. Port Awon first. Preserve the original dialogue and staging intent, including the house/back-room choreography, while adapting legacy coordinates to named anchors in the rebuilt Kentridge.
3. Gate Medrare from the same progression state as the original. Premature interaction must not skip Awon or fire later exposition.
4. Port Medrare’s full applicable sequence, including the church walk and first-spell event when source order shows they immediately belong here. Preserve blocking semantics rather than merely copying lines.
5. Keep scene authoring declarative: identities, triggers, anchors, dialogue, and scenario parameters may be Kentridge-specific; missing reusable behavior belongs in shared Game/cutscene code.
6. Preserve one-shot completion, post-event revisit behavior, re-entry, and save/load continuity across every sequence boundary.

## Regression and verification
Add focused behavioral coverage for event order/gating, exact source dialogue identity, important movement/facing destinations, one-shot completion, and save/load or re-entry between beats. Run the built-application Kentridge harness from a fresh game through the complete applicable Logan → Awon → Medrare → church/first-spell flow and verify no startup/runtime exceptions.

## Non-goals
Do not rewrite dialogue, modernize the story, redesign blocking for taste, add unrelated Kentridge content, or create Kentridge-only cutscene machinery when a reusable primitive is required.
