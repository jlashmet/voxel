# CI operations

## First exact gate
Final source for the first closure attempt: `4ee3148f36c916928a1600aba9bb5a5f65408554`.

Exact targeted-CI request `22fe801f55fc6ac5ae65248d9d4260ae77f52ad8` was directly parented by that source and changed only `.github/test-request.json`. Workflow run `34204143335`, job `101989578869`, completed successfully on 2026-09-08. The source received `ci/single-test=success`.

Repository-derived validation planning audited the exact master→source diff as SceneIssue-local Markdown/SVG reference content only: `hasProductionChanges=false`, `hasValidationWork=false`, no affected modules, no required player validations, and no requested Unity test. The CI planner/tool regression suite executed 103 tests and passed. Artifact `single-test-34204143335` / ID `10050980169` was published with digest `sha256:e2cdcb925ccd74e96bf8fa657011720cb1865367dcacff9c4a643e54155cf01d`.

## Reopened consistency correction
Final PR review after that gate found an acceptance defect in two stale `art-direction/` summaries: they described an obsolete six-town draft, introduced uncorroborated Wharfington, and omitted Orc Village/Fairy Village. PR #335 was converted to draft before merge and the SceneIssue reopened. The successful first gate remains valid evidence for its tested SHA but does **not** validate the corrected final source.

## Final corrected exact gate
Final corrected feature source: `9c92a736d52c818ac951c29b468f70093b9a43ff`.

Exact targeted-CI request `a267944ed2790b484d6885f8374c2d4fe8293874` is directly parented by that source and changes only `.github/test-request.json`. Workflow run `34215812697`, job `102027158713`, completed successfully on 2026-09-08. The source received `ci/single-test=success`.

The exact corrected master→source diff was again classified as SceneIssue-local Markdown/SVG reference content only: `hasProductionChanges=false`, `hasValidationWork=false`, `modules=[]`, `playerValidations=[]`, and `tests=[]`. The planner/tool regression suite executed 103 tests and passed. No automatic module validation, requested Unity test, or standalone SceneIssue replay was applicable for this docs/art-only source-of-truth change.

Artifact `single-test-34215812697` / ID `10052390936` was published at 1552 bytes with digest `sha256:4ddcc097cdf401308c57ebd7ece3033c8d5b802153aa1b3f46e887b0ce24d48b`. This final gate validates the corrected seven-settlement roster package and supersedes the first gate for closure.
