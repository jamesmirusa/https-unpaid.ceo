#!/usr/bin/env python3
"""Downloads finished 3D jobs into game/Assets/UnpaidCeo/Art/Imported/<Kind>s/<id>.glb.
Usage: fetch_batch.py <index>=<result_url> ...   (records the URL in batch-jobs.json; skips files already present)."""
import json, os, subprocess, sys
here = os.path.dirname(os.path.abspath(__file__))
jobs_path = os.path.join(here, "batch-jobs.json")
root = os.path.join(here, "..", "..", "game", "Assets", "UnpaidCeo", "Art", "Imported")
d = json.load(open(jobs_path))
by_index = {a["index"]: a for a in d["assets"]}
for arg in sys.argv[1:]:
    idx, url = arg.split("=", 1)
    a = by_index[int(idx)]
    a["result"] = url
    folder = os.path.join(root, {"Building": "Buildings", "Prop": "Props", "Character": "Characters"}[a["kind"]])
    os.makedirs(folder, exist_ok=True)
    out = os.path.join(folder, a["id"] + ".glb")
    if not os.path.exists(out):
        subprocess.run(["curl", "-sS", "-o", out, url], check=True)
    with open(out, "rb") as f: assert f.read(4) == b"glTF", out
    print(a["id"], os.path.getsize(out) // 1024, "KB")
json.dump(d, open(jobs_path, "w"), ensure_ascii=False, indent=1)
