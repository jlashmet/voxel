#!/usr/bin/env python3
"""Reference-aware terrain diagnostics using only the Python standard library.

The existing Unity test remains the visual gate. This script adds diagnostic metrics that are
harder to satisfy by camera/value alignment alone: luma/chroma error, gradient agreement,
regional SSIM, local contrast, and directional/detail energy. It intentionally does not collapse
these into a single pass/fail score yet; thresholds should only be set after observing stable
baselines and genuine visual improvements.
"""

from __future__ import annotations

import argparse
import json
import math
import pathlib
import struct
import zlib


def _paeth(a: int, b: int, c: int) -> int:
    p = a + b - c
    pa = abs(p - a)
    pb = abs(p - b)
    pc = abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    if pb <= pc:
        return b
    return c


def read_png_rgb(path: pathlib.Path):
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"{path} is not a PNG")

    pos = 8
    width = height = bit_depth = colour_type = interlace = None
    compressed = bytearray()
    while pos < len(data):
        length = struct.unpack(">I", data[pos:pos + 4])[0]
        kind = data[pos + 4:pos + 8]
        payload = data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if kind == b"IHDR":
            width, height, bit_depth, colour_type, _, _, interlace = struct.unpack(">IIBBBBB", payload)
        elif kind == b"IDAT":
            compressed.extend(payload)
        elif kind == b"IEND":
            break

    if bit_depth != 8 or colour_type not in (2, 6) or interlace != 0:
        raise ValueError(
            f"Unsupported PNG format for {path}: bit_depth={bit_depth}, "
            f"colour_type={colour_type}, interlace={interlace}")

    channels = 3 if colour_type == 2 else 4
    stride = width * channels
    raw = zlib.decompress(bytes(compressed))
    expected = height * (stride + 1)
    if len(raw) != expected:
        raise ValueError(f"Unexpected decompressed byte count for {path}: {len(raw)} != {expected}")

    rows = []
    previous = bytearray(stride)
    offset = 0
    for _ in range(height):
        filter_type = raw[offset]
        source = raw[offset + 1:offset + 1 + stride]
        offset += stride + 1
        row = bytearray(stride)
        for i, value in enumerate(source):
            left = row[i - channels] if i >= channels else 0
            up = previous[i]
            upper_left = previous[i - channels] if i >= channels else 0
            if filter_type == 0:
                reconstructed = value
            elif filter_type == 1:
                reconstructed = value + left
            elif filter_type == 2:
                reconstructed = value + up
            elif filter_type == 3:
                reconstructed = value + ((left + up) >> 1)
            elif filter_type == 4:
                reconstructed = value + _paeth(left, up, upper_left)
            else:
                raise ValueError(f"Unsupported PNG filter {filter_type} in {path}")
            row[i] = reconstructed & 0xFF
        rows.append(row)
        previous = row

    rgb = []
    for row in rows:
        out = []
        for x in range(width):
            base = x * channels
            out.append((row[base], row[base + 1], row[base + 2]))
        rgb.append(out)
    return width, height, rgb


def box_downsample(rgb, src_w: int, src_h: int, dst_w: int, dst_h: int):
    if src_w % dst_w or src_h % dst_h:
        raise ValueError(f"Integer box downsample required: {src_w}x{src_h} -> {dst_w}x{dst_h}")
    bw = src_w // dst_w
    bh = src_h // dst_h
    out = []
    for oy in range(dst_h):
        row = []
        for ox in range(dst_w):
            sr = sg = sb = 0
            for y in range(oy * bh, (oy + 1) * bh):
                for x in range(ox * bw, (ox + 1) * bw):
                    r, g, b = rgb[y][x]
                    sr += r
                    sg += g
                    sb += b
            n = bw * bh
            row.append((sr / n, sg / n, sb / n))
        out.append(row)
    return out


def luma(p):
    return (0.2126 * p[0] + 0.7152 * p[1] + 0.0722 * p[2]) / 255.0


def chroma(p):
    y = luma(p)
    r = p[0] / 255.0
    b = p[2] / 255.0
    return (b - y, r - y)


def flatten(rgb):
    return [p for row in rgb for p in row]


def mean(values):
    return sum(values) / len(values) if values else 0.0


def stddev(values):
    if not values:
        return 0.0
    m = mean(values)
    return math.sqrt(mean([(v - m) ** 2 for v in values]))


def rgb_mae(a, b):
    total = 0.0
    n = 0
    for pa, pb in zip(flatten(a), flatten(b)):
        total += abs(pa[0] - pb[0]) + abs(pa[1] - pb[1]) + abs(pa[2] - pb[2])
        n += 3
    return total / (255.0 * n)


def luma_mae(a, b):
    return mean([abs(luma(pa) - luma(pb)) for pa, pb in zip(flatten(a), flatten(b))])


def chroma_mae(a, b):
    errors = []
    for pa, pb in zip(flatten(a), flatten(b)):
        ca = chroma(pa)
        cb = chroma(pb)
        errors.extend((abs(ca[0] - cb[0]), abs(ca[1] - cb[1])))
    return mean(errors)


def patch_luma_ssim(a, b, width: int, height: int, patch: int = 4,
                    y_start: int = 0, y_end: int | None = None):
    c1 = 0.0001
    c2 = 0.0009
    y_end = height if y_end is None else y_end
    scores = []
    for y0 in range(y_start, y_end, patch):
        for x0 in range(0, width, patch):
            x1 = min(x0 + patch, width)
            y1 = min(y0 + patch, y_end)
            aa = [luma(a[y][x]) for y in range(y0, y1) for x in range(x0, x1)]
            bb = [luma(b[y][x]) for y in range(y0, y1) for x in range(x0, x1)]
            ma = mean(aa)
            mb = mean(bb)
            va = mean([(v - ma) ** 2 for v in aa])
            vb = mean([(v - mb) ** 2 for v in bb])
            cov = mean([(va0 - ma) * (vb0 - mb) for va0, vb0 in zip(aa, bb)])
            numerator = (2.0 * ma * mb + c1) * (2.0 * cov + c2)
            denominator = (ma * ma + mb * mb + c1) * (va + vb + c2)
            scores.append(numerator / denominator if denominator else 1.0)
    return mean(scores)


def luma_grid(rgb):
    return [[luma(p) for p in row] for row in rgb]


def gradients(grid):
    h = len(grid)
    w = len(grid[0])
    gx = [[0.0] * w for _ in range(h)]
    gy = [[0.0] * w for _ in range(h)]
    for y in range(h):
        ym = max(0, y - 1)
        yp = min(h - 1, y + 1)
        for x in range(w):
            xm = max(0, x - 1)
            xp = min(w - 1, x + 1)
            gx[y][x] = 0.5 * (grid[y][xp] - grid[y][xm])
            gy[y][x] = 0.5 * (grid[yp][x] - grid[ym][x])
    return gx, gy


def gradient_metrics(a, b):
    agx, agy = gradients(luma_grid(a))
    bgx, bgy = gradients(luma_grid(b))
    av = [(agx[y][x], agy[y][x]) for y in range(len(a)) for x in range(len(a[0]))]
    bv = [(bgx[y][x], bgy[y][x]) for y in range(len(b)) for x in range(len(b[0]))]
    mae = mean([(abs(ax - bx) + abs(ay - by)) * 0.5 for (ax, ay), (bx, by) in zip(av, bv)])
    dot = sum(ax * bx + ay * by for (ax, ay), (bx, by) in zip(av, bv))
    na = math.sqrt(sum(ax * ax + ay * ay for ax, ay in av))
    nb = math.sqrt(sum(bx * bx + by * by for bx, by in bv))
    cosine = dot / (na * nb) if na and nb else 1.0
    a_abs_x = mean([abs(v[0]) for v in av])
    a_abs_y = mean([abs(v[1]) for v in av])
    b_abs_x = mean([abs(v[0]) for v in bv])
    b_abs_y = mean([abs(v[1]) for v in bv])
    return {
        "gradient_mae": mae,
        "gradient_cosine": cosine,
        "actual_gradient_energy": mean([math.hypot(x, y) for x, y in av]),
        "reference_gradient_energy": mean([math.hypot(x, y) for x, y in bv]),
        "actual_vertical_to_horizontal": a_abs_y / max(a_abs_x, 1e-9),
        "reference_vertical_to_horizontal": b_abs_y / max(b_abs_x, 1e-9),
    }


def blur3(grid):
    h = len(grid)
    w = len(grid[0])
    out = [[0.0] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            values = []
            for yy in range(max(0, y - 1), min(h, y + 2)):
                for xx in range(max(0, x - 1), min(w, x + 2)):
                    values.append(grid[yy][xx])
            out[y][x] = mean(values)
    return out


def detail_energy(rgb):
    grid = luma_grid(rgb)
    low = blur3(grid)
    return mean([abs(grid[y][x] - low[y][x])
                 for y in range(len(grid)) for x in range(len(grid[0]))])


def region_stats(actual, reference, width, height):
    cuts = [("top", 0, height // 3),
            ("middle", height // 3, 2 * height // 3),
            ("bottom", 2 * height // 3, height)]
    output = {}
    for name, y0, y1 in cuts:
        av = [luma(actual[y][x]) for y in range(y0, y1) for x in range(width)]
        rv = [luma(reference[y][x]) for y in range(y0, y1) for x in range(width)]
        output[name] = {
            "ssim": patch_luma_ssim(actual, reference, width, height, 4, y0, y1),
            "actual_luma_mean": mean(av),
            "reference_luma_mean": mean(rv),
            "actual_luma_std": stddev(av),
            "reference_luma_std": stddev(rv),
        }
    return output


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("actual", type=pathlib.Path)
    parser.add_argument("reference", type=pathlib.Path)
    parser.add_argument("--json", dest="json_path", type=pathlib.Path)
    parser.add_argument("--text", dest="text_path", type=pathlib.Path)
    args = parser.parse_args()

    aw, ah, actual_full = read_png_rgb(args.actual)
    rw, rh, reference = read_png_rgb(args.reference)
    if aw % rw or ah % rh:
        raise SystemExit(f"Capture {aw}x{ah} is not an integer multiple of reference {rw}x{rh}")
    actual = box_downsample(actual_full, aw, ah, rw, rh)

    metrics = {
        "capture": [aw, ah],
        "reference": [rw, rh],
        "macro_patch_ssim": patch_luma_ssim(actual, reference, rw, rh, 4),
        "rgb_mae": rgb_mae(actual, reference),
        "luma_mae": luma_mae(actual, reference),
        "chroma_mae": chroma_mae(actual, reference),
        "actual_detail_energy": detail_energy(actual),
        "reference_detail_energy": detail_energy(reference),
        "regions": region_stats(actual, reference, rw, rh),
    }
    metrics.update(gradient_metrics(actual, reference))
    metrics["detail_energy_ratio"] = (
        metrics["actual_detail_energy"] / max(metrics["reference_detail_energy"], 1e-9))

    text = [
        f"macroPatchSsim={metrics['macro_patch_ssim']:.4f}",
        f"rgbMae={metrics['rgb_mae']:.4f}",
        f"lumaMae={metrics['luma_mae']:.4f}",
        f"chromaMae={metrics['chroma_mae']:.4f}",
        f"gradientMae={metrics['gradient_mae']:.4f}",
        f"gradientCosine={metrics['gradient_cosine']:.4f}",
        f"detailEnergyRatio={metrics['detail_energy_ratio']:.4f}",
        f"verticalHorizontalActual={metrics['actual_vertical_to_horizontal']:.4f}",
        f"verticalHorizontalReference={metrics['reference_vertical_to_horizontal']:.4f}",
    ]
    for name in ("top", "middle", "bottom"):
        r = metrics["regions"][name]
        text.append(f"{name}Ssim={r['ssim']:.4f}")
        text.append(f"{name}LumaMeanActual={r['actual_luma_mean']:.4f}")
        text.append(f"{name}LumaMeanReference={r['reference_luma_mean']:.4f}")
        text.append(f"{name}LumaStdActual={r['actual_luma_std']:.4f}")
        text.append(f"{name}LumaStdReference={r['reference_luma_std']:.4f}")

    output_text = "\n".join(text) + "\n"
    print(output_text, end="")
    if args.json_path:
        args.json_path.parent.mkdir(parents=True, exist_ok=True)
        args.json_path.write_text(json.dumps(metrics, indent=2, sort_keys=True) + "\n")
    if args.text_path:
        args.text_path.parent.mkdir(parents=True, exist_ok=True)
        args.text_path.write_text(output_text)


if __name__ == "__main__":
    main()
