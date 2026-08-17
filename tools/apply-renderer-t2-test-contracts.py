#!/usr/bin/env python3
"""Update legacy EditMode expectations for demand-driven renderer work, then remove this helper."""
from pathlib import Path

DISCOVERY = Path("Assets/Tests/EditMode/SurfaceBrickDiscoveryTests.cs")
ARCH = Path("Assets/Tests/EditMode/VoxelSurfaceArchitectureTests.cs")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    if old not in text:
        raise RuntimeError(f"could not find {label}")
    return text.replace(old, new, 1)


def main() -> None:
    text = DISCOVERY.read_text()
    old = '''            Assert.AreEqual(1, cache.KnownCount);
            Assert.AreEqual(1, cache.DirtyCount);

            Assert.AreEqual(0, cache.DiscoverSurfaceBricks(new[] { brick }),
                "Later publication slices for the same unchanged region must not create a new "
              + "source generation for an already-known chunk.");
            Assert.AreEqual(1, cache.KnownCount);
            Assert.AreEqual(1, cache.DirtyCount);

            // Real edits keep the old semantics: known chunks are explicitly invalidated. The
            // dirty set coalesces membership, but the call is still routed through the mutation
            // path rather than discovery admission.
            cache.InvalidateSurfaceBricks(new[] { brick });
            Assert.AreEqual(1, cache.KnownCount);
            Assert.AreEqual(1, cache.DirtyCount);'''
    new = '''            Assert.AreEqual(1, cache.KnownCount);
            Assert.AreEqual(0, cache.DirtyCount,
                "Discovery must admit render ownership without creating geometry work.");

            Assert.AreEqual(0, cache.DiscoverSurfaceBricks(new[] { brick }),
                "Later publication slices for the same unchanged region must not create a new "
              + "source generation for an already-known chunk.");
            Assert.AreEqual(1, cache.KnownCount);
            Assert.AreEqual(0, cache.DirtyCount);

            // A real edit invalidates the generation proof but remains cold until visible
            // coverage explicitly requests it. This is the demand-driven T2 contract.
            cache.InvalidateSurfaceBricks(new[] { brick });
            Assert.AreEqual(1, cache.KnownCount);
            Assert.AreEqual(0, cache.DirtyCount);
            Assert.True(cache.RequestHierarchyCoverage(
                int3.zero, SurfaceBuildPriority.VisibleRefinement));
            Assert.AreEqual(1, cache.DirtyCount,
                "Explicit coverage demand must enqueue the invalidated generation.");'''
    text = replace_once(text, old, new, "surface discovery demand contract")
    DISCOVERY.write_text(text)

    text = ARCH.read_text()
    old = '''            cache.InvalidateSurfaceBricks(new[] { new int3(1, 1, 1) });
            Assert.AreEqual(1, cache.KnownCount);
            Assert.AreEqual(1, cache.DirtyCount);

            // Brick eight begins the next 64-voxel extraction chunk. Because it lies on all three
            // local zero faces, the one-brick sampling halo also invalidates seven neighbours.
            cache.InvalidateSurfaceBricks(new[] { new int3(8, 8, 8) });
            Assert.AreEqual(8, cache.KnownCount);
            Assert.AreEqual(8, cache.DirtyCount);'''
    new = '''            cache.InvalidateSurfaceBricks(new[] { new int3(1, 1, 1) });
            Assert.AreEqual(1, cache.KnownCount);
            Assert.AreEqual(0, cache.DirtyCount,
                "A cold mutation advances truth without creating offscreen render work.");
            Assert.True(cache.RequestHierarchyCoverage(
                int3.zero, SurfaceBuildPriority.VisibleRefinement));
            Assert.AreEqual(1, cache.DirtyCount);

            // Brick eight begins the next 64-voxel extraction chunk. Because it lies on all three
            // local zero faces, the one-brick sampling halo admits seven neighbours too. Those
            // invalidations stay cold: only the already-requested origin remains queued.
            cache.InvalidateSurfaceBricks(new[] { new int3(8, 8, 8) });
            Assert.AreEqual(8, cache.KnownCount);
            Assert.AreEqual(1, cache.DirtyCount,
                "Halo invalidation must not flood newly admitted cold neighbours into the queue.");'''
    text = replace_once(text, old, new, "bounded invalidation demand contract")
    ARCH.write_text(text)


if __name__ == "__main__":
    main()
