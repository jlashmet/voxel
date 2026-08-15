from pathlib import Path
import json
import re
import subprocess
import uuid

ROOT = Path('.')
source_dir = ROOT / 'Assets/VoxelEngine/Core/Features'
api_dir = ROOT / 'Assets/VoxelEngine/Structures/Api'

api_files = {
    'AnchorSpec.cs': 'AnchorSpec.cs',
    'FeatureBudget.cs': 'FeatureBudget.cs',
    'FeatureCatalogue.cs': 'FeatureCatalogue.cs',
    'FeatureDefinition.cs': 'FeatureDefinition.cs',
    'FeatureHash.cs': 'FeatureHash.cs',
    'ParameterSpec.cs': 'ParameterSpec.cs',
    'PlacementRule.cs': 'PlacementRule.cs',
    'Primitive.cs': 'Primitive.cs',
    'ShapeOps.cs': 'ShapeOps.cs',
    'CatalogueLoader.cs': 'FeatureCatalogueBuilder.cs',
}
expected_runtime = {
    'ArchFeature.cs',
    'BondedBlockVeneer.cs',
    'FeatureGeneration.cs',
    'PrimitiveRasteriser.cs',
    'ProfileBlockStore.cs',
    'ShapeProgram.cs',
}
actual_root_cs = {p.name for p in source_dir.glob('*.cs')}
expected_all = set(api_files) | expected_runtime
assert actual_root_cs == expected_all, (
    'Unclassified Core/Features files.\n'
    f'Unexpected: {sorted(actual_root_cs - expected_all)}\n'
    f'Missing: {sorted(expected_all - actual_root_cs)}'
)

emitters = source_dir / 'Emitters'
assert emitters.is_dir()
assert list(emitters.glob('*.cs')), 'Expected feature emitters'

api_dir.mkdir(parents=True, exist_ok=True)

# Move source assets and metas together so Unity GUIDs stay stable.
for old_name, new_name in api_files.items():
    src = source_dir / old_name
    dst = api_dir / new_name
    subprocess.run(['git', 'mv', str(src), str(dst)], check=True)
    meta = Path(str(src) + '.meta')
    meta_dst = Path(str(dst) + '.meta')
    assert meta.exists(), f'missing meta for {src}'
    subprocess.run(['git', 'mv', str(meta), str(meta_dst)], check=True)

# Canonical API namespace + clean builder rename, with no compatibility type.
for path in api_dir.glob('*.cs'):
    text = path.read_text()
    assert 'namespace VoxelEngine.Core.Features' in text, path
    text = text.replace('namespace VoxelEngine.Core.Features', 'namespace VoxelEngine.Structures.Api', 1)
    if path.name == 'FeatureCatalogueBuilder.cs':
        assert 'public static class CatalogueLoader' in text
        text = text.replace('CatalogueLoader', 'FeatureCatalogueBuilder')
    path.write_text(text)

# Rename every real caller in the repository in the same cutover; no forwarding alias survives.
for base in [ROOT / 'Assets', ROOT / 'Packages']:
    for path in base.rglob('*.cs'):
        if path.is_relative_to(api_dir):
            continue
        text = path.read_text()
        if 'CatalogueLoader' in text:
            text = text.replace('CatalogueLoader', 'FeatureCatalogueBuilder')
            path.write_text(text)

# Files still in Runtime/Core and external authors can temporarily need both the old runtime
# namespace and the new API namespace. Add the explicit API import only when a moved contract token
# is actually named. This is intentionally additive; old runtime imports are removed with Runtime.
api_tokens = (
    'AnchorSpec', 'ResolvedAnchor', 'SlotSpec',
    'FeatureBudget', 'FeatureCatalogue', 'FeatureDefinition', 'FeatureKind', 'BasePlaneRule',
    'FeatureHash', 'ParameterSpec', 'ParameterSet', 'PlacementRule', 'ExplicitPlacement',
    'Primitive', 'PrimitiveMode', 'PrimitiveShape', 'PrismProfile',
    'ShapeOp', 'ShapeOps', 'ArithmeticOp', 'CatalogueLoadResult', 'FeatureCatalogueBuilder',
)
for base in [ROOT / 'Assets', ROOT / 'Packages']:
    for path in base.rglob('*.cs'):
        if path.is_relative_to(api_dir):
            continue
        text = path.read_text()
        if 'using VoxelEngine.Structures.Api;' in text:
            continue
        if not any(re.search(r'\b' + re.escape(token) + r'\b', text) for token in api_tokens):
            continue
        namespace_pos = text.find('namespace ')
        if namespace_pos < 0:
            continue
        prefix = text[:namespace_pos]
        suffix = text[namespace_pos:]
        if prefix and not prefix.endswith('\n'):
            prefix += '\n'
        text = prefix + 'using VoxelEngine.Structures.Api;\n\n' + suffix
        path.write_text(text)

# New explicit API assembly. No runtime or Core dependency is permitted here.
asmdef = {
    'name': 'VoxelEngine.Structures.Api',
    'rootNamespace': 'VoxelEngine.Structures.Api',
    'references': ['VoxelEngine.Storage.Api', 'Unity.Collections', 'Unity.Mathematics'],
    'includePlatforms': [],
    'excludePlatforms': [],
    'allowUnsafeCode': True,
    'overrideReferences': False,
    'precompiledReferences': [],
    'autoReferenced': False,
    'defineConstraints': [],
    'versionDefines': [],
    'noEngineReferences': False,
}
(api_dir / 'VoxelEngine.Structures.Api.asmdef').write_text(json.dumps(asmdef, indent=2) + '\n')
(api_dir / 'VoxelEngine.Structures.Api.asmdef.meta').write_text(
    'fileFormatVersion: 2\n'
    f'guid: {uuid.uuid4().hex}\n'
    'AssemblyDefinitionImporter:\n'
    '  externalObjects: {}\n'
    '  userData:\n'
    '  assetBundleName:\n'
    '  assetBundleVariant:\n'
)
api_meta = ROOT / 'Assets/VoxelEngine/Structures/Api.meta'
if not api_meta.exists():
    api_meta.write_text(
        'fileFormatVersion: 2\n'
        f'guid: {uuid.uuid4().hex}\n'
        'folderAsset: yes\n'
        'DefaultImporter:\n'
        '  externalObjects: {}\n'
        '  userData:\n'
        '  assetBundleName:\n'
        '  assetBundleVariant:\n'
    )

# Transitional implementation owners explicitly reference the API until Runtime moves.
def add_reference(path_string, reference):
    path = ROOT / path_string
    data = json.loads(path.read_text())
    refs = data.setdefault('references', [])
    if reference not in refs:
        refs.append(reference)
    path.write_text(json.dumps(data, indent=2) + '\n')

add_reference('Assets/VoxelEngine/Core/VoxelEngine.Core.asmdef', 'VoxelEngine.Structures.Api')
add_reference('Assets/VoxelEngine/Structures/VoxelEngine.Structures.asmdef', 'VoxelEngine.Structures.Api')
add_reference('Packages/com.mountingforce.worldgen/Runtime/Voxel/MountingForce.WorldGen.Voxel.asmdef', 'VoxelEngine.Structures.Api')

# Hard guarantees for this slice.
for path in api_dir.glob('*.cs'):
    text = path.read_text()
    assert 'VoxelEngine.Core.Features' not in text, path
    assert 'VoxelEngine.Structures.Runtime' not in text, path
assert not (source_dir / 'CatalogueLoader.cs').exists()
assert not any('class CatalogueLoader' in p.read_text() for p in (ROOT / 'Assets').rglob('*.cs'))
assert not any('class CatalogueLoader' in p.read_text() for p in (ROOT / 'Packages').rglob('*.cs'))

print('Structures.Api extracted:', ', '.join(sorted(p.name for p in api_dir.glob('*.cs'))))
