#!/usr/bin/env python3
"""Paint a pattern onto a generated room's floor, inside its GLB's base-colour texture.

Image-to-3D models flatten a patterned floor in the concept into one flat colour (the bank's
checkerboard came back plain white). This finds the floor (upward faces at the floor's height),
walks its texels in UV space, and gives each the pattern's colour at that texel's world position,
keeping the texture's own shading (contact shadows, AO) as a brightness factor.
Pure Python (no numpy): the texture goes through macOS sips as an uncompressed BMP.

Usage: paint_floor.py room.glb checker|herringbone TILES_ACROSS [out.glb]
"""
import math, os, struct, subprocess, sys, tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from strip_anim import read_glb, write_glb  # noqa: E402
from floor_patterns import PATTERNS  # noqa: E402

REACH_PX = 1.5   # texels this close outside a floor triangle are painted too (mip bleed)


def accessor(js, binc, index):
    acc = js["accessors"][index]
    view = js["bufferViews"][acc["bufferView"]]
    comps = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[acc["type"]]
    fmt = {5126: "f", 5125: "I", 5123: "H", 5121: "B"}[acc["componentType"]]
    size = struct.calcsize("<" + fmt)
    stride = view.get("byteStride", size * comps)
    start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    return [struct.unpack_from("<" + fmt * comps, binc, start + i * stride) for i in range(acc["count"])]


def read_bmp(path):
    data = open(path, "rb").read()
    offset, = struct.unpack_from("<I", data, 10)
    w, h, _, bpp = struct.unpack_from("<iiHH", data, 18)
    top_down = h < 0
    h = abs(h)
    row = ((bpp * w + 31) // 32) * 4
    step = bpp // 8
    pixels = bytearray(w * h * 3)
    for y in range(h):
        src = offset + (y if top_down else h - 1 - y) * row
        line = data[src:src + w * step]
        o = y * w * 3
        out = bytearray(w * 3)
        out[0::3], out[1::3], out[2::3] = line[2::step], line[1::step], line[0::step]
        pixels[o:o + w * 3] = out
    return w, h, pixels


def write_bmp(path, w, h, pixels):
    row = ((24 * w + 31) // 32) * 4
    out = bytearray(b"BM" + struct.pack("<IHHI", 54 + row * h, 0, 0, 54))
    out += struct.pack("<IiiHHIIiiII", 40, w, h, 1, 24, 0, row * h, 2835, 2835, 0, 0)
    pad = bytes(row - w * 3)
    for y in range(h - 1, -1, -1):
        src = pixels[y * w * 3:(y + 1) * w * 3]
        line = bytearray(w * 3)
        line[0::3], line[1::3], line[2::3] = src[2::3], src[1::3], src[0::3]
        out += line + pad
    open(path, "wb").write(out)


def floor_triangles(P, I):
    ys = [p[1] for p in P]
    lo, hi = min(ys), max(ys)
    span = hi - lo
    up = []
    for t in range(0, len(I), 3):
        a, b, c = P[I[t]], P[I[t + 1]], P[I[t + 2]]
        u = [b[k] - a[k] for k in range(3)]
        v = [c[k] - a[k] for k in range(3)]
        n = (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])
        area = math.sqrt(n[0] ** 2 + n[1] ** 2 + n[2] ** 2)
        if area > 0 and n[1] / area > 0.95:
            up.append((t, (a[1] + b[1] + c[1]) / 3, area / 2))
    bins = {}
    for _, y, area in up:
        if y < lo + 0.4 * span:
            k = int((y - lo) / span * 200)
            bins[k] = bins.get(k, 0) + area
    # The floor is the highest of the big flat planes: a pedestal top or a step under it can be as
    # large, but lies lower (the academy's pedestal top is 4 big triangles 2 cm under its floor).
    biggest = max(bins.values())
    level = lo + (max(k for k, a in bins.items() if a >= 0.5 * biggest) + 0.5) / 200 * span
    tol = 0.015 * span
    return [t for t, y, _ in up if abs(y - level) < tol], level


def paint(glb, pattern, across, out):
    js, binc = read_glb(glb)
    prim = js["meshes"][0]["primitives"][0]
    P = accessor(js, binc, prim["attributes"]["POSITION"])
    T = accessor(js, binc, prim["attributes"]["TEXCOORD_0"])
    I = [i[0] for i in accessor(js, binc, prim["indices"])]
    tris, level = floor_triangles(P, I)
    xs = [P[I[t + k]][0] for t in tris for k in range(3)]
    zs = [P[I[t + k]][2] for t in tris for k in range(3)]
    x0, z0 = min(xs), min(zs)
    tile = max(max(xs) - x0, max(zs) - z0) / across

    material = js["materials"][prim.get("material", 0)]
    image_index = js["textures"][material["pbrMetallicRoughness"]["baseColorTexture"]["index"]]["source"]
    image = js["images"][image_index]
    view = js["bufferViews"][image["bufferView"]]
    jpeg = binc[view.get("byteOffset", 0): view.get("byteOffset", 0) + view["byteLength"]]
    with tempfile.TemporaryDirectory() as tmp:
        src, bmp, dst = (os.path.join(tmp, n) for n in ("in.jpg", "in.bmp", "out.jpg"))
        open(src, "wb").write(jpeg)
        subprocess.run(["sips", "-s", "format", "bmp", src, "--out", bmp], check=True, capture_output=True)
        W, H, px = read_bmp(bmp)

        texels = {}
        for t in tris:
            A, B, C = (P[I[t + k]] for k in range(3))
            (ua, va), (ub, vb), (uc, vc) = (T[I[t + k]] for k in range(3))
            ax, ay, bx, by, cx, cy = ua * W - .5, va * H - .5, ub * W - .5, vb * H - .5, uc * W - .5, vc * H - .5
            area2 = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay)
            if abs(area2) < 1e-9:
                continue
            lens = (math.hypot(cx - bx, cy - by), math.hypot(ax - cx, ay - cy), math.hypot(bx - ax, by - ay))
            for j in range(max(0, int(math.floor(min(ay, by, cy) - 2))), min(H, int(math.ceil(max(ay, by, cy) + 2)) + 1)):
                for i in range(max(0, int(math.floor(min(ax, bx, cx) - 2))), min(W, int(math.ceil(max(ax, bx, cx) + 2)) + 1)):
                    w0 = ((bx - i) * (cy - j) - (cx - i) * (by - j)) / area2
                    w1 = ((cx - i) * (ay - j) - (ax - i) * (cy - j)) / area2
                    w2 = 1.0 - w0 - w1
                    ws = (w0, w1, w2)
                    if any(ws[k] * abs(area2) / lens[k] < -REACH_PX for k in range(3) if lens[k] > 0):
                        continue
                    inside = min(ws) >= 0
                    if (i, j) in texels and not inside:
                        continue
                    c0, c1, c2 = max(w0, 0), max(w1, 0), max(w2, 0)
                    s = c0 + c1 + c2
                    x = (c0 * A[0] + c1 * B[0] + c2 * C[0]) / s
                    z = (c0 * A[2] + c1 * B[2] + c2 * C[2]) / s
                    texels[(i, j)] = (x, z)

        lum = {}
        for (i, j) in texels:
            r, g, b = px[(j * W + i) * 3:(j * W + i) * 3 + 3]
            lum[(i, j)] = 0.2126 * r + 0.7152 * g + 0.0722 * b
        ref = sorted(lum.values())[int(0.9 * (len(lum) - 1))] or 1.0
        colour = PATTERNS[pattern]
        for (i, j), (x, z) in texels.items():
            shade = min(1.0, max(0.55, lum[(i, j)] / ref))
            r, g, b = colour(x - x0, z - z0, tile)
            o = (j * W + i) * 3
            px[o:o + 3] = bytes(min(255, max(0, int(c * shade + 0.5))) for c in (r, g, b))

        write_bmp(bmp, W, H, px)
        subprocess.run(["sips", "-s", "format", "jpeg", "-s", "formatOptions", "92", bmp, "--out", dst], check=True, capture_output=True)
        data = open(dst, "rb").read()

    image["mimeType"] = "image/jpeg"
    new_bin = bytearray()
    for index, v in enumerate(js["bufferViews"]):
        start = v.get("byteOffset", 0)
        chunk = data if index == image["bufferView"] else binc[start:start + v["byteLength"]]
        while len(new_bin) % 4:
            new_bin.append(0)
        v["byteOffset"] = len(new_bin)
        v["byteLength"] = len(chunk)
        new_bin += chunk
    js["buffers"][0]["byteLength"] = len(new_bin)
    write_glb(out, js, bytes(new_bin))
    print(f"{os.path.basename(glb)}: {len(tris)} floor triangles at y {level:.3f}, {len(texels)} texels, "
          f"tile {tile:.3f} ({across} across), shading ref {ref:.0f}")


if __name__ == "__main__":
    a = sys.argv
    paint(a[1], a[2], float(a[3]), a[4] if len(a) > 4 else a[1])
