from pathlib import Path

path = Path('.github/scripts/one_shot_canonicalize_kentridge_shape_programs.py')
source = path.read_text()
old = "subprocess.run(['git', 'rm', str(COMPAT), str(COMPAT) + '.meta'], check=True)"
new = "subprocess.run(['git', 'rm', str(COMPAT)], check=True)\nmeta = Path(str(COMPAT) + '.meta')\nif meta.exists():\n    subprocess.run(['git', 'rm', str(meta)], check=True)"
assert source.count(old) == 1
exec(compile(source.replace(old, new, 1), str(path), 'exec'))
