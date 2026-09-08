# Plan

## Defect / acceptance
Downstream worldbuilding has no single provenance-backed visual source of truth for the Mounting Force towns, so current placeholder/generic medieval styling can drift into canon. Acceptance requires a frozen authoritative town roster, a pre-concept inventory of each town's named buildings/landmarks, a written brief per town, at least six production-quality environment concepts per town, and a game-wide comparison/anti-drift index.

Existing voxel town visuals are explicitly non-authoritative. This assignment is reference-content work only: no production scene, gameplay, character, or UI implementation is intended.

## Hypotheses
1. **Original-source identity is sufficient but modern art direction is missing.** The original `jlashmet/mounting-force` repository should establish town names, named places, relationships, and thematic cues; Project North Star then supplies the new visual language.
2. **A later source supersedes the original town canon.** `jlashmet/MountingForce2` could contain a newer roster or conflicting place definitions.
3. **Cambridge is fully recoverable from connected historical sources.** If not, only the coordinator's explicit water-town requirement is source-backed and all added Cambridge specifics must be labeled derived.

## Next discriminator / material results
Pin and inventory the original project manifest plus connected references, then compare the later repository. Results so far:
- Original source pinned at `f37512e0175f46e14ad2d7aeb5ecea290af14a14` contains dedicated settlement groups for Kentridge, Hightown, Moordell, Rossdam, Orc-Town, plus `fairy-village.tmx` and named interior maps.
- Original narrative explicitly calls Moordell a town and contrasts it with Kentridge/Hightown.
- `MountingForce2` pinned at `5e111d5a72e441824311b4a3a176f72e9c86f653` has no matching Cambridge/Kentridge/Hightown asset-path evidence, so supersession is currently disfavored.
- Cambridge remains coordinator-canonical but has not yet been recovered from the connected historical archive.

## Selected fix
Freeze the canonical roster and each town's named-place inventory before generating concepts. Separate historical facts from derived Project North Star choices. Then commit briefs, a comparison index, and the required environment concept set inside this SceneIssue only.

## Regression / built-scene evidence
The intended diff is docs/art/evidence/closure metadata only. Behavioral regression is therefore the final diff proving no executable/runtime or serialized scene path changed. `WorldbuildingGalleryShowcase` is context only; its existing town visuals are explicitly non-authoritative and final in-engine implementation is out of scope, so built-scene evidence is not applicable unless a runtime/scene file is unexpectedly touched. If that boundary changes, module-local regression plus exact-SHA built-player validation becomes mandatory.

## Remaining gates
Freeze roster and inventories; author briefs/index; generate and commit all required concept art; audit acceptance and diff scope; pass exact-SHA targeted CI; close open→closed with fixed metadata; merge current master; PR + auto-merge; monitor required `affected` gate until merged and closed issue is visible on master.
