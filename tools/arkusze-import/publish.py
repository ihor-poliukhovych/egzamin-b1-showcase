"""Publishing by parts: moves parsed arkusze into content/arkusze when at least one part with a key matched (Słuchanie, Czytanie, Gramatyka).
Pisanie is published only together with such a part. Mówienie is added only if the exam is complete (all main parts are present).
Usage: publish.py <parsed_dir> <content_arkusze_dir> [codes...]. The hand-verified 2022-03 and 2023-04 are left untouched."""
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
parsed, target = sys.argv[1], sys.argv[2]
only = set(sys.argv[3:])
HAND = {"2022-03", "2023-04"}
CORE = ["sluchanie", "czytanie", "gramatyka", "pisanie"]
KEYED = {"sluchanie", "czytanie", "gramatyka"}
report = json.load(open(os.path.join(parsed, "_report.json"), encoding="utf-8"))
rows = []
for ident in sorted(report):
    path = os.path.join(parsed, f"{ident}.json")
    if not os.path.exists(path):
        continue
    exam = json.load(open(path, encoding="utf-8"))
    code = exam["code"]
    if code in HAND or (only and ident not in only and code not in only):
        continue
    ok = set(exam.get("_ok", []))
    if not (ok & KEYED):
        rows.append((code, "draft", []))
        continue
    exam.pop("_warnings", None)
    exam.pop("_ok", None)
    full = all(k in ok for k in CORE)
    exam["parts"] = [p for p in exam["parts"] if p["kind"] in ok or (p["kind"] == "mowienie" and full)]
    for p in exam["parts"]:
        if p["kind"] == "sluchanie" and not p.get("audio"):
            p["audio"] = code
    out = os.path.join(target, f"{code}.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(exam, f, ensure_ascii=False, indent=2)
        f.write("\n")
    rows.append((code, "full" if full else "parts", [p["kind"] for p in exam["parts"]]))
for code, status, kinds in rows:
    print(f"{code}: {status} {' '.join(kinds)}")
