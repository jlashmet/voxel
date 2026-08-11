from pathlib import Path
import re

ROOT = Path('.')

# Mechanical symbol moves for callers. Exclude the two files being removed below.
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

# Ensure callers of Core tree APIs import the semantic namespace.
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

# VoxelShowcase: the gameplay adapter now calls the Core metre-space damage service and passes
# projectile direction as the semantic impulse. Voxel destruction remains separate.
showcase = Path('Assets/Scenes/Showcase/VoxelShowcase.cs')
text = showcase.read_text()
old = '''                    if (semanticTreeHit || changed > 0)\n                        ProceduralTreeDamageBridge.ApplyExplosion(hit, shot.ImpactRadius);'''
new = '''                    if (semanticTreeHit || changed > 0)\n                    {\n                        float3 impactMetres = (float3)hit * VoxelSurfaceRenderer.VoxelSize;\n                        ProceduralTreeDamageService.ApplyBlast(\n                            impactMetres, shot.ImpactRadius * VoxelSurfaceRenderer.VoxelSize,\n                            (float3)shot.Direction);\n                    }'''
if old not in text:
    # The mechanical replacement may already have changed the type name.
    old = '''                    if (semanticTreeHit || changed > 0)\n                        ProceduralTreeDamageService.ApplyExplosion(hit, shot.ImpactRadius);'''
if old not in text:
    raise SystemExit('Could not find VoxelShowcase tree explosion adapter block')
text = text.replace(old, new, 1)
showcase.write_text(text)

# Hard-surface cache: semantic trees no longer have hidden voxel timber proxies.
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

# Transvoxel cache: no semantic vegetation is encoded as hidden smooth bricks anymore.
trans = Path('Assets/VoxelEngine/Rendering/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
text = trans.read_text()
text = text.replace('using VoxelEngine.Rendering.Vegetation;\n', '')
text = re.sub(r'^.*ProceduralTreeRegistry\.IsLegacyHiddenSmoothBrick\([^\n]*\).*\n', '', text,
              flags=re.M)
text = re.sub(r'^.*ProceduralTreeRegistry\.Version.*\n', '', text, flags=re.M)
# Remove a now-orphaned tree registry version field if present.
text = re.sub(r'^\s*private int _treeRegistryVersion = int\.MinValue;\n', '', text, flags=re.M)
trans.write_text(text)

# Render pass: remove special Surface Nets handoff for old voxel-tree proxy chunks.
render = Path('Assets/VoxelEngine/Rendering/RenderFeature/VoxelRenderPass.cs')
text = render.read_text()
text = text.replace('using VoxelEngine.Rendering.Vegetation;\n', '')
text = text.replace('                bool legacyTreeProxy = ProceduralTreeRegistry.IsLegacyProxyRenderChunk(coordinate);\n', '')
text = text.replace('                bool drawTransvoxel = transvoxelReady\n                    && (_transvoxelActivated || hardReady || legacyTreeProxy);',
                    '                bool drawTransvoxel = transvoxelReady\n                    && (_transvoxelActivated || hardReady);')
text = text.replace('                bool legacyTreeProxy =\n                    ProceduralTreeRegistry.IsLegacyProxyRenderChunk(entry.Coordinate);\n', '')
text = text.replace('                bool drawTransvoxel = transvoxelReady\n                    && (_transvoxelActivated || hardReady || legacyTreeProxy);',
                    '                bool drawTransvoxel = transvoxelReady\n                    && (_transvoxelActivated || hardReady);')
text = text.replace('            ProceduralTreeRegistry.ClearCoarseLegacyProxyRenderChunks();\n', '')
text = text.replace('                if (legacyTreeProxy)\n                    ProceduralTreeRegistry.MarkCoarseLegacyProxyRenderChunk(entry.Coordinate);\n', '')
text = text.replace('            ProceduralTreeRegistry.ClearCoarseLegacyProxyRenderChunks();\n', '')
text = re.sub(
    r'\n\s*// A hard layer no longer owns an entire render chunk\..*?keep an upright copy of the old voxel tree underneath the procedural tree\.\n',
    '\n            // Hard and smooth derived layers may coexist. Surface Nets remains only as a\n            // temporary smooth-terrain warmup source until Transvoxel is ready.\n', text, flags=re.S)
render.write_text(text)

# Remove obsolete runtime owners completely. No compatibility wrapper remains.
for rel in [
    'Assets/VoxelEngine/Rendering/Vegetation/ProceduralTreeRegistry.cs',
    'Assets/VoxelEngine/Rendering/Vegetation/ProceduralTreeRegistry.cs.meta',
    'Assets/Scenes/Showcase/ProceduralTreeDamageBridge.cs',
    'Assets/Scenes/Showcase/ProceduralTreeDamageBridge.cs.meta',
]:
    path = Path(rel)
    if path.exists():
        path.unlink()

# Reject accidental legacy architecture references. Comments are included deliberately.
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
