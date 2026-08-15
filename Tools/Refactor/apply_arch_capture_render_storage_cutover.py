from pathlib import Path

path = Path("Assets/VoxelEngine/CI/Editor/ArchStudyCapture.cs")
text = path.read_text()
old = """                VoxelRenderBridge.SolidBuildBudgetMs = 12.0;
                VoxelRenderBridge.WaterBuildBudgetMs = 2.0;
                VoxelRenderBridge.Source = () => new VoxelWorldView
                {
                    Table = table,
                    Pool = pool,
                    Palette = palette,
"""
new = """                VoxelRenderBridge.SolidBuildBudgetMs = 12.0;
                VoxelRenderBridge.WaterBuildBudgetMs = 2.0;
                var readSource = new RegionReadSource(in table, in pool, changes);
                VoxelRenderBridge.Source = () => new VoxelWorldView
                {
                    Storage = readSource,
                    Palette = palette,
"""
count = text.count(old)
if count != 1:
    raise RuntimeError(f"expected one ArchStudy render-world initializer, found {count}")
text = text.replace(old, new)
for token in ("Table = table", "Pool = pool"):
    if token in text:
        raise RuntimeError(f"stale VoxelWorldView physical storage initializer remains: {token}")
path.write_text(text)
print("ArchStudy capture render Storage cutover applied successfully.")
