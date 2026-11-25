# content/arkusze: arkusz file format

This folder holds no exam content: the repository contains code only. Exam sets are prepared by the operator
and loaded into the database with

```
dotnet run --project src/Api -- seed-arkusze [--from <dir>]
```

`--from` defaults to this folder (`content/arkusze` in the repository root). One file = one arkusz (`{code}.json`).
Running the import again updates texts by `(code, part.kind, task.number, item.number)`, does not duplicate anything
and does not break attempts already taken.

## File

Top level: `code`, `title`, `source` (`official` | `own`), optional `level` (default `B1`) and `variant`
(default `dorosli`), and `parts[]`. A part is `{ kind, timeLimitMinutes, tasks[], audio?, maxPoints? }`,
`kind` is one of `sluchanie | czytanie | gramatyka | pisanie | mowienie`.

```jsonc
{
  "code": "own-01", "title": "Arkusz ćwiczeniowy 1", "source": "own",
  "parts": [
    {
      "kind": "gramatyka", "timeLimitMinutes": 45,
      "tasks": [
        {
          "number": 1,                       // I–VIII
          "type": "chooseInText",            // see the tables below
          "instruction": "Proszę podkreślić poprawną formę.",
          "pointsPerItem": 0.5,              // 0.5 or 1
          "text": "Rozmawiałam z {1} koleżanką o {2} książce.", // {n} is gap number n, [[słowo]] is a solved EXAMPLE
          "wordBank": ["a", "i", "…"],       // only for wordBank / matching (including the extra word)
          "example": "Zdanie __część__.\n– Pytanie?\n– Odpowiedź.", // only for askQuestion / transform
          "basedOn": "Na podstawie: …",      // source of the text, if any
          "image": "zadania/own-01-s5.jpg",  // optional picture, served by GET /media/zadania/own-01-s5.jpg from the content store
          "transcript": "…",                 // Słuchanie only: the recording as text
          "items": [
            { "number": 1, "options": ["tę", "tą", "ta"], "answerKey": ["tą"] },                    // chooseInText
            { "number": 1, "answerKey": ["ponieważ"] },                                             // wordBank
            { "number": 1, "prompt": "pracować", "answerKey": ["pracują"] },                        // fillForm: prompt = the word in brackets
            { "number": 1, "prompt": "Ania pojechała __z mamą__ nad morze.",                        // askQuestion: __…__ = the underlined part
              "hint": "– … Ania pojechała nad morze?\n– Z mamą.", "answerKey": ["Z kim", "Z kim Ania pojechała nad morze"] },
            { "number": 1, "prompt": "Ola dała bratu książkę.", "hint": "dostał",                   // transform: hint = the word in brackets
              "answerKey": ["Brat dostał książkę od Oli", "Brat dostał od Oli książkę"], "explanation": "2 transformacje (2 x 0,5)" },
            { "number": 1, "points": 1 }                                                            // optional per-item points override
          ]
        }
      ]
    }
  ]
}
```

## Gramatyka task types

| type | Frontend input | Checking |
|---|---|---|
| `chooseInText` | pick the form right in the text | exact match (case-insensitive) |
| `wordBank` | dropdown from the word box | exact match |
| `fillForm` | input field in the text, the bracketed word next to it | normalized; no match → "needs review" |
| `askQuestion` | input field under the sentence and the answer | normalized; no match → "needs review" |
| `transform` | input field under the sentence and the hint word | normalized; no match → "needs review" |

Normalization (`B1.Domain.Exam.AnswerNormalizer`): trim, lower case, collapse whitespace, strip a leading
dash and all punctuation (`?`, `.`, `,`, `!`, quotes). Diacritics are **not** stripped: `cene` ≠ `cenę`.
`answerKey` is a list of accepted forms (e.g. the short and the full question, or word-order variants).
Only an answer marked "needs review" can later be accepted by the student.

## Czytanie task types

Checking is an exact match of the selected string against `answerKey`.

| type | Item |
|---|---|
| `multipleChoice` | `prompt` = the passage, `hint` = the beginning of the question, `options` = `"a) …"`, key = the same string |
| `trueFalse` | `text` = the article, `prompt` = the statement, `options` = `["TAK","NIE"]` |
| `matching` with `{n}` in `text` | `wordBank` = `"A. passage"`, gaps in the text |
| `matching` without `{n}` | `prompt` = the heading, `wordBank` = `"A. text"` |
| `chooseInText` | as in Gramatyka |

## Słuchanie

Same task types as Czytanie. The part's `audio` is a key of a recording in the content store
(`GET /audio/{key}` serves `<Content:LocalPath>/<key>.mp3`); without a recording the task `transcript` is used.

## Pisanie and Mówienie

`pisanie`: a task is a zestaw (`type: writing`) with 2 items, a and b; `hint` = the text form, a key of
`Checklists.Writing`; `targetWords` = the required length.

`mowienie`: a task is a zestaw (`type: speaking`) with 3 items; `hint` is one of `opis | monolog | sytuacja`,
`options` = prompt questions. For `sytuacja` the conversation partner picks a scenario by keywords in the prompt
(`B1.Domain.Exam.Dialogs`). Photos for `opis` are served from `GET /media/zdjecia/<file>`.

## Media files

Images are not part of the repository. Put them in the content store root (`Content:LocalPath`) under
`media/zadania/` (task illustrations) or `media/zdjecia/` (Mówienie photos). File names must match
`^[A-Za-z0-9-]{1,60}\.(jpg|jpeg|png|webp)$`.
