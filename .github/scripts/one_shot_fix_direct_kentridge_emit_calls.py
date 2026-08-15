from pathlib import Path

ROOT = Path('Packages/com.mountingforce.worldgen/Runtime/Voxel')
OPERANDS = {
    'EmitBox': 10,
    'EmitCylinder': 10,
    'EmitPrism': 11,
    'EmitCapsule': 11,
    'EmitRamp': 11,
    'EmitRoundedBox': 11,
    'EmitEllipsoid': 10,
    'EmitFrustum': 11,
    'EmitAnnulus': 12,
    'EmitArcWedge': 15,
}


def split_top_level(content):
    starts = [0]
    depth = 0
    in_string = False
    escape = False
    for i, ch in enumerate(content):
        if in_string:
            if escape:
                escape = False
            elif ch == '\\':
                escape = True
            elif ch == '"':
                in_string = False
            continue
        if ch == '"':
            in_string = True
        elif ch in '([{':
            depth += 1
        elif ch in ')]}':
            depth -= 1
        elif ch == ',' and depth == 0:
            starts.append(i + 1)
    args = []
    for n, start in enumerate(starts):
        end = starts[n + 1] - 1 if n + 1 < len(starts) else len(content)
        raw = content[start:end]
        leading = len(raw) - len(raw.lstrip())
        args.append((raw.strip(), start + leading))
    return args


def find_direct_emit_calls(text):
    needle = 'Emit(ShapeOp.Emit'
    at = 0
    calls = []
    while True:
        start = text.find(needle, at)
        if start < 0:
            return calls
        open_paren = text.find('(', start)
        depth = 0
        in_string = False
        escape = False
        close = None
        for i in range(open_paren, len(text)):
            ch = text[i]
            if in_string:
                if escape:
                    escape = False
                elif ch == '\\':
                    escape = True
                elif ch == '"':
                    in_string = False
                continue
            if ch == '"':
                in_string = True
            elif ch == '(':
                depth += 1
            elif ch == ')':
                depth -= 1
                if depth == 0:
                    close = i
                    break
        assert close is not None, f'unterminated direct Emit call at {start}'
        calls.append((open_paren, close))
        at = close + 1

changed = []
patched_one_short = 0
patched_two_short = 0
canonical = 0

for path in sorted(ROOT.rglob('*.cs')):
    text = path.read_text()
    calls = find_direct_emit_calls(text)
    if not calls:
        continue
    inserts = []
    for open_paren, close in calls:
        args = split_top_level(text[open_paren + 1:close])
        op = args[0][0]
        name = op.split('.')[-1]
        assert name in OPERANDS, (path, name)
        actual = len(args) - 1
        expected = OPERANDS[name]
        if actual == expected:
            canonical += 1
            continue
        last_arg_start = args[-1][1]
        absolute = open_paren + 1 + last_arg_start
        if actual == expected - 1:
            inserts.append((absolute, '0, '))
            patched_one_short += 1
        elif actual == expected - 2:
            inserts.append((absolute, '0, 0, '))
            patched_two_short += 1
        else:
            raise AssertionError(
                f'{path}: direct {name} has {actual} operands; expected {expected}, '
                f'{expected - 1}, or {expected - 2}')
    if inserts:
        for absolute, insertion in reversed(inserts):
            text = text[:absolute] + insertion + text[absolute:]
        path.write_text(text)
        changed.append(str(path))

assert patched_one_short + patched_two_short > 0, 'Expected at least one noncanonical direct Emit call.'

# Every direct builder call must now be canonical.
for path in sorted(ROOT.rglob('*.cs')):
    text = path.read_text()
    for open_paren, close in find_direct_emit_calls(text):
        args = split_top_level(text[open_paren + 1:close])
        name = args[0][0].split('.')[-1]
        assert len(args) - 1 == OPERANDS[name], (path, name, len(args) - 1)

print(f'Patched one-short direct emits: {patched_one_short}')
print(f'Patched two-short direct emits: {patched_two_short}')
print(f'Already canonical direct emits: {canonical}')
print('Changed files:')
for item in changed:
    print('  ' + item)
