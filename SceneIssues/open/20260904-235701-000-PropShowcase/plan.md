# PropShowcase plan

## Acceptance and ownership
Browse every independently previewable production prop exactly once, render its production realization, retire prior state, and prove useful framing/materials/contact plus bounded switching through exact standalone-player evidence. Only production-quality visuals pass; no gate or checkbox is waived.

Canonical set remains 529 entries: 440 registered decorations, 25 presets, 8 mine-cave kinds, 8 natural-cave kinds, 48 world-object kinds. Structures owns enumeration and shared presenters; SceneRuntime owns the browser/resource probe; Materials owns the procedural-material adapter. Each runtime owner has a module-local validation surface. Parent/top-level showcase scenes remain integration consumers only.

## Current source and exact failures
Current implementation is `2b7e30e1efb3dc8d63a82923ca42101f7ab36a9f`, built on compile-ownership repair `2154840b`, trapdoor mount repair `849875e3`, material ownership `9697d365`, resource instrumentation `36141bae`, and PlayMode-process isolation `79b6a2f4`. Latest observed master is `356b2e0e4d2818901c73bbc6b1788f8d6850356d`; final master merge is outstanding.

Old request `e83a7fd8` / run `34003328146` failed required PlayMode orchestration but supplied accepted material-mode evidence and rejected blockout visuals. Request `57ab96ca` / run `34007356710` then failed compilation before any Unity tests/player: unsupported `NonParallelizable` plus production SceneRuntime referencing a validation-assembly helper. `2154840b` fixes both; exact details are in `review-34007356710.md`.

## Selected visual fixes
The rejected relationships are addressed only through shared production paths: painting-family thin surfaces receive reusable raised frame/emblem geometry; Door/SecretDoor/Trapdoor use normalized generated mechanism-panel meshes in `UnityWorldObjectPresentationSink`; decoration effect hooks now have a reusable light/particle presenter; Trapdoor retains its corrected horizontal baseline. Voxel-backed selections now remain `LOADING` until the real surface reports complete publication, then transition to `READY`.

Forge Hearth still uses its canonical voxel geometry; the new semantic light/particle presentation is the next discriminator. Fresh screenshots decide whether geometry itself remains below bar—do not broaden that emitter without evidence.

## Remaining gates
Run the latest exact SHA through `ci-test/fixes/agent-9`; inspect all module-local and SceneIssue player captures directly. Verify sign finish, Door/Trapdoor construction, hearth/effects, initial loading/readiness, tiny/large/ceiling/procedural framing, and three-cycle resource measurements. Fix only demonstrated remaining failures. After every task/acceptance item passes, complete issue metadata, move open→closed, merge current master into the feature branch, open the final PR, enable auto-merge, and monitor `affected` until closed state is visible on master.
