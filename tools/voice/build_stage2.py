#!/usr/bin/env python3
"""Append ElevenLabs batches for the voice written ahead of its recording (tools/voice/pending.txt).

Same batching as build_manifest.py (one voice, one language, at most 10 lines / 1100 characters, lines joined by
the [long pause] separator so split_batch.py can cut them back apart), but only for pending ids, under new batch
ids ("s2_<lang>_<speaker>_<nn>", "s2_<lang>_lessons_<nn>"); every stage-1 batch in manifest.json stays untouched.
Usage: tools/voice/build_stage2.py  -> rewrites the s2_ batches in manifest.json and prints a summary.
"""
import json, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import build_manifest as bm  # noqa: E402

PREFIX = "s2_"
LESSON_CHARS = 2400


def pending_ids():
    return {l.strip() for l in open(os.path.join(HERE, "pending.txt"), encoding="utf-8") if l.strip() and not l.startswith("#")}


def main():
    cfg = json.load(open(os.path.join(HERE, "voices.json"), encoding="utf-8"))
    sep = " %s " % cfg["separator"]
    todo = pending_ids()
    lines = [l for l in bm.spoken_lines() if l["id"] in todo]
    lessons = [l for l in bm.lessons() if l["id"] in todo]
    unknown = sorted({l["speaker"] for l in lines} - set(cfg["voices"]))
    if unknown:
        sys.exit("speakers without a voice: %s" % unknown)
    missing = sorted(todo - {l["id"] for l in lines} - {l["id"] for l in lessons})
    if missing:
        sys.exit("pending ids that are neither a spoken line nor a lesson: %s" % missing)
    batches = []
    for lang in ("en", "ru"):
        for speaker in cfg["voices"]:
            mine = [l for l in lines if l["speaker"] == speaker]
            for i, b in enumerate(bm.chunk(mine, lang)):
                batches.append({"id": "%s%s_%s_%02d" % (PREFIX, lang, re.sub(r"\W+", "", speaker).lower(), i), "lang": lang,
                                "speaker": speaker, "voice": cfg["voices"][speaker][lang][0], "kind": "yarn",
                                "lines": [l["id"] for l in b], "texts": [l[lang] for l in b], "prompt": sep.join(l[lang] for l in b)})
        # Lesson bodies are long single lines; stage 1 voiced five in one 2,376-character take and they split cleanly,
        # so lessons go about three to a batch.
        teacher = cfg["lessonVoice"]
        bm.MAX_CHARS, scene_chars = LESSON_CHARS, bm.MAX_CHARS
        lesson_batches = list(bm.chunk(lessons, lang))
        bm.MAX_CHARS = scene_chars
        for i, b in enumerate(lesson_batches):
            batches.append({"id": "%s%s_lessons_%02d" % (PREFIX, lang, i), "lang": lang, "speaker": teacher,
                            "voice": cfg["voices"][teacher][lang][0], "kind": "lesson",
                            "lines": [l["id"] for l in b], "texts": [l[lang] for l in b], "prompt": sep.join(l[lang] for l in b)})
    path = os.path.join(HERE, "manifest.json")
    manifest = json.load(open(path, encoding="utf-8"))
    manifest["batches"] = [b for b in manifest["batches"] if not b["id"].startswith(PREFIX)] + batches
    json.dump(manifest, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    total = sum(len(b["prompt"]) for b in batches)
    print("stage-2: lines %d, lessons %d, batches %d, chars %d (en %d, ru %d)" % (
        len(lines), len(lessons), len(batches), total,
        sum(len(b["prompt"]) for b in batches if b["lang"] == "en"), sum(len(b["prompt"]) for b in batches if b["lang"] == "ru")))


if __name__ == "__main__":
    main()
