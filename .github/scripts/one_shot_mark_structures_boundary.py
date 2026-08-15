from pathlib import Path

path = Path('docs/ARCHITECTURE_IMPLEMENTATION_PLAN.md')
text = path.read_text()

old_focus = '**Current focus:** Cutover 4 Structures — `PrimitiveRasteriser` / `FeatureGeneration` Storage write boundary  '
new_focus = '**Current focus:** Cutover 4 Structures — physical Api/Runtime split and canonical Kentridge shape encoding  '
assert text.count(old_focus) == 1
text = text.replace(old_focus, new_focus, 1)

items = [
    '`PrimitiveRasteriser` consumes Storage.Api only and preserves primitive ordering/surface/boundary semantics.',
    '`FeatureGeneration` consumes the Storage.Api authoring capability rather than `RegionTable`/`BrickPool`.',
    'feature generation/rasterisation hash/count parity remains identical;',
]
for item in items:
    old = '- [ ] ' + item
    new = '- [x] ' + item
    assert text.count(old) == 1, item
    text = text.replace(old, new, 1)

accepted_anchor = '- [x] Full-cell mutation/read parity slice accepted by CI: 374 total / 359 passed / exact 15 baseline failures.\n'
assert text.count(accepted_anchor) == 1
text = text.replace(
    accepted_anchor,
    accepted_anchor
    + '- [x] Feature rasterisation/generation Storage.Api boundary accepted by CI: 377 total / 362 passed / exact 15 baseline failures.\n',
    1,
)

import re
pattern = re.compile(r'- Latest accepted code gate before this status update: `[^`]+` — \d+ tests, \d+ passed, exactly 15 known baseline failures\.')
match = pattern.search(text)
assert match, 'latest accepted gate line not found'
text = text[:match.start()] + (
    '- Latest accepted code gate: `f63d5be6e92d6b91862b9ac4cf539ecfdde85b18` — '
    '377 tests, 362 passed, exactly 15 known baseline failures.'
) + text[match.end():]

path.write_text(text)
