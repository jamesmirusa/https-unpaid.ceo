#!/usr/bin/env python3
"""Checks the UGS configuration in server/ugs against the game: boards, tiers, reset hour, flags, access policy.

Usage:
  python3 tools/server/check_ugs_config.py                 # check; prints "ugs config ok" or the problems
  python3 tools/server/check_ugs_config.py --stamp DIR     # check, then set every ResetConfig.Start in DIR's .lb files
                                                           # to the next Monday reset (deploy.sh does this on a copy)
"""
import datetime as dt
import glob
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
UGS = os.path.join(ROOT, "server", "ugs")
CORE = os.path.join(ROOT, "game", "Assets", "UnpaidCeo", "Core", "Online")
BALANCE = os.path.join(ROOT, "game", "Assets", "UnpaidCeo", "Content", "balance.json")
BOARDS = os.path.join(CORE, "GameBoards.cs")

FLAGS = ["online_enabled", "challenge_enabled", "duels_enabled", "market_wars_enabled"]
SCORES_URN = "urn:ugs:leaderboards:/v1/projects/*/leaderboards/*/scores/players/*"
SID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_-]{5,59}$")

problems = []


def problem(msg):
    problems.append(msg)


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def load_json(path):
    try:
        return json.loads(read(path))
    except FileNotFoundError:
        problem(f"missing: {os.path.relpath(path, ROOT)}")
    except json.JSONDecodeError as e:
        problem(f"not JSON: {os.path.relpath(path, ROOT)}: {e}")
    return None


def cs_array(text, name):
    m = re.search(name + r"\s*=\s*\{([^}]*)\}", text)
    return [x.strip().strip('"') for x in m.group(1).split(",")] if m else []


def cs_const(text, name):
    m = re.search(name + r"\s*=\s*([^,;]+)[,;]", text)
    return m.group(1).strip().strip('"') if m else None


def reset_hour():
    """The board resets when the challenge's grace period ends (WeeklyChallenge.GraceSeconds after Monday 00:00 UTC)."""
    grace = cs_const(read(os.path.join(CORE, "WeeklyChallenge.cs")), "GraceSeconds")
    return int(grace) // 3600 if grace and grace.isdigit() else None


def check_start(name, reset):
    start = reset.get("Start")
    try:
        t = dt.datetime.fromisoformat(start.replace("Z", "+00:00"))
    except (AttributeError, ValueError):
        problem(f"{name}: ResetConfig.Start is not an ISO time: {start!r}")
        return
    hour = reset_hour()
    if t.utcoffset() != dt.timedelta(0) or t.weekday() != 0 or t.hour != hour or t.minute != 0:
        problem(f"{name}: ResetConfig.Start must be a Monday {hour:02d}:00Z, got {start}")


def check_board(name, lb, weekly, archive=False):
    if lb is None:
        return
    if lb.get("SortOrder") != "desc":
        problem(f"{name}: SortOrder must be desc")
    if lb.get("UpdateType") != "keepBest":
        problem(f"{name}: UpdateType must be keepBest")
    if not lb.get("Name"):
        problem(f"{name}: Name is required")
    reset = lb.get("ResetConfig")
    if not weekly:
        if reset:
            problem(f"{name}: an all-time board has no ResetConfig")
        return
    if not reset:
        problem(f"{name}: weekly board needs a ResetConfig")
        return
    want = f"0 {reset_hour()} * * 1"
    if reset.get("Schedule") != want:
        problem(f"{name}: Schedule must be {want!r} (Monday, after the grace hour), got {reset.get('Schedule')!r}")
    if bool(reset.get("Archive")) != archive:
        problem(f"{name}: Archive must be {str(archive).lower()}")
    check_start(name, reset)


def check_tiers(lb):
    leagues = read(os.path.join(CORE, "Leagues.cs"))
    ids = cs_array(leagues, "All")
    cents = [int(x.replace("_", "")) for x in cs_array(leagues, "ThresholdsCents")]   # C# digit separators
    if not ids or len(ids) != len(cents):
        problem("Leagues.cs: could not read All / ThresholdsCents")
        return
    want = [{"Id": i, "Cutoff": c / 100} for i, c in zip(ids, cents)][::-1]
    want[-1] = {"Id": want[-1]["Id"]}   # the lowest tier takes everyone else
    tiering = (lb or {}).get("TieringConfig") or {}
    got = [{k: (float(v) if k == "Cutoff" else v) for k, v in t.items()} for t in tiering.get("Tiers", [])]
    if tiering.get("Strategy") != "score" or got != [{k: (float(v) if k == "Cutoff" else v) for k, v in t.items()} for t in want]:
        problem(f"capital: tiers must be score tiers {want} (Leagues, dollars, highest first)")


def check_boards():
    balance = load_json(BALANCE) or {}
    industries = balance.get("chapter2Industries", [])
    if not industries:
        problem("balance.json: chapter2Industries is empty")
    boards = read(BOARDS)
    capital, challenge, market = (cs_const(boards, n) for n in ("Capital", "WeeklyChallenge", "MarketPrefix"))

    capital_lb = load_json(os.path.join(UGS, f"{capital}.lb"))
    check_board(capital, capital_lb, weekly=False)
    check_tiers(capital_lb)
    check_board(challenge, load_json(os.path.join(UGS, f"{challenge}.lb")), weekly=True, archive=True)

    want = {f"{market}{i}" for i in industries}
    have = {os.path.basename(p)[:-3] for p in glob.glob(os.path.join(UGS, f"{market}*.lb"))}
    for extra in sorted(have - want):
        problem(f"{extra}.lb: no such chapter-2 industry in balance.json")
    for board in sorted(want):
        check_board(board, load_json(os.path.join(UGS, f"{board}.lb")), weekly=True)


def check_flags():
    rc = load_json(os.path.join(UGS, "flags.rc"))
    if rc is None:
        return
    entries, types = rc.get("entries", {}), rc.get("types", {})
    if sorted(entries) != sorted(FLAGS):
        problem(f"flags.rc: entries must be exactly {FLAGS} (Remote Config carries flags only)")
    for f in FLAGS:
        if entries.get(f) is not True or types.get(f) != "BOOL":
            problem(f"flags.rc: {f} must be BOOL true")


def check_access():
    ac = load_json(os.path.join(UGS, "no-client-scores.ac"))
    if ac is None:
        return
    statements = ac.get("Statements", [])
    for s in statements:
        if not SID.match(s.get("Sid", "")):
            problem(f"no-client-scores.ac: bad Sid {s.get('Sid')!r}")
    if not any(s.get("Effect") == "Deny" and s.get("Principal") == "Player" and s.get("Action") == ["Write"]
               and s.get("Resource") == SCORES_URN for s in statements):
        problem(f"no-client-scores.ac: needs a Player Deny Write statement on {SCORES_URN}")


def next_reset(now=None):
    now = now or dt.datetime.now(dt.timezone.utc)
    monday = (now + dt.timedelta(days=(7 - now.weekday()) % 7)).replace(hour=reset_hour(), minute=0, second=0, microsecond=0)
    return monday if monday > now else monday + dt.timedelta(days=7)


def stamp(directory):
    start = next_reset().strftime("%Y-%m-%dT%H:%M:%SZ")
    for path in glob.glob(os.path.join(directory, "*.lb")):
        lb = json.loads(read(path))
        if lb.get("ResetConfig"):
            lb["ResetConfig"]["Start"] = start
            with open(path, "w", encoding="utf-8") as f:
                json.dump(lb, f, indent=2)
                f.write("\n")
    print(f"reset start {start}")


def main():
    check_boards()
    check_flags()
    check_access()
    if problems:
        print("ugs config problems:")
        for p in problems:
            print("  - " + p)
        return 1
    print("ugs config ok")
    if len(sys.argv) == 3 and sys.argv[1] == "--stamp":
        stamp(sys.argv[2])
    return 0


if __name__ == "__main__":
    sys.exit(main())
