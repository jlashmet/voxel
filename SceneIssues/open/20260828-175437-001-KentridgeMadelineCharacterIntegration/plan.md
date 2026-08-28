# Plan

## Observed / acceptance
`KentridgePlayableSlice` owned `CharacterMotor`, campaign actor-host implementation, cutscene player/NPC actors, model selection/animation, and Kentridge-only foot grounding. Madeline therefore flowed through placeholder presentation despite the production `Resources/Characters/Madeline/Madeline.prefab` (Humanoid model GUID `f593982524b374c80b946d9e4670471d`).

Acceptance: scene code chooses scenario/spawn/input only; a Game-owned character host owns movement, campaign actors, model/animation binding and grounding. Authored Madeline resolves to the production Humanoid prefab, opening choreography still runs, gameplay returns, and the real built Kentridge scene starts without runtime exceptions.

No marked captures/poses exist: this is an Architecture SceneIssue (`captures: []`), so there are no original regions to replay.

## Hypotheses / discriminator
1. **Falsified:** a reusable Game-owned actor host already existed and the scene only bypassed it. Audit found the interface but no reusable implementation.
2. **Selected:** reusable lower-level seams existed, but Kentridge implemented them locally. Move ownership behind `Game.Composition.Kentridge.Playable.KentridgeCharacterHost`; falsifier is any changed choreography, actor position, gameplay handoff, or startup failure.

## Fix / regression
- Extracted motor, campaign actors, model selection and animation driving into `KentridgeCharacterHost`.
- Moved generic foot alignment to `CharacterVisualFootGrounding`, preserving the old script GUID so the scene cannot gain a Missing Script.
- Madeline identities now instantiate the production prefab under an outer actor root; imported Animator/skeleton hierarchy stays intact, root motion is disabled, Idle/Walk remain presentation-only.
- Scene runtime delegates movement/ticking/actor lookup to one shared host and no longer directly references `VoxelEngine.Characters.Runtime`.
- Existing production tests now assert shared-host ownership and the authored Madeline registry entry's Humanoid Animator/controller, production visual hierarchy, root-motion policy and reusable grounding.
- `KentridgePlayableScenePlayTests.LaunchScene_NewGameCutscene_ReleasesControl_AndPlayerWalksOutIntoKentridge` drives the new host through the full opening, pub exit and destination interaction. Its filter is already mapped by the repository harness to the real `KentridgePlayableSlice` player.

## Evidence / blast radius / cost
First final CI on source `19fd18cd` failed before tests: extracted host asmdef omitted Campaign/WorldBuilderWorldGen/Showcase dependencies; scene-issue replay also rejected this capture-less architecture issue for missing captured dimensions. Product dependency boundary is fixed; validation now uses the existing Kentridge test-filter player profile instead of fabricating capture metadata. See `experiment-001-final-ci-failure.md`.

Blast radius: Kentridge playable character composition, generic visual grounding, and existing Kentridge production acceptance only. Other placeholder identities are unchanged. Cost is one existing host tick plus one production Madeline prefab; no new world/streaming subsystem or per-frame scan was added.

Current feature head: `7f295712461ca6135d6c85d1ebfc7cbbd0910847` (before this plan-only commit).

## Remaining gates
- [ ] Refresh/merge current `master` immediately before final request.
- [ ] Green exact-SHA PlayMode test above **and** its configured real-player Kentridge capture/artifact.
- [ ] Promote open -> pending with metadata, then pending -> closed/fixed after green gates.
- [ ] Merge latest `master` and non-force push exact feature head to `master`.
