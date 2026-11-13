#!/usr/bin/env python3
"""Download every batch take listed in raw/urls.txt (generation, session, signature) and split it.

Takes come from the ElevenLabs flow recorded in generations.json. Signed URLs expire, so this is a
one-shot import step; the raw takes stay in tools/voice/raw (git-ignored) for re-splitting.
Usage: tools/voice/fetch_and_split.py [--dry]
"""
import json, os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
WS = "a73569624e1b4a368ac1bd8b29e64337"
URL = ("https://elevenlabsusercontent.com/database/workspace/%s/content_generation/%s/%s/content.mp3"
       "?Expires=1794175200&KeyName=assets-cdn-key-1&Signature=%s")

takes = json.load(open(os.path.join(HERE, "generations.json")))["takes"]
by_gen = {g: b for b, g in takes.items()}
rows = [l.split() for l in open(os.path.join(HERE, "raw", "urls.txt")) if l.strip()]
failed = []
for gen, session, sig in rows:
    batch = by_gen[gen]
    dst = os.path.join(HERE, "raw", batch + ".mp3")
    if not os.path.exists(dst):
        subprocess.run(["curl", "-sSfL", "-o", dst, URL % (WS, session, gen, sig)], check=True)
    r = subprocess.run([sys.executable, os.path.join(HERE, "split_batch.py"), batch, dst] + sys.argv[1:],
                       capture_output=True, text=True)
    last = r.stdout.strip().splitlines()[-1] if r.stdout.strip() else r.stderr.strip()
    print(last)
    if r.returncode != 0 or " 0 flagged" not in last:
        failed.append(batch)
        print(r.stdout + r.stderr)
print("FLAGGED/FAILED:", failed if failed else "none")
