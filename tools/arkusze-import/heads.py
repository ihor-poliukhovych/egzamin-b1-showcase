import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.stdout.reconfigure(encoding="utf-8")
import sections as S
src, ident = sys.argv[1], sys.argv[2]
t = open(f"{src}/{ident}-arkusz.txt", encoding="utf-8").read()
parts = S.split_parts(t)
for k, v in parts.items():
    print("##", k, len(v))
    for x in S.split_tasks(v):
        print("  ", x["roman"], "|", x["head"][:115], "|", " ".join(x["body"][:3])[:70])
