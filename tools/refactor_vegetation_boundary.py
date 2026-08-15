from pathlib import Path
import json
import re
import uuid

ROOT = Path('.')
VEG = ROOT / 'Assets/VoxelEngine/Vegetation'
RUNTIME = VEG / 'Runtime'
API = VEG / 'Api'


def simple_meta(path: Path):
    path.write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n')


def folder_meta(path: Path):
    path.write_text(
        f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n'
        '  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


def ensure_using(source: str, using_line: str) -> str:
    if using_line in source:
        return source
    lines = source.splitlines(True)
    insert = 0
    for i, line in enumerate(lines):
        if line.startswith('using '):
            insert = i + 1
    lines.insert(insert, using_line + '\n')
    return ''.join(lines)


def update_refs(path: Path, remove=(), add=()):
    data = json.loads(path.read_text())
    refs = [x for x in data.get('references', []) if x not in remove]
    for item in add:
        if item not in refs:
            refs.append(item)
    data['references'] = refs
    path.write_text(json.dumps(data, indent=2) + '\n')


# --- physical Runtime move, GUIDs preserved ---
RUNTIME.mkdir(parents=True, exist_ok=True)
if not (VEG / 'Runtime.meta').exists():
    folder_meta(VEG / 'Runtime.meta')

for name in ('ProceduralTreeDamageService', 'ProceduralTreeSkeletonBuilder', 'TreeWorldState'):
    old = VEG / f'{name}.cs'
    new = RUNTIME / f'{name}.cs'
    old_meta = VEG / f'{name}.cs.meta'
    new_meta = RUNTIME / f'{name}.cs.meta'
    if not old.exists():
        raise SystemExit(f'missing source for Runtime move: {old}')
    old.rename(new)
    old_meta.rename(new_meta)

old_asm = VEG / 'VoxelEngine.Vegetation.asmdef'
old_asm_meta = VEG / 'VoxelEngine.Vegetation.asmdef.meta'
rt_asm = RUNTIME / 'VoxelEngine.Vegetation.Runtime.asmdef'
rt_asm_meta = RUNTIME / 'VoxelEngine.Vegetation.Runtime.asmdef.meta'
old_asm.rename(rt_asm)
old_asm_meta.rename(rt_asm_meta)
asm = json.loads(rt_asm.read_text())
asm['name'] = 'VoxelEngine.Vegetation.Runtime'
asm['rootNamespace'] = 'VoxelEngine.Vegetation.Runtime'
asm['references'] = ['VoxelEngine.Vegetation.Api', 'Unity.Mathematics']
asm['autoReferenced'] = False
rt_asm.write_text(json.dumps(asm, indent=2) + '\n')

for name in ('ProceduralTreeDamageService', 'ProceduralTreeSkeletonBuilder', 'TreeWorldState'):
    p = RUNTIME / f'{name}.cs'
    s = p.read_text().replace('namespace VoxelEngine.Core.Vegetation\n',
                              'namespace VoxelEngine.Vegetation.Runtime\n', 1)
    p.write_text(s)

# --- skeleton implementation produces immutable Api snapshots ---
p = RUNTIME / 'ProceduralTreeSkeletonBuilder.cs'
s = p.read_text()
start = s.index('    public struct TreeBranchSegment')
end = s.index('    public static class ProceduralTreeSkeletonBuilder')
build_state = '''    internal sealed class TreeSkeletonBuildState\n    {\n        public readonly List<TreeBranchSegment> Branches = new(256);\n        public readonly List<TreeLeafAnchor> Leaves = new(768);\n        public TreeSpeciesProfile Profile;\n        public float Height;\n        public int[] BranchParents;\n        public int[] LeafParents;\n    }\n\n'''
s = s[:start] + build_state + s[end:]
s = re.sub(r'\bProceduralTreeSkeleton\b', 'TreeSkeletonBuildState', s)
s = s.replace('public static TreeSkeletonBuildState Generate(in TreeInstance instance)',
              'public static TreeSkeletonSnapshot Generate(in TreeInstance instance)', 1)
marker = '            ResolveTopology(skeleton);\n            return skeleton;'
replacement = '''            ResolveTopology(skeleton);\n            return new TreeSkeletonSnapshot(\n                skeleton.Branches.ToArray(), skeleton.Leaves.ToArray(), skeleton.Profile,\n                skeleton.Height, skeleton.BranchParents, skeleton.LeafParents);'''
if marker not in s:
    raise SystemExit('missing skeleton return marker')
s = s.replace(marker, replacement, 1)
a = s.index('        public static void ResolveRemovedBranches(')
b = s.index('        private static void GrowBranch(', a)
s = s[:a] + s[b:]
# Contains was used only by the removed topology helpers.
s = re.sub(r'\n        private static bool Contains\(IReadOnlyCollection<int> values, int value\)\n        \{.*?\n        \}\n',
           '\n', s, flags=re.S)
p.write_text(s)

# --- mutable world state is Runtime-internal; public Runtime facade is composition-only ---
p = RUNTIME / 'TreeWorldState.cs'
s = p.read_text()
a = s.index('    public readonly struct TreeBranchCutEvent')
b = s.index('    /// <summary>\n    /// Authoritative semantic state', a)
s = s[:a] + s[b:]
a = s.index('        public struct TreeDamageState')
b = s.index('        private const float RootQuantizationMetres', a)
s = s[:a] + s[b:]
s = s.replace('public static class TreeWorldState', 'internal static class TreeWorldState', 1)
s = s.replace('new TreeDamageState\n                    {\n                        FoliageHealth = 1f,\n                        Severed = false,\n                    }',
              'new TreeDamageState(1f, false)')
s = s.replace('new TreeDamageState\n            {\n                FoliageHealth = nextFoliageHealth,\n                Severed = nextSevered,\n            }',
              'new TreeDamageState(nextFoliageHealth, nextSevered)')
field_marker = '        private static int s_DamageVersion;\n'
if field_marker not in s:
    raise SystemExit('missing TreeWorldState field marker')
s = s.replace(field_marker, field_marker + '''\n        static TreeWorldState()\n        {\n            TreeWorldReadRegistry.Register(TreeWorldReadSource.Instance);\n        }\n''', 1)
reset_marker = '''            unchecked\n            {\n                s_Version++;\n                s_DamageVersion++;\n            }\n        }\n\n        public static void Replace'''
if reset_marker not in s:
    raise SystemExit('missing TreeWorldState reset marker')
s = s.replace(reset_marker, '''            unchecked\n            {\n                s_Version++;\n                s_DamageVersion++;\n            }\n            TreeWorldReadRegistry.Register(TreeWorldReadSource.Instance);\n        }\n\n        public static void Replace''', 1)
p.write_text(s)

(RUNTIME / 'TreeWorldReadSource.cs').write_text('''using System;\nusing System.Collections.Generic;\nusing VoxelEngine.Vegetation.Api;\n\nnamespace VoxelEngine.Vegetation.Runtime\n{\n    internal sealed class TreeWorldReadSource : ITreeWorldReadSource\n    {\n        internal static readonly TreeWorldReadSource Instance = new();\n        private TreeWorldReadSource() { }\n\n        public IReadOnlyList<TreeInstance> Instances => TreeWorldState.Instances;\n        public IReadOnlyList<TreeDamageState> Damage => TreeWorldState.Damage;\n        public int Version => TreeWorldState.Version;\n        public int DamageVersion => TreeWorldState.DamageVersion;\n\n        public event Action SnapshotChanged { add => TreeWorldState.SnapshotChanged += value; remove => TreeWorldState.SnapshotChanged -= value; }\n        public event Action<TreeBranchCutEvent> BranchCut { add => TreeWorldState.BranchCut += value; remove => TreeWorldState.BranchCut -= value; }\n        public event Action<TreeDamageChangedEvent> DamageChanged { add => TreeWorldState.DamageChanged += value; remove => TreeWorldState.DamageChanged -= value; }\n        public event Action<TreeSeveredEvent> TreeSevered { add => TreeWorldState.TreeSevered += value; remove => TreeWorldState.TreeSevered -= value; }\n\n        public IReadOnlyCollection<int> RemovedBranches(int treeIndex) => TreeWorldState.RemovedBranches(treeIndex);\n        public TreeSkeletonSnapshot SkeletonFor(int treeIndex) => ProceduralTreeDamageService.SkeletonFor(treeIndex);\n        public TreeSkeletonSnapshot SkeletonFor(in TreeInstance instance) => ProceduralTreeSkeletonBuilder.Generate(in instance);\n    }\n}\n''')
simple_meta(RUNTIME / 'TreeWorldReadSource.cs.meta')

(RUNTIME / 'TreeWorldRuntime.cs').write_text('''using System;\nusing System.Collections.Generic;\nusing Unity.Mathematics;\nusing VoxelEngine.Vegetation.Api;\n\nnamespace VoxelEngine.Vegetation.Runtime\n{\n    /// <summary>Composition/gameplay facade for mutating Runtime-owned tree state.</summary>\n    public static class TreeWorldRuntime\n    {\n        public static IReadOnlyList<TreeInstance> Instances => TreeWorldState.Instances;\n        public static IReadOnlyList<TreeDamageState> Damage => TreeWorldState.Damage;\n        public static int Version => TreeWorldState.Version;\n        public static int DamageVersion => TreeWorldState.DamageVersion;\n\n        public static event Action SnapshotChanged { add => TreeWorldState.SnapshotChanged += value; remove => TreeWorldState.SnapshotChanged -= value; }\n        public static event Action<TreeBranchCutEvent> BranchCut { add => TreeWorldState.BranchCut += value; remove => TreeWorldState.BranchCut -= value; }\n        public static event Action<TreeDamageChangedEvent> DamageChanged { add => TreeWorldState.DamageChanged += value; remove => TreeWorldState.DamageChanged -= value; }\n        public static event Action<TreeSeveredEvent> TreeSevered { add => TreeWorldState.TreeSevered += value; remove => TreeWorldState.TreeSevered -= value; }\n\n        public static void Replace(IReadOnlyList<TreeInstance> instances) => TreeWorldState.Replace(instances);\n        public static IReadOnlyCollection<int> RemovedBranches(int treeIndex) => TreeWorldState.RemovedBranches(treeIndex);\n        public static bool RemoveBranch(int treeIndex, int branchIndex, float3 hitPointMetres = default, float3 impulse = default) =>\n            TreeWorldState.RemoveBranch(treeIndex, branchIndex, hitPointMetres, impulse);\n        public static void SetDamage(int index, float foliageHealth, bool severed,\n                                     float3 hitPointMetres = default, float3 impulse = default,\n                                     int breakBranchIndex = -1) =>\n            TreeWorldState.SetDamage(index, foliageHealth, severed, hitPointMetres, impulse, breakBranchIndex);\n    }\n}\n''')
simple_meta(RUNTIME / 'TreeWorldRuntime.cs.meta')

# --- damage service consumes Api snapshots/topology ---
p = RUNTIME / 'ProceduralTreeDamageService.cs'
s = p.read_text()
s = re.sub(r'\bProceduralTreeSkeleton\b', 'TreeSkeletonSnapshot', s)
s = s.replace('ProceduralTreeSkeletonBuilder.IsBranchRemoved', 'TreeSkeletonTopology.IsBranchRemoved')
s = s.replace('TreeWorldState.TreeDamageState', 'TreeDamageState')
s = s.replace('.BranchParents.Length', '.BranchParents.Count')
s = s.replace('.LeafParents.Length', '.LeafParents.Count')
s = s.replace('int[] parents = skeleton.BranchParents;',
              'IReadOnlyList<int> parents = skeleton.BranchParents;')
p.write_text(s)

# --- Rendering consumes only immutable Vegetation.Api views ---
for p in (ROOT / 'Assets/VoxelEngine/Rendering/Vegetation').glob('*.cs'):
    s = p.read_text()
    s = s.replace('using VoxelEngine.Core.Vegetation;\n', '')
    s = re.sub(r'\bProceduralTreeSkeleton\b', 'TreeSkeletonSnapshot', s)
    s = s.replace('TreeWorldState.TreeDamageState', 'TreeDamageState')
    s = s.replace('TreeWorldState.', 'TreeWorldReadRegistry.Current.')
    s = s.replace('ProceduralTreeSkeletonBuilder.ResolveRemovedBranches', 'TreeSkeletonTopology.ResolveRemovedBranches')
    s = s.replace('ProceduralTreeSkeletonBuilder.IsBranchRemoved', 'TreeSkeletonTopology.IsBranchRemoved')
    s = s.replace('ProceduralTreeDamageService.SkeletonFor', 'TreeWorldReadRegistry.Current.SkeletonFor')
    s = s.replace('ProceduralTreeSkeletonBuilder.Generate', 'TreeWorldReadRegistry.Current.SkeletonFor')
    s = s.replace('.BranchParents.Length', '.BranchParents.Count')
    s = s.replace('.LeafParents.Length', '.LeafParents.Count')
    s = ensure_using(s, 'using VoxelEngine.Vegetation.Api;')
    p.write_text(s)

# --- composition/editor/test consumers may reference Runtime implementation ---
for base in ('Assets/Scenes/Showcase', 'Assets/VoxelEngine/CI', 'Assets/Tests'):
    root = ROOT / base
    if not root.exists():
        continue
    for p in root.rglob('*.cs'):
        s = p.read_text()
        if 'VoxelEngine.Core.Vegetation' not in s and 'TreeWorldState.' not in s \
           and 'ProceduralTreeSkeleton' not in s:
            continue
        s = s.replace('using VoxelEngine.Core.Vegetation;', 'using VoxelEngine.Vegetation.Runtime;')
        s = re.sub(r'\bProceduralTreeSkeleton\b', 'TreeSkeletonSnapshot', s)
        s = s.replace('ProceduralTreeSkeletonBuilder.ResolveRemovedBranches', 'TreeSkeletonTopology.ResolveRemovedBranches')
        s = s.replace('ProceduralTreeSkeletonBuilder.IsBranchRemoved', 'TreeSkeletonTopology.IsBranchRemoved')
        s = s.replace('TreeWorldState.TreeDamageState', 'TreeDamageState')
        s = s.replace('TreeWorldState.', 'TreeWorldRuntime.')
        s = s.replace('.BranchParents.Length', '.BranchParents.Count')
        s = s.replace('.LeafParents.Length', '.LeafParents.Count')
        s = ensure_using(s, 'using VoxelEngine.Vegetation.Api;')
        if 'VoxelEngine.Vegetation.Runtime' not in s and (
            'TreeWorldRuntime.' in s or 'ProceduralTreeDamageService' in s or 'ProceduralTreeSkeletonBuilder' in s):
            s = ensure_using(s, 'using VoxelEngine.Vegetation.Runtime;')
        p.write_text(s)

# --- replace broad Vegetation asmdef references; unknown production consumers fail closed ---
for p in ROOT.rglob('*.asmdef'):
    data = json.loads(p.read_text())
    refs = data.get('references', [])
    if 'VoxelEngine.Vegetation' not in refs:
        continue
    refs = [x for x in refs if x != 'VoxelEngine.Vegetation']
    posix = p.as_posix()
    if posix.endswith('Assets/VoxelEngine/Rendering/VoxelEngine.Rendering.asmdef'):
        additions = ['VoxelEngine.Vegetation.Api']
    elif 'Packages/com.mountingforce.worldgen/Runtime/Voxel/' in posix:
        additions = ['VoxelEngine.Vegetation.Api']
    elif ('Assets/Scenes/Showcase/' in posix or 'Assets/VoxelEngine/CI/' in posix
          or 'Assets/Tests/' in posix):
        additions = ['VoxelEngine.Vegetation.Api', 'VoxelEngine.Vegetation.Runtime']
    else:
        raise SystemExit(f'unreviewed broad Vegetation assembly consumer: {p}')
    for item in additions:
        if item not in refs:
            refs.append(item)
    data['references'] = refs
    p.write_text(json.dumps(data, indent=2) + '\n')

# Explicit expected assembly edges even if the broad reference had already been partially removed.
update_refs(ROOT / 'Assets/VoxelEngine/Rendering/VoxelEngine.Rendering.asmdef',
            remove=('VoxelEngine.Vegetation', 'VoxelEngine.Vegetation.Runtime'),
            add=('VoxelEngine.Vegetation.Api',))
for path in (
    'Assets/Scenes/Showcase/VoxelEngine.Showcase.asmdef',
    'Assets/VoxelEngine/CI/Editor/VoxelEngine.CI.Editor.asmdef',
    'Assets/VoxelEngine/CI/PlayMode/VoxelEngine.CI.PlayMode.asmdef'):
    p = ROOT / path
    if p.exists():
        update_refs(p, remove=('VoxelEngine.Vegetation',),
                    add=('VoxelEngine.Vegetation.Api', 'VoxelEngine.Vegetation.Runtime'))

# Tests that directly use Runtime vegetation need the explicit Runtime edge.
for p in (ROOT / 'Assets/Tests').rglob('*.asmdef'):
    folder = p.parent
    direct = False
    for cs in folder.rglob('*.cs'):
        text = cs.read_text()
        if ('VoxelEngine.Vegetation.Runtime' in text or 'TreeWorldRuntime.' in text
                or 'ProceduralTreeDamageService' in text or 'ProceduralTreeSkeletonBuilder' in text):
            direct = True
            break
    if direct:
        update_refs(p, remove=('VoxelEngine.Vegetation',),
                    add=('VoxelEngine.Vegetation.Api', 'VoxelEngine.Vegetation.Runtime'))

# Boundary regression test.
boundary = ROOT / 'Assets/Tests/EditMode/VegetationAssemblyBoundaryTests.cs'
boundary.write_text('''using System;\nusing System.IO;\nusing NUnit.Framework;\n\nnamespace VoxelEngine.Tests.EditMode\n{\n    public sealed class VegetationAssemblyBoundaryTests\n    {\n        [Test]\n        public void RenderingAndWorldGenUseVegetationApiOnly()\n        {\n            string root = FindRepoRoot();\n            AssertApiOnly(Path.Combine(root, "Assets", "VoxelEngine", "Rendering", "VoxelEngine.Rendering.asmdef"));\n            AssertApiOnly(Path.Combine(root, "Packages", "com.mountingforce.worldgen", "Runtime", "Voxel", "MountingForce.WorldGen.Voxel.asmdef"));\n\n            string rendering = Path.Combine(root, "Assets", "VoxelEngine", "Rendering");\n            foreach (string path in Directory.EnumerateFiles(rendering, "*.cs", SearchOption.AllDirectories))\n            {\n                string source = File.ReadAllText(path);\n                Assert.That(source.Contains("VoxelEngine.Vegetation.Runtime"), Is.False, Path.GetFileName(path));\n                Assert.That(source.Contains("VoxelEngine.Core.Vegetation"), Is.False, Path.GetFileName(path));\n            }\n        }\n\n        private static void AssertApiOnly(string asmdefPath)\n        {\n            string text = File.ReadAllText(asmdefPath);\n            Assert.That(text.Contains("\\\"VoxelEngine.Vegetation.Api\\\""), Is.True, asmdefPath);\n            Assert.That(text.Contains("\\\"VoxelEngine.Vegetation.Runtime\\\""), Is.False, asmdefPath);\n            Assert.That(text.Contains("\\\"VoxelEngine.Vegetation\\\""), Is.False, asmdefPath);\n        }\n\n        private static string FindRepoRoot()\n        {\n            string directory = Directory.GetCurrentDirectory();\n            while (!string.IsNullOrEmpty(directory))\n            {\n                if (Directory.Exists(Path.Combine(directory, "Assets"))) return directory;\n                directory = Directory.GetParent(directory)?.FullName;\n            }\n            throw new InvalidOperationException("Could not locate repository root.");\n        }\n    }\n}\n''')
if not boundary.with_suffix('.cs.meta').exists():
    simple_meta(boundary.with_suffix('.cs.meta'))

# Final source-level validation.
for root in (RUNTIME, ROOT / 'Assets/VoxelEngine/Rendering',
             ROOT / 'Packages/com.mountingforce.worldgen/Runtime/Voxel'):
    for p in root.rglob('*'):
        if p.suffix not in ('.cs', '.asmdef'):
            continue
        text = p.read_text()
        if 'VoxelEngine.Core.Vegetation' in text:
            raise SystemExit(f'stale Core Vegetation reference: {p}')

for p in ROOT.rglob('*.asmdef'):
    refs = json.loads(p.read_text()).get('references', [])
    if 'VoxelEngine.Vegetation' in refs:
        raise SystemExit(f'stale broad Vegetation assembly ref: {p}')

for root in (ROOT / 'Assets/VoxelEngine/Rendering',
             ROOT / 'Packages/com.mountingforce.worldgen/Runtime/Voxel'):
    for p in root.rglob('*'):
        if p.suffix not in ('.cs', '.asmdef'):
            continue
        if 'VoxelEngine.Vegetation.Runtime' in p.read_text():
            raise SystemExit(f'Api-only consumer references Vegetation.Runtime: {p}')

if 'internal static class TreeWorldState' not in (RUNTIME / 'TreeWorldState.cs').read_text():
    raise SystemExit('TreeWorldState is not Runtime-internal')
print('Vegetation boundary transformation validated.')
