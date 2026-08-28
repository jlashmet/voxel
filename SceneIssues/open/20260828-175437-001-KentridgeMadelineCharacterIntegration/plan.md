# Plan

## Observed / acceptance
`KentridgePlayableSlice` owned `CharacterMotor`, campaign actor-host implementation, cutscene player/NPC actors, model selection/animation, and Kentridge-only foot grounding. Madeline therefore flowed through placeholder presentation despite the production `Resources/Characters/Madeline/Madeline.prefab` (Humanoid model GUID `f593982524b374c80b946d9e4670471d`).

Acceptance: scene code chooses scenario/spawn/input only; a Game-owned character host owns movement, campaign actors, model/animation binding and grounding. Authored Madeline resolves to the production Humanoid prefab, opening choreography still runs, gameplay returns, and the exact built Kentridge scene starts without runtime exceptions. This Architecture SceneIssue has `captures: []`, so there are no marked image regions/poses to replay.

## Hypotheses / discriminator
1. **Falsified:** a reusable Game-owned actor host already existed and the scene only bypassed it. Audit found the interface but no reusable implementation.
2. **Selected:** reusable lower-level seams existed, but Kentridge implemented them locally. Move ownership behind `KentridgeCharacterHost`; falsifier is any changed choreography, actor position, gameplay handoff, or startup failure.

## Fix / regression
- Extracted motor, campaign actors, model selection and animation driving into `Game.Composition.Kentridge.Playable.KentridgeCharacterHost`.
- Moved generic foot alignment to `CharacterVisualFootGrounding`, preserving the old script GUID so the scene cannot gain a Missing Script.
- Madeline identities instantiate the production prefab under an outer actor root; imported Animator/skeleton hierarchy stays intact, root motion is disabled, Idle/Walk remain presentation-only.
- Scene runtime delegates movement/ticking/actor lookup to one shared host and no longer directly owns `CharacterMotor`.
- Existing production tests assert shared-host ownership plus the authored Madeline registry entry's Humanoid Animator/controller, production visual hierarchy, root-motion policy and reusable grounding.
- `KentridgePlayableScenePlayTests.LaunchScene_NewGameCutscene_ReleasesControl_AndPlayerWalksOutIntoKentridge` drives that host through the opening, generated pub exit, destination interaction and control handoffs. Its repository profile builds the exact `KentridgePlayableSlice` standalone player.

## Evidence / blast radius / cost
- `experiment-001-final-ci-failure.md`: first request failed before tests because extracted assembly dependencies were incomplete; capture-based replay was invalid for this capture-less Architecture issue.
- `experiment-002-assembly-dependency-closure.md`: second request proved the configured test selects the exact Kentridge player, then exposed the remaining exact owner assemblies (`Game.WorldBuilder.Api` for `NpcRef`; `Game.Composition.Showcase` for `ShowcaseWorld`). Those edges are now fixed.

Blast radius: Kentridge playable character composition, generic visual grounding, and existing Kentridge production acceptance only. Other placeholder identities are unchanged. Cost remains the existing host tick plus one production Madeline prefab; grounding replaces the prior per-character Kentridge grounding behavior rather than adding a new world/streaming scan.

## Remaining gates
- [ ] Refresh/merge current `master` immediately before final request.
- [ ] Green exact-SHA PlayMode test above **and** its configured real-player Kentridge artifact.
- [ ] Promote open -> pending with metadata, then pending -> closed/fixed after green gates.
- [ ] Merge latest `master` and non-force push exact feature head to `master`.
