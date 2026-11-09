#!/usr/bin/env python3
"""Group chapter 1 spoken lines into ElevenLabs batches (one voice, one language, a few lines each).

Lines inside a batch are joined by the separator tag so one generation can be split back into
per-line clips by tools/voice/split_batch.py. Options ("->") are not voiced.
Usage: tools/voice/build_manifest.py  -> writes tools/voice/manifest.json and prints a summary.
"""
import csv, glob, json, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(ROOT, "game", "Assets", "UnpaidCeo")
HERE = os.path.dirname(os.path.abspath(__file__))
MAX_LINES, MAX_CHARS = 10, 1100
SPEAKER = re.compile(r"^([^:]{1,30}):\s+")
LINE = re.compile(r"#line:(\S+)")


def spoken_lines():
    ru = {r["id"][5:]: r["text"] for r in csv.DictReader(open(os.path.join(ASSETS, "Yarn", "Story-ru.csv"), encoding="utf-8"))}
    out = []
    for f in sorted(glob.glob(os.path.join(ASSETS, "Yarn", "*.yarn"))):
        for raw in open(f, encoding="utf-8"):
            m = LINE.search(raw)
            if not m:
                continue
            text = raw[:m.start()].strip()
            if text.startswith("->"):
                continue
            lid = m.group(1)
            s = SPEAKER.match(text)
            speaker = s.group(1) if s else "Narrator"
            en = SPEAKER.sub("", text, count=1) if s else text
            r = ru.get(lid)
            if r is None:
                sys.exit("no ru text for " + lid)
            r = SPEAKER.sub("", r, count=1) if s else r
            for t in (en, r):
                if "{" in t or "<<" in t:
                    sys.exit("line has runtime markup, cannot pre-voice: " + lid)
            out.append({"id": lid, "speaker": speaker, "file": os.path.basename(f), "en": en, "ru": r})
    return out


def lessons():
    ids = [l["id"] for l in json.load(open(os.path.join(ASSETS, "Content", "lessons.json"), encoding="utf-8"))["lessons"]]
    body = {}
    for row in csv.reader(open(os.path.join(ASSETS, "Localization", "Source", "Academy.tsv"), encoding="utf-8"), delimiter="\t"):
        if len(row) >= 3:
            body[row[0]] = {"en": row[1], "ru": row[2]}
    return [{"id": i, "en": body["lesson.%s.body" % i]["en"], "ru": body["lesson.%s.body" % i]["ru"]} for i in ids]


def chunk(items, lang):
    batch, size = [], 0
    for it in items:
        n = len(it[lang])
        if batch and (len(batch) >= MAX_LINES or size + n > MAX_CHARS):
            yield batch
            batch, size = [], 0
        batch.append(it)
        size += n
    if batch:
        yield batch


def main():
    """--add <file.yarn>: append batches for one new Yarn file to the existing manifest, keeping every
    batch that already has a recorded take untouched."""
    cfg = json.load(open(os.path.join(HERE, "voices.json"), encoding="utf-8"))
    sep = " %s " % cfg["separator"]
    lines = spoken_lines()
    only = sys.argv[sys.argv.index("--add") + 1] if "--add" in sys.argv else None
    if only:
        lines = [l for l in lines if l["file"] == only]
    tag = re.sub(r"\W+", "", os.path.splitext(only)[0]) if only else None
    batches = []
    for lang in ("en", "ru"):
        for speaker in cfg["voices"]:
            mine = [l for l in lines if l["speaker"] == speaker]
            for i, b in enumerate(chunk(mine, lang)):
                bid = "%s_%s_%02d" % (lang, re.sub(r"\W+", "", speaker).lower(), i)
                if tag:
                    bid = "%s_%s_%02d" % (lang, tag, i)
                batches.append({"id": bid, "lang": lang,
                                "speaker": speaker, "voice": cfg["voices"][speaker][lang][0], "kind": "yarn",
                                "lines": [l["id"] for l in b], "texts": [l[lang] for l in b],
                                "prompt": sep.join(l[lang] for l in b)})
        if only:
            continue
        ls = lessons()
        batches.append({"id": "%s_lessons" % lang, "lang": lang, "speaker": cfg["lessonVoice"],
                        "voice": cfg["voices"][cfg["lessonVoice"]][lang][0], "kind": "lesson",
                        "lines": [l["id"] for l in ls], "texts": [l[lang] for l in ls],
                        "prompt": sep.join(l[lang] for l in ls)})
    unknown = sorted({l["speaker"] for l in lines} - set(cfg["voices"]))
    if unknown:
        sys.exit("speakers without a voice: %s" % unknown)
    if only:
        old = json.load(open(os.path.join(HERE, "manifest.json"), encoding="utf-8"))
        new_ids = {b["id"] for b in batches}
        batches = [b for b in old["batches"] if b["id"] not in new_ids] + batches
    json.dump({"model": cfg["model"], "batches": batches}, open(os.path.join(HERE, "manifest.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    total = sum(len(b["prompt"]) for b in batches)
    print("lines %d, batches %d, chars %d (en %d, ru %d)" % (len(lines), len(batches), total,
          sum(len(b["prompt"]) for b in batches if b["lang"] == "en"), sum(len(b["prompt"]) for b in batches if b["lang"] == "ru")))


if __name__ == "__main__":
    main()
