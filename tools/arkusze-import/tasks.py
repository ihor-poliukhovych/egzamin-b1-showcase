"""Task parsers by type. Each returns a dict in the content/arkusze/*.json format or raises ParseError."""
import re

import keys as K

DOTS = r"(?:[….·]\s*){3,}"
EX = "PRZYKŁAD"


class ParseError(Exception):
    pass


def points_of(t):
    src = t["head"] + " " + " ".join(t["body"][:6])
    m = re.search(r"\((\d+)\s*x\s*([\d,]+)\s*p\.?\)", src)
    if m:
        return int(m.group(1)), float(m.group(2).replace(",", "."))
    m = re.search(r"/\s*(\d+)\s*p\.", src)
    if m:
        return int(m.group(1)), 1.0
    raise ParseError("no points in the heading")


def instruction_of(head):
    h = re.sub(r"_+\s*/\s*[\d,]+\s*p?\.?\s*\(.*?\)", "", head)
    h = re.sub(r"\s+", " ", h).strip()
    return h


def norm_key(s):
    return re.sub(r"\s+", " ", s.strip().rstrip(".!?")).lower()


PTS_PREFIX = re.compile(r"^\s*_+\s*/\s*[\d,]+\s*p?\.?\s*\(\d+\s*x\s*[\d,]+\s*p?\.?\)\s*")


def body_text(lines, drop_points=True):
    out = []
    for l in lines:
        s = l.strip()
        if drop_points:
            s = PTS_PREFIX.sub("", s).strip()
            if re.match(r"^_+\s*/", s):
                continue
        if s:
            out.append(s)
    return out


def join(lines):
    text = " ".join(lines)
    text = re.sub(r"(\w)- (\w)", lambda m: m.group(0), text)
    return re.sub(r"\s+", " ", text).strip()


def find_key_in(options, key):
    nk = norm_key(key)
    for o in options:
        if norm_key(o) == nk:
            return o
    return None


# ---------------------------------------------------------------- Gramatyka / Czytanie V: wybór formy w tekście
def parse_choose_in_text(t, kb, **_):
    n, pts = points_of(t)
    lines = body_text(t["body"])
    text = join(lines)
    text = re.sub(r"\s+(Na podstawie:.*)$", "", text) if False else text
    based = None
    mb = re.search(r"\s(Na podstawie:.*)$", text)
    if mb:
        based = mb.group(1).strip()
        text = text[: mb.start()]
    items = {}
    example_seen = []

    def repl(m):
        opts = [o.strip() for o in m.group(1).split(",") if o.strip()]
        tag = m.group(2)
        if tag == EX:
            example_seen.append(opts)
            return "(" + " / ".join(opts) + ") [przykład]"
        num = int(tag)
        items[num] = opts
        return "{%d}" % num

    text = re.sub(r"\(([^()]+)\)\s*(PRZYKŁAD|\d{1,2})(?!\d)", repl, text)
    if sorted(items) != list(range(1, n + 1)):
        raise ParseError(f"found {len(items)} gaps out of {n}")
    words, _extra = kb["w"]
    out_items = []
    for i in range(1, n + 1):
        opts = items[i]
        if len(opts) < 2:
            raise ParseError(f"item {i}: fewer than two options")
        if i not in words:
            raise ParseError(f"no key for item {i}")
        hit = find_key_in(opts, words[i][0])
        if hit is None:
            raise ParseError(f"item {i}: key '{words[i][0]}' is not among {opts}")
        out_items.append({"number": i, "options": opts, "answerKey": [hit]})
    task = {"type": "chooseInText", "instruction": instruction_of(t["head"]), "pointsPerItem": pts, "text": text, "items": out_items}
    if based:
        task["basedOn"] = based
    return task


# ---------------------------------------------------------------- words from the box
def parse_word_bank(t, kb, **_):
    n, pts = points_of(t)
    lines = body_text(t["body"])
    # box: consecutive short lines (1-4 words) at the start, or a single line of >=5 words
    bank, used = [], 0
    for idx, l in enumerate(lines):
        toks = l.split()
        ok = 1 <= len(toks) <= 7 and all(re.fullmatch(r"[\wąćęłńóśźżĄĆĘŁŃÓŚŹŻ-]+", x) and len(x) <= 14 for x in toks)
        if ok and (not bank or used == idx):
            bank.extend(toks)
            used = idx + 1
        elif bank:
            break
        elif idx > 3:
            break
    if not (5 <= len(bank) <= 20):
        raise ParseError(f"box of {len(bank)} words")
    rest = lines[used:]
    text = join(rest)
    mb = re.search(r"\s(Na podstawie:.*)$", text)
    based = None
    if mb:
        based = mb.group(1).strip()
        text = text[: mb.start()]
    gaps = list(re.finditer(rf"({DOTS})\s*(\d{{1,2}})(?!\d)", text))
    nums = [int(g.group(2)) for g in gaps]
    if nums != list(range(1, n + 1)):
        raise ParseError(f"gaps {nums}, expected 1..{n}")
    text = re.sub(rf"{DOTS}\s*(\d{{1,2}})(?!\d)", lambda m: "{%s}" % m.group(1), text)
    text = re.sub(rf"(\S+)\s+{EX}", lambda m: "[[%s]]" % m.group(1), text, count=1)
    words, extra = kb["w"]
    items = []
    for i in range(1, n + 1):
        if i not in words:
            raise ParseError(f"no key {i}")
        hit = find_key_in(bank, words[i][0])
        if hit is None and " " in words[i][0] and all(find_key_in(bank, w) for w in words[i][0].split()):
            hit = words[i][0]  # an answer made of several words from the box («lecz także»)
        if hit is None:
            raise ParseError(f"item {i}: '{words[i][0]}' is not in the box {bank}")
        items.append({"number": i, "answerKey": [hit]})
    task = {"type": "wordBank", "instruction": instruction_of(t["head"]), "pointsPerItem": pts, "wordBank": bank, "text": text, "items": items}
    if based:
        task["basedOn"] = based
    return task


# ---------------------------------------------------------------- IV: word forms in brackets
def parse_fill_form(t, kb, **_):
    n, pts = points_of(t)
    text = join(body_text(t["body"]))
    mb = re.search(r"\s(Na podstawie:.*)$", text)
    based = None
    if mb:
        based = mb.group(1).strip()
        text = text[: mb.start()]
    prompts = {}

    def repl(m):
        num = int(m.group(2))
        prompts[num] = m.group(3).strip()
        return "{%d}" % num

    text = re.sub(rf"({DOTS})\s*(\d{{1,2}})\s*\(([^)]+)\)\s*(?:\d{{1,2}})?(?!\d)", repl, text)
    if sorted(prompts) != list(range(1, n + 1)):
        raise ParseError(f"found {len(prompts)} gaps out of {n}")

    def example(m):
        verb = m.group(2)
        words = 2 if " się" in verb else 1
        before = text_before(m)
        return m.group(0)

    # example: "<answer> (verb)PRZYKŁAD", answer = 1-2 words before the bracket
    def ex_repl(m):
        verb = m.group(2)
        k = 2 if verb.strip().endswith(" się") or " się" in verb else 1
        words = m.group(1).split()
        if len(words) < k:
            return m.group(0)
        ans = " ".join(words[-k:])
        return " ".join(words[:-k]) + (" " if words[:-k] else "") + "[[%s]] (%s)" % (ans, verb)

    text = re.sub(rf"((?:\S+\s+){{1,2}}?\S+)\s*\(([^)]+)\)\s*{EX}", lambda m: ex_repl(m), text)
    words, _e = kb["w"]
    items = []
    for i in range(1, n + 1):
        if i not in words:
            raise ParseError(f"no key {i}")
        items.append({"number": i, "prompt": prompts[i], "answerKey": words[i]})
    task = {"type": "fillForm", "instruction": instruction_of(t["head"]), "pointsPerItem": pts, "text": text, "items": items}
    if based:
        task["basedOn"] = based
    return task


def text_before(m):
    return ""


# ---------------------------------------------------------------- V: question about the underlined part
DASH = r"[-–—]"
DOTS_RX = r"[….]{3,}"


def parse_ask_question(t, kb, **_):
    n, pts = points_of(t)
    text = join(body_text(t["body"]))
    m0 = re.match(rf"^{EX}\s+(.*?)\s+(?=1\.\s+[A-ZŁŚŻŹĆ„])", text)
    if not m0:
        raise ParseError("example or item 1 not found")
    example = re.sub(rf"\s+{DASH}\s*", "\n– ", m0.group(1).strip())
    rest = text[m0.end():]
    sentences = {}
    for m in re.finditer(rf"(?:(?<=\s)|^)(\d)\.\s+([A-ZŁŚŻŹĆ„].*?)(?=\s+{DASH}\s*{DOTS_RX}|\s+\d\.\s+[A-ZŁŚŻŹĆ„]|$)", rest):
        sentences.setdefault(int(m.group(1)), m.group(2).strip())
    frames = [(m.group(1).strip(), m.group(2).strip().rstrip(".")) for m in re.finditer(
        rf"{DASH}\s*{DOTS_RX}\s*(.*?\?)\s*{DASH}\s*(.+?)(?=\s+{DASH}\s*{DOTS_RX}|\s+\d\.\s+[A-ZŁŚŻŹĆ„]|$)", rest)]
    if sorted(sentences) != list(range(1, n + 1)) or len(frames) != n:
        raise ParseError(f"V: sentences {sorted(sentences)}, frames {len(frames)} out of {n}")
    keys, _e = kb["w"]
    items = []
    for i in range(1, n + 1):
        sentence = sentences[i]
        frame, answer = frames[i - 1]
        idx = sentence.lower().find(answer.lower())
        if idx >= 0:
            sentence = sentence[:idx] + "__" + sentence[idx: idx + len(answer)] + "__" + sentence[idx + len(answer):]
        if i not in keys:
            raise ParseError(f"no key {i}")
        cands = []
        for k in keys[i]:
            for piece in re.split(r"(?<=\?)\s+|\s*/\s*", k):
                piece = piece.replace("...", "").replace("…", "").strip().rstrip("?").strip()
                if piece:
                    cands.append(piece)
        rest_frame = frame.rstrip("?").strip()
        full = [c + " " + rest_frame for c in cands if rest_frame]
        items.append({"number": i, "prompt": sentence, "hint": "– … " + frame + "\n– " + answer + ".", "answerKey": list(dict.fromkeys(cands + full))})
    return {"type": "askQuestion", "instruction": instruction_of(t["head"]), "pointsPerItem": pts, "example": example, "items": items}


# ---------------------------------------------------------------- VI: transformation
_DESC = re.compile(r"\s+(?:\d+\s+transformacj|odpowiedni|odpowiednio|usunięcie|brak transformacji|\d+\s+odpowiedni)")


def parse_transform(t, kb, **_):
    n, pts = points_of(t)
    lines = body_text(t["body"])
    ex_idx = next((i for i, l in enumerate(lines) if l.startswith(EX)), None)
    first_item = next((i for i, l in enumerate(lines) if re.match(r"^1\.\s", l)), None)
    if ex_idx is None or first_item is None:
        raise ParseError("example or item 1 missing")
    ex_lines = [l.replace(EX, "").strip() for l in lines[ex_idx:first_item] if l.replace(EX, "").strip() and not set(l) <= set("….. ")]
    example = "\n".join(ex_lines)
    items_raw = {}
    cur = None
    for l in lines[first_item:]:
        m = re.match(r"^(\d)\.\s+(.*)$", l)
        if m:
            cur = int(m.group(1))
            items_raw[cur] = [m.group(2)]
        elif cur is not None and not set(l) <= set("….. ") and not re.match(r"^\s*(POZIOM [A-C]\d.*|TEST PRZYKŁADOWY.*|(styczeń|luty|marzec|kwiecień|maj|czerwiec|listopad|grudzień) \d{4}|\d{1,2})\s*$", l):
            items_raw[cur].append(l)
    if sorted(items_raw) != list(range(1, n + 1)):
        raise ParseError("VI items: %s" % sorted(items_raw))
    nl_text = " ".join(l.strip() for l in kb["nl"] if l.strip())
    nl_text = re.sub(r"Należy uznać.*?poleceniem\.?", "", nl_text)
    pairs_nl = {}
    for mm in re.finditer(r"(?<!\d)(\d)\.\s+(.+?)(?=\s+\d\.\s+[A-ZŁŚŻŹĆ]|$)", nl_text):
        pairs_nl[int(mm.group(1))] = mm.group(2).strip()
    pairs_l = {k: " ".join(v) if isinstance(v, list) else v for k, v in kb["w"][0].items()}
    pairs = pairs_nl if all(i in pairs_nl for i in range(1, n + 1)) else pairs_l
    items = []
    for i in range(1, n + 1):
        raw = re.sub(r"[….\s]+$", "", " ".join(items_raw[i]))
        m = re.match(r"^(.*?)\s*\(([^)]+)\)\s*$", raw)
        if not m:
            raise ParseError(f"item {i}: no word in brackets")
        prompt, word = m.group(1).strip(), m.group(2).strip()
        if i not in pairs:
            raise ParseError(f"no key {i}")
        k = pairs[i]
        d = _DESC.search(k)
        if d is None:
            d = re.search(r"\s{2,}", k)
        answer = (k[: d.start()] if d else k).strip()
        explanation = re.sub(r"\s+", " ", k[d.start():]).strip() if d else None
        variants = [v.strip() for v in re.split(r"\s+/\s+", answer) if v.strip()]
        item = {"number": i, "prompt": prompt, "hint": word, "answerKey": [v.rstrip(".") for v in variants]}
        if explanation:
            item["explanation"] = explanation
        items.append(item)
    return {"type": "transform", "instruction": instruction_of(t["head"]), "pointsPerItem": pts, "example": example, "items": items}


# ---------------------------------------------------------------- MC a/b/c (Czytanie I, Słuchanie I–III)
def parse_multiple_choice(t, kb, part, **_):
    n, pts = points_of(t)
    lines = body_text(t["body"])
    text = join(lines)
    mb = re.search(r"\s(Na podstawie:.*)$", text)
    based = None
    if mb and part != "czytanie":
        based = mb.group(1).strip()
        text = text[: mb.start()]
    if part == "sluchanie":
        try:
            return B2.parse_mc_groups(t, kb)
        except ParseError:
            return _mc_listening(t, kb, text, n, pts, based)
    return _mc_reading(t, kb, lines, n, pts)


def _opts(a, b, c):
    clean = lambda s: re.sub(r"\s+", " ", s).strip()
    return [clean(a), clean(b), clean(c)]


def _mc_listening(t, kb, text, n, pts, based):
    pat = re.compile(r"(?:(?<=\s)|^)(PRZYKŁAD|\d{1,2}\.)\s+(.+?)\s+a\)\s+(.+?)\s+b\)\s+(.+?)\s+c\)\s+(.+?)(?=\s+(?:\d{1,2}\.|PRZYKŁAD)\s+\S|\s*$)")
    items = {}
    example = None
    # instruction before the first PRZYKŁAD
    for m in pat.finditer(text):
        tag = m.group(1)
        stem, o = m.group(2), _opts(m.group(3), m.group(4), m.group(5))
        if tag == EX:
            example = stem + "\n" + "\n".join(f"{'abc'[i]}) {x}" for i, x in enumerate(o))
        else:
            items[int(tag.rstrip("."))] = (stem, o)
    if sorted(items) != list(range(1, n + 1)):
        raise ParseError(f"MC: items {sorted(items)} out of {n}")
    letters = K.letters_any(kb, "a-c", n)
    if len(letters) != n:
        raise ParseError(f"MC key: {len(letters)} letters out of {n}")
    out = []
    for i in range(1, n + 1):
        stem, o = items[i]
        k = "abc".index(letters[i - 1].lower())
        out.append({"number": i, "hint": stem, "options": [f"{'abc'[j]}) {x}" for j, x in enumerate(o)], "answerKey": [f"{'abc'[k]}) {o[k]}"]})
    task = {"type": "multipleChoice", "instruction": instruction_of(t["head"]) + _instr_tail(t), "pointsPerItem": pts, "items": out}
    if example:
        task["example"] = example
    if based:
        task["basedOn"] = based
    return task


def _instr_tail(t):
    lines = body_text(t["body"])
    tail = []
    for l in lines:
        if l.startswith(EX) or re.match(r"^\d+\.\s", l):
            break
        tail.append(l)
    return (" " + " ".join(tail)) if tail else ""


def _mc_reading(t, kb, lines, n, pts):
    stem_rx = re.compile(r"^(?:(\d)\.\s+)?(Z (?:tego )?tekstu .*)$")
    entries = []  # (num|None, passage_lines, stem_text)
    buf = []
    i = 0
    while i < len(lines):
        l = lines[i]
        m = stem_rx.match(l)
        if m:
            stem = m.group(2)
            while "c)" not in stem and i + 1 < len(lines):
                i += 1
                stem += " " + lines[i]
            while i + 1 < len(lines) and re.match(r"^[a-ząćęłńóśźż]", lines[i + 1]):
                i += 1
                stem += " " + lines[i]
            entries.append((int(m.group(1)) if m.group(1) else None, buf, stem))
            buf = []
        else:
            buf.append(l)
        i += 1
    items, example = {}, None
    for num, passage_lines, stem in entries:
        sm = re.match(r"^(Z (?:tego )?tekstu .*?)\s+a\)\s+(.+?)\s+b\)\s+(.+?)\s+c\)\s+(.+)$", stem)
        if not sm:
            raise ParseError("question not parsed: " + stem[:60])
        o = _opts(sm.group(2), sm.group(3), sm.group(4))
        pl = [x for x in passage_lines if not x.startswith("Na podstawie:") and not x.startswith("http") and not re.fullmatch(r"\d\.", x)]
        passage = " ".join(pl)
        passage = re.sub(r"^PRZYKŁAD\s+", "", passage)
        if num:
            passage = re.sub(rf"(?<=\s){num}\.(?=\s)", "", passage)
        passage = re.sub(r"\s+", " ", passage).strip()
        if num is None:
            example = passage + "\n" + sm.group(1).strip() + "\n" + "\n".join(f"{'abc'[k]}) {x}" for k, x in enumerate(o))
        else:
            items[num] = (passage, sm.group(1).strip(), o)
    if sorted(items) != list(range(1, n + 1)):
        raise ParseError(f"Czyt. I: items {sorted(items)} out of {n}")
    letters = K.letters_any(kb, "a-c", n)
    if len(letters) != n:
        raise ParseError(f"key: {len(letters)} letters out of {n}")
    out = []
    for i in range(1, n + 1):
        passage, stem, o = items[i]
        k = "abc".index(letters[i - 1].lower())
        out.append({"number": i, "prompt": passage, "hint": stem, "options": [f"{'abc'[j]}) {x}" for j, x in enumerate(o)], "answerKey": [f"{'abc'[k]}) {o[k]}"]})
    task = {"type": "multipleChoice", "instruction": instruction_of(t["head"]), "pointsPerItem": pts, "items": out}
    if example:
        task["example"] = example
    return task


# ---------------------------------------------------------------- TAK / NIE (layout)
def parse_true_false(t, kb, part, **_):
    n, pts = points_of(t)
    lines = [l for l in t["body_l"] if l.strip()]
    lines = [l for l in lines if not re.match(r"^\s*_+\s*/", l)]
    tak_idx = next((i for i, l in enumerate(lines) if "TAK" in l and "NIE" in l), None)
    if tak_idx is None:
        raise ParseError("no TAK/NIE table")
    before = [l.strip() for l in lines[:tak_idx]]
    table = lines[tak_idx:]
    ex_line = None
    stmts = {}
    cur = None
    for l in table:
        s = re.sub(r"\s{2,}X?\s*$", "", l.rstrip()).strip()
        s = re.sub(r"\s+X\s*$", "", s)
        if "TAK" in l and "NIE" in l:
            s = re.sub(r"\s*TAK\s+NIE\s*$", "", s).strip()
            if s.startswith(EX):
                s = s[len(EX):].strip()
                if s:
                    ex_line = s
            continue
        m = re.match(r"^(\d{1,2})\.\s*(.*)$", s)
        if m:
            cur = int(m.group(1))
            stmts[cur] = [m.group(2)] if m.group(2) else []
        elif s.startswith(EX):
            rest = s[len(EX):].strip()
            if rest:
                ex_line = rest
            cur = 0
        elif cur == 0 and ex_line is not None:
            ex_line += " " + s
        elif cur is None and s and ex_line is None:
            pass
        elif cur:
            stmts[cur].append(s)
    if sorted(stmts) != list(range(1, n + 1)):
        raise ParseError(f"TF: items {sorted(stmts)} out of {n}")
    key = K.tf_key(kb["l"])
    if sorted(key) != list(range(1, n + 1)):
        raise ParseError(f"TF key: {sorted(key)}")
    items = [{"number": i, "prompt": re.sub(r"\s+", " ", " ".join(x for x in stmts[i] if x)).strip(), "options": ["TAK", "NIE"], "answerKey": [key[i]]} for i in range(1, n + 1)]
    task = {"type": "trueFalse", "instruction": instruction_of(t["head"]) + (_instr_tail_l(before) if part == "sluchanie" else ""), "pointsPerItem": pts, "items": items}
    if ex_line:
        task["example"] = re.sub(r"\s+", " ", ex_line)
    if part == "czytanie":
        art = [x for x in before if x]
        if art:
            title_text = art[0] if len(art) > 1 else ""
            body = " ".join(art[1:]) if len(art) > 1 else " ".join(art)
            body = re.sub(r"\s+(Na podstawie:.*)$", "", body)
            mb = re.search(r"(Na podstawie:.*)$", " ".join(art))
            task["text"] = (title_text + "\n" if title_text else "") + body
            if mb:
                task["basedOn"] = mb.group(1)
    else:
        mb = next((x for x in [l.strip() for l in lines] if x.startswith("Na podstawie:")), None)
        if mb:
            task["basedOn"] = mb
    return task


def _instr_tail_l(before):
    tail = [x for x in before if x and not x.startswith("Na podstawie")]
    return (" " + " ".join(tail)) if tail else ""


# ---------------------------------------------------------------- Czytanie III: fragments
def parse_fragments(t, kb, **_):
    n, pts = points_of(t)
    lines = body_text(t["body"])
    frag_start = next((i for i, l in enumerate(lines) if re.match(r"^A\s+\S", l)), None)
    if frag_start is None:
        raise ParseError("no list of fragments A-H")
    text_lines, frag_lines = lines[:frag_start], lines[frag_start:]
    flat = " " + " ".join(frag_lines)
    pos = 0
    cut = []
    for letter in "ABCDEFGHIJK":
        m = re.compile(rf"(?:(?<=\s))({letter})\s+(?=\S)").search(flat, pos)
        if not m:
            break
        cut.append((letter, m.start(1), m.end()))
        pos = m.end()
    frags = []
    for i, (letter, s0, e0) in enumerate(cut):
        end = cut[i + 1][1] if i + 1 < len(cut) else len(flat)
        txt = re.sub(r"\s+", " ", flat[e0:end]).strip()
        txt = re.sub(rf"^{EX}\s*", "", txt)
        txt = re.sub(rf"\s*{EX}$", "", txt).strip()
        frags.append([letter, txt])
    letters = [f[0] for f in frags]
    if letters != list("ABCDEFGHIJK")[: len(letters)] or len(letters) < n + 1:
        raise ParseError(f"fragments {letters}")
    text = join(text_lines)
    mb = re.search(r"\s(Na podstawie:.*)$", text)
    based = None
    if mb:
        based = mb.group(1).strip()
        text = text[: mb.start()]
    d = {f[0]: f[1] for f in frags}
    ex_letter = None
    gaps = []

    def repl(m):
        nonlocal ex_letter
        letter, tag = m.group(1), m.group(2)
        if tag == EX:
            ex_letter = letter
            return "{0}"
        gaps.append(int(tag))
        return "{%s}" % tag

    text = re.sub(r"\[\s*_*\s*([A-K])?\s*_*\s*\]\s*(PRZYKŁAD|\d{1,2})(?!\d)", repl, text)
    if gaps != list(range(1, n + 1)):
        raise ParseError(f"gaps {gaps}")
    if ex_letter and not d.get(ex_letter):
        # example: the fragment text is printed next to "PRZYKŁAD"
        raise ParseError("example text not found")
    if ex_letter:
        text = text.replace("{0}", "[[%s]]" % d[ex_letter])
    letters_k = [c.upper() for c in K.letters_any(kb, "A-Ka-k", n)]
    if len(letters_k) != n:
        raise ParseError(f"fragment key: {len(letters_k)} out of {n}")
    wb = [f"{k}. {v}" for k, v in d.items() if v]
    if not all(f"{c}. {d.get(c)}" in wb for c in letters_k):
        raise ParseError("key letter without text")
    items = [{"number": i, "answerKey": [f"{letters_k[i-1]}. {d[letters_k[i-1]]}"]} for i in range(1, n + 1)]
    task = {"type": "matching", "instruction": instruction_of(t["head"]), "pointsPerItem": pts, "wordBank": wb, "text": text, "items": items}
    if based:
        task["basedOn"] = based
    return task


# ---------------------------------------------------------------- Czytanie IV: headings <-> texts (pdftotext raw order)
def parse_two_columns(t, kb, part, **_):
    n, pts = points_of(t)
    lines = [l.strip() for l in t["body_r"] if l.strip()]
    lines = [PTS_PREFIX.sub("", l).strip() for l in lines]
    lines = [l for l in lines if l and not re.match(r"^_+\s*/", l)]
    left, entries = {}, {}
    mode, cur_left, cur_letter = "head", None, None
    heading = []
    example_title = []
    for l in lines:
        if l.startswith("Na podstawie:") or re.match(r"^PRZYKŁAD\s+1\.", l) or re.match(r"^(\d\.\s+){3,}", l):
            break
        if re.fullmatch(r"I\s+II", l) or l in ("I", "II"):
            mode = "pre"
            continue
        m = re.match(r"^(\d)\.\s*(.*)$", l) or (re.match(r"^(\d)\s+(\S.*)$", l) if l[:1].isdigit() and int(l[0]) == (cur_left or 0) + 1 else None)
        if m and mode != "head":
            cur_left = int(m.group(1))
            rest = m.group(2)
            mode = "left"
            lm0 = re.match(r"^(.*?)\s+([A-K])$", rest)
            lm1 = re.match(r"^(\S+(?:\s\S+)?)\s+([A-K])\s+(\S.*)$", rest) or re.match(r"^(.*?[?.!a-ząćęłńóśźż])\s+([A-K])\s+([A-ZŁŚŻŹĆ„].*)$", rest)
            if lm1:
                left[cur_left] = [lm1.group(1)]
                cur_letter = lm1.group(2)
                entries[cur_letter] = [lm1.group(3)]
                mode = "entry"
            elif lm0:
                left[cur_left] = [lm0.group(1)]
                cur_letter = lm0.group(2)
                entries[cur_letter] = []
                mode = "entry"
            else:
                left[cur_left] = [rest] if rest else []
            continue
        if l.startswith(EX) and mode in ("pre", "head"):
            cur_left = 0
            left[0] = [l[len(EX):].strip()] if l[len(EX):].strip() else []
            mode = "left"
            continue
        if mode == "head":
            heading.append(l)
            continue
        lm_start = re.match(r"^([A-K])\s+([A-ZŁŚŻŹĆ„].*)$", l) if mode == "left" else None
        if lm_start:
            cur_letter = lm_start.group(1)
            entries[cur_letter] = [lm_start.group(2)]
            mode = "entry"
            continue
        lm = re.match(r"^(.*?)\s*\b([A-K])$", l) if mode == "left" else None
        if mode == "left" and (re.fullmatch(r"[A-K]", l) or (lm and lm.group(1))):
            if re.fullmatch(r"[A-K]", l):
                letter, rest = l, ""
            else:
                letter, rest = lm.group(2), lm.group(1)
            if rest:
                left[cur_left].append(rest)
            cur_letter = letter
            entries[cur_letter] = []
            mode = "entry"
            continue
        if mode == "left" and cur_left is not None:
            left[cur_left].append(l)
        elif mode == "entry":
            entries[cur_letter].append(l)
    if sorted(k for k in left if k) != list(range(1, n + 1)):
        raise ParseError(f"left: items {sorted(left)} out of {n}")
    if len(entries) < n + 1:
        raise ParseError(f"right: only {sorted(entries)}")
    d = {k: re.sub(r"\s+", " ", " ".join(v)).strip() for k, v in entries.items()}
    letters_k = [c.upper() for c in K.letters_any(kb, "A-Ka-k", n)]
    if len(letters_k) != n:
        raise ParseError(f"key: {len(letters_k)} out of {n}")
    if any(c not in d for c in letters_k):
        raise ParseError("key letter without text")
    wb = [f"{k}. {v}" for k, v in sorted(d.items())]
    items = [{"number": i, "prompt": re.sub(r"\s+", " ", " ".join(x for x in left[i] if x)).strip(), "answerKey": [f"{letters_k[i-1]}. {d[letters_k[i-1]]}"]} for i in range(1, n + 1)]
    task = {"type": "matching", "instruction": instruction_of(t["head"]), "pointsPerItem": pts, "wordBank": wb, "items": items}
    if heading:
        task["text"] = " ".join(heading)
    if 0 in left and any(left[0]):
        task["example"] = " ".join(left[0])
    return task


# ---------------------------------------------------------------- Słuchanie V: "osoba N" <-> statements A-E (raw order)
BOX = re.compile(r"\[_*\d*_*\]")


def parse_persons(t, kb, **_):
    n, pts = points_of(t)
    lines = [PTS_PREFIX.sub("", l.strip()).strip() for l in t["body_r"] if l.strip()]
    persons = sorted({int(x) for l in lines for x in re.findall(r"-\s*osoba\s+(\d)", l)})
    if not persons:
        # the osoba N list is sometimes on a single line
        persons = sorted({int(x) for l in lines for x in re.findall(r"osoba\s+(\d)", l)})
    ents = []
    if not persons:
        for l in lines:
            if l.startswith(EX):
                break
            em = re.match(r"^(\d)\.\s+(\S.*)$", l)
            if em:
                ents.append((int(em.group(1)), em.group(2).strip()))
        persons = [e[0] for e in ents]
    if not persons:
        raise ParseError("no osoba N list")
    start = next((i for i, l in enumerate(lines) if l.startswith(EX)), None)
    if start is None:
        raise ParseError("no PRZYKŁAD")
    stmts, order, cur = {}, [], None
    example, ex_person = [], None
    for l in lines[start + 1:]:
        if l.startswith("Na podstawie") or l.startswith("http") or re.match(r"^(styczeń|luty|marzec|kwiecień|maj|czerwiec|listopad|grudzień) \d{4}$", l) or l.startswith("LICZBA ZADAŃ"):
            continue
        if l.startswith("O tym mówi"):
            continue
        em = re.search(r"\[_*(\d)_*\]", l)
        m = re.match(r"^([A-K])(?:\s+(.*))?$", l)
        if m and not (cur is None and em and not m.group(2)):
            cur = m.group(1)
            stmts[cur] = [BOX.sub("", m.group(2) or "").strip()]
            order.append(cur)
        elif cur is None:
            if em and ex_person is None:
                ex_person = em.group(1)
            txt = BOX.sub("", l).strip()
            if txt:
                example.append(txt)
        else:
            stmts[cur].append(BOX.sub("", l).strip())
    d = {k: re.sub(r"\s+", " ", " ".join(x for x in v if x)).strip() for k, v in stmts.items()}
    if len(d) < 3 or any(not v for v in d.values()):
        raise ParseError(f"statements {sorted(d)}")
    kl = [l for l in kb["l"] if l.strip()]
    hdr = next((i for i, l in enumerate(kl) if len(l.replace("*", "").split()) >= 3 and all(re.fullmatch(r"[A-K]", x) for x in l.replace("*", "").split())), None)
    if hdr is None or hdr + 1 >= len(kl):
        raise ParseError("no letter key table")
    letters = kl[hdr].replace("*", " ").split()
    groups = re.findall(r"\d(?:\s*,\s*\d)*", kl[hdr + 1])
    if len(groups) != len(letters):
        raise ParseError(f"key: {len(groups)} values for {len(letters)} letters")
    items, num = [], 0
    names = dict(ents)
    label = lambda x: names.get(x) and f"{x}. {names[x]}" or f"osoba {x}"
    for letter, g in zip(letters, groups):
        who = [int(x) for x in re.findall(r"\d", g)]
        if letter not in d:
            raise ParseError(f"no text for statement {letter}")
        for j, p in enumerate(who, 1):
            num += 1
            suffix = f" ({j}/{len(who)})" if len(who) > 1 else ""
            items.append({"number": num, "prompt": f"{letter}. {d[letter]}{suffix}", "hint": "Proszę zdecydować, kto:", "options": [label(x) for x in persons], "answerKey": [label(x) for x in who]})
    if num != n:
        raise ParseError(f"items {num} out of {n}")
    task = {"type": "multipleChoice", "instruction": instruction_of(t["head"]) + " " + " ".join(x for x in lines[: start] if not x.startswith(("-", "O tym", "Na podstawie")) and not re.match(r"^_+\s*/", x))[:400], "pointsPerItem": pts, "items": items}
    if example:
        task["example"] = " ".join(example) + (f" – osoba {ex_person}" if ex_person else "")
    return task


# ---------------------------------------------------------------- Słuchanie V: names <-> descriptions (layout, letter column)
def parse_matching_layout(t, kb, part, **_):
    n, pts = points_of(t)
    lines = [l.rstrip() for l in t["body_l"] if l.strip() and not re.match(r"^\s*_+\s*/", l)]
    cols = {}
    for l in lines:
        for m in re.finditer(r"(?<=\s)([A-K])\s+\S", l):
            cols[m.start(1)] = cols.get(m.start(1), 0) + 1
    if not cols:
        raise ParseError("no letter column")
    x = max(cols, key=cols.get)
    left, right = {}, {}
    cur_l = cur_r = None
    for l in lines:
        if re.match(r"^\s*PRZYKŁAD\s+1\.", l) or re.match(r"^\s*(\d\.\s+){3,}", l):
            break
        near = [m for m in re.finditer(r"(?<=\s)([A-K])\s+\S", l) if abs(m.start(1) - x) <= 8]
        if near:
            cut = near[0].start(1)
            lt, rt = l[:cut], l[cut:]
        else:
            padded = l.ljust(x + 2)
            lt, rt = padded[:x], padded[x:]
        lt_s, rt_s = lt.strip(), rt.strip()
        rm = re.match(r"^([A-K])\s+(.*)$", rt_s) if (rt_s and near) else None
        if rm:
            cur_r = rm.group(1)
            right[cur_r] = [rm.group(2)]
        elif rt_s and cur_r:
            right[cur_r].append(rt_s)
        if lt_s:
            lm = re.match(r"^(\d)\.\s*(.*)$", lt_s)
            if lm:
                cur_l = int(lm.group(1))
                left[cur_l] = [lm.group(2)] if lm.group(2) else []
            elif lt_s.startswith(EX):
                cur_l = 0
                left[0] = [lt_s[len(EX):].strip()] if lt_s[len(EX):].strip() else []
            elif cur_l is not None:
                left[cur_l].append(lt_s)
    if sorted(k for k in left if k) != list(range(1, n + 1)):
        raise ParseError(f"left: items {sorted(left)} out of {n}")
    if len(right) < n + 1:
        raise ParseError(f"right: only {sorted(right)}")
    d = {k: re.split(r"\s*\S{0,3}odstawie:|\s+https?://", re.sub(r"\s+", " ", " ".join(v)).strip())[0].strip() for k, v in right.items()}
    letters_k = [c.upper() for c in K.letters_any(kb, "A-Ka-k", n)]
    if len(letters_k) != n or any(c not in d for c in letters_k):
        raise ParseError("key does not match")
    wb = [f"{k}. {v}" for k, v in sorted(d.items())]
    items = [{"number": i, "prompt": re.sub(r"\s+", " ", " ".join(x for x in left[i] if x)).strip(), "answerKey": [f"{letters_k[i-1]}. {d[letters_k[i-1]]}"]} for i in range(1, n + 1)]
    task = {"type": "matching", "instruction": instruction_of(t["head"]), "pointsPerItem": pts, "wordBank": wb, "items": items}
    if 0 in left and any(left[0]):
        task["example"] = " ".join(left[0])
    return task


import tasks_b2 as B2  # noqa: E402

def _try(first, second, t, kb, **kw):
    try:
        return first(t, kb, **kw)
    except ParseError:
        return second(t, kb, **kw)


def _multi(parser):
    """Tries the task body from text, raw and layout: the pdftotext reading order differs between exams."""
    def run(t, kb, **kw):
        last = None
        for key in ("body", "body_r", "body_l"):
            if not t.get(key):
                continue
            try:
                return parser({**t, "body": t[key]}, kb, **kw)
            except ParseError as e:
                last = last or e
        raise last or ParseError("empty task")
    return run


PARSERS = {
    "mcGroups": B2.parse_mc_groups,
    "shortAnswer": B2.parse_short_answers,
    "chooseInText": _multi(parse_choose_in_text),
    "wordBank": _multi(parse_word_bank),
    "fillForm": _multi(parse_fill_form),
    "askQuestion": parse_ask_question,
    "transform": parse_transform,
    "multipleChoice": parse_multiple_choice,
    "trueFalse": parse_true_false,
    "fragments": parse_fragments,
    "titles": parse_two_columns,
    "matching": lambda t, kb, **kw: _try(parse_matching_layout, parse_two_columns, t, kb, **kw),
    "persons": parse_persons,
}
