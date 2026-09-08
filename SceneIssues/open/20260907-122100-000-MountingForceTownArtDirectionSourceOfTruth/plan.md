# Plan

## Defect / acceptance
Downstream worldbuilding lacked one provenance-backed visual source of truth, allowing generic placeholder styling to drift into canon. Acceptance requires a frozen authoritative settlement roster, pre-concept named-place inventory, written brief per town, six environment concepts per town, and a game-wide comparison/anti-drift reference.

Existing voxel-town visuals are explicitly non-authoritative. This assignment changes reference docs/art only; no final in-engine town implementation, gameplay, character, or UI work is in scope.

## Competing hypotheses / discriminators
1. **Original-source identity is authoritative but modern art direction is missing.** `jlashmet/mounting-force` should establish settlement names/functions; Project North Star supplies the new visual language.
2. **A later source supersedes that canon.** `jlashmet/MountingForce2` could replace the roster/places.
3. **Count/category-complete concept sheets are sufficient.** Falsified if they read as schematic/blockout art rather than implementation-guiding concepts.
4. **All final roster-bearing documents agree.** Falsified by a cross-document audit showing any convenience summary adds/removes a frozen-roster settlement.

Pinned source comparison supports hypothesis 1: the original project at `f37512e0175f46e14ad2d7aeb5ecea290af14a14` establishes Kentridge, Hightown, Moordell, Rossdam, Orc Village/Orc-Town and Fairy Village; the later repo at `5e111d5a72e441824311b4a3a176f72e9c86f653` does not supersede them. Cambridge is coordinator-canonical only; its water-town requirement is source-backed by this SceneIssue and added venue names are labeled derived. Initial schematic studies were rejected, so category count alone is falsified.

Final PR review falsified hypothesis 4 for the first closure attempt: two stale `art-direction/` sidecars still described an obsolete six-town Mountain Forest draft, introduced uncorroborated Wharfington, and omitted Orc Village/Fairy Village. PR #335 was stopped as a draft before merge. The authoritative source/provenance and seven actual concept folders agree, so the sidecars were stale summaries rather than competing evidence.

## Selected fix / material result
Keep the seven-settlement canonical source and concept packs. Rewrite the two stale sidecars as explicitly secondary indexes that mirror the same seven-settlement roster, exclude Wharfington for lack of corroboration, and link to primary/per-town authority instead of maintaining a divergent duplicate canon.

The correction is complete. `art-direction/index.md`, `art-direction/town-briefs.md`, `source-evidence.md`, `experiment-001-source-roster.md`, `town-art-direction-source-of-truth.md`, `concept-catalog.md`, `visual-review.md`, `acceptance-audit.md`, and the actual `towns/` directory now agree on exactly seven settlements: Kentridge, Hightown, Moordell, Rossdam, Orc Village/Orc-Town, Fairy Village, and Cambridge. Wharfington is consistently identified only as an excluded/unverified lead.

## Regression / built-scene evidence
The master→feature diff remains SceneIssue-local Markdown/SVG reference content only. The prior exact gate proved this classifies as `hasProductionChanges=false` / `hasValidationWork=false`; therefore there is no executable/runtime owner to regress and built-player evidence remains **not applicable** under `issue-readme.md`. `WorldbuildingGalleryShowcase` is context only and explicitly non-authoritative for visual acceptance.

## Current state / remaining gates
The first exact source `4ee3148f36c916928a1600aba9bb5a5f65408554` passed request `22fe801f55fc6ac5ae65248d9d4260ae77f52ad8` / workflow `34204143335`, but that result predates the required consistency correction and is not the final-source gate. The SceneIssue is reopened and the roster consistency correction/audit is complete. Remaining gates are: submit the corrected exact feature SHA through `ci-test/fixes/agent-2`, inspect/record that exact result, complete closure bookkeeping directly `open→closed`, reconcile current `origin/master`, mark PR #335 ready, enable auto-merge, and require the repository PR gate through actual merge.
