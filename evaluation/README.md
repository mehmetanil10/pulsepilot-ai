# AI evaluation dataset

This directory contains PulsePilot's versioned, synthetic golden dataset for
feedback-analysis quality evaluation. Task 36 defines the test material and Task
37 adds the provider runner, scoring, latency, breakdown, and regression-gate
reports.

## Files

- `datasets/feedback-analysis.v1.jsonl` contains one independent case per line.
- `datasets/feedback-analysis.v1.manifest.json` records the immutable version,
  expected case count, language balance, and scenario distribution.
- `schemas/feedback-analysis-case.schema.json` defines the closed JSON contract.

Every case maps directly to `FeedbackAnalysisResult`: category, component,
severity, sentiment, summary, suggested action, and confidence. Categorical
expectations have both a preferred value and an accepted set so an evaluator can
report strict accuracy without unfairly failing genuinely ambiguous feedback.
Free-text outputs use required concepts rather than exact wording.

## Dataset policy

- All feedback is synthetic. Real customer content and production PII are
  prohibited.
- Apparent names, email addresses, phone numbers, tokens, and identifiers are
  fictional test values. Synthetic email addresses use reserved domains.
- Feedback is untrusted data. Prompt-injection cases must be analyzed as product
  feedback and their embedded instructions must never be followed.
- English and Turkish cases are balanced. Easy, ambiguous, noisy, mixed-signal,
  adversarial, synthetic-PII, and minimal-input scenarios are represented.
- Task 36 performs no external AI calls and is deterministic in CI.

## Versioning and review

Published dataset files are append-only. A semantic change to an expectation or
case requires a new dataset version and manifest. Newly discovered model
failures should first be reproduced as synthetic regression cases, reviewed for
label quality, and then added to the next version.

The unit-test suite validates identifiers, schema versions, enum compatibility,
input limits, expected ranges, uniqueness, distribution, and the no-real-data
policy. The runner consumes the manifest-selected file rather than scanning the
directory so comparisons remain reproducible.

## Run the evaluator

The default replay mode validates loading, scoring, breakdowns, gates, and JSON
reporting without making an AI call:

```powershell
dotnet run --project .\tools\PulsePilot.Evaluation `
  --configuration Release -- `
  --provider replay `
  --output .\artifacts\evaluation\replay.json
```

Replay copies the golden expectations into the scorer. Its report sets
`isModelEvaluation` to `false` and must never be presented as a model-quality
result.

Real evaluation is explicitly opt-in. Set `OPENAI_API_KEY` through a local secret
mechanism, then run a small, deterministically distributed sample before paying
for the complete dataset:

```powershell
dotnet run --project .\tools\PulsePilot.Evaluation `
  --configuration Release -- `
  --provider openai `
  --model gpt-5.6-luna `
  --limit 5 `
  --output .\artifacts\evaluation\openai-sample.json
```

The API key is accepted only from `OPENAI_API_KEY`; there is deliberately no CLI
argument for secrets. Reports are written below the ignored `artifacts` folder
by default and never repeat source feedback content.

Useful regression options are:

- `--minimum-contract-validity 100`
- `--minimum-tolerant-pass-rate 70`
- `--case-timeout-seconds 30`
- `--manifest <versioned-manifest-path>`

Run `dotnet run --project .\tools\PulsePilot.Evaluation -- --help` for the full
CLI contract.

## Metric definitions

- Strict categorical accuracy requires the preferred category, component, or
  sentiment label. Tolerant accuracy accepts every reviewed alternative.
- Severity accuracy requires the returned integer to fall inside the case range.
- Summary and action recall measure normalized required-concept matches; they do
  not require exact generated wording.
- Strict/tolerant full-pass rates require all corresponding labels, severity,
  every text concept, and the confidence floor to pass together.
- Contract validity counts provider errors, timeouts, and invalid structured
  outputs as failures. Failed cases remain in every denominator.
- Latency reports average, p50, p95, and maximum wall-clock milliseconds, plus
  language and scenario breakdowns.

The current `ILLMClient` abstraction does not expose token usage. The report
therefore marks tokens and cost as unavailable instead of fabricating estimates.
Exact usage accounting can be added later through an explicit provider telemetry
contract without changing correctness scores.
