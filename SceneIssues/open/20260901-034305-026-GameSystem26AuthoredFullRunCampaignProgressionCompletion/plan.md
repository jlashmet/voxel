# 26 Authored full-run campaign progression & completion — implementation plan

## Acceptance / ownership

Complete the authored Kentridge -> Rorik/Moordell/Rossdam/Logan route, exactly-once System15 terminal/frontend aftermath, mid-run restore, shared multiplayer progression, and real milestone-driven built-player full-run evidence. Optional content is non-gating. Story consumes semantic facts; System11 owns objectives, System15 outcomes, and Systems16/14 persistence/restore. No parallel chapter authority, fake regions, alternate transport, or privileged progression shortcuts.

## Current evidence / independent required correction

Resume source `dd81b895811a168b9fbfadb8a5c993acb988d2fa` already includes master `356b2e0e4d2818901c73bbc6b1788f8d6850356d`. Local Git fetch fails DNS; connected GitHub reads/writes work. Historical exact-source module proof remains `86911d9dec109c588310754f28c7a5644aed687a`, request `57a0b1f00615d0901e2d25ee0d2216296b13f163`, run `33944532957`; earlier restore/Story runs are retained in tasks.md.

PR run `34007038175`, job `101416067096`, completed success on synthetic merge `2933925e86d83e324ec7f9ae42be78e35cb42440`. Artifact `9981558493` has 652 passed EditMode cases, no failures/skips, and a 30-second player run. Both captures show only **Loading Kentridge...**; final diagnostics retain `coverage=False`, `missingVisible=241`. Visual acceptance: **unacceptable**. Neither workflow success nor screenshot count proves playable readiness, full-run completion or performance. See `pr-evidence-34007038175.md`.

**H1 proven by logs/source:** PR selection omitted `Game.Story.Tests` and `Game.Composition.Kentridge.Tests`; neither asmdef declares a test marker recognized by unchanged `tools/select-tests.py`. Their test fixtures are absent from this run's execution log.

**H2 falsified for this run:** this was not merely missing log names after selected execution; both assemblies are absent from selection and the configured assembly list. Historical exact-module evidence is separate.

Selected T26-057: add standard `optionalUnityReferences: ["TestAssemblies"]` to the two existing editor-only asmdefs. Add a Python regression exercising the real selector with repository metadata for all-EditMode and each owning-runtime change. Require fail-before/pass-after selection and later exact Unity execution. Do not add registry arrays, alter selection policy, widen budgets, or claim unexecuted NUnit results.

Ownership: Story and Kentridge composition test assemblies, plus `tools/tests` selector regression. This changes test discovery only, not runtime/scene behavior; existing headless tests remain the appropriate local surface. No additional player scene is justified for assembly metadata.

## Remaining gates / cost

T26-021/022/044–046 remain blocked by macro-world's documentation-only deferral, not a validated implementation landing. System25 still needs production provider/topology/gameplay/recovery acceptance for T26-043; newer asynchronous formation code is not that proof. Preserve both owners' work.

Keep all original eight unchecked tasks open; add T26-057 for the demonstrated coverage defect. Submit corrected exact source only through `ci-test/fixes/agent-8`; preserve active requests. Increased test discovery has no gameplay cost and changes no runtime/capture budgets. PR #312 stays draft until full acceptance; only then close this assignment and promote by PR + auto-merge.
