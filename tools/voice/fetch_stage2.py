#!/usr/bin/env python3
"""Download the chapter 2-3 takes from ElevenLabs flow x5VjoCtgGk5DIMmJzi7G and split them into line clips.

Joins stage2_nodes.txt (batch -> node), stage2_gens.txt (node -> generation [label]) and raw/s2_urls.txt
(generation, session, signature). Writes generations_stage2.json as the record of what was generated.
Music takes land in raw/<label>.mp3 for the music import step; voice takes go through split_batch.py.
Signed URLs expire, so this is a one-shot step; raw takes stay in tools/voice/raw (git-ignored).
Usage: tools/voice/fetch_stage2.py [--dry]
"""
import json, os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
FLOW = "x5VjoCtgGk5DIMmJzi7G"
WS = "a73569624e1b4a368ac1bd8b29e64337"
URL = ("https://elevenlabsusercontent.com/database/workspace/%s/content_generation/%s/%s/content.mp3"
       "?Expires=1794196800&KeyName=assets-cdn-key-1&Signature=%s")


def rows(name):
    return [l.split() for l in open(os.path.join(HERE, name)) if l.strip() and not l.startswith("#")]


node_of = {r[0]: r[1] for r in rows("stage2_nodes.txt") if r[0].startswith("s2_")}
gen_of, credits, label_of = {}, {}, {}
for r in rows("stage2_gens.txt"):
    gen_of[r[0]] = r[1]
    credits[r[1]] = int(r[2])
    if len(r) > 3:
        label_of[r[1]] = r[3]
signed = {r[0]: (r[1], r[2]) for r in rows(os.path.join("raw", "s2_urls.txt"))}

takes = {b: gen_of[n] for b, n in node_of.items()}
music = {label_of[g]: g for g in label_of}
json.dump({"flow": FLOW, "takes": takes, "music": music, "credits": sum(credits.values())},
          open(os.path.join(HERE, "generations_stage2.json"), "w"), indent=1, sort_keys=True)


def fetch(gen, dst):
    if not os.path.exists(dst):
        session, sig = signed[gen]
        subprocess.run(["curl", "-sSfL", "-o", dst, URL % (WS, session, gen, sig)], check=True)


for label, gen in sorted(music.items()):
    fetch(gen, os.path.join(HERE, "raw", label + ".mp3"))
    print("music", label, "ok")

failed = []
for batch, gen in sorted(takes.items()):
    dst = os.path.join(HERE, "raw", batch + ".mp3")
    fetch(gen, dst)
    r = subprocess.run([sys.executable, os.path.join(HERE, "split_batch.py"), batch, dst] + sys.argv[1:],
                       capture_output=True, text=True)
    last = r.stdout.strip().splitlines()[-1] if r.stdout.strip() else r.stderr.strip()
    print(last)
    if r.returncode != 0 or " 0 flagged" not in last:
        failed.append(batch)
        print(r.stdout + r.stderr)
print("credits:", sum(credits.values()), "takes:", len(takes), "music:", len(music))
print("FLAGGED/FAILED:", failed if failed else "none")
