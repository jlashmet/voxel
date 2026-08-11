from pathlib import Path
import re

ROOT = Path('.')

replacements = {
    'ProceduralTreeRegistry.Instances': 'TreeWorldState.Instances',
    'ProceduralTreeRegistry.Damage': 'TreeWorldState.Damage',
    'ProceduralTreeRegistry.Version': 'TreeWorldState.Version',
    'ProceduralTreeRegistry.DamageVersion': 'TreeWorldState.DamageVersion',
    'ProceduralTreeRegistry.RemovedBranches': 'TreeWorldState.RemovedBranches',
    'ProceduralTreeRegistry.RemoveBranch': 'TreeWorldState.RemoveBranch',
    'ProceduralTreeRegistry.SetDamage': 'TreeWorldState.SetDamage',
    'ProceduralTreeRegistry.Replace': 'TreeWorldState.Replace',
    'ProceduralTreeRegistry.TreeDamageState': 'TreeWorldState.TreeDamageState',
    'ProceduralTreeMeshBuilder.TreeSkeleton': 'ProceduralTreeSkeleton',
    'ProceduralTreeMeshBuilder.BranchSegment': 'TreeBranchSegment',
    'ProceduralTreeMeshBuilder.LeafAnchor': 'TreeLeafAnchor',
    'ProceduralTreeMeshBuilder.GenerateSkeleton': 'ProceduralTreeSkeletonBuilder.Generate',
    'ProceduralTreeMeshBuilder.ResolveRemovedBranches': 'ProceduralTreeSkeletonBuilder.ResolveRemovedBranches',
    'ProceduralTreeDamageBridge.TrySweepImpact': 'ProceduralTreeDamageService.TrySweepImpact',
}

excluded = {
    Path('Assets/VoxelEngine/Rendering/Vegetation/ProceduralTreeRegistry.cs'),
    Path('Assets/Scenes/Showcase/ProceduralTreeDamageBridge.cs'),
}

for path in ROOT.rglob('*.cs'):
    if path in excluded:
        continue
    text = path.read_text()
    original = text
    for old, new in replacements.items():
        text = text.replace(old, new)
    if text != original:
        path.write_text(text)

for rel in [
    'Assets/Scenes/Showcase/VoxelShowcase.cs',
    'Assets/Scenes/Showcase/ShowcaseTreePopulation.cs',
    'Assets/VoxelEngine/CI/Editor/SingleTreeCapture.cs',
    'Assets/VoxelEngine/CI/PlayMode/RegistryTreeVisualTests.cs',
    'Assets/VoxelEngine/CI/PlayMode/ShowcaseTreeVisualTests.cs',
    'Assets/VoxelEngine/CI/PlayMode/TreeDestructionVisualTests.cs',
    'Assets/VoxelEngine/CI/PlayMode/ShowcaseTreeDestructionTests.cs',
]:
    path = Path(rel)
    if not path.exists():
        continue
    text = path.read_text()
    if 'VoxelEngine.Core.Vegetation' not in text:
        anchor = 'using UnityEngine;\n'
        if anchor in text:
            text = text.replace(anchor, anchor + 'using VoxelEngine.Core.Vegetation;\n', 1)
        else:
            text = 'using VoxelEngine.Core.Vegetation;\n' + text
    path.write_text(text)

showcase = Path('Assets/Scenes/Showcase/VoxelShowcase.cs')
text = showcase.read_text()
old = '''                    if (semanticTreeHit || changed > 0)\n                        ProceduralTreeDamageBridge.ApplyExplosion(hit, shot.ImpactRadius);'''
new = '''                    if (semanticTreeHit || changed > 0)\n                    {\n                        float3 impactMetres = (float3)hit * VoxelSurfaceRenderer.VoxelSize;\n                        ProceduralTreeDamageService.ApplyBlast(\n                            impactMetres, shot.ImpactRadius * VoxelSurfaceRenderer.VoxelSize,\n                            (float3)shot.Direction);\n                    }'''
if old not in text:
    old = '''                    if (semanticTreeHit || changed > 0)\n                        ProceduralTreeDamageService.ApplyExplosion(hit, shot.ImpactRadius);'''
if old not in text:
    raise SystemExit('Could not find VoxelShowcase tree explosion adapter block')
text = text.replace(old, new, 1)
showcase.write_text(text)

hard = Path('Assets/VoxelEngine/Rendering/SurfaceExtraction/CpuHardSurfaceChunkCache.cs')
text = hard.read_text()
text = text.replace('using VoxelEngine.Rendering.Vegetation;\n', '')
text = re.sub(r'\n\s*private int _treeRegistryVersion = int\.MinValue;\n', '\n', text)
text = re.sub(
    r'\n\s*// Semantic trees are published after.*?foreach \(int3 chunk in _knownHardChunks\) _pending\.Add\(chunk\);\n\s*}\n',
    '\n', text, flags=re.S)
text = re.sub(r'\n\s*if \(ProceduralTreeRegistry\.IsLegacyHiddenHardBrick\(worldBrick\)\) continue;', '', text)
text = text.replace(
    'Legacy tree timber can share the same hard\n                // bit because the old classifier only knew materials; semantic trees explicitly\n                // suppress those gameplay proxy bricks here.',
    'Tree vegetation is semantic and is never authored into this hard-voxel layer.')
hard.write_text(text)

trans = Path('Assets/VoxelEngine/Rendering/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
text = trans.read_text()
text = text.replace('using VoxelEngine.Rendering.Vegetation;\n', '')
text = re.sub(r'^.*ProceduralTreeRegistry\.IsLegacyHiddenSmoothBrick\([^\n]*\).*\n', '', text,
              flags=re.M)
text = re.sub(r'^.*ProceduralTreeRegistry\.Version.*\n', '', text, flags=re.M)
text = re.sub(r'^\s*private int _treeRegistryVersion = int\.MinValue;\n', '', text, flags=re.M)
trans.write_text(text)

render = Path('Assets/VoxelEngine/Rendering/RenderFeature/VoxelRenderPass.cs')
text = render.read_text()
text = text.replace('using VoxelEngine.Rendering.Vegetation;\n', '')
text = re.sub(
    r'\s*// A hard layer no longer owns an entire render chunk\..*?keep an upright copy of the old voxel tree underneath the procedural tree\.\n',
    '\n            // Hard and smooth derived layers may coexist. Surface Nets remains only as a\n            // temporary smooth-terrain warmup source until Transvoxel is ready.\n', text, flags=re.S)
text = re.sub(r'\n\s*bool legacyTreeProxy = ProceduralTreeRegistry\.IsLegacyProxyRenderChunk\(coordinate\);', '', text)
text = re.sub(r'\n\s*bool legacyTreeProxy =\n\s*ProceduralTreeRegistry\.IsLegacyProxyRenderChunk\(entry\.Coordinate\);', '', text)
text = text.replace('&& (_transvoxelActivated || hardReady || legacyTreeProxy)',
                    '&& (_transvoxelActivated || hardReady)')
text = re.sub(r'^\s*ProceduralTreeRegistry\.ClearCoarseLegacyProxyRenderChunks\(\);\n', '', text,
              flags=re.M)
text = re.sub(r'\n\s*if \(legacyTreeProxy\)\n\s*ProceduralTreeRegistry\.MarkCoarseLegacyProxyRenderChunk\(entry\.Coordinate\);', '', text)
text = text.replace('                    || ProceduralTreeRegistry.IsLegacyProxyRenderChunk(coordinate)', '')
text = text.replace('                    && !ProceduralTreeRegistry.IsLegacyProxyRenderChunk(entry.Coordinate)', '')
render.write_text(text)

# Voxel debris now represents only actual voxel destruction. There are no tree-proxy samples to
# discover/filter, so keep every detached voxel and simplify sampling.
debris = Path('Assets/Scenes/Showcase/GpuDebrisSystem.cs')
text = debris.read_text()
text = text.replace('using VoxelEngine.Rendering.Vegetation;\n', '')
text = re.sub(
    r'\n\s*// Semantic trees own their destruction presentation\..*?pivot /= nonProxyCount;\n',
    '''\n                int sourceCount = chunk.Voxels.Length;\n                if (sourceCount == 0) continue;\n                Vector3 pivot = Vector3.zero;\n                for (int i = 0; i < sourceCount; i++)\n                {\n                    pivot += ((Vector3)(float3)chunk.Voxels[i] + Vector3.one * 0.5f)\n                           * VoxelSurfaceRenderer.VoxelSize;\n                }\n                pivot /= sourceCount;\n''', text, flags=re.S)
text = text.replace('nonProxyCount', 'sourceCount')
text = re.sub(
    r'\n\s*private static bool IsLegacyTreeProxySample\(int3 voxel\).*?\n\s*}\n\n\s*private static int FindVisibleSourceIndex',
    '\n\n        private static int FindVisibleSourceIndex', text, flags=re.S)
# Replace filtered source selection with evenly sampled source indices.
text = re.sub(
    r'private static int FindVisibleSourceIndex\(ShowcaseWorld\.DetachedVoxelChunk chunk,\n\s*int ordinal, int visibleCount,\n\s*int sourceCount\)\n\s*\{.*?\n\s*}\n\n\s*private static Vector4 MaterialColour',
    '''private static int FindVisibleSourceIndex(ShowcaseWorld.DetachedVoxelChunk chunk,\n                                                  int ordinal, int visibleCount,\n                                                  int sourceCount)\n        {\n            if (sourceCount <= 1) return 0;\n            return math.min(sourceCount - 1,\n                            ordinal * sourceCount / math.max(1, visibleCount));\n        }\n\n        private static Vector4 MaterialColour''', text, flags=re.S)
debris.write_text(text)

for rel in [
    'Assets/VoxelEngine/Rendering/Vegetation/ProceduralTreeRegistry.cs',
    'Assets/VoxelEngine/Rendering/Vegetation/ProceduralTreeRegistry.cs.meta',
    'Assets/Scenes/Showcase/ProceduralTreeDamageBridge.cs',
    'Assets/Scenes/Showcase/ProceduralTreeDamageBridge.cs.meta',
]:
    path = Path(rel)
    if path.exists():
        path.unlink()

legacy_needles = [
    'ProceduralTreeRegistry',
    'LegacyHiddenHardBrick',
    'LegacyHiddenSmoothBrick',
    'LegacyProxyRenderChunk',
    'CoarseLegacyProxyRenderChunk',
    'legacy voxel tree proxy',
]
violations = []
for path in ROOT.rglob('*.cs'):
    text = path.read_text()
    for needle in legacy_needles:
        if needle in text:
            violations.append(f'{path}: {needle}')
if violations:
    raise SystemExit('Legacy tree cutover incomplete:\n' + '\n'.join(violations))

print('Semantic tree Core cutover complete.')
