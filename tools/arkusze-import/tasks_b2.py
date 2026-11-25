"""Parsers for the types that appear from B2: groups of questions about texts (MC) and open short answers."""
import re

import keys as K
from tasks import EX, PTS_PREFIX, ParseError, instruction_of, points_of

_OPT = re.compile(r"^(.*?)\s*\ba\)\s+(.+?)\s+b\)\s+(.+?)\s+c\)\s+(.+)$")


def _clean(s):
    return re.sub(r"\s+", " ", s).strip()


def mc_units(lines):
    """Line-by-line parsing (raw order): blocks "N. Proszę wysłuchać …" with questions and a/b/c options on separate lines."""
    units = []
    unit = None
    q = None
    mode = None

    def new_q(stem):
        nonlocal q, mode
        q = {"stem": stem, "opts": ["", "", ""], "i": -1}
        unit["qs"].append(q)
        mode = "stem"

    for l in lines:
        m = re.match(rf"^({EX}|\d{{1,2}}\.)\s*(.*)$", l)
        o = re.match(r"^([abc])\)\s*(.*)$", l)
        if m and not (q and mode == "opt" and q["i"] < 2 and re.match(r"^\d", m.group(1)) is None):
            marker, rest = m.group(1), m.group(2)
            unit = {"marker": marker, "instr": None, "qs": []}
            units.append(unit)
            q = None
            if rest.startswith("Proszę"):
                unit["instr"] = rest
                mode = "instr"
            elif rest:
                new_q(rest)
            else:
                mode = "pre"
        elif unit is None:
            continue
        elif o:
            if q is None:
                new_q(unit["instr"] or "")
                unit["instr"] = None if unit["instr"] else None
            q["i"] = "abc".index(o.group(1))
            q["opts"][q["i"]] = o.group(2).strip()
            mode = "opt"
        else:
            if mode == "instr":
                if re.search(r"(odpowiedzi|odpowiedzieć|pytania)\.?\s*$", unit["instr"]) or unit["instr"].endswith("."):
                    new_q(l)
                else:
                    unit["instr"] += " " + l
            elif mode == "stem":
                q["stem"] += " " + l
            elif mode == "opt":
                if q["i"] == 2 and re.match(r"^[A-ZŁŚŻŹĆ„]", l):
                    new_q(l)
                else:
                    q["opts"][q["i"]] += " " + l
            elif mode == "pre":
                new_q(l)
    return units


def parse_mc_groups(t, kb, **_):
    n, pts = points_of(t)
    lines = [PTS_PREFIX.sub("", l.strip()).strip() for l in t["body_r"] if l.strip()]
    lines = [l for l in lines if l and not re.match(r"^_+\s*/", l) and not l.startswith("Na podstawie") and not re.match(r"^POZIOM B2", l)
             and not re.match(r"^(styczeń|luty|marzec|kwiecień|maj|czerwiec|listopad|grudzień) \d{4}", l) and not l.startswith("http")]
    intro = []
    while lines and not re.match(rf"^(?:{EX}|\d+\.)", lines[0]):
        intro.append(lines.pop(0))
    units = mc_units(lines)
    example, items = None, []
    for u in units:
        qs = [q for q in u["qs"] if all(q["opts"])]
        if u["marker"] == EX:
            if qs:
                example = _clean((u["instr"] or "") + " " + " ".join(q["stem"] + " " + " ".join(f"{'abc'[k]}) {x}" for k, x in enumerate(q["opts"])) for q in qs))
            continue
        for q in qs:
            items.append((u["instr"] if len(u["qs"]) else None, _clean(q["stem"]), [_clean(x) for x in q["opts"]]))
    if len(items) != n:
        raise ParseError(f"MC: {len(items)} items out of {n}")
    letters = K.letters_any(kb, "a-c", n)
    if len(letters) != n:
        raise ParseError(f"MC key: {len(letters)} letters out of {n}")
    out = []
    for idx, ((instr, stem, opts), letter) in enumerate(zip(items, letters), 1):
        k = "abc".index(letter.lower())
        item = {"number": idx, "hint": stem, "options": [f"{'abc'[j]}) {o}" for j, o in enumerate(opts)], "answerKey": [f"{'abc'[k]}) {opts[k]}"]}
        if instr:
            item["prompt"] = _clean(instr)
        out.append(item)
    task = {"type": "multipleChoice", "instruction": instruction_of(t["head"]) + " " + _clean(" ".join(intro)), "pointsPerItem": pts, "items": out}
    if example:
        task["example"] = example
    return task


def _key_rows(lines):
    """Key of the open answers (layout): {number: [left-column lines]}; the right column (a quote from the transcript) is discarded."""
    rows, cur, x = {}, None, None
    for raw in lines:
        if not raw.strip():
            continue
        m = re.match(r"^\s*(\d+)\*?\.?\s+(\S.*)$", raw)
        if m and not re.match(r"^\s*\d+\s*[ab]\)\s*$", raw):
            cur = int(m.group(1))
            rows[cur] = []
            gap = re.search(r"\s{3,}\S", raw[m.start(2):])
            x = m.start(2) + gap.start() if gap else None
            left = raw[m.start(2): x].strip() if x else m.group(2).strip()
            if left:
                rows[cur].append(left)
        elif cur is not None and "Należy uznać" not in raw and "POZIOM" not in raw:
            left = raw[:x].strip() if x else raw.strip()
            if left:
                rows[cur].append(left)
    return rows


def parse_short_answers(t, kb, **_):
    n_total, _pts = points_of(t)
    lines = [l.rstrip() for l in t["body_l"] if l.strip()]
    start = next((i for i, l in enumerate(lines) if re.match(r"^\s*1\.\s", l)), None)
    if start is None:
        raise ParseError("question 1 missing")
    intro = [l.strip() for l in lines[:start] if not l.strip().startswith(EX) and not re.match(r"^\s*_+\s*/", l)]
    example = _clean(" ".join(x.strip() for x in lines[:start] if x.strip().startswith(EX) or (intro and False)))
    ex_i = next((i for i, l in enumerate(lines) if l.strip().startswith(EX)), None)
    if ex_i is not None:
        example = _clean(" ".join(l.strip() for l in lines[ex_i:start]).replace(EX, "", 1))
        intro = [l.strip() for l in lines[:ex_i] if not re.match(r"^\s*_+\s*/", l)]
    questions = []  # (num, text, [(sub, pts)])
    qnum = None
    for l in lines[start:]:
        s = l.strip()
        if s.startswith("Na podstawie") or "POZIOM" in s:
            continue
        qm = re.match(r"^(\d+)\.\s+(.*)$", s)
        dm = re.match(r"^(?:([ab])\)\s*)?[….\s]{5,}\(?\s*([\d,]+)\s*p\.\)?", s)
        if qm and not re.match(r"^[….]", qm.group(2)):
            qnum = int(qm.group(1))
            questions.append([qnum, qm.group(2).strip(), []])
        elif dm and questions:
            questions[-1][2].append((dm.group(1), float(dm.group(2).replace(",", "."))))
        elif questions and not re.match(r"^[….]", s):
            questions[-1][1] += " " + s
    if not questions:
        raise ParseError("no questions")
    rows = _key_rows(kb["l"])
    items, num = [], 0
    for qn, text, subs in questions:
        left = rows.get(qn)
        if not left:
            raise ParseError(f"no key for question {qn}")
        if len(subs) > 1 or (subs and subs[0][0]):
            joined = " ".join(left)
            parts = re.split(r"(?:^|\s)([ab])\)\s*", joined)
            by = {parts[i]: parts[i + 1].strip() for i in range(1, len(parts) - 1, 2)}
            for sub, p in subs:
                num += 1
                ans = by.get(sub or "", "")
                items.append({"number": num, "prompt": f"{qn}. {_clean(text)} ({sub})", "answerKey": _variants([ans] if ans else left), "points": p})
        else:
            num += 1
            items.append({"number": num, "prompt": f"{qn}. {_clean(text)}", "answerKey": _variants(left), "points": subs[0][1] if subs else 1.0})
    total = sum(i["points"] for i in items)
    if abs(total - n_total) > 1e-6:
        raise ParseError(f"open-answer points {total}, expected {n_total}")
    task = {"type": "shortAnswer", "instruction": instruction_of(t["head"]) + " " + _clean(" ".join(intro)), "pointsPerItem": 1, "items": items}
    if example:
        task["example"] = example
    return task


def _variants(rows):
    out = []
    for r in rows:
        for v in re.split(r"\s*/\s*", r):
            v = re.sub(r"\(\s*[\d,]+\s*p\.\)", "", v).strip().rstrip(".")
            if v:
                out.append(v)
    joined = re.sub(r"\s+", " ", " ".join(rows)).strip().rstrip(".")
    if joined and joined not in out:
        out.append(joined)
    return list(dict.fromkeys(out)) or [""]
