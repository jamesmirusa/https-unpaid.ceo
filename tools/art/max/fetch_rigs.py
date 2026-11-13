#!/usr/bin/env python3
"""Download finished 3d_rigging takes into the Unity project.

urls.txt holds "<job_id> <result_url>" lines (from jobs_wait). The idle take becomes
Art/Imported/Characters/<name>.glb; walk/cheer/think go to Characters/Clips/<name>_<clip>.glb.
Records each URL in max-jobs.json and reports what is still missing.
"""
import json, os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
CHAR = os.path.join(ROOT, "unpaid-ceo", "game", "Assets", "UnpaidCeo", "Art", "Imported", "Characters")
if not os.path.isdir(CHAR):
    CHAR = os.path.join(HERE, "..", "..", "..", "game", "Assets", "UnpaidCeo", "Art", "Imported", "Characters")
CHAR = os.path.abspath(CHAR)

manifest_path = os.path.join(HERE, "max-jobs.json")
manifest = json.load(open(manifest_path))
urls = {}
for line in open(os.path.join(HERE, "urls.txt")):
    parts = line.split()
    if len(parts) == 2:
        urls[parts[0]] = parts[1]

os.makedirs(os.path.join(CHAR, "Clips"), exist_ok=True)
missing = []
for name in manifest["order"]:
    for clip, entry in manifest["characters"][name]["rig"].items():
        url = urls.get(entry["job"]) or entry.get("url")
        if not url:
            missing.append(f"{name}:{clip}")
            continue
        entry["url"] = url
        dest = os.path.join(CHAR, f"{name}.glb") if clip == "idle" else os.path.join(CHAR, "Clips", f"{name}_{clip}.glb")
        if not os.path.exists(dest) or entry.get("downloaded") != url:
            if clip == "idle":
                subprocess.run(["curl", "-sfL", "-o", dest, url], check=True)
            else:
                full = os.path.join(HERE, "rig-full", os.path.basename(dest))
                os.makedirs(os.path.dirname(full), exist_ok=True)
                subprocess.run(["curl", "-sfL", "-o", full, url], check=True)
                subprocess.run([sys.executable, os.path.join(HERE, "strip_anim.py"), full, dest], check=True)
            entry["downloaded"] = url
json.dump(manifest, open(manifest_path, "w"), indent=2)
print("missing:", missing if missing else "none")
sys.exit(1 if missing else 0)
