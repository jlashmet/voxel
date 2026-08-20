from __future__ import annotations

from dataclasses import dataclass
import math


Rows = tuple[tuple[tuple[int, int], ...], ...]


@dataclass(frozen=True)
class ComponentSelection:
    rows: Rows
    component_count: int
    kept_component_count: int
    largest_pixels: int
    minimum_pixels: int
    kept_pixels: int


def select_meaningful_components(
    rows: Rows,
    *,
    minimum_pixels: int = 32,
    relative_to_largest: float = 0.001,
) -> ComponentSelection:
    """Keep substantial 8-connected foreground islands and discard tiny speckles.

    Character and garment turnarounds are expected to be one connected silhouette,
    but rigid equipment can legitimately contain detached gems, guards, ornaments,
    floating straps, or multipart props. A largest-component-only mask deletes those
    references. This selector keeps every component that is either the largest or
    at least max(minimum_pixels, relative_to_largest * largest) pixels.
    """

    parent: list[int] = []
    weight: list[int] = []
    row_nodes: list[list[tuple[int, int, int]]] = []

    def make_node(pixel_count: int) -> int:
        node = len(parent)
        parent.append(node)
        weight.append(pixel_count)
        return node

    def find(node: int) -> int:
        while parent[node] != node:
            parent[node] = parent[parent[node]]
            node = parent[node]
        return node

    def union(a: int, b: int) -> None:
        root_a = find(a)
        root_b = find(b)
        if root_a == root_b:
            return
        if weight[root_a] < weight[root_b]:
            root_a, root_b = root_b, root_a
        parent[root_b] = root_a
        weight[root_a] += weight[root_b]

    previous: list[tuple[int, int, int]] = []
    for runs in rows:
        current: list[tuple[int, int, int]] = []
        previous_index = 0
        for start, end in runs:
            node = make_node(end - start + 1)
            while previous_index < len(previous) and previous[previous_index][1] < start - 1:
                previous_index += 1
            candidate = previous_index
            while candidate < len(previous) and previous[candidate][0] <= end + 1:
                union(node, previous[candidate][2])
                candidate += 1
            current.append((start, end, node))
        row_nodes.append(current)
        previous = current

    if not parent:
        return ComponentSelection(
            rows=rows,
            component_count=0,
            kept_component_count=0,
            largest_pixels=0,
            minimum_pixels=max(1, int(minimum_pixels)),
            kept_pixels=0,
        )

    roots = {find(node) for node in range(len(parent))}
    root_weights = {root: weight[find(root)] for root in roots}
    largest = max(root_weights.values())
    threshold = max(
        max(1, int(minimum_pixels)),
        int(math.ceil(largest * max(0.0, float(relative_to_largest)))),
    )
    kept_roots = {root for root, pixels in root_weights.items() if pixels >= threshold}
    # The threshold can only exclude the largest if a caller supplies a minimum
    # larger than the entire source. The largest subject must always survive.
    largest_root = max(root_weights, key=root_weights.get)
    kept_roots.add(largest_root)

    selected = tuple(
        tuple(
            (start, end)
            for start, end, node in row
            if find(node) in kept_roots
        )
        for row in row_nodes
    )
    kept_pixels = sum(root_weights[root] for root in kept_roots)
    return ComponentSelection(
        rows=selected,
        component_count=len(roots),
        kept_component_count=len(kept_roots),
        largest_pixels=largest,
        minimum_pixels=threshold,
        kept_pixels=kept_pixels,
    )
