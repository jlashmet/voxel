#!/usr/bin/env python3
"""Bake a static COLLADA mesh into compact voxel x-runs for DragonStatueMeshVoxelData.

This development tool intentionally uses only the Python standard library so it can run on the
self-hosted Unity runner without additional packages. It samples every triangle densely enough to
form a closed 3-voxel-thick voxel shell, preserving thin features such as wings and horns. Runtime
code consumes only the generated voxel runs; COLLADA and mesh rendering are not part of the game
path.
"""

from __future__ import annotations

import argparse
import json
import math
import pathlib
import xml.etree.ElementTree as ET
from collections import defaultdict

NS = {"c": "http://www.collada.org/2005/11/COLLADASchema"}


def parse_mesh(path: pathlib.Path):
    root = ET.parse(path).getroot()
    geometry = root.find(".//c:geometry[@id='dragon-mesh']", NS)
    if geometry is None:
        geometry = root.find(".//c:geometry", NS)
    if geometry is None:
        raise RuntimeError("No COLLADA geometry found")

    mesh = geometry.find("c:mesh", NS)
    positions_node = mesh.find("c:source[@id='dragon-mesh-positions']/c:float_array", NS)
    if positions_node is None:
        for source in mesh.findall("c:source", NS):
            if "position" in source.attrib.get("id", "").lower():
                positions_node = source.find("c:float_array", NS)
                if positions_node is not None:
                    break
    if positions_node is None or not positions_node.text:
        raise RuntimeError("No position array found")

    values = [float(v) for v in positions_node.text.split()]
    if len(values) % 3:
        raise RuntimeError("Position array is not xyz triples")
    vertices = [tuple(values[i:i + 3]) for i in range(0, len(values), 3)]

    triangles_node = mesh.find("c:triangles", NS)
    if triangles_node is None:
        raise RuntimeError("No triangle set found")
    inputs = triangles_node.findall("c:input", NS)
    stride = max(int(i.attrib.get("offset", "0")) for i in inputs) + 1
    vertex_offset = next(int(i.attrib.get("offset", "0")) for i in inputs if i.attrib.get("semantic") == "VERTEX")
    p = triangles_node.find("c:p", NS)
    if p is None or not p.text:
        raise RuntimeError("Triangle index buffer is empty")
    raw = [int(v) for v in p.text.split()]
    corners = [raw[i + vertex_offset] for i in range(0, len(raw), stride)]
    if len(corners) % 3:
        raise RuntimeError("Triangle corner count is not divisible by three")
    faces = [tuple(corners[i:i + 3]) for i in range(0, len(corners), 3)]
    return vertices, faces


def transform(vertices, target_height: int):
    # COLLADA declares Z_UP. Map source x->voxel x, source z->voxel y (up), source y->voxel z.
    xs = [v[0] for v in vertices]
    ys = [v[1] for v in vertices]
    zs = [v[2] for v in vertices]
    zmin, zmax = min(zs), max(zs)
    scale = target_height / max(1e-9, zmax - zmin)
    cx = (min(xs) + max(xs)) * 0.5
    cy = (min(ys) + max(ys)) * 0.5
    transformed = [
        ((x - cx) * scale, (z - zmin) * scale + 3.0, (y - cy) * scale)
        for x, y, z in vertices
    ]
    return transformed, scale


def add_ball(voxels: set[tuple[int, int, int]], x: int, y: int, z: int, radius: int):
    for dz in range(-radius, radius + 1):
        for dy in range(-radius, radius + 1):
            for dx in range(-radius, radius + 1):
                if dx * dx + dy * dy + dz * dz <= radius * radius + 1:
                    voxels.add((x + dx, y + dy, z + dz))


def voxelize(vertices, faces, thickness: int):
    voxels: set[tuple[int, int, int]] = set()
    for face_index, (ia, ib, ic) in enumerate(faces):
        a, b, c = vertices[ia], vertices[ib], vertices[ic]
        edge = max(math.dist(a, b), math.dist(b, c), math.dist(c, a))
        # <= ~0.38 voxel spacing along the longest edge. This is deliberately oversampled because
        # the low-poly source contains long triangles across wings and tail.
        n = max(1, int(math.ceil(edge * 2.65)))
        for i in range(n + 1):
            u = i / n
            remaining = n - i
            for j in range(remaining + 1):
                v = j / n
                w = 1.0 - u - v
                px = a[0] * w + b[0] * u + c[0] * v
                py = a[1] * w + b[1] * u + c[1] * v
                pz = a[2] * w + b[2] * u + c[2] * v
                add_ball(voxels, int(round(px)), int(round(py)), int(round(pz)), thickness)
        if face_index and face_index % 250 == 0:
            print(f"sampled {face_index}/{len(faces)} triangles; {len(voxels):,} voxels")
    return voxels


def make_runs(voxels):
    by_yz = defaultdict(list)
    for x, y, z in voxels:
        by_yz[(y, z)].append(x)
    runs = []
    for (y, z), xs in sorted(by_yz.items()):
        xs.sort()
        start = prev = xs[0]
        for x in xs[1:]:
            if x == prev + 1:
                prev = x
                continue
            runs.append((y, z, start, prev))
            start = prev = x
        runs.append((y, z, start, prev))
    return runs


def write_csharp(path: pathlib.Path, runs, bounds):
    path.parent.mkdir(parents=True, exist_ok=True)
    flat = [n for run in runs for n in run]
    lines = []
    for i in range(0, len(flat), 32):
        lines.append("            " + ", ".join(str(n) for n in flat[i:i + 32]) + ",")
    minx, miny, minz, maxx, maxy, maxz = bounds
    source = f'''using Game.Materials.Api;\nusing Unity.Mathematics;\nusing VoxelEngine.Structures.Api;\n\nnamespace Game.Structures.Runtime\n{{\n    /// <summary>\n    /// Generated from the CC0 Cethiel/Drummyfish dragon mesh by tools/bake-dragon-mesh.py.\n    /// This is literal voxel run data; runtime rendering remains canonical voxel storage/surface extraction.\n    /// </summary>\n    public static class DragonStatueMeshVoxelData\n    {{\n        public static readonly int3 LocalMin = new int3({minx}, {miny}, {minz});\n        public static readonly int3 LocalSize = new int3({maxx - minx + 1}, {maxy - miny + 1}, {maxz - minz + 1});\n\n        private static readonly short[] Runs =\n        {{\n{chr(10).join(lines)}\n        }};\n\n        public static void Author(IStructureAuthoringSession authoring, int3 origin)\n        {{\n            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));\n            for (int i = 0; i < Runs.Length; i += 4)\n            {{\n                int y = Runs[i];\n                int z = Runs[i + 1];\n                int x0 = Runs[i + 2];\n                int x1 = Runs[i + 3];\n                for (int x = x0; x <= x1; x++)\n                    authoring.Set(origin.x + x, origin.y + y, origin.z + z, GameMaterialIds.Slate);\n            }}\n        }}\n    }}\n}}\n'''
    path.write_text(source, encoding="utf-8")


def svg_projection(path: pathlib.Path, vertices, faces, axes, title):
    ia, ib = axes
    pts = [(v[ia], v[ib]) for v in vertices]
    minx, maxx = min(p[0] for p in pts), max(p[0] for p in pts)
    miny, maxy = min(p[1] for p in pts), max(p[1] for p in pts)
    width, height, pad = 900, 900, 30
    sx = (width - 2 * pad) / max(1e-6, maxx - minx)
    sy = (height - 2 * pad) / max(1e-6, maxy - miny)
    scale = min(sx, sy)
    def xy(p):
        return pad + (p[0] - minx) * scale, height - pad - (p[1] - miny) * scale
    segments = []
    for f in faces:
        q = [xy(pts[i]) for i in f]
        segments.append(f'<path d="M {q[0][0]:.1f},{q[0][1]:.1f} L {q[1][0]:.1f},{q[1][1]:.1f} L {q[2][0]:.1f},{q[2][1]:.1f} Z"/>')
    path.write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">'\
        f'<rect width="100%" height="100%" fill="white"/><g fill="none" stroke="#222" stroke-width="0.7" opacity="0.65">'\
        + ''.join(segments) + f'</g><text x="20" y="24" font-family="sans-serif">{title}</text></svg>',
        encoding="utf-8")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--input", required=True, type=pathlib.Path)
    ap.add_argument("--output", required=True, type=pathlib.Path)
    ap.add_argument("--height", type=int, default=160)
    ap.add_argument("--thickness", type=int, default=1)
    ap.add_argument("--diagnostics", type=pathlib.Path)
    args = ap.parse_args()

    raw_vertices, faces = parse_mesh(args.input)
    vertices, scale = transform(raw_vertices, args.height)
    xs, ys, zs = zip(*vertices)
    print(f"vertices={len(vertices)} faces={len(faces)} scale={scale:.3f}")
    print(f"transformed bounds x=[{min(xs):.1f},{max(xs):.1f}] y=[{min(ys):.1f},{max(ys):.1f}] z=[{min(zs):.1f},{max(zs):.1f}]")
    voxels = voxelize(vertices, faces, args.thickness)
    runs = make_runs(voxels)
    minx = min(v[0] for v in voxels); maxx = max(v[0] for v in voxels)
    miny = min(v[1] for v in voxels); maxy = max(v[1] for v in voxels)
    minz = min(v[2] for v in voxels); maxz = max(v[2] for v in voxels)
    write_csharp(args.output, runs, (minx, miny, minz, maxx, maxy, maxz))
    print(f"voxels={len(voxels):,} runs={len(runs):,} output={args.output}")

    if args.diagnostics:
        args.diagnostics.mkdir(parents=True, exist_ok=True)
        svg_projection(args.diagnostics / "dragon-front.svg", vertices, faces, (0, 1), "front: X / Z-up")
        svg_projection(args.diagnostics / "dragon-side.svg", vertices, faces, (2, 1), "side: Y / Z-up")
        svg_projection(args.diagnostics / "dragon-top.svg", vertices, faces, (0, 2), "top: X / Y")
        (args.diagnostics / "summary.json").write_text(json.dumps({
            "vertices": len(vertices), "faces": len(faces), "scale": scale,
            "bounds": {"x": [min(xs), max(xs)], "y": [min(ys), max(ys)], "z": [min(zs), max(zs)]},
            "voxels": len(voxels), "runs": len(runs),
        }, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
