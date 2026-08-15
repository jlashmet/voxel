from pathlib import Path


def replace_once(path, old, new):
    text = path.read_text()
    if old not in text:
        raise SystemExit(f"expected seam not found in {path}: {old[:80]!r}")
    path.write_text(text.replace(old, new, 1))
    print(f"updated {path}")

surface = Path("Assets/VoxelEngine/Core/Storage/SurfaceCatalogue.cs")
replace_once(
    surface,
    "public unsafe struct CoatingCatalogue : ICoatingAuthoringCatalogue",
    "public unsafe struct CoatingCatalogue : ICoatingAuthoringCatalogue, ICoatingPresentationCatalogue")
replace_once(
    surface,
    """        public bool Allows(byte coatingId, byte materialId) =>
            coatingId == Coatings.None || Get(coatingId).Allows(materialId);""",
    """        CoatingReadDefinition ICoatingPresentationCatalogue.GetPresentation(byte coatingId)
        {
            CoatingDefinition definition = Get(coatingId);
            return new CoatingReadDefinition
            {
                StableId = definition.StableId,
                AllowedMaterialMask = definition.AllowedMaterialMask,
                Displacement = definition.Displacement,
                DecorationShape = (VoxelEngine.Storage.Api.SurfaceDecorationShape)definition.DecorationShape,
                DecorationDensity = definition.DecorationDensity,
                DecorationRadiusQ4 = definition.DecorationRadiusQ4,
                DecorationHeightQ4 = definition.DecorationHeightQ4,
                DecorationDropQ4 = definition.DecorationDropQ4,
                DecorationSeparation = definition.DecorationSeparation,
                DecorationFaceMask = definition.DecorationFaceMask,
            };
        }

        public static implicit operator CoatingCatalogueView(CoatingCatalogue source) =>
            CoatingCatalogueView.Capture(in source);

        public bool Allows(byte coatingId, byte materialId) =>
            coatingId == Coatings.None || Get(coatingId).Allows(materialId);""")
replace_once(
    surface,
    "public unsafe struct SurfaceCatalogue : ISurfaceStyleAuthoringCatalogue",
    "public unsafe struct SurfaceCatalogue : ISurfaceStyleAuthoringCatalogue, ISurfacePresentationCatalogue")
replace_once(
    surface,
    """        private static void Canonicalize(ref byte a, ref byte b)
        {""",
    """        SurfaceStyleReadDefinition ISurfacePresentationCatalogue.GetPresentation(ushort styleId)
        {
            SurfaceStyleDefinition definition = Get(styleId);
            return new SurfaceStyleReadDefinition
            {
                StableId = definition.StableId,
                Reconstruction = (VoxelEngine.Storage.Api.SurfaceReconstruction)definition.Reconstruction,
                Curvature = definition.Curvature,
                JoinGroup = definition.JoinGroup,
                PreserveSharpFeatures = definition.PreserveSharpFeatures,
            };
        }

        SurfaceJoinReadRule ISurfacePresentationCatalogue.GetPresentationJoin(byte groupA, byte groupB)
        {
            SurfaceJoinRule rule = GetJoin(groupA, groupB);
            return new SurfaceJoinReadRule
            {
                Compatibility = (VoxelEngine.Storage.Api.SurfaceCompatibility)rule.Compatibility,
                Continuity = (VoxelEngine.Storage.Api.SurfaceContinuity)rule.Continuity,
                BlendWidth = rule.BlendWidth,
                DominantGroup = rule.DominantGroup,
                TransitionStyleId = rule.TransitionStyleId,
                PreserveSharpFeature = rule.PreserveSharpFeature,
            };
        }

        public static implicit operator SurfaceCatalogueView(SurfaceCatalogue source) =>
            SurfaceCatalogueView.Capture(in source);

        private static void Canonicalize(ref byte a, ref byte b)
        {""")

journal = Path("Assets/VoxelEngine/Core/Storage/VoxelChangeJournal.cs")
text = journal.read_text()
start = text.find("    [Flags]\n    public enum VoxelChangeKind")
end = text.find("    /// <summary>\n    /// Bounded append-only world-change stream.", start)
if start < 0 or end < 0:
    raise SystemExit("VoxelChangeJournal domain-value seam not found")
text = text[:start] + text[end:]
text = text.replace("using Unity.Mathematics;", "using Unity.Mathematics;\nusing VoxelEngine.Storage.Api;", 1)
text = text.replace("public sealed class VoxelChangeJournal", "public sealed class VoxelChangeJournal : IVoxelChangeSource", 1)
text = text.replace("VoxelDimensions.RegionVoxelEdge", "VoxelGrid.RegionVoxelEdge")
journal.write_text(text)
print(f"updated {journal}")

root = Path("Assets/VoxelEngine/Rendering/Runtime")
replacements = [
    ("using VoxelEngine.Core.Storage;\n", ""),
    ("MaterialPalette", "MaterialPaletteView"),
    ("SurfaceCatalogue", "SurfaceCatalogueView"),
    ("CoatingCatalogue", "CoatingCatalogueView"),
    ("VoxelChangeJournal", "IVoxelChangeSource"),
    ("SurfaceStyleDefinition", "SurfaceStyleReadDefinition"),
    ("CoatingDefinition", "CoatingReadDefinition"),
    ("SurfaceJoinRule", "SurfaceJoinReadRule"),
    ("VoxelDimensions.MaterialEmpty", "VoxelGrid.MaterialEmpty"),
    ("VoxelDimensions.VoxelsPerBrick", "VoxelReadGrid.VoxelsPerBlock"),
    ("VoxelDimensions.BricksPerRegion", "VoxelReadGrid.BlocksPerRegion"),
    ("VoxelDimensions.RegionEdgeLog2", "VoxelReadGrid.BlocksPerRegionEdgeLog2"),
    ("VoxelDimensions.RegionEdgeMask", "VoxelReadGrid.BlocksPerRegionEdgeMask"),
    ("VoxelDimensions.RegionEdge", "VoxelReadGrid.BlocksPerRegionEdge"),
    ("VoxelDimensions.BrickEdgeLog2", "VoxelReadGrid.BlockEdgeLog2"),
    ("VoxelDimensions.BrickEdgeMask", "VoxelReadGrid.BlockEdgeMask"),
    ("VoxelDimensions.BrickEdge", "VoxelReadGrid.BlockEdge"),
]
changed = []
for path in root.rglob("*.cs"):
    original = path.read_text()
    text = original
    for old, new in replacements:
        text = text.replace(old, new)
    if text != original:
        path.write_text(text)
        changed.append(path)
        print(f"updated {path}")

if len(changed) < 10:
    raise SystemExit(f"expected broad Rendering boundary migration, changed only {len(changed)} files")

# The read-grid API needs one stable aggregate count used by GPU/read batching.
grid = Path("Assets/VoxelEngine/Storage/Api/VoxelReadGrid.cs")
text = grid.read_text()
old = "    public const int BlocksPerRegionEdgeMask = BlocksPerRegionEdge - 1;"
new = old + "\n        public const int BlocksPerRegion = BlocksPerRegionEdge * BlocksPerRegionEdge * BlocksPerRegionEdge;"
if old not in text or "public const int BlocksPerRegion =" in text:
    raise SystemExit("VoxelReadGrid BlocksPerRegion seam not found or already changed")
grid.write_text(text.replace(old, new, 1))
print(f"updated {grid}")

# Core must remain absent from Rendering Runtime after this migration.
for path in root.rglob("*.cs"):
    body = path.read_text()
    if "VoxelEngine.Core.Storage" in body or "VoxelDimensions." in body:
        raise SystemExit(f"Rendering physical Storage dependency remains in {path}")
