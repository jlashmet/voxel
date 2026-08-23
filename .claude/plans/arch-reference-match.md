# Arch reference match

## Goal
Make `Assets/Scenes/ArchLookdev.unity` visually match the version-controlled `References/arch_reference.png` as closely as practical using the production voxel rendering path, while fixing real geometry/rendering defects rather than hiding them with camera tricks.

## Scope
- Ensure the ArchLookdev/ArchDev CI capture artifact contains both the generated scene captures and the version-controlled reference PNG so connector-only agents can inspect both images.
- Use those artifact images as the authoritative visual iteration loop.
- Fix the remaining disconnected/missing arch geometry visible from front/back/oblique views.
- Improve moss/growth so it resembles the reference rather than uniform/jagged coating noise.
- Preserve the recently-fixed smooth intrados and existing architecture boundaries.

## Constraints
- Follow root `AGENTS.md`, `CLAUDE.md`, and referenced architecture constraints.
- Feature branch: `agent/arch-reference-match`.
- CI request branch: `ci-test/agent/arch-reference-match` only; reuse it for every iteration.
- A requested single test must complete in under 5 minutes after its job starts.
- Do not make authoritative/gameplay state depend on GPU output or floating-point presentation data.
- Prefer proven geometry/sampling invariants over visual masking.

## Acceptance criteria
- [ ] CI artifact for the ArchLookdev capture includes `References/arch_reference.png` alongside generated screenshots/settings.
- [ ] Reference and generated screenshots can both be downloaded and visually inspected from the CI artifact.
- [ ] Front view has no visually disconnected arch/profile piece.
- [ ] Back/oblique view has no missing/disconnected arch surface or obvious staircase defect attributable to a known field/composition bug.
- [ ] Smooth intrados regression remains fixed.
- [ ] Moss/growth is visually coherent and substantially closer to the reference; avoid uniform voxel fuzz.
- [ ] Relevant targeted CI is green, with no zero-test false success.
- [ ] Final diff reviewed; CI request branch deleted after validation.

## Task list
- [x] Read `AGENTS.md` and `CLAUDE.md`.
- [x] Create the fixed feature branch from current master (`bf7d125`).
- [x] Confirm ArchLookdev now loads `References/arch_reference.png` from version control.
- [x] Locate the ArchDev/ArchLookdev build/capture test and artifact publisher.
- [x] Add the reference PNG to the artifact without changing the production scene just to expose the image.
- [ ] Publish a temporary hosted reference-only evidence artifact while the self-hosted Mac is queued; remove this helper before final validation.
- [ ] Run the smallest capture test via `ci-test/agent/arch-reference-match` and inspect the artifact.
- [ ] Record concrete visual deltas versus reference.
- [ ] Diagnose and fix the first proven geometry/rendering cause.
- [ ] Re-run capture and inspect from front/back/oblique views.
- [ ] Diagnose moss path; decide whether coating-only, instanced vegetation, or hybrid best matches the reference while remaining capturable.
- [ ] Implement and tune moss/growth.
- [ ] Re-run visual capture loop until remaining differences are minor or a concrete blocker is documented.
- [ ] Run appropriate regression test(s), remove temporary evidence plumbing, review final diff, and delete CI request branch.

## Findings / evidence
- Master `bf7d125` changed `ArchLookdev.LoadTargetImage()` to try `References/arch_reference.png` first, so fresh checkouts can load the reference.
- Prior smoothing commits fixed the front intrados by aligning authored radial distance with occupancy and trusting authored samples on sign agreement. The commit notes explicitly say the oblique soffit still has a separate cause.
- Current moss in ArchLookdev is authored primarily as `Coatings.Moss` plus coating decoration; the engine also has an instanced `VegetationKind.Moss` renderer that supports arbitrary surface normals. The real-player Arch capture is driven by `RealPlayerScreenshotFallback`/`ScreenCapture`, so presented-frame vegetation will be capturable there.
- `Assets/Tests/PlayMode/ArchLookdevSceneTests.cs` is the explicit visual acceptance entry point. The single-test workflow invokes `tools/showcase-player-capture.sh`, which maps this test to `Assets/Scenes/ArchLookdev.unity`, builds a standalone macOS player, and captures presented frames every 10 seconds for 30 seconds.
- The visual acceptance now copies `References/arch_reference.png` to `Artifacts/SingleTest/RealPlayer/Reference/arch_reference.png`. The workflow uploads `Artifacts/SingleTest/**`, so the reference should be delivered beside `RealPlayer/Screenshots/**` once a run reaches artifact upload.
- CI run `32611015737` started for the ArchLookdev request but the pre-test Unity-idle guard failed after 60 seconds because another Unity editor was already running. A newer request correctly cancelled that run. Current request commit `6721152` remains queued with no classic status, so it has not started and cannot yet count as validation evidence.
- Direct repository binary access is unavailable in this connector session: contents/blob/raw routes expose metadata or reject non-UTF-8 bytes. A temporary GitHub-hosted artifact is therefore justified only to inspect the tracked reference while the authoritative self-hosted player run is queued; it must not substitute for the player capture or remain in the final diff.
- Deterministic inspection of the default Arch authoring rules shows all 13 structural voussoirs are face-connected to neighbors and the spring wedges connect to the piers. The visible disconnect is therefore not a literal structural occupancy break; rendered boundary/profile composition remains the relevant subsystem.
- The retained profile centroid-backing check was tested analytically against the default profile dimensions and does not reject the default front/radial segments, so changing that guard speculatively is not justified.
- The earlier `ArchCapLayerDiagnosticTests` fixture placed the pre-fix oblique defect in authored field composition rather than the Transvoxel mesher. Later radial/sign fixes changed that field, so this remains a subsystem lead rather than a proven current cause until the new artifact locates the remaining defect.
