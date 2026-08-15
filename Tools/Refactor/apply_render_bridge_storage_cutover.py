from pathlib import Path
import re

BRIDGE = Path("Assets/VoxelEngine/Rendering/RenderFeature/VoxelRenderBridge.cs")
PASS = Path("Assets/VoxelEngine/Rendering/RenderFeature/VoxelRenderPass.cs")
SCHEDULER = Path("Assets/VoxelEngine/Rendering/SurfaceExtraction/VoxelSurfaceScheduler.cs")


def replace_exact(text: str, old: str, new: str, expected: int = 1) -> str:
    count = text.count(old)
    if count != expected:
        raise RuntimeError(f"expected {expected} occurrences, found {count}: {old[:120]!r}")
    return text.replace(old, new)


bridge = BRIDGE.read_text()
bridge = replace_exact(
    bridge,
    "using VoxelEngine.Rendering.SurfaceExtraction;\n",
    "using VoxelEngine.Rendering.SurfaceExtraction;\nusing VoxelEngine.Storage.Api;\n",
)
bridge = replace_exact(
    bridge,
    "    /// <see cref=\"RegionTable\"/> and <see cref=\"BrickPool\"/> are handle-like: copying them copies\n"
    "    /// native container handles, not the data, so the pass reads exactly what the simulation\n"
    "    /// holds. The direction is one-way by construction — the pass consumes the brickmap and\n"
    "    /// produces pixels, never the reverse (Constitution Principle I).\n",
    "    /// Storage is exposed only through the borrowed read-view contract. The render pass never\n"
    "    /// receives the physical region table, brick pool, allocator identity, or pool slots. The\n"
    "    /// direction remains one-way: rendering consumes authoritative reads and produces pixels.\n",
)
bridge = replace_exact(
    bridge,
    "        public RegionTable Table;\n        public BrickPool Pool;\n",
    "        public IRegionReadSource Storage;\n",
)
bridge = replace_exact(
    bridge,
    "        public bool IsValid => Table.IsCreated && Pool.IsCreated\n            && SurfaceCatalogue.CatalogueHash != 0 && CoatingCatalogue.CatalogueHash != 0;",
    "        public bool IsValid => Storage != null\n            && SurfaceCatalogue.CatalogueHash != 0 && CoatingCatalogue.CatalogueHash != 0;",
)
BRIDGE.write_text(bridge)

scheduler = SCHEDULER.read_text()
scheduler = replace_exact(scheduler, "        private RegionReadSource _readSource;\n", "")
scheduler = replace_exact(
    scheduler,
    "        public void Prepare(ref RegionTable table, ref BrickPool pool, in MaterialPalette palette,\n",
    "        public void Prepare(IRegionReadSource storage, in MaterialPalette palette,\n",
)
scheduler = replace_exact(
    scheduler,
    "            _readSource ??= new RegionReadSource(in table, in pool);\n            _readSource.Refresh(in table, in pool);\n\n",
    "            if (storage == null) throw new ArgumentNullException(nameof(storage));\n\n",
)
scheduler = scheduler.replace("_readSource", "storage")
scheduler = replace_exact(scheduler, "            storage = null;\n", "")
for forbidden in ("RegionTable", "BrickPool", "BrickRef"):
    if forbidden in scheduler:
        raise RuntimeError(f"physical Storage type remains in scheduler: {forbidden}")
SCHEDULER.write_text(scheduler)

render_pass = PASS.read_text()
render_pass = replace_exact(
    render_pass,
    "            _scheduler.Prepare(ref world.Table, ref world.Pool, in world.Palette,\n",
    "            _scheduler.Prepare(world.Storage, in world.Palette,\n",
)
PASS.write_text(render_pass)

for path in (BRIDGE, PASS, SCHEDULER):
    text = path.read_text()
    if path != PASS:
        for forbidden in ("RegionTable", "BrickPool", "BrickRef"):
            if forbidden in text:
                raise RuntimeError(f"{path}: physical Storage type remains: {forbidden}")

print("Rendering bridge Storage cutover applied successfully.")
