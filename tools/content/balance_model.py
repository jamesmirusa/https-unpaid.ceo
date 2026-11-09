#!/usr/bin/env python3
"""Quick model of one business month (mirrors BusinessSimulator) to pick tuning values before the Unity simulation.

Prints, per chapter-2 industry: optimal monthly operating profit (price ±30%, marketing 0..2K, staff), the casual one
(reference price, no marketing, owner at 55% work, staff only if it adds capacity), and their net after tax and living costs.
"""
import json
import math
import os
import sys

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "game", "Assets", "UnpaidCeo", "Content")
ind = {x["id"]: x for x in json.load(open(os.path.join(ROOT, "industries.json")))["industries"]}
bal = json.load(open(os.path.join(ROOT, "balance.json")))
REP = 1.04  # reputation 60


def month(d, price, mk, units, emp, owner, district):
    staff = min(1.0, (emp + owner) / max(1, units * d.get("staffPerUnit", 1)))
    cap = math.floor(d["capacityPerUnit"] * units * staff + 1e-9)
    pf = (price / d["refPrice"]) ** (-d["elasticity"])
    mf = 1 + d["marketingA"] * math.log(1 + mk / d["marketingK"]) if mk > 0 else 1
    demand = math.floor(d["baseDemand"] * pf * mf * REP * district + 1e-9)
    sold = min(demand, cap)
    fixed = d["fixedMonthly"] * units + d["salary"] * emp * (1 + bal["payrollOverhead"])
    return sold * (price - d["unitCost"]) - fixed - mk, sold, cap, demand


def best(d, units, district, owner=1.0):
    top = None
    for i in range(-6, 7):
        p = round(d["refPrice"] * (1 + 0.05 * i))
        for m in (0, 0.5, 1, 2):
            mk = round(d["marketingK"] * m)
            for emp in range(0, units * d.get("staffPerUnit", 1) * 2 + 1):
                r = month(d, p, mk, units, emp, owner, district)
                if top is None or r[0] > top[0][0]:
                    top = (r, p, mk, emp)
    return top


def casual(d, units, district):
    owner = 55 / 60
    emp = 0
    while True:
        r = month(d, d["refPrice"], 0, units, emp, owner, district)
        staff = min(1.0, (emp + owner) / max(1, units * d.get("staffPerUnit", 1)))
        if r[1] >= r[2] and staff <= 0.5:
            emp += 1
            continue
        return r, emp


def net(op, personal):
    return op * 0.75 - personal if op > 0 else op - personal


p2 = 190000 + 34320 + 16720 + 16500
p3 = 230000 + 37752 + 18392 + 18150
print(f"{'industry':22} {'capex':>7} | ch2 opt op/net (p,mk,emp)        | casual op/net emp | ratio | ch3 2u opt op/net")
for k in bal["chapter2Industries"]:
    d = ind[k]
    (o, p, mk, emp) = best(d, 1, 1.0)
    (c, cemp) = casual(d, 1, 1.0)
    (o3, p3_, mk3, emp3) = best(d, 2, 1.5)
    (o31, _, _, _) = best(d, 1, 1.5)
    no, nc = net(o[0], p2), net(c[0], p2)
    print(f"{k:22} {d['capex']//100:>7} | {o[0]//100:>6}/{no//100:>6} ({p},{mk},{emp}) sold {o[1]}/{o[2]} | {c[0]//100:>6}/{nc//100:>6} {cemp} | {nc/no if no>0 else 0:5.2f} | {o3[0]//100:>6}/{net(o3[0],p3)//100:>6} e{emp3} | 1u {o31[0]//100}")
