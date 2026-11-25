# Evaluating the AI grader for written work (Pisanie)

This document describes how the AI review of the written part was measured against human examiners,
what changed between prompt iterations, and what the results do and do not show.

**Short version:** on four works graded by the exam board, the share of criterion scores within ±0.5 of the
examiners went from **4/20 to 15/20**, and the mean absolute error went from **0.79 to 0.45**. One review costs
about **$0.07** with Claude Sonnet. The calibration target (all 20 criteria within ±0.5 and the same verdict on
every work) was **not reached**. The sample is very small and there is no held-out set, so treat these numbers
as a development signal, not as a validated accuracy figure.

## What is being graded

The written part of the B1 exam has two tasks: a short form (a notice, a message) and a longer one (a letter,
an opinion). Examiners score the whole work on five criteria, each 0–4 in steps of 0.25:

| Criterion | Polish name | What it covers |
|---|---|---|
| Task completion | wykonanie zadania | content points, form, length |
| Grammar | poprawność gramatyczna | inflection, syntax |
| Vocabulary | słownictwo | range and accuracy |
| Style | styl | register, cohesion, the conventions of the form |
| Spelling | ortografia i interpunkcja | spelling and punctuation |

The app also derives a verdict from the sum of the five criteria (`PisanieVerdict` in `src/Domain/Exam/Review.cs`):
≥ 90 % *bardzo dobra*, ≥ 80 % *dość dobra*, ≥ 60 % *poprawna*, below that *nie spełnia wymagań B1*.
These cut-offs were chosen to reproduce the examiners' verdicts on the same four works, so they are not an
official scale.

## Dataset

- **4 candidate works** (8 texts, tasks a and b), each scored independently by two examiners, taken from a
  published collection of graded sample works.
- **Reference score** per criterion: the mean of the two examiners.
- The works, the examiners' scores and their comments are copyrighted, so they are **not included** in this
  repository. The harness in [`tools/pisanie-eval`](../tools/pisanie-eval/README.md) runs on any folder of
  fixtures in the documented format.

Target: every criterion within ±0.5 of the reference and the same verdict.

## Setup

- Grader: `ClaudePisanieReviewer` (`src/Infrastructure/Review/ClaudeReviewers.cs`). It builds one prompt per
  work, with both tasks graded together, as the examiners do. The model returns one JSON object: five scores,
  up to 12 annotated errors and counts. The code then parses it, clamps each score to 0–4, rounds it to 0.25 and
  computes the verdict.
- Model: Claude Sonnet, called through the `claude -p` command (`ClaudeCliRunner`) with tools disabled, no MCP
  servers, no session persistence, a short system prompt and an empty working folder. The overhead is about
  850 tokens per call.
- The production API path (`ClaudeApiRunner`) uses the same prompt and parser; its model is a setting
  (`Review:ApiModel`). **Only Sonnet was evaluated.**
- One run per iteration, one call per work.

```
dotnet run --project tools/pisanie-eval -- --fixtures <dir>                # one call per work
dotnet run --project tools/pisanie-eval -- --fixtures <dir> --from <run>  # redraw the table, no calls
```

## Iterations

All three runs used the same four works on the same day.

| Iteration | Change | Criteria within ±0.5 | MAE | Mean bias | Verdict matches | Works fully within target |
|---|---|---|---|---|---|---|
| 1 | Baseline: both tasks in one call, word count computed in code, form checklists, scoring bands worded like the examiners' rubric, six error types | 4/20 | 0.79 | −0.78 | 1/4 | 0/4 |
| 2 | Explicit bands A–F: the model first names a band with a reason, then gives the score; length judged on the whole work; over-length and optional form elements no longer penalised; criteria scored independently | 15/20 | 0.39 | −0.14 | 2/4 | 0/4 |
| 3 (shipped) | The model first counts words, sentences, unclear sentences and errors by type across the whole text; grammar and spelling bands use thresholds per 100 words; a missing comma costs at most 0.25 | 15/20 | 0.45 | −0.39 | 1/4 | 1/4 |

### What the runs showed

- **Iteration 1** was systematically stricter than the examiners, by about 0.8 points per criterion. It treated
  "errors are present" as a low band, where the examiners give a high band as long as the errors do not block
  understanding. It also penalised texts for being longer than required and for missing optional elements.
  It graded the weakest work correctly; the gap was mostly on good works.
- **Iteration 2** removed the overall bias, but grammar stopped telling the works apart: every work got 2.25–3.
  The weakest work was overrated by about 1.1 points and moved from a fail to *poprawna*. The cause was the
  error list being capped at 12 items: every work came back with exactly 12 errors, so the model could not
  see error density.
- **Iteration 3** made the model count before scoring. The counts separate the works clearly: grammar errors
  per 100 words ranged from about 4 to about 22 across the four works. The remaining misses come from the
  thresholds in the prompt, which are stricter than the examiners' scale for good works.

### Why iteration 3 is the one in the code

Iteration 3 is conservative. It overrates any criterion by at most +0.25 and never passes a work that the
examiners failed; iteration 2 did. For exam preparation a false "you will pass" is worse than a cautious score.
The cost is that good works are underrated by about 0.4–0.5 points on average.

### Explanation quality

Each annotated error carries a type, a correction, a short rule and, for Russian- and Ukrainian-speaking users,
a contrast with their language. In iteration 3 all 48 errors had the rule in the user's interface language,
41 of 48 had a contrast note, and every error type came from the examiners' legend.

## Cost and latency

| Iteration | Input tokens | Output tokens | Time per work | Cost per work (Sonnet API prices) |
|---|---|---|---|---|
| 1 | ~3,300–3,600 | ~3,000–4,000 | 23–35 s | ~$0.045–0.055 |
| 3 | ~4,200–4,500 | ~5,000 | 37–41 s | ~$0.07 |

In the app, cost is bounded per user: AI reviews are limited per user per UTC day (`Review:DailyLimit`,
default 50), dialogue turns separately (`Review:DailyDialogTurns`, default 200), and the usage row is written
before the model is called, so failed or cancelled calls also count. See [architecture.md](architecture.md#ai-cost-control).

## Limitations

- **n = 4.** Twenty criterion scores from four works is far too small to estimate accuracy.
- **No held-out set.** The bands in iterations 2 and 3, the per-100-word thresholds and the verdict cut-offs
  were all tuned on these same four works.
- **One run per iteration.** Run-to-run variance was not measured.
- **One model.** Only Sonnet was evaluated. Other models configured for production (`Review:ApiModel`) have not
  been measured.
- **Scale.** The sample collection uses an older scoring scale for the written part; the criteria are the
  same, the point totals differ.

## Next step (not implemented)

Move the mapping from the model's error counts to grammar and spelling scores out of the prompt and into code,
as a piecewise-linear function of errors per 100 words. A first try on the saved iteration-3 answers looked
much better. It is deliberately not reported as a result: the thresholds were fitted on the same four works,
so the score would be in-sample.

Before adopting it, the plan is to:

1. collect new graded works for a held-out check, for example works graded by a teacher in the app's
   teacher groups;
2. run each work 2–3 times to measure how much the counts vary;
3. compare the prompt-only and code-mapped scoring on the held-out set.
