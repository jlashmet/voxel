#!/usr/bin/env python3
import math
import pathlib
import struct
import sys
import zlib

PNG_SIG = b"\x89PNG\r\n\x1a\n"


def read_png_rgb(path):
    data = pathlib.Path(path).read_bytes()
    if not data.startswith(PNG_SIG):
        raise ValueError(f"{path}: not a PNG")

    pos = len(PNG_SIG)
    width = height = None
    bit_depth = color_type = interlace = None
    idat = bytearray()
    while pos < len(data):
        length = struct.unpack(">I", data[pos:pos + 4])[0]
        kind = data[pos + 4:pos + 8]
        payload = data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if kind == b"IHDR":
            width, height, bit_depth, color_type, _, _, interlace = struct.unpack(">IIBBBBB", payload)
        elif kind == b"IDAT":
            idat.extend(payload)
        elif kind == b"IEND":
            break

    if bit_depth != 8 or interlace != 0 or color_type not in (2, 6):
        raise ValueError(
            f"{path}: unsupported PNG format bitDepth={bit_depth} colorType={color_type} interlace={interlace}")

    channels = 3 if color_type == 2 else 4
    stride = width * channels
    decoded = zlib.decompress(bytes(idat))
    expected = height * (stride + 1)
    if len(decoded) != expected:
        raise ValueError(f"{path}: decoded byte count {len(decoded)} != {expected}")

    rows = []
    previous = bytearray(stride)
    cursor = 0
    for _ in range(height):
        filter_type = decoded[cursor]
        cursor += 1
        scan = bytearray(decoded[cursor:cursor + stride])
        cursor += stride
        recon = bytearray(stride)
        for i, value in enumerate(scan):
            left = recon[i - channels] if i >= channels else 0
            up = previous[i]
            upper_left = previous[i - channels] if i >= channels else 0
            if filter_type == 0:
                predictor = 0
            elif filter_type == 1:
                predictor = left
            elif filter_type == 2:
                predictor = up
            elif filter_type == 3:
                predictor = (left + up) // 2
            elif filter_type == 4:
                p = left + up - upper_left
                pa = abs(p - left)
                pb = abs(p - up)
                pc = abs(p - upper_left)
                predictor = left if pa <= pb and pa <= pc else (up if pb <= pc else upper_left)
            else:
                raise ValueError(f"{path}: unsupported PNG filter {filter_type}")
            recon[i] = (value + predictor) & 0xFF
        rows.append(recon)
        previous = recon

    rgb = bytearray(width * height * 3)
    out = 0
    for row in rows:
        for x in range(width):
            source = x * channels
            rgb[out] = row[source]
            rgb[out + 1] = row[source + 1]
            rgb[out + 2] = row[source + 2]
            out += 3
    return width, height, bytes(rgb)


def downsample_block_average(rgb, width, height, target_width, target_height):
    if width % target_width or height % target_height:
        raise ValueError("source dimensions must divide evenly into target dimensions")
    block_width = width // target_width
    block_height = height // target_height
    samples = block_width * block_height
    result = bytearray(target_width * target_height * 3)
    for out_y in range(target_height):
        for out_x in range(target_width):
            sums = [0, 0, 0]
            for dy in range(block_height):
                y = out_y * block_height + dy
                for dx in range(block_width):
                    x = out_x * block_width + dx
                    i = (y * width + x) * 3
                    sums[0] += rgb[i]
                    sums[1] += rgb[i + 1]
                    sums[2] += rgb[i + 2]
            o = (out_y * target_width + out_x) * 3
            result[o] = sums[0] // samples
            result[o + 1] = sums[1] // samples
            result[o + 2] = sums[2] // samples
    return bytes(result)


def half_average(rgb, width, height):
    target_width = width // 2
    target_height = height // 2
    result = bytearray(target_width * target_height * 3)
    for y in range(target_height):
        for x in range(target_width):
            for c in range(3):
                total = 0
                for dy in (0, 1):
                    for dx in (0, 1):
                        i = (((y * 2 + dy) * width + (x * 2 + dx)) * 3) + c
                        total += rgb[i]
                result[(y * target_width + x) * 3 + c] = total // 4
    return bytes(result), target_width, target_height


def luma(rgb, index):
    return (0.2126 * rgb[index] + 0.7152 * rgb[index + 1] + 0.0722 * rgb[index + 2]) / 255.0


def rgb_mae(actual, reference):
    return sum(abs(a - b) / 255.0 for a, b in zip(actual, reference)) / len(actual)


def luma_mae(actual, reference):
    pixels = len(actual) // 3
    return sum(abs(luma(actual, i * 3) - luma(reference, i * 3)) for i in range(pixels)) / pixels


def patch_luma_ssim(actual, reference, width, height, patch_size):
    c1 = 0.0001
    c2 = 0.0009
    score = 0.0
    patches = 0
    for y0 in range(0, height, patch_size):
        for x0 in range(0, width, patch_size):
            x1 = min(x0 + patch_size, width)
            y1 = min(y0 + patch_size, height)
            values_a = []
            values_b = []
            for y in range(y0, y1):
                for x in range(x0, x1):
                    i = (y * width + x) * 3
                    values_a.append(luma(actual, i))
                    values_b.append(luma(reference, i))
            count = len(values_a)
            mean_a = sum(values_a) / count
            mean_b = sum(values_b) / count
            variance_a = sum((v - mean_a) ** 2 for v in values_a) / count
            variance_b = sum((v - mean_b) ** 2 for v in values_b) / count
            covariance = sum((a - mean_a) * (b - mean_b) for a, b in zip(values_a, values_b)) / count
            numerator = (2.0 * mean_a * mean_b + c1) * (2.0 * covariance + c2)
            denominator = (mean_a * mean_a + mean_b * mean_b + c1) * (variance_a + variance_b + c2)
            score += numerator / denominator if denominator > 0.0 else 1.0
            patches += 1
    return score / patches


def multiscale_ssim(actual, reference, width, height):
    scores = [patch_luma_ssim(actual, reference, width, height, 4)]
    a1, w1, h1 = half_average(actual, width, height)
    r1, _, _ = half_average(reference, width, height)
    scores.append(patch_luma_ssim(a1, r1, w1, h1, 4))
    a2, w2, h2 = half_average(a1, w1, h1)
    r2, _, _ = half_average(r1, w1, h1)
    scores.append(patch_luma_ssim(a2, r2, w2, h2, 2))
    return scores, sum(scores) / len(scores)


def luma_correlation(actual, reference):
    pixels = len(actual) // 3
    a = [luma(actual, i * 3) for i in range(pixels)]
    b = [luma(reference, i * 3) for i in range(pixels)]
    mean_a = sum(a) / pixels
    mean_b = sum(b) / pixels
    numerator = sum((x - mean_a) * (y - mean_b) for x, y in zip(a, b))
    denominator = math.sqrt(sum((x - mean_a) ** 2 for x in a) * sum((y - mean_b) ** 2 for y in b))
    return numerator / denominator if denominator > 0.0 else 1.0


def gradients(rgb, width, height):
    lum = [luma(rgb, i * 3) for i in range(width * height)]
    vectors = []
    magnitudes = []
    for y in range(height):
        for x in range(width):
            gx = 0.0
            gy = 0.0
            if 0 < x < width - 1:
                gx = (lum[y * width + x + 1] - lum[y * width + x - 1]) * 0.5
            if 0 < y < height - 1:
                gy = (lum[(y + 1) * width + x] - lum[(y - 1) * width + x]) * 0.5
            vectors.append((gx, gy))
            magnitudes.append(math.sqrt(gx * gx + gy * gy))
    return vectors, magnitudes


def gradient_metrics(actual, reference, width, height):
    ga, ma = gradients(actual, width, height)
    gb, mb = gradients(reference, width, height)
    dot = sum(ax * bx + ay * by for (ax, ay), (bx, by) in zip(ga, gb))
    norm_a = math.sqrt(sum(ax * ax + ay * ay for ax, ay in ga))
    norm_b = math.sqrt(sum(bx * bx + by * by for bx, by in gb))
    cosine = dot / (norm_a * norm_b) if norm_a > 0.0 and norm_b > 0.0 else 1.0
    edge_mae = sum(abs(a - b) for a, b in zip(ma, mb)) / len(ma)
    return cosine, edge_mae


def main():
    terrain_path = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "Artifacts/Terrain/terrain.png")
    reference_path = pathlib.Path(sys.argv[2] if len(sys.argv) > 2 else "Artifacts/Terrain/terrain-reference.png")
    output_path = pathlib.Path(sys.argv[3] if len(sys.argv) > 3 else "Artifacts/Terrain/terrain-perceptual.txt")

    width, height, terrain = read_png_rgb(terrain_path)
    ref_width, ref_height, reference = read_png_rgb(reference_path)
    actual = downsample_block_average(terrain, width, height, ref_width, ref_height)

    patch4 = patch_luma_ssim(actual, reference, ref_width, ref_height, 4)
    patch8 = patch_luma_ssim(actual, reference, ref_width, ref_height, 8)
    scale_scores, ms_ssim = multiscale_ssim(actual, reference, ref_width, ref_height)
    corr = luma_correlation(actual, reference)
    grad_cos, edge_mae = gradient_metrics(actual, reference, ref_width, ref_height)
    rgb_error = rgb_mae(actual, reference)
    luma_error = luma_mae(actual, reference)

    text = (
        f"patchSsim4={patch4:.4f}\n"
        f"patchSsim8={patch8:.4f}\n"
        f"multiScaleSsim={ms_ssim:.4f}\n"
        f"multiScaleLevels={scale_scores[0]:.4f},{scale_scores[1]:.4f},{scale_scores[2]:.4f}\n"
        f"lumaCorrelation={corr:.4f}\n"
        f"gradientCosine={grad_cos:.4f}\n"
        f"edgeMagnitudeMae={edge_mae:.4f}\n"
        f"rgbMae={rgb_error:.4f}\n"
        f"lumaMae={luma_error:.4f}\n"
        f"capture={width}x{height}\n"
        f"reference={ref_width}x{ref_height}\n"
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(text)
    print(text, end="")


if __name__ == "__main__":
    main()
