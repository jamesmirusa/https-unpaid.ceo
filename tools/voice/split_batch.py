#!/usr/bin/env python3
"""Split one ElevenLabs batch take into per-line clips named by line id.

The batch prompt joins lines with a [long pause] tag. We detect silences with ffmpeg and pick the N-1 cut
points by dynamic programming: segment lengths should match each line's share of characters, and longer
silences are preferred. Every cut is reported so a bad split is visible before anything is committed.

Usage: tools/voice/split_batch.py <batch_id> <take.mp3> [--dry]
"""
import json, math, os, re, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.join(os.path.dirname(os.path.dirname(HERE)), "game", "Assets", "UnpaidCeo")
MIN_GAP = 0.30      # shortest silence that may be a cut (s)
WEAK_GAP = 0.55     # a chosen cut shorter than this is flagged
STRONG_GAP = 0.90   # silence this long can only be the [long pause] separator
MARGIN = 0.12       # separators must be this much longer than any silence inside a line
PAD = 0.06          # silence kept around each clip (s)
LAMBDA = 1.5        # preference for long silences vs length match


def duration(path):
    out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path],
                         capture_output=True, text=True, check=True).stdout
    return float(out.strip())


def silences(path):
    err = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", path, "-af",
                          "silencedetect=noise=-38dB:d=%.2f" % MIN_GAP, "-f", "null", "-"],
                         capture_output=True, text=True).stderr
    starts = [float(x) for x in re.findall(r"silence_start: ([\d.]+)", err)]
    ends = [float(x) for x in re.findall(r"silence_end: ([\d.]+)", err)]
    return list(zip(starts, ends))


def choose_cuts(gaps, total, weights):
    """Pick len(weights)-1 gaps (in order) minimising length mismatch minus a bonus for long gaps."""
    n = len(weights)
    if n == 1:
        return []
    share = [w / sum(weights) for w in weights]
    expected = [s * total for s in share]
    mids = [((a + b) / 2, b - a) for a, b in gaps]
    C = len(mids)
    if C < n - 1:
        raise SystemExit("only %d candidate silences for %d lines" % (C, n))
    INF = float("inf")
    # best[k][j]: cost of placing cut k (0-based) at gap j
    best = [[INF] * C for _ in range(n - 1)]
    back = [[-1] * C for _ in range(n - 1)]

    def seg_cost(a, b, i):
        d = max(b - a, 0.05)
        return math.log(d / expected[i]) ** 2

    for j in range(C):
        best[0][j] = seg_cost(0.0, mids[j][0], 0) - LAMBDA * mids[j][1]
    for k in range(1, n - 1):
        for j in range(k, C):
            for p in range(k - 1, j):
                if best[k - 1][p] == INF:
                    continue
                c = best[k - 1][p] + seg_cost(mids[p][0], mids[j][0], k) - LAMBDA * mids[j][1]
                if c < best[k][j]:
                    best[k][j], back[k][j] = c, p
    final = min(range(C), key=lambda j: best[n - 2][j] + seg_cost(mids[j][0], total, n - 1)
                if best[n - 2][j] < INF else INF)
    picks = [final]
    for k in range(n - 2, 0, -1):
        picks.append(back[k][picks[-1]])
    picks.reverse()
    return [gaps[j] for j in picks]


def main():
    batch_id, take = sys.argv[1], sys.argv[2]
    dry = "--dry" in sys.argv
    manifest = json.load(open(os.path.join(HERE, "manifest.json"), encoding="utf-8"))
    batch = next(b for b in manifest["batches"] if b["id"] == batch_id)
    total = duration(take)
    gaps = silences(take)
    lead = gaps[0][1] if gaps and gaps[0][0] < 0.05 else 0.0
    tail = gaps[-1][0] if gaps and gaps[-1][1] > total - 0.05 else total
    inner = [g for g in gaps if g[0] > lead + 0.05 and g[1] < tail - 0.05]
    weights = [len(t) + 8 for t in batch["texts"]]
    # The [long pause] separator renders as a 0.6-2 s silence while pauses inside a line stay under ~0.5 s.
    # The split is unambiguous when the N-1 longest silences stand clear of every other silence.
    need = len(weights) - 1
    ranked = sorted(inner, key=lambda g: g[1] - g[0], reverse=True)
    top = sorted(ranked[:need])
    longest_rest = (ranked[need][1] - ranked[need][0]) if len(ranked) > need else 0.0
    shortest_top = min((g[1] - g[0] for g in top), default=STRONG_GAP)
    exact = len(top) == need and shortest_top >= WEAK_GAP and shortest_top - longest_rest >= MARGIN
    if exact:
        cuts = top
    else:
        print("note: separators not clear (shortest cut %.2f, next silence %.2f), using length matching"
              % (shortest_top, longest_rest))
        cuts = choose_cuts(inner, tail - lead, weights) if need else []
    cuts = [(a - lead, b - lead) for a, b in cuts]
    bounds = [(0.0, None)] + [(a, b) for a, b in cuts] + [(None, tail - lead)]
    out_dir = os.path.join(ASSETS, "Yarn", "Voice", batch["lang"]) if batch["kind"] == "yarn" \
        else os.path.join(ASSETS, "Audio", "Lessons", batch["lang"])
    os.makedirs(out_dir, exist_ok=True)
    share_total = sum(weights)
    span = tail - lead
    flagged = 0
    for i, lid in enumerate(batch["lines"]):
        start = lead + (bounds[i][1] if i > 0 else 0.0)
        end = lead + (bounds[i + 1][0] if i < len(batch["lines"]) - 1 else span)
        ratio = (end - start) / (weights[i] / share_total * span)
        gap = (bounds[i + 1][1] - bounds[i + 1][0]) if i < len(batch["lines"]) - 1 else None
        flag = ""
        if not exact:
            flag += " AMBIGUOUS"
        if not 0.3 <= ratio <= 2.5:
            flag += " LENGTH?"
        if gap is not None and gap < WEAK_GAP:
            flag += " WEAK-CUT"
        flagged += bool(flag)
        print("%-34s %6.2f-%6.2f  %5.2fs  ratio %.2f  gap %s%s" % (lid, start, end, end - start, ratio,
              "%.2f" % gap if gap is not None else "  - ", flag))
        if not dry:
            subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", take,
                            "-ss", "%.3f" % max(start - PAD, 0), "-to", "%.3f" % min(end + PAD, total),
                            "-ac", "1", "-ar", "44100", "-codec:a", "libmp3lame", "-b:a", "96k",
                            os.path.join(out_dir, lid + ".mp3")], check=True)
    print("%s: %d clips, %d flagged, take %.1fs" % (batch_id, len(batch["lines"]), flagged, total))


if __name__ == "__main__":
    main()
