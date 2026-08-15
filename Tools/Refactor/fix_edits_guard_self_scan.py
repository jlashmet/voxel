from pathlib import Path


def replace_exact(path, old, new):
    p=Path(path)
    text=p.read_text()
    count=text.count(old)
    if count != 1:
        raise RuntimeError(f'{path}: expected 1 match, found {count}: {old[:140]!r}')
    p.write_text(text.replace(old,new))

OLD='Assets/Tests/EditMode/EditsStorageBoundaryTests.cs'
NEW='Assets/Tests/EditMode/EditsMutationCallerGuardTests.cs'

replace_exact(
    OLD,
    '            foreach (string scanRoot in roots)\n            foreach (string path in Directory.EnumerateFiles(scanRoot, "*.cs", SearchOption.AllDirectories))\n            {\n                string source = StripComments(File.ReadAllText(path));',
    '            foreach (string scanRoot in roots)\n            foreach (string path in Directory.EnumerateFiles(scanRoot, "*.cs", SearchOption.AllDirectories))\n            {\n                if (Path.GetFileName(path) == nameof(EditsStorageBoundaryTests) + ".cs")\n                    continue; // The guard necessarily contains the forbidden literals it searches for.\n\n                string source = StripComments(File.ReadAllText(path));')

replace_exact(
    NEW,
    '            foreach (string scanRoot in roots)\n            foreach (string path in Directory.EnumerateFiles(scanRoot, "*.cs", SearchOption.AllDirectories))\n            {\n                string source = File.ReadAllText(path);',
    '            foreach (string scanRoot in roots)\n            foreach (string path in Directory.EnumerateFiles(scanRoot, "*.cs", SearchOption.AllDirectories))\n            {\n                string fileName = Path.GetFileName(path);\n                if (fileName == nameof(EditsMutationCallerGuardTests) + ".cs" ||\n                    fileName == nameof(EditsStorageBoundaryTests) + ".cs")\n                    continue; // Guard definitions necessarily contain the signatures they prohibit.\n\n                string source = File.ReadAllText(path);')

print('Edits guard self-scan exclusions applied.')
