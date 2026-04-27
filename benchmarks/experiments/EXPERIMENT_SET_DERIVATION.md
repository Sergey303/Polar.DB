# Experiment Set Derivation

## 1. Object of study

The object of study is:

> storage engine behavior under controlled data, workload, target, and fairness conditions.

Therefore an experiment is not just a benchmark run. It is a controlled scientific protocol.

## 2. Axes of the full measurement program

The complete measurement program needs these axes:

| Axis | What it explains |
|---|---|
| Engine target | current Polar.DB, SQLite, Polar.DB released versions |
| Scale | whether behavior changes with data volume |
| Lifecycle phase | load, build, reopen, lookup, append |
| Query shape | existing point lookup, missing lookup, scan/filter, future range/index queries |
| Durability state | fresh, reopened, appended, future crash-recovered |
| Statistical profile | warmup/measured runs, p50/p95/p99/jitter |
| Artifact economics | primary bytes, side bytes, index/state bytes, growth |
| Regression role | current-vs-SQLite, current-vs-old-Polar.DB, historical exact reference |

## 3. Why the executable set is smaller than the full matrix

A full factorial matrix would explode quickly:

```text
engines x scales x workload shapes x lifecycle modes x durability states x run profiles
```

So the canonical active set uses representative experiments:

1. Reference scale ladder.
2. Lifecycle append/reopen.
3. Broad current-vs-SQLite adapter coverage.
4. Polar.DB search diagnostics across versions.
5. Historical exact USequence regression.

## 4. Why Q3 is not a version matrix anymore

Q3 answers:

> Is current Polar.DB competitive with SQLite on a broad adapter scenario?

That is a current-vs-external-engine question.

If old NuGet versions are added to Q3, it starts answering a second question:

> Did current Polar.DB regress against old Polar.DB versions?

That second question is valid, but it belongs to Q4/Q5 or to a future dedicated version-regression suite.

## 5. Version comparisons kept in the catalog

Version comparisons remain only where they have a precise diagnostic role:

- Search diagnostics across Polar.DB versions.
- Historical USequence exact-reference regression across Polar.DB versions.

This keeps Q3 clean and makes interpretation easier.
