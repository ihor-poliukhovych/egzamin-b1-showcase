import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.stdout.reconfigure(encoding="utf-8")
import sections as s
SRC = sys.argv[1]
for c in "2021-01 2021-05 2021-06 2021-11 2022-02 2022-03 2022-06 2022-11 2023-02 2023-04 2023-06 2023-11 2024-02 2024-04 2024-06".split():
    parts = s.split_parts(open(f"{SRC}/{c}-arkusz.txt", encoding="utf-8").read())
    out = []
    for k in ("sluchanie", "czytanie"):
        tasks = s.split_tasks(parts[k])
        out.append(k[:3] + ":" + ",".join(f"{t['roman']}={s.classify(k, t['head'], t['intro']) or '??'}" for t in tasks))
    print(c, " ".join(out))
    for k in ("sluchanie", "czytanie"):
        for t in s.split_tasks(parts[k]):
            if s.classify(k, t["head"], t["intro"]) is None:
                print("   ??", k, t["roman"], t["head"][:150], "|", t["intro"][:120])
