import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.stdout.reconfigure(encoding="utf-8")
import sections as s  # noqa: E402

STD = {
    "sluchanie": ["multipleChoice", "multipleChoice", "multipleChoice", "trueFalse", "matching"],
    "czytanie": ["multipleChoice", "trueFalse", "fragments", "titles", "chooseInText"],
    "gramatyka": ["chooseInText", "wordBank", "chooseInText", "fillForm", "askQuestion", "transform", "chooseInText", "wordBank"],
}
if len(sys.argv) < 2:
    sys.exit("usage: signature.py <src_dir>")
SRC = sys.argv[1]
for c in "2021-01 2021-05 2021-06 2021-11 2022-02 2022-03 2022-06 2022-11 2023-02 2023-04 2023-06 2023-11 2024-02 2024-04 2024-06".split():
    t = open(f"{SRC}/{c}-arkusz.txt", encoding="utf-8").read()
    sig = s.signature(t)
    flags = []
    for k, want in STD.items():
        got = [x[1] for x in (sig[k] or [])]
        flags.append(k[:3] + (":ok" if got == want else f":DIFF {got}"))
    print(c, " | ".join(flags))
