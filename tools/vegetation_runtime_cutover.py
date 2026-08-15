from pathlib import Path
import json
import re
import uuid

ROOT = Path('.')
VEG = ROOT / 'Assets/VoxelEngine/Vegetation'
RUNTIME = VEG / 'Runtime'
API = VEG / 'Api'


def folder_meta(path: Path, guid: str):
    path.write_text(
        f'fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n'
        '  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    )


def simple_meta(path: Path, guid: str):
    path.write_text(f'fileFormatVersion: 2\nguid: {guid}\n')


def replace_required(text: str, old: str, new: str, label: str, count: int = 1) -> str:
    actual = text.count(old)
    if actual < count:
        raise SystemExit(f'{label}: expected at least {count} occurrences, found {actual}')
    return text.replace(old, new, count)


RUNTIME.mkdir(parents=True, exist_ok=True)
if not (VEG / 'Runtime.meta').exists():
    folder_meta(VEG / 'Runtime.meta', '08b87e413a584c6d9b8fcd98509187ba')

# Move implementation sources and their Unity identities.
for name in ('ProceduralTreeDamageService', 'ProceduralTreeSkeletonBuilder', 'TreeWorldState'):
    src = VEG / f'{name}.cs'
    dst = RUNTIME / f'{name}.cs'
    src_meta = VEG / f'{name}.cs.meta'
    dst_meta = RUNTIME / f'{name}.cs.meta'
    if src.exists():
        src.rename(dst)
    if src_meta.exists():
        src_meta.rename(dst_meta)

# Move the broad assembly identity to the Runtime assembly.
old_asm = VEG / 'VoxelEngine.Vegetation.asmdef'
new_asm = RUNTIME / 'VoxelEngine.Vegetation.Runtime.asmdef'
old_asm_meta = VEG / 'VoxelEngine.Vegetation.asmdef.meta'
new_asm_meta = RUNTIME / 'VoxelEngine.Vegetation.Runtime.asmdef.meta'
if old_asm.exists():
    old_asm.rename(new_asm)
if old_asm_meta.exists():
    old_asm_meta.rename(new_asm_meta)

asm = json.loads(new_asm.read_text())
asm['name'] = 'VoxelEngine.Vegetation.Runtime'
asm['rootNamespace'] = 'VoxelEngine.Vegetation.Runtime'
asm['references'] = ['VoxelEngine.Vegetation.Api', 'Unity.Mathematics']
asm['autoReferenced'] = False
new_asm.write_text(json.dumps(asm, indent=2) + '\n')

# Runtime namespaces.
for name in ('ProceduralTreeDamageService', 'ProceduralTreeSkeletonBuilder', 'TreeWorldState'):
    p = RUNTIME / f'{name}.cs'
    s = p.read_text()
    s = replace_required(s, 'namespace VoxelEngine.Core.Vegetation\n',
                         'namespace VoxelEngine.Vegetation.Runtime\n', f'{name} namespace')
    p.write_text(s)

# Skeleton builder: mutable build state stays Runtime; immutable snapshot is Api.
p = RUNTIME / 'ProceduralTreeSkeletonBuilder.cs'
s = p.read_text()
start = s.index('    public struct TreeBranchSegment')
end = s.index('    public static class ProceduralTreeSkeletonBuilder')
build_state = '''    internal sealed class TreeSkeletonBuildState\n    {\n        public readonly List<TreeBranchSegment> Branches = new(256);\n        public readonly List<TreeLeafAnchor> Leaves = new(768);\n        public TreeSpeciesProfile Profile;\n        public float Height;\n        public int[] BranchParents;\n        public int[] LeafParents;\n    }\n\n'''
s = s[:start] + build_state + s[end:]
s = re.sub(r'\bProceduralTreeSkeleton\b', 'TreeSkeletonBuildState', s)
s = replace_required(s,
    'public static TreeSkeletonBuildState Generate(in TreeInstance instance)',
    'public static TreeSkeletonSnapshot Generate(in TreeInstance instance)',
    'skeleton Generate signature')
marker = '            ResolveTopology(skeleton);\n            return skeleton;'
replacement = '''            ResolveTopology(skeleton);\n            return new TreeSkeletonSnapshot(\n                skeleton.Branches.ToArray(), skeleton.Leaves.ToArray(), skeleton.Profile,\n                skeleton.Height, skeleton.BranchParents, skeleton.LeafParents);'''
s = replace_required(s, marker, replacement, 'skeleton snapshot return')
a = s.index('        public static void ResolveRemovedBranches(')
b = s.index('        private static void GrowBranch(', a)
s = s[:a] + s[b:]
p.write_text(s)

# Runtime world state now uses Api event/damage values and publishes a read-only source.
p = RUNTIME / 'TreeWorldState.cs'
s = p.read_text()
a = s.index('    public readonly struct TreeBranchCutEvent')
b = s.index('    /// <summary>\n    /// Authoritative semantic state', a)
s = s[:a] + s[b:]
a = s.index('        public struct TreeDamageState')
b = s.index('        private const float RootQuantizationMetres', a)
s = s[:a] + s[b:]
s = replace_required(s,
'''new TreeDamageState\n                    {\n                        FoliageHealth = 1f,\n                        Severed = false,\n                    }''',
'new TreeDamageState(1f, false)', 'initial damage state')
s = replace_required(s,
'''new TreeDamageState\n            {\n                FoliageHealth = nextFoliageHealth,\n                Severed = nextSevered,\n            }''',
'new TreeDamageState(nextFoliageHealth, nextSevered)', 'updated damage state')
reset_marker = '''            unchecked\n            {\n                s_Version++;\n                s_DamageVersion++;\n            }\n        }'''
reset_replacement = '''            unchecked\n            {\n                s_Version++;\n                s_DamageVersion++;\n            }\n            TreeWorldReadRegistry.Register(TreeWorldReadSource.Instance);\n        }'''
s = replace_required(s, reset_marker, reset_replacement, 'play-session read-source registration')
replace_marker = '''        public static void Replace(IReadOnlyList<TreeInstance> instances)\n        {\n            s_Instances.Clear();'''
replace_replacement = '''        public static void Replace(IReadOnlyList<TreeInstance> instances)\n        {\n            TreeWorldReadRegistry.Register(TreeWorldReadSource.Instance);\n            s_Instances.Clear();'''
s = replace_required(s, replace_marker, replace_replacement, 'replace read-source registration')
p.write_text(s)

# Runtime adapter implementing the Api read boundary.
read_source = RUNTIME / 'TreeWorldReadSource.cs'
read_source.write_text('''using System;\nusing System.Collections.Generic;\nusing VoxelEngine.Vegetation.Api;\n\nnamespace VoxelEngine.Vegetation.Runtime\n{\n    internal sealed class TreeWorldReadSource : ITreeWorldReadSource\n    {\n        internal static readonly TreeWorldReadSource Instance = new();\n        private TreeWorldReadSource() { }\n        public IReadOnlyList<TreeInstance> Instances => TreeWorldState.Instances;\n        public IReadOnlyList<TreeDamageState> Damage => TreeWorldState.Damage;\n        public int Version => TreeWorldState.Version;\n        public int DamageVersion => TreeWorldState.DamageVersion;\n        public event Action SnapshotChanged { add => TreeWorldState.SnapshotChanged += value; remove => TreeWorldState.SnapshotChanged -= value; }\n        public event Action<TreeBranchCutEvent> BranchCut { add => TreeWorldState.BranchCut += value; remove => TreeWorldState.BranchCut -= value; }\n        public event Action<TreeDamageChangedEvent> DamageChanged { add => TreeWorldState.DamageChanged += value; remove => TreeWorldState.DamageChanged -= value; }\n        public event Action<TreeSeveredEvent> TreeSevered { add => TreeWorldState.TreeSevered += value; remove => TreeWorldState.TreeSevered -= value; }\n        public IReadOnlyCollection<int> RemovedBranches(int treeIndex) => TreeWorldState.RemovedBranches(treeIndex);\n        public TreeSkeletonSnapshot SkeletonFor(int treeIndex) => ProceduralTreeDamageService.SkeletonFor(treeIndex);\n        public TreeSkeletonSnapshot SkeletonFor(in TreeInstance instance) => ProceduralTreeSkeletonBuilder.Generate(in instance);\n    }\n}\n''')
if not read_source.with_suffix('.cs.meta').exists():
    simple_meta(read_source.with_suffix('.cs.meta'), '9d5c11d90e554e9bb11a478a50c49b7a')

# Damage service consumes immutable Api snapshots/topology.
p = RUNTIME / 'ProceduralTreeDamageService.cs'
s = p.read_text()
s = re.sub(r'\bProceduralTreeSkeleton\b', 'TreeSkeletonSnapshot', s)
s = s.replace('ProceduralTreeSkeletonBuilder.IsBranchRemoved', 'TreeSkeletonTopology.IsBranchRemoved')
s = s.replace('TreeWorldState.TreeDamageState', 'TreeDamageState')
s = s.replace('.BranchParents.Length', '.BranchParents.Count')
s = s.replace('.LeafParents.Length', '.LeafParents.Count')
p.write_text(s)

# Rendering becomes an Api-only observer.
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
    p.write_text(s)

# Composition/tooling/tests may use Runtime implementation; Api values stay separate.
for root in (ROOT / 'Assets/Scenes/Showcase', ROOT / 'Assets/VoxelEngine/CI', ROOT / 'Assets/Tests'):
    if not root.exists():
        continue
    for p in root.rglob('*.cs'):
        s = p.read_text()
        if 'using VoxelEngine.Core.Vegetation;' in s:
            s = s.replace('using VoxelEngine.Core.Vegetation;', 'using VoxelEngine.Vegetation.Runtime;')
            if 'using VoxelEngine.Vegetation.Api;' not in s:
                lines = s.splitlines(True)
                insert = 0
                while insert < len(lines) and lines[insert].startswith('using '):
                    insert += 1
                lines.insert(insert, 'using VoxelEngine.Vegetation.Api;\n')
                s = ''.join(lines)
        s = re.sub(r'\bProceduralTreeSkeleton\b', 'TreeSkeletonSnapshot', s)
        s = s.replace('ProceduralTreeSkeletonBuilder.ResolveRemovedBranches', 'TreeSkeletonTopology.ResolveRemovedBranches')
        s = s.replace('ProceduralTreeSkeletonBuilder.IsBranchRemoved', 'TreeSkeletonTopology.IsBranchRemoved')
        s = s.replace('TreeWorldState.TreeDamageState', 'TreeDamageState')
        s = s.replace('.BranchParents.Length', '.BranchParents.Count')
        s = s.replace('.LeafParents.Length', '.LeafParents.Count')
        p.write_text(s)


def refs(path: str, remove=(), add=()):
    p = ROOT / path
    data = json.loads(p.read_text())
    rs = [x for x in data.get('references', []) if x not in remove]
    for x in add:
        if x not in rs:
            rs.append(x)
    data['references'] = rs
    p.write_text(json.dumps(data, indent=2) + '\n')

refs('Assets/VoxelEngine/Rendering/VoxelEngine.Rendering.asmdef',
     remove=('VoxelEngine.Vegetation', 'VoxelEngine.Vegetation.Runtime'),
     add=('VoxelEngine.Vegetation.Api',))
refs('Assets/Scenes/Showcase/VoxelEngine.Showcase.asmdef',
     remove=('VoxelEngine.Vegetation',),
     add=('VoxelEngine.Vegetation.Api', 'VoxelEngine.Vegetation.Runtime'))
refs('Assets/VoxelEngine/CI/Editor/VoxelEngine.CI.Editor.asmdef',
     remove=('VoxelEngine.Vegetation',),
     add=('VoxelEngine.Vegetation.Api', 'VoxelEngine.Vegetation.Runtime'))
refs('Assets/VoxelEngine/CI/PlayMode/VoxelEngine.CI.PlayMode.asmdef',
     remove=('VoxelEngine.Vegetation',),
     add=('VoxelEngine.Vegetation.Api', 'VoxelEngine.Vegetation.Runtime'))

# Test assemblies that directly use implementation need Runtime explicitly now that it is not auto-referenced.
for p in (ROOT / 'Assets/Tests').rglob('*.asmdef'):
    data = json.loads(p.read_text())
    refs_list = data.get('references', [])
    if 'VoxelEngine.Vegetation' in refs_list:
        refs_list = [x for x in refs_list if x != 'VoxelEngine.Vegetation']
        if 'VoxelEngine.Vegetation.Api' not in refs_list:
            refs_list.append('VoxelEngine.Vegetation.Api')
        if 'VoxelEngine.Vegetation.Runtime' not in refs_list:
            refs_list.append('VoxelEngine.Vegetation.Runtime')
        data['references'] = refs_list
        p.write_text(json.dumps(data, indent=2) + '\n')

# Architecture guard for this boundary.
guard = ROOT / 'Assets/Tests/EditMode/VegetationAssemblyBoundaryTests.cs'
guard.write_text('''using System;\nusing System.IO;\nusing NUnit.Framework;\n\nnamespace VoxelEngine.Tests.EditMode\n{\n    public sealed class VegetationAssemblyBoundaryTests\n    {\n        [Test]\n        public void RenderingAndWorldGenUseVegetationApiOnly()\n        {\n            string root = FindRepoRoot();\n            AssertApiOnly(Path.Combine(root, "Assets", "VoxelEngine", "Rendering", "VoxelEngine.Rendering.asmdef"));\n            AssertApiOnly(Path.Combine(root, "Packages", "com.mountingforce.worldgen", "Runtime", "Voxel", "MountingForce.WorldGen.Voxel.asmdef"));\n\n            string rendering = Path.Combine(root, "Assets", "VoxelEngine", "Rendering");\n            foreach (string path in Directory.EnumerateFiles(rendering, "*.cs", SearchOption.AllDirectories))\n            {\n                string source = File.ReadAllText(path);\n                Assert.That(source.Contains("VoxelEngine.Vegetation.Runtime"), Is.False, Path.GetFileName(path));\n                Assert.That(source.Contains("VoxelEngine.Core.Vegetation"), Is.False, Path.GetFileName(path));\n            }\n        }\n\n        private static void AssertApiOnly(string asmdefPath)\n        {\n            string text = File.ReadAllText(asmdefPath);\n            Assert.That(text.Contains("\\\"VoxelEngine.Vegetation.Api\\\""), Is.True, asmdefPath);\n            Assert.That(text.Contains("\\\"VoxelEngine.Vegetation.Runtime\\\""), Is.False, asmdefPath);\n            Assert.That(text.Contains("\\\"VoxelEngine.Vegetation\\\""), Is.False, asmdefPath);\n        }\n\n        private static string FindRepoRoot()\n        {\n            string directory = Directory.GetCurrentDirectory();\n            while (!string.IsNullOrEmpty(directory))\n            {\n                if (Directory.Exists(Path.Combine(directory, "Assets"))) return directory;\n                directory = Directory.GetParent(directory)?.FullName;\n            }\n            throw new InvalidOperationException("Could not locate repository root.");\n        }\n    }\n}\n''')
if not guard.with_suffix('.cs.meta').exists():
    simple_meta(guard.with_suffix('.cs.meta'), '099f0a12f7334a43a26d450085371ec2')

# Hard boundary validation.
for p in list((ROOT / 'Assets/VoxelEngine/Rendering').rglob('*.cs')) + list((ROOT / 'Packages/com.mountingforce.worldgen/Runtime/Voxel').rglob('*.cs')):
    text = p.read_text()
    if 'VoxelEngine.Vegetation.Runtime' in text or 'VoxelEngine.Core.Vegetation' in text:
        raise SystemExit(f'foreign runtime/core Vegetation reference: {p}')

for asmdef in (ROOT / 'Assets').rglob('*.asmdef'):
    data = json.loads(asmdef.read_text())
    if 'VoxelEngine.Vegetation' in data.get('references', []):
        raise SystemExit(f'stale broad Vegetation assembly reference: {asmdef}')

if (VEG / 'VoxelEngine.Vegetation.asmdef').exists():
    raise SystemExit('old broad Vegetation asmdef still exists')

print('Vegetation Runtime/Rendering boundary transformation complete.')
