# Portable Polar.DB research snapshot — 2026-11-07

This directory preserves a repository-hosted snapshot of the **small, available** files from the original `/work/ram-study` workspace, so work can continue via GitHub after RDC and the Windows Docker host are switched off.

## Artifact and verification

- [ram-study-research-snapshot.tar.gz](ram-study-research-snapshot.tar.gz) — **2,022,699 bytes**, **852 archived files**.
- SHA-256: `ce53fed176518e3aca32c334d8eb47c7bb72110f4be48267970b020185545a79`.
- Archived names begin `ram-study/` and preserve the source relative paths. Contains review notes, experimental protocols, run scripts, immutable study locks, logs, condition-level data, the 48 individual Stage 4B result JSON files and 48 raw-query JSON files, the final Stage 4B analysis and editorial notes.
- A further **83 files** were excluded as binary/data files, large files (>5 MB), or desktop-agent logs. Runtime caches were also excluded. In particular, this does **not** contain the multi-gigabyte prepared databases, SQLite database file, or their page-cache state.
- The main comparative-series archive was **not located when this snapshot was made**. It was subsequently found intact on RDC and [published separately with checksum and recount instructions](../20261104/README.md). It is not part of this Stage 4B snapshot.

Verify and extract:

```bash
sha256sum ram-study-research-snapshot.tar.gz
tar -tzf ram-study-research-snapshot.tar.gz | head
mkdir -p research-restored
tar -xzf ram-study-research-snapshot.tar.gz -C research-restored
```

The SHA-256 above is the authoritative integrity check. The original dataset/runner source SHA values and the locked 4B protocol can be found inside the archive in `ram-study/stage4b-pinned-confirmation/`. The original prepared database can only be recreated using the sealed dataset-generation source, then independently verified against its preparation manifest.

## Fixed results, not to reinterpret

- Original Stage 4: `INCONCLUSIVE`; 40/48 eligible attempts, four qualifying primary pairs out of eight.
- Stage 4.1 engineering cache stabilization: 12/12 accepted (separate from scientific confirmation).
- Stage 4B: `PRIMARY_REPLICATED`; 48/48 accepted and 8/8 qualifying primary pairs, geometric-mean latency ratio **23.6216**, two-sided paired-block bootstrap 95% CI **20.8268–26.3142** for cold offsets `f2.bin` versus cold persisted hashes `f1.bin` **within Polar.DB**. Does not imply universal superiority, a general RAM threshold, or a 23× comparison with SQLite.
- Main article Stage 5: commit `2b841c3f7643afc54978100d9d556d4d1b9555c1`; nine previous numerical tables preserved.

Do not modify frozen protocols/observations or create substitute runs with old identifiers. Refer to [GitHub-only continuation](../../docs/RESEARCH_CONTINUATION_20261107.md). No GitHub Issues.
