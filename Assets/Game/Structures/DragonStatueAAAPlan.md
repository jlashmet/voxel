# Dragon Statue AAA Iteration Plan

## Goal

Make Model Viewer **Dragon A** visually match the established studio-reference dragon at a professional showcase bar using the authoritative 10 cm voxel grid and the normal production surface renderer, with the exact same authored object used by World Builder placement.

## Reference read

The target is a powerful seated/crouched dragon with a diagonal body gesture, graceful S-neck, low angular skull, integrated layered crown horns, open mouth, broad overlapping chest armor, compact articulated limbs, four separated claws per foot, a controlled foreground tail, and two huge raised bat wings with curved leading edges, depth sweep, structural fingers, warm membranes, and broad shallow scallops. The silhouette must feel elegant and predatory rather than upright, goat-like, gorilla-like, snake-tailed, curtain-winged, or assembled from primitive blobs.

## Hard constraints

- Authoritative geometry remains canonical CPU-authored voxel cells; no mesh renderer or GPU-derived authoritative state.
- Shape construction remains deterministic implicit/SDF-style authoring sampled into the canonical 10 cm voxel grid.
- Model Viewer renders through the normal production voxel surface path.
- Model Viewer Dragon A and the World Builder dragon object invoke the same authoring entry point.
- Every visual pass is judged from a fresh production capture artifact, not source-code intent.
- The production visual gate captures hero, opposite three-quarter, and rear/wing views. One flattering angle cannot approve the model.
- Do not accept a pass merely because it is recognizable as a dragon.

## Acceptance criteria

- [ ] Head reads as a dragon at thumbnail scale: compact wedge muzzle, open jaw, brows/eyes, layered cheek fins, and integrated swept crown horns with no goat/antler read.
- [ ] Neck has a graceful S-curve and broad shoulder transition rather than a vertical tube.
- [ ] Torso is crouched/diagonal and chest-to-pelvis masses read anatomically rather than as one vertical sausage.
- [ ] Forelimbs visibly bend at shoulder/elbow/wrist, stay compact, and end in four separated long claws without hanging-column/gorilla proportions.
- [ ] Rear legs/haunches carry the seated weight; hocks and feet tuck under the body and four toes read cleanly.
- [ ] Both wings meaningfully frame the hero silhouette, sweep in depth, and have arched leading edges, curved structural fingers, broad warm membranes, and shallow scallops without flat panels, torn sheets, or dangling wires.
- [ ] Tail is thick only at the root, forms one elegant open foreground sweep, tapers continuously, shows armor/ridge detail, and never dominates the composition or leaves detached tip fragments.
- [ ] Ventral armor reads as 5–7 large overlapping shields, not ribs or a striped turtle belly.
- [ ] Surface detail includes readable scale/plate/spine hierarchy at 10 cm without masonry noise.
- [x] Model Viewer Dragon A is authored through `DragonStatueWorldBuilderObject` -> `DecorationVoxelStampBackend` -> `DragonStatueDetailedVoxelAuthoring`.
- [x] Production path converges with no missing visible chunks for V5, V6, V7 and V8 targeted runs.
- [ ] No detached digits, floating remnants, isolated strips, clear/rebuild seams, or hidden rear-view failures in any capture angle.
- [ ] Hero framing contains the full silhouette with intentional breathing room.

## Iteration log

- [x] Early primitive/reference-voxel attempts rejected for crocodilian/goat-like head, blunt feet, ribbed chest, weak tail and rectangular membranes.
- [x] V3/V4/V5 progressively corrected the original authoring but accumulated inherited anatomy. V5 production run `32797911327` was technically green and visually rejected.
- [x] V6 rebuilt head/neck/wings/tail/distal limbs and unified the actual World Builder path with Model Viewer. Production run `32798951633` was green but still visibly gorilla-like with a curtain wing, antler crown and damaged silhouette.
- [x] Strategy correction: stopped composing V3-V6 and created clean V7 from empty state.
- [x] V7 production run `32799735270`: **major improvement but rejected**. It finally reads as one coherent authored dragon with attached claws, warm membrane and substantial tail. Against the reference it remains far below AAA: visible wing is flat and toothy; far wing almost disappears; front legs hang too straight/heavy; rear stance sprawls; tail is oversized and snake-smooth; crown horns remain goat-like; chest plates are dark/striped; detached ground fragment remains.
- [x] Added multi-angle production acceptance (`dragon-a-detailed`, `dragon-a-opposite`, `dragon-a-rear`) specifically to expose hidden joins and rear-view cheats.
- [x] Removed abandoned temporary mesh-bake workflow/tool so it no longer forces every dragon edit into all seven affected-test assemblies.
- [x] V8 targeted-form correction production run `32800291348`: **rejected, including from new multi-angle evidence**. Hero wing scallops improve, but the model still has an upright sausage torso, hanging-column forelimbs, torn-panel far wing, goat/dog head, oversized loop tail, weak rear anatomy and the detached fragment. Rear capture exposes giant haunch/tail-root blobs and wing fingers that read as wires. V8 proves V7's base proportions are still wrong; more regional surgery would be wasteful.
- [x] Strategy correction: V9 is another clean-from-empty rebuild and is now the sole production sculpt. V7/V8 remain historical only.
- [x] V9 changes: lower diagonal torso; smaller/tucked limbs; shorter integrated horns; larger wedge skull; symmetric huge wings swept strongly backward in Z; scallops constructed as shallow five-point concave arcs rather than carved holes; slimmer non-looping tail with connected tip; broad warm ventral shields; explicit secondary scales and leading-edge armor.
- [ ] Capture V9 through all three production review angles and perform harsh comparison against the reference.
- [ ] If V9 primary anatomy/silhouette passes, tune camera composition and then add tertiary scale, membrane, horn and weathering polish.
- [ ] If V9 still has structural failures, replace the failed primary form rather than decorating it.
- [ ] Final cleanup: remove historical dead authoring passes no longer needed, run targeted production visual gate, affected CI, architecture boundary gate, and inspect final PR diff.
