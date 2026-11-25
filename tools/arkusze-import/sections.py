"""Splits the arkusz text (pdftotext, without -layout) into parts and tasks, and classifies the task type by its polecenie."""
import re

ROMAN = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII"]
NOISE = re.compile(r"^(POZIOM B[12].*|TEST PRZYKŁADOWY.*|©.*|Języka Polskiego jako Obcego\s*|B1\s*|=== STRONA.*|\d{1,2}\s*)$")
PART_MARKERS = [
    ("sluchanie", re.compile(r"ROZUMIENIE ZE SŁUCHU[^\n]*\n?\s*\d+ zadań")),
    ("czytanie", re.compile(r"ROZUMIENIE TEKSTÓW PISANYCH[^\n]*\n?\s*\d+ zadań")),
    ("gramatyka", re.compile(r"POPRAWNOŚĆ GRAMATYCZNA[^\n]*\n?\s*\d+ zadań")),
    ("pisanie", re.compile(r"PISANIE[^\n]*\n?\s*(Jeden zestaw|\d+ zadań)")),
]
HEADING = re.compile(r"^(VIII|VII|VI|IV|V|III|II|I)\.\s+((?:Proszę|Przeczytaj|Popatrz|Posłuchaj|Wysłuchaj|Uzupełnij|Dopasuj|Połącz|Wybierz|Zapytaj|Przekształć|Podkreśl|Odpowiedz|Zaznacz|Spójrz|Obejrzyj).*)$")
# the youth version (dim) writes polecenia in the informal "you" form: normalise them to the "Proszę …ć" forms that classify works with
INFORMAL = [("Przeczytaj", "przeczytać"), ("przeczytaj", "przeczytać"), ("dopasuj", "dopasować"), ("wybierz", "wybrać"), ("podkreśl", "podkreślić"),
            ("uzupełnij", "uzupełnić"), ("zaznacz", "zaznaczyć"), ("połącz", "połączyć"), ("przekształć", "przekształcić"), ("zapytaj", "zapytać"),
            ("posłuchaj", "słuchać"), ("odpowiedz", "odpowiedzieć"), ("Popatrz", "popatrzeć")]


def clean_lines(text):
    out = []
    for raw in text.replace("\f", "\n").split("\n"):
        line = raw.rstrip()
        if NOISE.match(line.strip()):
            continue
        out.append(line)
    return out


def split_parts(text):
    positions = []
    for kind, rx in PART_MARKERS:
        m = rx.search(text)
        if m:
            positions.append((m.start(), kind))
    positions.sort()
    parts = {}
    for i, (start, kind) in enumerate(positions):
        end = positions[i + 1][0] if i + 1 < len(positions) else len(text)
        parts[kind] = text[start:end]
    return parts


def split_tasks(part_text):
    """-> list of (roman, instruction_first_line, body_lines). Multi-line polecenia are joined up to the line with the points."""
    lines = clean_lines(part_text)
    tasks = []
    cur = None
    for line in lines:
        m = HEADING.match(line.strip())
        if m:
            cur = {"roman": m.group(1), "head": m.group(2), "body": []}
            tasks.append(cur)
        elif cur is not None:
            cur["body"].append(line)
    for t in tasks:
        head = t["head"]
        while t["body"] and not re.match(r"^\s*_", t["body"][0]) and not re.search(r"\(\d+\s*x\s*[\d,]+", t["body"][0]) and not t["body"][0].strip().startswith("PRZYKŁAD") and len(head) < 260:
            head += " " + t["body"].pop(0).strip()
        t["head"] = head
        t["intro"] = " ".join(x.strip() for x in t["body"][:9])
    return tasks


def classify(part, head, intro=""):
    h = head + " " + intro
    for a, b in INFORMAL:
        h = re.sub(r"(?<![\wąćęłńóśźż])%s(?![\wąćęłńóśźż])" % a, b, h)
    if part == "gramatyka":
        if "z ramki" in h:
            return "wordBank"
        if "uzupełnić" in h and "formami" in h and "nawiasach" in h:
            return "fillForm"
        if "zapytać o podkreśloną" in h:
            return "askQuestion"
        if "przekształcić" in h:
            return "transform"
        if "uzupełnić" in h and ("nawiasach" in h or "brakujące wyrazy" in h):
            return "fillForm"
        if "podkreślić" in h or "wybrać" in h:
            return "chooseInText"
    if part == "czytanie":
        if "z ramki" in h:
            return "wordBank"
        if "zaznaczyć właściwą odpowiedź" in h or "zaznaczyć właściwe odpowiedzi" in h:
            return "multipleChoice"
        if "dopasować do nich tytuły" in h:
            return "titles"
        if "brakującymi zdaniami" in h:
            return "fragments"
        if "TAK" in h and "NIE" in h:
            return "trueFalse"
        if "dopasować usunięte" in h:
            return "fragments"
        if "połączyć tytuły" in h:
            return "titles"
        if "podkreślić" in h:
            return "chooseInText"
    if part == "sluchanie":
        if "odpowiedzieć na pytania" in h:
            return "shortAnswer"
        if "kolejnych tekstów" in h:
            return "mcGroups"
        if "dopasować osoby" in h or "zdecydować, kto" in h:
            return "persons"
        if "TAK" in h and "NIE" in h or re.search(r"zaznaczyć:\s*TAK", h):
            return "trueFalse"
        if "połączyć" in h or "dopasować" in h:
            return "matching"
        if "zaznaczyć właściwe" in h or "uważnie słuchać" in h or "wybrać właściwe odpowiedzi" in h:
            return "multipleChoice"
    return None


def signature(text):
    parts = split_parts(text)
    sig = {}
    for kind in ("sluchanie", "czytanie", "gramatyka"):
        if kind not in parts:
            sig[kind] = None
            continue
        sig[kind] = [(t["roman"], classify(kind, t["head"], t["intro"])) for t in split_tasks(parts[kind])]
    return sig
