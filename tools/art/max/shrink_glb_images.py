#!/usr/bin/env python3
"""Downscale the textures embedded in a GLB to at most N pixels (default 2048) with macOS sips.

Max-quality Tripo takes embed an 8192 base colour and 4096 PBR maps; the game imports at most 2048
(GlbPrefabBuilder.AppTextureSize), so the extra resolution only costs repository (LFS) space.
Usage: shrink_glb_images.py file.glb [max]
"""
import json, os, struct, subprocess, sys, tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from strip_anim import read_glb, write_glb  # noqa: E402


def shrink(path, limit):
    js, binc = read_glb(path)
    image_views = {im["bufferView"]: im for im in js.get("images", []) if "bufferView" in im}
    new_bin = bytearray()
    for index, view in enumerate(js["bufferViews"]):
        start = view.get("byteOffset", 0)
        data = binc[start:start + view["byteLength"]]
        if index in image_views:
            ext = ".png" if image_views[index].get("mimeType") == "image/png" else ".jpg"
            with tempfile.TemporaryDirectory() as tmp:
                src = os.path.join(tmp, "in" + ext)
                open(src, "wb").write(data)
                subprocess.run(["sips", "-Z", str(limit), src], check=True, capture_output=True)
                data = open(src, "rb").read()
        while len(new_bin) % 4:
            new_bin.append(0)
        view["byteOffset"] = len(new_bin)
        view["byteLength"] = len(data)
        new_bin += data
    js["buffers"][0]["byteLength"] = len(new_bin)
    write_glb(path, js, bytes(new_bin))


if __name__ == "__main__":
    limit = int(sys.argv[2]) if len(sys.argv) > 2 else 2048
    before = os.path.getsize(sys.argv[1])
    shrink(sys.argv[1], limit)
    print(f"{os.path.basename(sys.argv[1])}: {before // 1024} KB -> {os.path.getsize(sys.argv[1]) // 1024} KB")
