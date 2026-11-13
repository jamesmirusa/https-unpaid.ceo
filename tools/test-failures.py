#!/usr/bin/env python3
"""Prints each failed test of a Unity test run with the first lines of its message.
   tools/test-failures.py [playmode|editmode] [max message chars]"""
import sys, os, xml.etree.ElementTree as ET
kind = sys.argv[1] if len(sys.argv) > 1 else "playmode"
limit = int(sys.argv[2]) if len(sys.argv) > 2 else 300
path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "game", "TestResults", kind + ".xml")
root = ET.parse(path).getroot()
print({k: root.get(k) for k in ("result", "total", "passed", "failed", "skipped")})
for case in root.iter("test-case"):
    if case.get("result") != "Failed":
        continue
    message = case.find("failure/message")
    text = (message.text or "").strip().replace("\n", " | ") if message is not None else ""
    print("-", case.get("fullname").replace("UnpaidCeo.Game.Tests.", ""), "::", text[:limit])
