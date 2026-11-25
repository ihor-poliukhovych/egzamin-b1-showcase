"""Builds arkusz JSON from pdftotext texts and keys. Usage: build.py <src_dir> <out_dir> [codes...]"""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.stdout.reconfigure(encoding="utf-8")
import keys as K  # noqa: E402
import sections as S  # noqa: E402
import tasks as T  # noqa: E402

CODES = "2021-01 2021-05 2021-06 2021-11 2022-02 2022-03 2022-06 2022-11 2023-02 2023-04 2023-06 2023-11 2024-02 2024-04 2024-06".split()
MONTHS = {"07": "lipiec", "08": "sierpień", "09": "wrzesień", "10": "październik", "12": "grudzień", "01": "styczeń", "02": "luty", "03": "marzec", "04": "kwiecień", "05": "maj", "06": "czerwiec", "11": "listopad"}
# Form by the sentence with the polecenie ("Proszę …", "W e-mailu …"); order matters: "e-mail z pozdrowieniami" is pozdrowienia,
# "list … i opisać" is list, "rozprawka" is esej. Keys = Checklists.Writing.
FORMS = [("pozdrowienia", r"pozdrowieni"), ("zyczenia", r"życzeni"), ("zaproszenie", r"zaproszeni|zaprosić"), ("ogloszenie", r"ogłoszeni"),
         ("sms", r"\bSMS"), ("esej", r"esej|wypracowani|rozprawk|argumentacyjn"), ("opinia", r"opini[eęią]"),
         ("sprawozdanie", r"sprawozdani|relacj"), ("charakterystyka", r"charakterystyk"), ("opowiadanie", r"opowiadani"),
         ("list", r"e-mail|\blist\b|\blistu\b|skarg|email"), ("opis", r"opisać|opisze|opis\b")]
EXPECT = {"sluchanie": (5, 30.0, 30), "czytanie": (5, 30.0, 40), "gramatyka": (8, 30.0, 45)}


def declared(part_text):
    """(number of tasks, minutes) from the part heading and the cover."""
    n = re.search(r"(\d+)\s+zadań", part_text)
    minutes = re.search(r"(?:CZAS:?\s*(?:do\s*)?|,\s*)(\d+)\s*minut", part_text)
    pm = re.search(r"Liczba punktów:?\s*(\d+)", part_text)
    pts_sum = float(pm.group(1)) if pm else 0.0
    return (int(n.group(1)) if n else None), (int(minutes.group(1)) if minutes else None), pts_sum


def roman_of(i):
    return S.ROMAN[i]


def part_keys(klucz_nl, klucz_l):
    out = {}
    rn, rl = K.key_ranges(klucz_nl), K.key_ranges(klucz_l)
    for part in ("sluchanie", "czytanie", "gramatyka"):
        out[part] = {"nl": K.task_blocks(rn.get(part, "")), "l": K.task_blocks(rl.get(part, "")), "w": K.layout_words(rl.get(part, ""))}
    return out


HAND = {}  # hand-written tasks of the current exam: {kind: {number: task}}, file hand/<ident>.json


def build_part(kind, text_nl, text_l, text_r, pk, trans, errors):
    parts_nl, parts_l = S.split_parts(text_nl), S.split_parts(text_l)
    if kind not in parts_nl or kind not in parts_l:
        errors.append(f"{kind}: section missing")
        return None
    tn = S.split_tasks(parts_nl[kind])
    tl = {t["roman"]: t for t in S.split_tasks(parts_l[kind])}
    parts_r = S.split_parts(text_r)
    tr_ = {t["roman"]: t for t in S.split_tasks(parts_r[kind])} if kind in parts_r else {}
    expected_n, expected_pts, minutes = EXPECT[kind]
    dn, dm, dp = declared(parts_nl[kind])
    covers = re.findall(r"CZAS:?\s*(?:do\s*)?(\d+)\s*minut", text_nl)
    cover_pts = re.findall(r"Liczba punktów:?\s*_+\s*/\s*(\d+)\s*p\.", text_nl)
    idx = ("sluchanie", "czytanie", "gramatyka", "pisanie").index(kind)
    if len(covers) > idx:
        dm = int(covers[idx])
    if len(cover_pts) > idx:
        dp = float(cover_pts[idx])
    expected_n = dn or expected_n
    minutes = dm or minutes
    expected_pts = dp or expected_pts
    w_all, _x, rblocks = pk[kind]["w"]
    ten = []
    for t0 in tn:
        try:
            if T.points_of(t0)[0] == 10:
                ten.append(t0["roman"])
        except T.ParseError:
            pass
    for i, roman in enumerate(ten):
        if i < len(rblocks):
            w_all.setdefault(roman, {}).update(rblocks[i])
    out_tasks = []
    for t in tn:
        roman = t["roman"]
        number = S.ROMAN.index(roman) + 1
        hand_task = HAND.get(kind, {}).get(str(number))
        if hand_task:
            if not any(x["number"] == number for x in out_tasks):
                extra = {"transcript": trans[roman]} if kind == "sluchanie" and "transcript" not in hand_task and trans.get(roman) else {}
                out_tasks.append({**hand_task, **extra, "number": number})
            continue
        ttype = S.classify(kind, t["head"], t["intro"])
        if ttype is None:
            errors.append(f"{kind} {roman}: task type not recognised")
            continue
        t["body_l"] = tl[roman]["body"] if roman in tl else []
        t["body_r"] = tr_[roman]["body"] if roman in tr_ else []
        w_all, x_all = pk[kind]["w"][0], pk[kind]["w"][1]
        kb = {"nl": pk[kind]["nl"].get(roman, []), "l": pk[kind]["l"].get(roman, []), "w": (w_all.get(roman, {}), x_all.get(roman))}
        if not kb["nl"] and not kb["l"]:
            errors.append(f"{kind} {roman}: no key block")
            continue
        try:
            task = T.PARSERS[ttype](t, kb, part=kind)
        except T.ParseError as e:
            errors.append(f"{kind} {roman} ({ttype}): {e}")
            continue
        except Exception as e:  # noqa: BLE001
            errors.append(f"{kind} {roman} ({ttype}): failure {type(e).__name__}: {e}")
            continue
        task["number"] = number
        if kind == "sluchanie":
            tr = trans.get(roman)
            if not tr:
                errors.append(f"sluchanie {roman}: no transcript")
            else:
                task["transcript"] = tr
        out_tasks.append(task)
    for num, hand_task in HAND.get(kind, {}).items():  # hand-written tasks that have no heading of their own in the PDF (numbering typos)
        if not any(x["number"] == int(num) for x in out_tasks):
            out_tasks.append({**hand_task, "number": int(num)})
    total = sum(i.get("points", x["pointsPerItem"]) for x in out_tasks for i in x["items"])
    if len(out_tasks) != expected_n:
        errors.append(f"{kind}: parsed {len(out_tasks)} tasks out of {expected_n}")
    if abs(total - expected_pts) > 1e-6:
        errors.append(f"{kind}: points total {total}, expected {expected_pts}")
    return {"kind": kind, "timeLimitMinutes": minutes, "tasks": sorted(out_tasks, key=lambda x: x["number"])}


def form_of(text):
    """Task form: first by the sentences with the polecenie, then by the whole text (context such as "Przeczytał Pan ogłoszenie" does not count)."""
    sentences = re.split(r"(?<=[.!?:])\s+", text)
    orders = [s for s in sentences if re.search(r"\b[Pp]roszę\b|\bW e-mailu\b", s)]
    for scope in (" ".join(orders), text):
        form = next((f for f, rx in FORMS if re.search(rx, scope, re.I)), None)
        if form:
            return form
    return None


def split_ab(lines, ia, ib):
    """Boundary between a and b in the zestaw lines (without empty ones). The "a."/"b." label in the PDF sits in the middle of its block:
    label line = (n - 1) // 2 from the start of a block of n lines, so the first line of b often comes above "b.".
    The boundary is the end of a sentence (or an address such as "strona.pl") after which both parts have a polecenie;
    of those, the one is taken where both labels are closer to the centre of their blocks."""
    total = len(lines)

    def sentence_end(na):
        prev = re.sub(r"\s*\d{2,3}\s+słów\s*$", "", lines[na - 1])
        ends = re.search(r"([.!?”\")]|\.[a-z]{2,3})\s*$", prev) and not re.search(r"\b(pt|ks|np|ul|tel)\.\s*$", prev)
        return bool(ends and re.match(r"^\s*(b\.\s+)?[„\"(A-ZĄĆĘŁŃÓŚŹŻ]", lines[na]))

    def off_center(na):
        return abs(ia - (na - 1) / 2) + abs((ib - na) - (total - na - 1) / 2)

    def has_order(chunk):  # each task has a polecenie: "Proszę napisać …", "– proszę opisać …"
        return re.search(r"[Pp]roszę", " ".join(chunk))

    cands = [na for na in range(ia + 1, ib + 1) if sentence_end(na) and has_order(lines[:na]) and has_order(lines[na:])]
    if not cands:
        na = min(range(ia + 1, ib + 1), key=off_center)
        return na, f"a/b boundary is not at a sentence end, line {na} taken (check)"
    cands.sort(key=off_center)
    if len(cands) > 1 and off_center(cands[0]) == off_center(cands[1]):
        return cands[0], f"a/b boundary is ambiguous ({cands[0]} or {cands[1]}), {cands[0]} taken (check)"
    return cands[0], None


def build_pisanie(text_l, errors, warnings):
    parts = S.split_parts(text_l)
    if "pisanie" not in parts:
        errors.append("pisanie: section missing")
        return None
    lines = parts["pisanie"].split(chr(10))
    blocks, cur = {}, None
    for line in lines:
        m = re.match(r"^\s*Z\s*e\s*s\s*t\s*a\s*w\s+(III|II|I)(.*)$", line)
        if m:
            cur = m.group(1)
            blocks[cur] = [m.group(2)] if m.group(2).strip() else []
        elif cur is not None:
            if re.search(r"POZIOM [ABC][12]|ZESTAW NR|TEST PRZYKŁADOWY", line):
                cur = None
            else:
                blocks[cur].append(line)
    tasks = []
    for roman, blines in blocks.items():
        blines = [l for l in blines if l.strip()]
        ia = next((i for i, l in enumerate(blines) if re.match(r"^\s*a\.\s", l)), None)
        ib = next((i for i, l in enumerate(blines) if re.match(r"^\s*b\.\s", l)), None)
        if ia is None or ib is None or ib <= ia:
            errors.append(f"pisanie zestaw {roman}: item a or b missing")
            continue
        na, warn = split_ab(blines, ia, ib)
        if warn:
            warnings.append(f"pisanie {roman}: {warn}")
        items = []
        pool = re.findall(r"(\d{2,3})\s+słów", " ".join(blines))
        for n, chunk in enumerate((blines[:na], blines[na:]), 1):
            text = " ".join(chunk)
            wm = re.search(r"(\d{2,3})\s+słów", text)
            if wm:
                words = int(wm.group(1))
                text = re.sub(r"\d{2,3}\s+słów", " ", text)
            elif len(pool) >= 2:
                words = int(pool[n - 1])
            else:
                ends = [re.search(r"(?<=\s)(\d{2,3})\s*$", c) for c in chunk]
                ends = [e for e in ends if e and 20 <= int(e.group(1)) <= 200]
                if not ends:
                    words = 30 if n == 1 else 170
                    warnings.append(f"pisanie {roman}.{n}: no word count in the PDF, {words} substituted (check)")
                    ends = None
                else:
                    words = int(ends[0].group(1))
                text = " ".join(re.sub(r"(?<=\s)\d{2,3}\s*$", "", c) if re.search(r"(?<=\s)%d\s*$" % words, c) else c for c in chunk)
            text = re.sub(r"(^|\s)[ab]\.\s", " ", text)
            text = re.sub(r"\s+", " ", text).strip()
            form = form_of(text)
            if form is None:
                form = "list"
                warnings.append(f"pisanie {roman}.{n}: form not determined, 'list' substituted (check)")
            items.append({"number": n, "prompt": text, "hint": form, "targetWords": words, "answerKey": []})
        number = S.ROMAN.index(roman) + 1
        for item in items:  # manual form/text overrides: hand/<ident>.json → {"pisanie": {"<zestaw>": {"items": {"<a=1|b=2>": {...}}}}}
            item.update(HAND.get("pisanie", {}).get(str(number), {}).get("items", {}).get(str(item["number"]), {}))
        if len(items) == 2:
            tasks.append({"number": number, "type": "writing", "instruction": f"Zestaw {roman}. Uwaga! Proszę wybrać tylko jeden z zestawów i wykonać obydwa zadania (a i b) z tego samego zestawu.", "pointsPerItem": 15, "items": items})
    if len(tasks) != 3:
        errors.append(f"pisanie: found {len(tasks)} zestawy out of 3")
    return {"kind": "pisanie", "timeLimitMinutes": 75, "tasks": tasks}


def meta_of(ident):
    m = re.match(r"^(dorosli|dim)-(A2|B1|B2|C1|C2)-(.+)$", ident)
    if not m:
        return None
    variant, level, rest = m.groups()
    code = f"{level}-{rest}" if variant == "dorosli" else f"dim-{level}-{rest}"
    return {"variant": variant, "level": level, "code": code, "rest": rest}


def build_exam(src, code, mowienie):
    def rd(n):
        import unicodedata
        txt = unicodedata.normalize("NFC", open(os.path.join(src, f"{code}-{n}.txt"), encoding="utf-8").read())
        return re.sub("[̀-ͯ]", "", txt)
    errors = []
    hp = os.path.join(os.path.dirname(__file__), "hand", f"{code}.json")
    HAND.clear()
    if os.path.exists(hp):
        HAND.update(json.load(open(hp, encoding="utf-8")))
    klucz_nl, klucz_l = rd("klucz"), rd("klucz-l")
    pk = part_keys(klucz_nl, klucz_l)
    trans = K.transcripts(klucz_nl)
    parts, part_errors, ok = [], {}, []
    for kind in ("sluchanie", "czytanie", "gramatyka"):
        perr = []
        p = build_part(kind, rd("arkusz"), rd("arkusz-l"), rd("arkusz-r"), pk, trans, perr)
        if p and not perr:
            parts.append(p)
            ok.append(kind)
        else:
            part_errors[kind] = perr
            errors.extend(perr)
    perr, warnings = [], []
    pis = build_pisanie(rd("arkusz-l"), perr, warnings)
    if pis and not perr:
        covers = re.findall(r"CZAS:?\s*(?:do\s*)?(\d+)\s*minut", rd("arkusz"))
        cpts = re.findall(r"Liczba punktów:?\s*_+\s*/\s*(\d+)\s*p\.", rd("arkusz"))
        if len(covers) > 3:
            pis["timeLimitMinutes"] = int(covers[3])
        if len(cpts) > 3 and int(cpts[3]) != 30:
            pis["maxPoints"] = int(cpts[3])
        parts.append(pis)
        ok.append("pisanie")
    else:
        part_errors["pisanie"] = perr
        errors.extend(perr)
    parts.append(mowienie)
    meta = meta_of(code)
    if meta:
        rest = meta["rest"]
        ym = re.match(r"^(\d{4})-(\d{2})$", rest)
        title = f"{meta['level']} {'młodzież ' if meta['variant'] == 'dim' else ''}{MONTHS.get(ym.group(2), ym.group(2)) + ' ' + ym.group(1) if ym else rest}"
        parts = [p for p in parts if p["kind"] != "mowienie"]
        for p in parts:
            if p["kind"] == "sluchanie":
                p["audio"] = code
        exam = {"code": meta["code"], "title": title, "source": "official", "level": meta["level"], "variant": meta["variant"], "parts": parts}
    else:
        y, m = code.split("-")
        exam = {"code": code, "title": f"{MONTHS[m]} {y}", "source": "official", "parts": parts}
    exam["_warnings"] = warnings
    exam["_ok"] = ok
    return exam, errors


if __name__ == "__main__":
    src, out = sys.argv[1], sys.argv[2]
    codes = sys.argv[3:] or CODES
    if codes == ["ALL2"]:
        codes = sorted({f[: -len("-arkusz.txt")] for f in os.listdir(src) if f.endswith("-arkusz.txt")})
    base = json.load(open(os.path.join(os.path.dirname(__file__), "..", "..", "content", "arkusze", "2022-03.json"), encoding="utf-8"))
    mow = next(p for p in base["parts"] if p["kind"] == "mowienie")
    os.makedirs(out, exist_ok=True)
    report = {}
    for code in codes:
        exam, errors = build_exam(src, code, mow)
        report[code] = errors
        with open(os.path.join(out, f"{code}.json"), "w", encoding="utf-8") as f:
            json.dump(exam, f, ensure_ascii=False, indent=2)
        print(code, "OK" if not errors else f"DRAFT ({len(errors)}):")
        for e in errors:
            print("   -", e[:200])
    with open(os.path.join(out, "_report.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
