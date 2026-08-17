#!/usr/bin/env python3
"""CI-only experiment for demand-driven solid surface residency.

This file is intentionally temporary. It patches the disposable Actions checkout so the
renderer LOD acceptance gate can validate the residency hypothesis before the equivalent
behavior is committed to CpuTransvoxelChunkCache itself.
"""

from pathlib import Path


PATH = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    if old not in text:
        raise RuntimeError(f"renderer probe could not find {label}")
    return text.replace(old, new, 1)


def main() -> None:
    text = PATH.read_text()

    text = replace_once(
        text,
        """                    if (!OwnsShard(chunk) || _known.Contains(chunk)) continue;
                    if (!TrackKnown(chunk)) continue;
                    Invalidate(chunk);
                    admitted++;""",
        """                    if (!OwnsShard(chunk) || _known.Contains(chunk)) continue;
                    if (!TrackKnown(chunk)) continue;
                    // Discovery establishes ownership only. Building every discovered chunk,
                    // including offscreen shells, fills the arena before player-visible work.
                    admitted++;""",
        "eager discovery invalidation",
    )

    text = replace_once(
        text,
        """            if (!_entries.TryGetValue(coordinate, out Entry entry) || !entry.Ready)
            {
                // A known-empty chunk is a completed build with nothing to draw, not a hole.
                if (!_emptyVersions.ContainsKey(coordinate)) MissingVisibleCount++;
                return;
            }""",
        """            if (!_entries.TryGetValue(coordinate, out Entry entry) || !entry.Ready)
            {
                // A known-empty chunk is a completed build with nothing to draw, not a hole.
                if (!_emptyVersions.ContainsKey(coordinate))
                {
                    MissingVisibleCount++;
                    // Visibility is demand. Keep one generation queued until it either publishes
                    // or proves empty; do not restart an active build every render pass.
                    bool active = _build.Active && _build.Coordinate.Equals(coordinate);
                    if (!active && !_dirty.Contains(coordinate))
                    {
                        if (!_desiredVersions.ContainsKey(coordinate))
                            _desiredVersions[coordinate] = ++_versionCounter;
                        MarkDirty(coordinate);
                    }
                }
                return;
            }""",
        "visible-demand admission",
    )

    text = replace_once(
        text,
        """            if (_entries.TryGetValue(victim, out Entry entry)) RecycleEntry(entry);
            _entries.Remove(victim);
            MarkDirty(victim);
            return true;""",
        """            if (_entries.TryGetValue(victim, out Entry entry)) RecycleEntry(entry);
            _entries.Remove(victim);
            // Keep the coordinate known but cold. Visibility will request it again if needed;
            // immediate redirty creates an arena eviction/rebuild loop for offscreen geometry.
            return true;""",
        "arena-pressure redirty",
    )

    text = replace_once(
        text,
        """            if (_entries.TryGetValue(victim, out Entry entry)) RecycleEntry(entry);
            _entries.Remove(victim);
            MarkDirty(victim);
        }""",
        """            if (_entries.TryGetValue(victim, out Entry entry)) RecycleEntry(entry);
            _entries.Remove(victim);
            // Capacity eviction is a cache miss, not a mutation. Rebuild only on visible demand.
        }""",
        "capacity redirty",
    )

    # Initial presentation-rule synchronization occurs after discovery. Invalidating every merely
    # known coordinate defeats demand-driven admission. Rebuild materialized meshes; visible cold
    # coordinates will be requested by CollectVisibleCoordinate.
    old = "            foreach (int3 chunk in _known) Invalidate(chunk);"
    count = text.count(old)
    if count < 4:
        raise RuntimeError(f"expected at least four known-set invalidation loops, found {count}")
    text = text.replace(old, "            foreach (int3 chunk in _entries.Keys) Invalidate(chunk);")

    PATH.write_text(text)
    print("Applied CI-only demand-driven renderer probe")


if __name__ == "__main__":
    main()
