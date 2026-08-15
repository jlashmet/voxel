from pathlib import Path
import subprocess

ROOT = Path('Packages/com.mountingforce.worldgen/Runtime/Voxel')
COMPAT = ROOT / 'KentridgeShapeProgramCompatibility.cs'
CORE = ROOT / 'KentridgeCombinedVoxelCatalogueCanonical.Core.cs'
MERGE = ROOT / 'KentridgeCombinedVoxelCatalogueCanonical.Merge.cs'

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
        end = (starts[n + 1] - 1) if n + 1 < len(starts) else len(content)
        raw = content[start:end]
        leading = len(raw) - len(raw.lstrip())
        args.append((raw.strip(), start + leading))
    return args


def find_calls(text):
    needle = 'Op(ShapeOp.Emit'
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
        assert close is not None, f'unterminated emit call at {start}'
        calls.append((start, open_paren, close))
        at = close + 1

changed_files = []
short_count = 0
canonical_count = 0

for path in sorted(ROOT.rglob('*.cs')):
    if path == COMPAT:
        continue
    text = path.read_text()
    calls = find_calls(text)
    if not calls:
        continue
    inserts = []
    for _, open_paren, close in calls:
        content = text[open_paren + 1:close]
        args = split_top_level(content)
        assert args, path
        op = args[0][0]
        assert op.startswith('ShapeOp.Emit'), (path, op)
        name = op.split('.')[-1]
        assert name in OPERANDS, (path, name)
        actual_operands = len(args) - 1
        canonical = OPERANDS[name]
        if actual_operands == canonical:
            canonical_count += 1
            continue
        assert actual_operands == canonical - 2, (
            f'{path}: {name} has {actual_operands} operands; expected {canonical} canonical '
            f'or {canonical - 2} legacy-short')
        last_arg_start = args[-1][1]
        absolute = open_paren + 1 + last_arg_start
        inserts.append(absolute)
        short_count += 1
    if inserts:
        for absolute in reversed(inserts):
            text = text[:absolute] + '0, 0, ' + text[absolute:]
        path.write_text(text)
        changed_files.append(str(path))

assert short_count > 0, 'No legacy short emit calls found; inventory assumption changed.'

# Verify all source-level emit-builder calls are canonical after transformation.
for path in sorted(ROOT.rglob('*.cs')):
    if path == COMPAT:
        continue
    text = path.read_text()
    for _, open_paren, close in find_calls(text):
        args = split_top_level(text[open_paren + 1:close])
        name = args[0][0].split('.')[-1]
        assert len(args) - 1 == OPERANDS[name], (path, name, len(args) - 1)

# Once all stages emit canonical programs, the combined catalogue must preserve bytes verbatim.
core = CORE.read_text()
old = '                    programs += KentridgeShapeProgramCompatibility.CanonicalLength(in stage);'
assert core.count(old) == 1
core = core.replace(old, '                    programs += stage.Program.Length;', 1)
CORE.write_text(core)

merge = MERGE.read_text()
old = '''                    int written = KentridgeShapeProgramCompatibility.CopyDefinition(
                        source.Program,
                        definition.ProgramOffset,
                        definition.ProgramLength,
                        target.Program,
                        programOffset,
                        definition.Name.ToString());
                    definition.ProgramOffset = programOffset;
                    definition.ProgramLength = written;
                    programOffset += written;
'''
new = '''                    for (int code = 0; code < definition.ProgramLength; code++)
                        target.Program[programOffset + code] =
                            source.Program[definition.ProgramOffset + code];
                    definition.ProgramOffset = programOffset;
                    programOffset += definition.ProgramLength;
'''
assert merge.count(old) == 1
merge = merge.replace(old, new, 1)
MERGE.write_text(merge)

# Delete the compatibility implementation and its Unity meta as one clean cutover.
assert COMPAT.exists()
subprocess.run(['git', 'rm', str(COMPAT), str(COMPAT) + '.meta'], check=True)

# No compatibility references may survive anywhere in the adapter source.
for path in ROOT.rglob('*.cs'):
    source = path.read_text()
    assert 'KentridgeShapeProgramCompatibility' not in source, path

print(f'Canonicalized {short_count} short emit calls; {canonical_count} calls were already canonical.')
print('Changed builder files:')
for p in changed_files:
    print('  ' + p)
