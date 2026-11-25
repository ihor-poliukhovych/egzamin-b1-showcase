# pisanie-eval

Calibrates the Pisanie (writing) AI review against works that human examiners have graded.
For every fixture it builds the same prompt the API uses (`ClaudePisanieReviewer.BuildPrompt`), calls the model,
and compares the five criterion scores and the verdict with the examiners' scores. It prints a Markdown table and
exits with 0 only when every work is within ±0.5 on every criterion and the verdict matches.

**The dataset is not included.** The graded works and the examiners' scores are copyrighted and cannot be published
in this repository. Prepare your own fixtures in the format below.

## Usage

```
dotnet run --project tools/pisanie-eval -- --fixtures <dir> [--provider cli|api] [--model <model>] [--only <id>] [--from <file.json>] [--out <dir>]
```

- `--fixtures <dir>` (required): folder with one `*.json` file per graded work.
- `--provider cli` (default): runs the local `claude -p` command. `--provider api`: calls the Claude API; the key is read
  from the environment variable `EGZAMINB1_Review__ApiKey`.
- `--model`: CLI model alias (default `sonnet`) or API model id (default from `ReviewOptions.ApiModel`).
- `--only <id>`: run one fixture.
- `--from <file.json>`: redraw the table from a saved run without calling the model.
- `--out <dir>`: output folder, default `<fixtures>/out`. Each run writes the parsed results and the raw model answers.

Each run costs one model call per work.

## Fixture format

One JSON object per file (property names are case-insensitive):

| Field | Type | Meaning |
|---|---|---|
| `id` | string | Short unique id of the work, e.g. `"I-1"`. |
| `zestaw` | string | Which task set the work answers, free text. |
| `zadania` | array | The tasks of the work, usually two (`a` short form, `b` longer form). |
| `zadania[].zadanie` | string | Task letter: `"a"` or `"b"`. |
| `zadania[].polecenie` | string | The task prompt shown to the candidate. |
| `zadania[].forma` | string | Text form, a key of `Checklists.Writing` (e.g. `"ogloszenie"`, `"list"`, `"opinia"`). |
| `zadania[].slowa` | number | Required length in words. |
| `zadania[].tekst` | string | The candidate's text exactly as written. |
| `egzaminatorA` | object | Scores of the first examiner: `wykonanie`, `gramatyka`, `slownictwo`, `styl`, `ortografia`, each 0–4 in steps of 0.25. |
| `egzaminatorB` | object | Scores of the second examiner, same keys. |
| `werdykt` | string | The examiners' verdict: `"bardzo dobra"`, `"dość dobra"`, `"poprawna"` or `"nie spełnia wymagań B1"`. |
| `komentarzKomisji` | string | The examiners' comment, free text (not used for scoring). |

The expected score per criterion is the mean of `egzaminatorA` and `egzaminatorB`.

```json
{
  "id": "sample-1",
  "zestaw": "I",
  "zadania": [
    { "zadanie": "a", "polecenie": "Proszę napisać ogłoszenie …", "forma": "ogloszenie", "slowa": 40, "tekst": "…" },
    { "zadanie": "b", "polecenie": "Proszę napisać list …", "forma": "list", "slowa": 150, "tekst": "…" }
  ],
  "egzaminatorA": { "wykonanie": 3.5, "gramatyka": 3, "slownictwo": 3.25, "styl": 3.5, "ortografia": 3 },
  "egzaminatorB": { "wykonanie": 3.75, "gramatyka": 3, "slownictwo": 3, "styl": 3.5, "ortografia": 3.25 },
  "werdykt": "dość dobra",
  "komentarzKomisji": "…"
}
```
