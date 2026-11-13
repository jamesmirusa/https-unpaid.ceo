#!/usr/bin/env python3
"""Soft UI sounds for Unpaid CEO, synthesized (stdlib only, no downloads, no credits).

One gentle family: rounded sine/triangle tones with fast attack and exponential decay, a touch of pitch glide and
a short room tail, so taps feel "toy-like" rather than clicky. Writes 44.1 kHz mono 16-bit WAVs.
usage: python3 tools/audio/ui_sounds.py game/Assets/UnpaidCeo/Audio/Sfx
"""
import math, os, random, struct, sys, wave

SR = 44100

def env(t, attack, decay):
    return (t / attack) if t < attack else math.exp(-(t - attack) / decay)

def tone(freq, dur, attack=0.004, decay=0.06, glide=0.0, harm=0.25, vol=0.6):
    out = []
    phase = 0.0
    n = int(dur * SR)
    for i in range(n):
        t = i / SR
        f = freq * (1 + glide * (t / dur))
        phase += 2 * math.pi * f / SR
        s = math.sin(phase) + harm * math.sin(2 * phase) * 0.5 + harm * 0.2 * math.sin(3 * phase)
        out.append(vol * env(t, attack, decay) * s / (1 + harm))
    return out

def mix(*tracks, offsets=None):
    offsets = offsets or [0] * len(tracks)
    n = max(int(o * SR) + len(t) for t, o in zip(tracks, offsets))
    out = [0.0] * n
    for t, o in zip(tracks, offsets):
        k = int(o * SR)
        for i, s in enumerate(t):
            out[k + i] += s
    return out

def room(x, delay=0.031, fb=0.22, taps=4):
    out = list(x) + [0.0] * int(delay * SR * taps)
    d = int(delay * SR)
    for k in range(1, taps + 1):
        g = fb ** k
        for i, s in enumerate(x):
            out[i + d * k] += s * g
    return out

def noise(dur, decay=0.05, vol=0.15, lp=0.85):
    random.seed(7)
    out, y = [], 0.0
    for i in range(int(dur * SR)):
        y = lp * y + (1 - lp) * random.uniform(-1, 1)
        out.append(vol * y * env(i / SR, 0.002, decay))
    return out

def sweep_noise(dur, vol=0.22):
    random.seed(11)
    out, y = [], 0.0
    n = int(dur * SR)
    for i in range(n):
        t = i / n
        lp = 0.97 - 0.5 * math.sin(math.pi * t)   # opens then closes: a soft "whoosh"
        y = lp * y + (1 - lp) * random.uniform(-1, 1)
        out.append(vol * y * math.sin(math.pi * t) ** 1.5)
    return out

def write(path, x):
    peak = max(1e-9, max(abs(s) for s in x))
    g = min(1.0, 0.85 / peak)
    fade = int(0.004 * SR)
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        frames = bytearray()
        for i, s in enumerate(x):
            if i > len(x) - fade: s *= (len(x) - i) / fade
            frames += struct.pack("<h", int(max(-1, min(1, s * g)) * 32767))
        w.writeframes(bytes(frames))

SOUNDS = {
    # a soft wooden "tock": short, low-mid, no click
    "ui_tap": lambda: room(mix(tone(740, 0.07, decay=0.018, glide=-0.25, harm=0.1, vol=0.5), noise(0.02, 0.006, 0.08))),
    # popup opens: two quick rising notes
    "ui_pop": lambda: room(mix(tone(523, 0.09, decay=0.03, glide=0.3), tone(784, 0.12, decay=0.04, glide=0.15), offsets=[0, 0.045])),
    # money: bright coin ding with a shimmer
    "ui_coin": lambda: room(mix(tone(1568, 0.25, decay=0.08, harm=0.4, vol=0.45), tone(2093, 0.3, decay=0.1, harm=0.3, vol=0.35), offsets=[0, 0.06]), fb=0.28),
    # done / goal: a major arpeggio
    "ui_success": lambda: room(mix(tone(523, 0.2, decay=0.07), tone(659, 0.2, decay=0.07), tone(784, 0.35, decay=0.12), offsets=[0, 0.07, 0.14]), fb=0.3),
    # a closed card: a muted low double-bump, not alarming
    "ui_closed": lambda: room(mix(tone(220, 0.08, decay=0.025, harm=0.05, vol=0.5), tone(196, 0.1, decay=0.03, harm=0.05, vol=0.45), offsets=[0, 0.07])),
    # tab change / panel slide
    "ui_whoosh": lambda: sweep_noise(0.22),
    # big moment (level up, profit week): bright fanfare
    "ui_fanfare": lambda: room(mix(tone(523, 0.18, decay=0.06), tone(659, 0.18, decay=0.06), tone(784, 0.18, decay=0.06), tone(1047, 0.6, decay=0.25, harm=0.35), offsets=[0, 0.08, 0.16, 0.24]), fb=0.33, taps=6),
}

if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out, exist_ok=True)
    for name, make in SOUNDS.items():
        write(os.path.join(out, name + ".wav"), make())
        print("wrote", name)
