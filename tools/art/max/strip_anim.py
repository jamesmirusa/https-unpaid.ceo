#!/usr/bin/env python3
"""Reduce a rigged GLB take to its node hierarchy and animation only.

The walk/cheer/think takes from 3d_rigging repeat the full textured mesh (~10-15 MB each); the game only
needs their AnimationClip, whose curves bind by node path. This keeps nodes (names, transforms, children)
and animations, drops meshes, skins, materials, textures and images, and repacks the binary buffer with
just the animation accessors. Usage: strip_anim.py in.glb out.glb
"""
import json, struct, sys


def read_glb(path):
    b = open(path, "rb").read()
    magic, version, length = struct.unpack("<III", b[:12])
    assert magic == 0x46546C67, "not a GLB"
    off, js, binc = 12, None, b""
    while off < length:
        clen, ctype = struct.unpack("<II", b[off:off + 8])
        data = b[off + 8:off + 8 + clen]
        if ctype == 0x4E4F534A:
            js = json.loads(data)
        elif ctype == 0x004E4942:
            binc = data
        off += 8 + clen
    return js, binc


def write_glb(path, js, binc):
    jbytes = json.dumps(js, separators=(",", ":")).encode()
    jbytes += b" " * ((4 - len(jbytes) % 4) % 4)
    binc += b"\0" * ((4 - len(binc) % 4) % 4)
    total = 12 + 8 + len(jbytes) + (8 + len(binc) if binc else 0)
    with open(path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, total))
        f.write(struct.pack("<II", len(jbytes), 0x4E4F534A) + jbytes)
        if binc:
            f.write(struct.pack("<II", len(binc), 0x004E4942) + binc)


def strip(js, binc):
    used = []
    for anim in js.get("animations", []):
        for s in anim["samplers"]:
            for key in ("input", "output"):
                if s[key] not in used:
                    used.append(s[key])
    remap, accessors, views, out = {}, [], [], bytearray()
    for old in used:
        acc = dict(js["accessors"][old])
        view = js["bufferViews"][acc["bufferView"]]
        start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
        comps = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[acc["type"]]
        size = {5126: 4, 5123: 2, 5125: 4, 5121: 1, 5122: 2, 5120: 1}[acc["componentType"]]
        stride = view.get("byteStride", comps * size)
        chunk = bytearray()
        for i in range(acc["count"]):
            chunk += binc[start + i * stride:start + i * stride + comps * size]
        while len(out) % 4:
            out.append(0)
        views.append({"buffer": 0, "byteOffset": len(out), "byteLength": len(chunk)})
        out += chunk
        acc["bufferView"] = len(views) - 1
        acc.pop("byteOffset", None)
        remap[old] = len(accessors)
        accessors.append(acc)
    for anim in js.get("animations", []):
        for s in anim["samplers"]:
            s["input"], s["output"] = remap[s["input"]], remap[s["output"]]
    nodes = []
    for n in js["nodes"]:
        nodes.append({k: v for k, v in n.items() if k not in ("mesh", "skin", "camera", "weights")})
    result = {
        "asset": js["asset"],
        "scene": js.get("scene", 0),
        "scenes": js["scenes"],
        "nodes": nodes,
        "animations": js["animations"],
        "accessors": accessors,
        "bufferViews": views,
        "buffers": [{"byteLength": len(out)}],
    }
    return result, bytes(out)


if __name__ == "__main__":
    js, binc = read_glb(sys.argv[1])
    out_js, out_bin = strip(js, binc)
    write_glb(sys.argv[2], out_js, out_bin)
    print(sys.argv[2], len(out_bin) // 1024, "KB of animation data,", len(out_js["nodes"]), "nodes")
