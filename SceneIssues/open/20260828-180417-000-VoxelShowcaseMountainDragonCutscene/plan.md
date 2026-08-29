# Plan

## Observed defect / acceptance
- Human review reopened the feature because the built VoxelShowcase did not show a convincing grounded mountain/readable ascent. Closure requires the exact built player to walk the full route normally, show the supported summit dragon and `Hello, I'm Mr. Dragon.`, and save approach/base/switchback/summit/dialogue captures that pass human visual review.
- The checked-in startup bake, not only generated source intent, must contain the accepted result.

## Competing hypotheses / discriminator
1. A stale startup bake can suppress otherwise-correct current WorldBuilder content.
2. Current authored geometry/evidence can still fail acceptance through integer ramp rasterization, traversal timing, or an artificial silhouette.

Discriminator: validate the exact serialized bake semantically, traverse it through `CharacterMotor`, and review rendered captures rather than catalogue counts.

## Material results
- Stale-bake hypothesis is confirmed: the checked-in `ShowcaseWorld.bytes` predates the mountain. Provenance now binds content signature + payload SHA-256, but `ShowcaseWorld.manifest.txt` itself is still missing.
- Previous CI exposed and fixed the 20–60 second replay contract, obsolete dialogue constructor argument, and missing `Game.Cutscenes.Api` reference. Run `33236729056` then built a fresh world and proved normal-motor path entry, but its exact mathematical ramp endpoint assertion sampled air and the 5.5 m/s replay did not finish in 60 seconds.
- The branch already contains asymmetric overlapping mountain shoulders and a replay-only production-motor sprint override; neither has yet passed final built-player visual/traversal validation.
- `fixes/agent-4` was refreshed with current master at merge commit `cbac390493f1db1c729b7dddf6af6663968bfa11`; master changes did not overlap this feature.

## Selected fix / remaining gates
- Replace exact ramp-endpoint proof with an interior ramp-column material scan and add a structural regression requiring multiple asymmetric mountain masses.
- Set the issue-owned replay timeout below the workflow's 60-second ceiling while retaining normal `CharacterMotor` movement only.
- Regenerate and commit the exact current `ShowcaseWorld.bytes` plus `ShowcaseWorld.manifest.txt`; then run one final exact-SHA request on `ci-test/fixes/agent-4` containing focused regression + exact-scene built-player replay.
- Inspect every resulting capture for grounded natural silhouette, readable supported switchbacks, supported dragon, and dialogue. Only then promote open→pending→closed, finish metadata, merge latest master, and non-force update master.

## Blast radius / cost
Changes remain bounded to reusable mountain realization, startup-bake proof, and opt-in SceneIssue replay. Extra frustums affect one-time world build/bake only; normal movement and steady-state world truth are unchanged.
