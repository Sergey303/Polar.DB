# Polar.DB paper — continuation without RDC

**State at shutdown:** main article and Stage 5 edits already published at `2b841c3f7643afc54978100d9d556d4d1b9555c1`. Subsequent research notes and raw portable experimental artifacts are in [the GitHub evidence snapshot](../evidence/20261107/README.md). The Windows Docker host and RDC may be unavailable. Use GitHub connector for the next editorial tasks; **do not rely on or promise background processing**.

## Read in order

1. [Current article](POLAR_DB_ARTICLE_DRAFT.md), [technical appendices](POLAR_DB_ARTICLE_TECHNICAL_MATERIALS.md) and [Stage 4B reproducibility note](PRIMARY_OFFSET_RESIDENCY_20261107.md).
2. The archived `ram-study/stage4b-pinned-confirmation/PROTOCOL.md`, `LOCK.json`, `SCHEDULE.json`, `STAGE4B_RESULTS.json`, `FINAL_REVIEW.md`, raw JSON and scripts.
3. [Snapshot provenance, exclusions and verification](../evidence/20261107/README.md).

## Scientifically fixed

- Main 4 November series on 50k/5m rows: 19 scenarios, originally measured once; do not infer independent-sample CIs from correlated within-run observations.
- Stage 4: historically **INCONCLUSIVE**, 40/48 qualifying attempts and 4/8 paired primary blocks; old data remain immutable.
- Stage 4.1: engineering fix (pinned `mlock`), 12/12 qualifying; independent of confirmatory statistics.
- Stage 4B: **PRIMARY_REPLICATED** under its own prespecified gate, 48/48 qualifying, 8/8 pairs, `f2/f1` geometric mean **23.6216×**, 95% CI **20.8268–26.3142×**. It is an *intra-Polar.DB cache residency intervention*, not an SQLite comparison or general RAM curve.
- Stage 5: manuscript §3.8 + §4.7 table 10, discussion/limitations and reproducibility note; original tables 1–9 unchanged.

## Next work through GitHub, by Pareto

1. Clarify the single scientific thesis, novelty relative to prior cache-aware/database indexing work and suitable type of journal contribution. Verify all new citations independently.
2. The original 4 November main-series archive is now published at [20261104 evidence](../evidence/20261104/README.md), separately from the Stage 4B research snapshot. All 160 table values were independently recalculated; prebuilt multi-gigabyte databases are still not transferred. No new runs are needed for arithmetic verification.
3. Make the comparative scenarios explicit: logical query, same returned outcome, measurement boundaries, unequal physical index work and durability semantics.
4. Fix ambiguous cross-series wording in §3.7/§4.9, preserving numbers and all decisions. Produce a short audit and commit only reviewed changes.
5. Leave performance mystery (`hashes` faster than `none`) and other benchmarking for later as requested.

**Stop condition for the next editorial pass:** a small reviewed commit/PR with explicit novelty, accurate accessible evidence, precise comparison limits and unchanged raw results; stop if unavailable raw original sources prevent a stronger claim. No GitHub Issues, no new benchmark runs, no alteration of locked studies, and no assumption that RDC is online.

The snapshot does not store the several-gigabyte prebuilt database. Need a re-generation pipeline with source SHA `0fd84e3da854aa9f536a75861e7838af41955bdc`, runner SHA `a11457ce502b416041ea7bde0b53d2dc047db491`, uniform key stream SHA `44106a815bb975ddbf956a475c5b6463832ce2bdaa139b59da6c18ffa50daf57`, and validation against frozen manifests if redoing experiments later.
