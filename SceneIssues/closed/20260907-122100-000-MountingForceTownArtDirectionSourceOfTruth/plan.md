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

Final PR review falsified hypothesis 4 for the first closure attempt: two stale `art-direction/` sidecars still described an obsolete six-town Mountain Forest draft, introduced uncorroborated Wharfington, and omitted Orc Village/Fairy Village. PR #335 was stopped as a draft before merge. The authoritative source/provenance and seven actual concept folders agreed, so the sidecars were stale summaries rather than competing evidence.

## Selected fix / material result
Keep the seven-settlement canonical source and concept packs. Rewrite the two stale sidecars as explicitly secondary indexes that mirror the same seven-settlement roster, exclude Wharfington for lack of corroboration, and link to primary/per-town authority instead of maintaining a divergent duplicate canon.

The correction is complete. `art-direction/index.md`, `art-direction/town-briefs.md`, `source-evidence.md`, `experiment-001-source-roster.md`, `town-art-direction-source-of-truth.md`, `concept-catalog.md`, `visual-review.md`, `acceptance-audit.md`, and the actual `towns/` directory now agree on exactly seven settlements: Kentridge, Hightown, Moordell, Rossdam, Orc Village/Orc-Town, Fairy Village, and Cambridge. Wharfington is consistently identified only as an excluded/unverified lead.

## Regression / built-scene evidence
The final exact source remains SceneIssue-local Markdown/SVG reference content only. Exact-SHA CI classified it as `hasProductionChanges=false`, `hasValidationWork=false`, with no affected modules, player validations, or Unity tests. There is therefore no executable/runtime owner to regress and built-player evidence is **not applicable** under `issue-readme.md`. `WorldbuildingGalleryShowcase` is context only and explicitly non-authoritative for visual acceptance.

## Final validation / closure
Corrected exact source `9c92a736d52c818ac951c29b468f70093b9a43ff` passed request `a267944ed2790b484d6885f8374c2d4fe8293874` in workflow run `34215812697` / job `102027158713`. The planner/tool regression suite ran 103 tests successfully; artifact `single-test-34215812697` / ID `10052390936` was published with digest `sha256:4ddcc097cdf401308c57ebd7ece3033c8d5b802153aa1b3f46e887b0ce24d48b`, and the source received `ci/single-test=success`.

All issue-work checklist items and acceptance criteria are complete. This closure transaction records the final evidence and moves the SceneIssue directly `open→closed`. Post-closure integration is PR #335: confirm current `origin/master` is reconciled, mark ready, enable auto-merge, and require the repository `affected` gate through actual merge.
