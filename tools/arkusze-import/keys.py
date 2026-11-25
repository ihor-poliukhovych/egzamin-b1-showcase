"""Parsing of a transkrypcja-klucz.pdf file: answer keys per task and transcripts."""
import re

FF = chr(12)
ROMAN = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII"]
NOISE_RX = re.compile(r"^\s*(POZIOM B1.*|=== STRONA.*|\d{1,2}|(styczeń|luty|marzec|kwiecień|maj|czerwiec|listopad|grudzień) \d{4})\s*$", re.I)


def key_ranges(text):
    """Pages with "Klucz do zadań" in order: Słuchanie, Czytanie, Gramatyka; pages without a heading belong to the previous part."""
    names = ["sluchanie", "czytanie", "gramatyka", "pisanie"]
    ranges = {}
    idx = -1
    for page in text.split(FF):
        if "Klucz do zadań" in page:
            idx += 1
        if 0 <= idx < 3:
            ranges[names[idx]] = ranges.get(names[idx], "") + "\n" + page
    return ranges


def task_blocks(part_text):
    """-> {roman: [lines]} by the "Zadanie X" markers."""
    blocks = {}
    cur = None
    for raw in part_text.split("\n"):
        m = re.match(r"^\s*Zadanie\s+(VIII|VII|VI|IV|V|III|II|I)\b(.*)$", raw)
        if m:
            cur = m.group(1)
            blocks[cur] = []
            rest = m.group(2)
            if rest.strip():
                blocks[cur].append(" " * (len(raw) - len(rest)) + rest.strip())
        elif cur is not None and not NOISE_RX.match(raw) and "Klucz do zadań" not in raw:
            blocks[cur].append(raw)
    return blocks


def letters_key(lines, allowed="a-k"):
    chars = []
    for line in lines:
        for tok in line.split():
            tok = tok.strip(",")
            if re.fullmatch(r"\d+\.?", tok):
                continue
            if re.fullmatch(rf"[{allowed}]+", tok, re.I):
                chars.extend(tok)
    return chars


def tf_key(lines):
    """Layout mode: TAK / NIE (or T / N) columns; the X is closer to one of them (the heading may sit next to the neighbouring key column)."""
    tak = nie = None
    out = {}
    for line in lines:
        if tak is None:
            mh = re.search(r"(?<!\S)(TAK|T)\s+(NIE|N)(?!\S)", line)
            if mh:
                tak, nie = mh.start(1), mh.start(2)
                continue
        m = re.match(r"^\s*(\d+)\.\s+(.*)$", line)
        if m and tak is not None:
            xs = [i for i, ch in enumerate(line) if ch == "X" and tak - 6 <= i <= nie + 14]
            if xs:
                x = xs[0]
                out[int(m.group(1))] = "TAK" if abs(x - tak) <= abs(x - nie) else "NIE"
    return out


def words_key(lines):
    """Non-layout mode: "N. answer" for word tasks; -> ({n: [variants]}, extra_word)."""
    text = " ".join(l.strip() for l in lines if l.strip())
    extra = None
    m = re.search(r"Wyraz zbędny:?\s*(\S+)", text)
    if m:
        extra = m.group(1)
        text = text[: m.start()]
    out = {}
    for mm in re.finditer(r"(?<!\d)(\d{1,2})\.\s+(.+?)(?=\s+\d{1,2}\.\s|$)", text):
        out[int(mm.group(1))] = [v.strip() for v in re.split(r"\s+/\s+", mm.group(2).strip()) if v.strip()]
    return out, extra


_SPEAKER = re.compile(r"^(\d+\.\s|[A-ZŁŚŻŹĆ][\w ]{0,15}:|Przykład:|Osoba \d)")


def transcripts(text):
    """-> {roman: str}"""
    pages = text.split(FF)
    first_key = next((i for i, p in enumerate(pages) if "Klucz do zadań" in p), len(pages))
    head = FF.join(pages[:first_key])
    parts = re.split(r"(?:^|\s)(?i:transkrypcja)\s+(VIII|VII|VI|IV|V|III|II|I)\b", "\n" + head)
    out = {}
    for i in range(1, len(parts) - 1, 2):
        lines = [l.strip() for l in parts[i + 1].split("\n") if l.strip() and not NOISE_RX.match(l) and not re.match(r"^(ROZUMIENIE ZE SŁUCHU|TRANSKRYPCJE|Transkrypcje|ZE SŁUCHU)", l.strip())]
        lines = [l for l in lines if not l.startswith("Na podstawie:") and not l.startswith("http")]
        merged = []
        for l in lines:
            if merged and not _SPEAKER.match(l):
                merged[-1] += " " + l
            else:
                merged.append(l)
        out[parts[i]] = "\n".join(merged)
    return out


_ITEM = re.compile(r"(?:(?<=\s)|^)(\d{1,2})\.\s+(\S.*?)(?=\s{2,}\d{1,2}\.\s|\s{3,}(?:Klucz|[A-ZŁŚŻŹĆ]{4,})|\s*$)")


def layout_words(text):
    """Layout mode: keys "N. word" in two columns. The right column (items 6-10) comes as separate blocks in the order of the tasks that have 10 items.
    -> ({roman: {n: [variants]}}, {roman: extra word}, [right-column blocks])"""
    res, extra, blocks = {}, {}, []
    cur = None
    last_left = last_right = None
    prev_right_n = 0
    for raw in text.split("\n"):
        line = raw.rstrip()
        if not line.strip() or NOISE_RX.match(line):
            continue
        m = re.match(r"^\s*Zadanie\s+(VIII|VII|VI|IV|V|III|II|I)\b(.*)$", line)
        body = line
        if m:
            cur = m.group(1)
            res.setdefault(cur, {})
            body = " " * (len(line) - len(m.group(2))) + m.group(2)
            last_left = None
        if cur is None:
            continue
        mz = re.search(r"Wyraz zbędny:?\s*(\S+)", body)
        if mz:
            extra[cur] = mz.group(1)
            continue
        found = list(_ITEM.finditer(body))
        if not found:
            continue
        for f in found:
            n, ans = int(f.group(1)), f.group(2).strip()
            ans = re.sub(r"\s+(styczeń|luty|marzec|kwiecień|maj|czerwiec|listopad|grudzień)\s+\d{4}.*$", "", ans)
            ans = re.sub(r"\s+POZIOM B1.*$", "", ans)
            variants = [v.strip() for v in re.split(r"\s+/\s+", ans) if v.strip()] or [ans]
            if f.start(1) >= 15 and n >= 6:
                if not blocks or n <= prev_right_n:
                    blocks.append({})
                prev_right_n = n
                blocks[-1][n] = variants
                last_right = (blocks[-1], n)
            else:
                res[cur][n] = variants
                last_left = (res[cur], n)
    return res, extra, blocks


def letters_any(kb, allowed, n):
    """Letter key: from the layout block or from the plain one where the number of letters matches n."""
    for src in ("l", "nl"):
        got = letters_key(kb[src], allowed)
        if len(got) == n:
            return got
    return letters_key(kb["nl"], allowed)
