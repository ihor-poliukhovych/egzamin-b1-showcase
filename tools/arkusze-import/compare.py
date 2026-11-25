"""Compares the automatic import with the hand-verified files (2022-03, 2023-04) and runs general quality checks. Usage: compare.py <parsed_dir> <hand_dir>"""
import json
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")
parsed, hand = sys.argv[1], sys.argv[2]


def keys_of(exam, kind):
    part = next(p for p in exam["parts"] if p["kind"] == kind)
    out = {}
    for t in part["tasks"]:
        out[t["number"]] = [tuple(i["answerKey"]) for i in t["items"]]
    return out


def norm(s):
    return re.sub(r"\W+", "", s.lower())


for code in ("2022-03", "2023-04"):
    a = json.load(open(os.path.join(parsed, f"{code}.json"), encoding="utf-8"))
    h = json.load(open(os.path.join(hand, f"{code}.json"), encoding="utf-8"))
    for kind in ("sluchanie", "czytanie", "gramatyka"):
        ka, kh = keys_of(a, kind), keys_of(h, kind)
        diffs = []
        for num in sorted(set(ka) | set(kh)):
            va, vh = ka.get(num), kh.get(num)
            if va is None or vh is None or len(va) != len(vh):
                diffs.append((num, "item count", len(va or []), len(vh or [])))
                continue
            for i, (x, y) in enumerate(zip(va, vh), 1):
                if {norm(s) for s in x} != {norm(s) for s in y}:
                    diffs.append((num, i, x[:1], y[:1]))
        print(code, kind, "key mismatches:", len(diffs))
        for d in diffs[:6]:
            print("    ", d)

print("--- general checks")
for fn in sorted(os.listdir(parsed)):
    if not fn.endswith(".json") or fn.startswith("_"):
        continue
    ex = json.load(open(os.path.join(parsed, fn), encoding="utf-8"))
    problems = []
    for part in ex["parts"]:
        for t in part["tasks"]:
            blob = json.dumps(t, ensure_ascii=False)
            if re.search(r"\{\d+\}", t.get("text", "") or "") is None and t["type"] in ("chooseInText", "wordBank", "fillForm", "matching") and t.get("text") and "{1}" not in t["text"]:
                if t["type"] != "matching":
                    problems.append((part["kind"], t["number"], "no gaps in the text"))
            for it in t["items"]:
                if t["type"] not in ("writing", "speaking") and not it.get("answerKey"):
                    problems.append((part["kind"], t["number"], it["number"], "empty key"))
                if t["type"] in ("multipleChoice", "trueFalse") and it.get("options") and not all(k in it["options"] for k in it["answerKey"]):
                    problems.append((part["kind"], t["number"], it["number"], "key not among the options"))
                if t["type"] in ("multipleChoice", "matching", "trueFalse") and it.get("prompt") == "":
                    problems.append((part["kind"], t["number"], it["number"], "empty prompt"))
            if "  " in blob.replace("\\n", " "):
                pass
    print(fn, "OK" if not problems else problems[:5])
