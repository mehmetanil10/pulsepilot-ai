# AI evaluation dataset

This directory contains PulsePilot's versioned, synthetic golden dataset for
feedback-analysis quality evaluation. Task 36 defines the test material only;
Task 37 will add the provider runner, scoring, latency, and cost reports.

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
policy. Task 37 should consume the manifest-selected file rather than scanning
the directory so comparisons remain reproducible.
