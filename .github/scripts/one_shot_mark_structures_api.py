from pathlib import Path

path = Path('docs/ARCHITECTURE_IMPLEMENTATION_PLAN.md')
text = path.read_text()

old_focus = '**Current focus:** Cutover 4 Structures — physical Api/Runtime split and canonical Kentridge shape encoding  '
new_focus = '**Current focus:** Cutover 4 Structures — canonical Kentridge shape encoding / compatibility seam deletion  '
assert text.count(old_focus) == 1
text = text.replace(old_focus, new_focus, 1)

old_row = '| 4 — Structures | **In progress — current** | full-cell Storage mutation/read parity accepted, including authored boundary on empty cells | migrate `PrimitiveRasteriser`/`FeatureGeneration`, then Structures.Api/Runtime split and Kentridge canonical shape encoding |'
new_row = '| 4 — Structures | **In progress — current** | Storage authoring boundary + `Structures.Api` extraction accepted; canonical authoring/encoding contracts now live in `VoxelEngine.Structures.Api` | delete Kentridge compatibility encoding, migrate Runtime dependencies, then move Structures.Runtime |'
assert text.count(old_row) == 1
text = text.replace(old_row, new_row, 1)

anchor = '- [x] `FeatureGeneration` consumes the Storage.Api authoring capability rather than `RegionTable`/`BrickPool`.\n'
assert text.count(anchor) == 1
text = text.replace(
    anchor,
    anchor
    + '- [x] Structures.Api extracted with canonical `VoxelEngine.Structures.Api` namespace; authoring contracts moved with Unity GUIDs preserved.\n'
    + '- [x] `CatalogueLoader` clean-renamed to `FeatureCatalogueBuilder`; no compatibility alias remains.\n'
    + '- [x] Structures.Api extraction accepted by CI: 379 total / 364 passed / exact 15 baseline failures.\n',
    1,
)

old_gate = '- [ ] Kentridge catalogue builders compile against Structures.Api only;'
new_gate = '- [x] Kentridge catalogue builders compile against Structures.Api for extracted authoring contracts;'
assert text.count(old_gate) == 1
text = text.replace(old_gate, new_gate, 1)

old_latest = '- Latest accepted code gate: `f63d5be6e92d6b91862b9ac4cf539ecfdde85b18` — 377 tests, 362 passed, exactly 15 known baseline failures.'
new_latest = '- Latest accepted code gate: `d040d3182ef636016578321bbd90870680f817ac` — 379 tests, 364 passed, exactly 15 known baseline failures.'
assert text.count(old_latest) == 1
text = text.replace(old_latest, new_latest, 1)

path.write_text(text)
