#!/usr/bin/env python3
"""Bring the chapter 2-3 models from jobs.json into the Unity project.

For every asset with a result URL: download the GLB to tools/art/stage2/glb/ (git-ignored),
fit it to the triangle budget (buildings 8000, props 3000; meshoptimizer simplify through
`npx @gltf-transform/cli`, which keeps UVs and textures), shrink embedded textures to the app
cap (buildings 2048, props 1024; tools/art/max/shrink_glb_images.py) and place it in
game/Assets/UnpaidCeo/Art/Imported/<Buildings|Props>/<id>.glb. Run ArtImportTool.ImportAll afterwards.
Usage: fetch_stage2.py [--force]
"""
import json, os, shutil, subprocess, sys, urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
IMPORTED = os.path.join(ROOT, "game", "Assets", "UnpaidCeo", "Art", "Imported")
sys.path.insert(0, os.path.join(ROOT, "tools", "art", "max"))
from strip_anim import read_glb  # noqa: E402

BUDGET = {"Building": 8000, "Prop": 3000}       # AssetBudgetValidator.TriangleBudget max
TARGET = {"Building": 7800, "Prop": 2800}       # a little headroom under the budget
TEXTURE = {"Building": 2048, "Prop": 1024}      # GlbPrefabBuilder.AppTextureSize
FOLDER = {"Building": "Buildings", "Prop": "Props"}
GLTF = ["npx", "-y", "@gltf-transform/cli@4"]


def triangles(path):
    js, _ = read_glb(path)
    total = 0
    for mesh in js.get("meshes", []):
        for prim in mesh["primitives"]:
            if prim.get("mode", 4) != 4:
                continue
            acc = prim["indices"] if "indices" in prim else prim["attributes"]["POSITION"]
            total += js["accessors"][acc]["count"] // 3
    return total


def fit(path, target):
    """Simplify in place until the mesh has at most `target` triangles."""
    before = triangles(path)
    if before <= target:
        return before
    tmp = path + ".tmp.glb"
    ratio = target / before * 0.97
    for error in (0.01, 0.02, 0.05, 0.1, 0.2):
        subprocess.run(GLTF + ["simplify", path, tmp, "--ratio", f"{ratio:.4f}", "--error", str(error)],
                       check=True, capture_output=True)
        if triangles(tmp) <= target:
            break
    after = triangles(tmp)
    if after > target:
        os.remove(tmp)
        raise SystemExit(f"{os.path.basename(path)}: could not simplify below {target} (best {after})")
    os.replace(tmp, path)
    return after


def main():
    force = "--force" in sys.argv
    jobs = json.load(open(os.path.join(HERE, "jobs.json")))
    os.makedirs(os.path.join(HERE, "glb"), exist_ok=True)
    missing = []
    for a in jobs["assets"]:
        if not a.get("result"):
            missing.append(a["id"])
            continue
        raw = os.path.join(HERE, "glb", a["id"] + ".glb")
        if force or not os.path.exists(raw):
            urllib.request.urlretrieve(a["result"], raw)
        dest = os.path.join(IMPORTED, FOLDER[a["kind"]], a["id"] + ".glb")
        shutil.copyfile(raw, dest)
        before = triangles(dest)
        after = fit(dest, TARGET[a["kind"]])
        subprocess.run([sys.executable, os.path.join(ROOT, "tools", "art", "max", "shrink_glb_images.py"),
                        dest, str(TEXTURE[a["kind"]])], check=True, capture_output=True)
        print(f"{a['id']}: {before} -> {after} triangles (budget {BUDGET[a['kind']]}), "
              f"{os.path.getsize(raw) // 1024} KB -> {os.path.getsize(dest) // 1024} KB")
    if missing:
        print("no result yet: " + ", ".join(missing))


if __name__ == "__main__":
    main()
