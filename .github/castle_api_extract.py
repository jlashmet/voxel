from pathlib import Path

API = Path('Assets/VoxelEngine/Structures/Api')
API.mkdir(parents=True, exist_ok=True)
CASTLE = Path('Assets/VoxelEngine/Structures/CastleBuilder.cs')

castle_api = '''using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Dimensions drawn for one castle. Every field is in voxels; one voxel is 10 cm.</summary>
    public struct CastlePlan
    {
        public int3 Centre;

        public int PlateauRadius;
        public int PlateauHeight;
        public int CliffDrop;

        public int BaileyHalfX, BaileyHalfZ;
        public int WallHeight, WallThickness;

        public int TowerRadius, TowerHeight;
        public int GateTowerRadius, GateTowerHeight;

        public int KeepHalfX, KeepHalfZ, KeepHeight;
        public int FloorHeight;
        public int Floors;

        public uint Seed;
    }

    /// <summary>
    /// Deterministic castle landmark geometry shared with API-only world-generation clients.
    /// Construction remains owned by Structures.Runtime.
    /// </summary>
    public static class CastleLayout
    {
        public const int TrapdoorHalfSize = 8;
        public const int ChapelBellTowerSize = 56;
        public const int ChapelBellTowerStairRadius = 16;
        public const int FrontGateWidth = 48;
        public const int FrontGateHeight = 60;
        public const int FrontGateDepth = 4;
        public const int LowerRiverDepth = 88;

        public static int3 TrapdoorCentre(in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int keepMinZ = plan.Centre.z - plan.KeepHalfZ + 60;
            return new int3(plan.Centre.x, baseY, keepMinZ + plan.KeepHalfZ + 40);
        }

        public static int3 FrontGateMinimum(in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            return new int3(plan.Centre.x - FrontGateWidth / 2, baseY + 1,
                            gateZ - plan.WallThickness + 2);
        }

        public static int WaterfallStreamX(in CastlePlan plan) =>
            plan.Centre.x + plan.BaileyHalfX + plan.TowerRadius + 36;

        public static int LowerRiverZAt(in CastlePlan plan, int x)
        {
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            return gateZ - plan.WallThickness - 92
                 + (int)math.round(math.sin((x - plan.Centre.x) * 0.028f) * 8f
                                  + math.sin((x - plan.Centre.x) * 0.071f) * 3f);
        }

        public static int WaterfallLipZ(in CastlePlan plan)
        {
            int streamX = WaterfallStreamX(in plan);
            return LowerRiverZAt(in plan, streamX) + 68;
        }

        public static int3 ChapelBellTowerCentre(in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int keepMinX = plan.Centre.x - plan.KeepHalfX;
            int keepMinZ = plan.Centre.z - plan.KeepHalfZ + 60;
            int keepWidth = plan.KeepHalfX * 2;
            int keepDepth = plan.KeepHalfZ * 2;
            int chapelWidth = math.max(78, keepWidth / 3);
            int chapelDepth = math.max(96, keepDepth * 3 / 5);
            int chapelMinX = keepMinX - chapelWidth + 4;
            int chapelMinZ = keepMinZ + keepDepth - chapelDepth - 38;
            int towerMinX = chapelMinX + 8;
            int towerMinZ = chapelMinZ + chapelDepth - 6;
            return new int3(towerMinX + ChapelBellTowerSize / 2, baseY,
                            towerMinZ + ChapelBellTowerSize / 2);
        }
    }
}
'''
(API / 'CastlePlan.cs').write_text(castle_api)
(API / 'CastlePlan.cs.meta').write_text('fileFormatVersion: 2\nguid: 9d1a67cd8fa34b5cab45e4902ea9b8ac\n')

# Material IDs are semantic authoring values, so the existing Unity asset identity moves to Api.
old_material = Path('Assets/VoxelEngine/Structures/StructureMaterials.cs')
old_material_meta = Path('Assets/VoxelEngine/Structures/StructureMaterials.cs.meta')
new_material = API / 'StructureMaterials.cs'
new_material_meta = API / 'StructureMaterials.cs.meta'
old_material.rename(new_material)
old_material_meta.rename(new_material_meta)
material = new_material.read_text()
old_ns = 'namespace VoxelEngine.Structures\n'
if material.count(old_ns) != 1:
    raise SystemExit(f'StructureMaterials namespace count was {material.count(old_ns)}, expected 1')
new_material.write_text(material.replace(old_ns, 'namespace VoxelEngine.Structures.Api\n', 1))

castle = CASTLE.read_text()
if 'using VoxelEngine.Structures.Api;' not in castle:
    castle = castle.replace('using VoxelEngine.Storage.Api;\n',
                            'using VoxelEngine.Storage.Api;\nusing VoxelEngine.Structures.Api;\n', 1)

plan_block = '''    /// <summary>Dimensions drawn for one castle. Every field is in voxels; one voxel is 10 cm.</summary>\n    public struct CastlePlan\n    {\n        public int3 Centre;\n\n        public int PlateauRadius;\n        public int PlateauHeight;\n        public int CliffDrop;\n\n        public int BaileyHalfX, BaileyHalfZ;\n        public int WallHeight, WallThickness;\n\n        public int TowerRadius, TowerHeight;\n        public int GateTowerRadius, GateTowerHeight;\n\n        public int KeepHalfX, KeepHalfZ, KeepHeight;\n        public int FloorHeight;\n        public int Floors;\n\n        public uint Seed;\n    }\n\n'''
layout_block = '''        public const int TrapdoorHalfSize = 8;\n        public const int ChapelBellTowerSize = 56;\n        public const int ChapelBellTowerStairRadius = 16;\n        public const int FrontGateWidth = 48;\n        public const int FrontGateHeight = 60;\n        public const int FrontGateDepth = 4;\n        public const int LowerRiverDepth = 88;\n\n        /// <summary>Centre of the ground-floor hatch leading to the cellar.</summary>\n        public static int3 TrapdoorCentre(in CastlePlan plan)\n        {\n            int baseY = plan.Centre.y + plan.PlateauHeight;\n            int keepMinZ = plan.Centre.z - plan.KeepHalfZ + 60;\n            return new int3(plan.Centre.x, baseY, keepMinZ + plan.KeepHalfZ + 40);\n        }\n\n        /// <summary>Minimum corner of the operable timber gate in the front gatehouse arch.</summary>\n        public static int3 FrontGateMinimum(in CastlePlan plan)\n        {\n            int baseY = plan.Centre.y + plan.PlateauHeight;\n            int gateZ = plan.Centre.z - plan.BaileyHalfZ;\n            return new int3(plan.Centre.x - FrontGateWidth / 2, baseY + 1,\n                            gateZ - plan.WallThickness + 2);\n        }\n\n        public static int WaterfallStreamX(in CastlePlan plan) =>\n            plan.Centre.x + plan.BaileyHalfX + plan.TowerRadius + 36;\n\n        public static int LowerRiverZAt(in CastlePlan plan, int x)\n        {\n            int gateZ = plan.Centre.z - plan.BaileyHalfZ;\n            return gateZ - plan.WallThickness - 92\n                 + (int)math.round(math.sin((x - plan.Centre.x) * 0.028f) * 8f\n                                  + math.sin((x - plan.Centre.x) * 0.071f) * 3f);\n        }\n\n        public static int WaterfallLipZ(in CastlePlan plan)\n        {\n            int streamX = WaterfallStreamX(in plan);\n            return LowerRiverZAt(in plan, streamX) + 68;\n        }\n\n        /// <summary>Centre of the occupied bell tower accumulated behind the chapel.</summary>\n        public static int3 ChapelBellTowerCentre(in CastlePlan plan)\n        {\n            int baseY = plan.Centre.y + plan.PlateauHeight;\n            int keepMinX = plan.Centre.x - plan.KeepHalfX;\n            int keepMinZ = plan.Centre.z - plan.KeepHalfZ + 60;\n            int keepWidth = plan.KeepHalfX * 2;\n            int keepDepth = plan.KeepHalfZ * 2;\n            int chapelWidth = math.max(78, keepWidth / 3);\n            int chapelDepth = math.max(96, keepDepth * 3 / 5);\n            int chapelMinX = keepMinX - chapelWidth + 4;\n            int chapelMinZ = keepMinZ + keepDepth - chapelDepth - 38;\n            int towerMinX = chapelMinX + 8;\n            int towerMinZ = chapelMinZ + chapelDepth - 6;\n            return new int3(towerMinX + ChapelBellTowerSize / 2, baseY,\n                            towerMinZ + ChapelBellTowerSize / 2);\n        }\n\n'''
for block, label in ((plan_block, 'CastlePlan block'), (layout_block, 'CastleLayout block')):
    count = castle.count(block)
    if count != 1:
        raise SystemExit(f'{label} exact-match count was {count}, expected 1')
    castle = castle.replace(block, '', 1)

symbols = [
    'TrapdoorHalfSize', 'ChapelBellTowerSize', 'ChapelBellTowerStairRadius',
    'FrontGateWidth', 'FrontGateHeight', 'FrontGateDepth', 'LowerRiverDepth',
    'TrapdoorCentre', 'FrontGateMinimum', 'WaterfallStreamX', 'LowerRiverZAt',
    'WaterfallLipZ', 'ChapelBellTowerCentre'
]
for symbol in symbols:
    castle = castle.replace(symbol, f'CastleLayout.{symbol}')
CASTLE.write_text(castle)

for root in (Path('Assets'), Path('Packages')):
    for path in root.rglob('*.cs'):
        if path in {CASTLE, API / 'CastlePlan.cs', new_material}:
            continue
        text = path.read_text()
        original = text
        for symbol in symbols:
            text = text.replace(f'CastleBuilder.{symbol}', f'CastleLayout.{symbol}')
        needs_api = 'CastleLayout.' in text or 'CastlePlan' in text or 'Mat.' in text
        if needs_api and 'using VoxelEngine.Structures.Api;' not in text \
                and 'namespace VoxelEngine.Structures.Api' not in text:
            lines = text.splitlines(True)
            insert = 0
            while insert < len(lines) and lines[insert].startswith('using '):
                insert += 1
            lines.insert(insert, 'using VoxelEngine.Structures.Api;\n')
            text = ''.join(lines)
        if path.as_posix() in {
            'Packages/com.mountingforce.worldgen/Runtime/Voxel/CastleVegetationPlanner.cs',
            'Packages/com.mountingforce.worldgen/Runtime/Voxel/KentridgeVegetationPlanner.cs',
        }:
            text = text.replace('using VoxelEngine.Structures;\n', '')
        if text != original:
            path.write_text(text)

asmdef_path = Path('Packages/com.mountingforce.worldgen/Runtime/Voxel/MountingForce.WorldGen.Voxel.asmdef')
asmdef = asmdef_path.read_text()
old_ref = '    "VoxelEngine.Structures",\n'
if asmdef.count(old_ref) != 1:
    raise SystemExit(f'WorldGen broad Structures ref count was {asmdef.count(old_ref)}, expected 1')
asmdef_path.write_text(asmdef.replace(old_ref, '', 1))
