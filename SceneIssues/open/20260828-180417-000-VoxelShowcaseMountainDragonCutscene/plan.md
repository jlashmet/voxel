# Plan

## Observed defect / acceptance
- Human review reopened the feature because the built VoxelShowcase did not show a convincing grounded mountain/readable ascent. Closure requires the exact built player to walk the full route normally, show the supported summit dragon and `Hello, I'm Mr. Dragon.`, and save approach/base/switchback/summit/dialogue captures that pass human visual review.
- The checked-in startup bake, not only generated source intent, must contain the accepted result.

## Competing hypotheses / discriminator
1. A stale startup bake can suppress otherwise-correct current WorldBuilder content.
2. Current authored geometry/evidence can still fail acceptance through integer ramp rasterization, traversal timing, an artificial silhouette, or visually unacceptable path support.

Discriminator: validate the exact serialized bake semantically, traverse it through `CharacterMotor`, and review rendered captures rather than catalogue counts.

## Material results
- Stale-bake hypothesis is confirmed: checked-in `ShowcaseWorld.bytes` predates the mountain and the manifest payload is absent. Provenance now rejects pre-naturalized output via contract revision 3 plus payload SHA-256.
- Prior run `33236729056` proved the old result was not acceptable: the mountain read as a smooth manufactured wedge and full-height support boxes read as dam-like retaining walls. It also exposed the brittle exact ramp-tip assertion and replay-duration problem.
- The reusable realization now uses a core plus asymmetric shoulder masses and tapered overlapping support under ramps/turns/summit connectors; exact one-voxel walking surfaces remain unchanged. A dedicated regression rejects tall mountain-material support boxes.
- Evidence replay remains production `CharacterMotor` movement with an opt-in replay-only sprint; route timeout is 55 seconds. The exact final CI filter is a wrapper that runs asymmetric-silhouette, natural-support/cost, semantic bake/path/dragon/route/dialogue assertions before built-player replay.
- `fixes/agent-4` already contains current master `9b452aedd9b5d1b1720bf0e9184d0381f159d352`; no feature/master path conflicts were found.

## Selected fix / remaining gates
- Run the one final targeted CI request directly from the final source SHA. The workflow must generate/restore the revision-3 bake, pass the wrapper regression, and complete the exact VoxelShowcase built-player route/captures.
- Promote the exact generated `ShowcaseWorld.bytes` and manifest from that successful artifact back to the feature branch, verifying their payload hash matches what passed CI; this is generated-output promotion only, with no source change.
- Human-review all approach/base/switchback/summit/dialogue captures. Only after all automated and visual gates pass: complete pending metadata, promote open→pending→closed, merge latest master, and non-force update master.

## Blast radius / cost
Changes stay bounded to reusable mountain realization, startup-bake proof, and opt-in SceneIssue replay. Expected landform program is ~63 primitives versus the shared 512-per-instance budget; added frustums affect one-time world build/bake only. Normal player speed, runtime movement semantics, and steady-state world truth are unchanged.
