#!/usr/bin/env python3
"""Recalculate the November 5 isolated ReopenOnly diagnostic from its raw evidence."""
import csv
import json
from pathlib import Path
from statistics import median

EVIDENCE = (Path(__file__).resolve().parents[1] / 'docs' / 'evidence'
            / 'reopen-diagnostic-20261105')
SOURCE_SHA = 'abbb03cdc34cff1c11e7550494f65b03dad9afd1'
RUNS = {
    '50k': (50000, '20261105T012349349Z-abbb03cd-reopen-only'),
    '5m': (5000000, '20261105T012409824Z-abbb03cd-reopen-only'),
}
CONSTRUCT = 'reopen/query-ready/construct-external-3'
REFRESH = 'reopen/query-ready/refresh-external-3'
TOTAL = 'query-ready reopen'


def check(run_name, rows_expected, run_id):
    folder = EVIDENCE / run_name
    manifest = json.loads((folder / 'manifest.json').read_text())
    raw = json.loads((folder / 'combined.raw.json').read_text())
    samples = list(csv.DictReader((folder / 'samples.csv').open(newline='')))
    assert manifest['runId'] == run_id
    assert raw['manifest']['runId'] == run_id
    assert manifest['coordinator']['commitSha'] == SOURCE_SHA
    assert manifest['coordinator']['runtimeVersion'] == '10.0.12'
    assert manifest['coordinator']['buildConfiguration'] == 'Release'
    assert manifest['coordinator']['publicationReady'] is True
    assert manifest['coordinator']['gitDirty'] is False
    assert sorted(p['engine'] for p in manifest['engineProcesses']) == ['polar-db', 'sqlite']
    assert all(p['commitSha'] == SOURCE_SHA and p['runtimeVersion'] == '10.0.12'
               for p in manifest['engineProcesses'])
    runs = raw['lifecycleRuns']
    assert len(runs) == 1 and runs[0]['setupRows'] == rows_expected
    expected = runs[0]['expected']
    assert expected['rows'] == rows_expected
    engines = runs[0]['engines']
    assert sorted(e['engine'] for e in engines) == ['polar-db-current', 'sqlite']
    assert all(e['rows'] == expected['rows'] and e['checksum'] == expected['checksum']
               for e in engines)
    assert all(r['run_id'] == run_id and int(r['setup_rows']) == rows_expected
               for r in samples)

    by_key = {}
    for sample in samples:
        key = (sample['engine'], sample['metric'], int(sample['sample_index']))
        assert key not in by_key
        by_key[key] = float(sample['value'])

    def times(engine, metric):
        result = [by_key[(engine, metric, i)] for i in range(3)]
        assert all(x >= 0 for x in result)
        return result

    polar = 'polar-db-current'
    stats = {name: median(times(polar, name)) for name in ('open-only', TOTAL, CONSTRUCT, REFRESH)}
    sqlite_open = median(times('sqlite', 'open-only'))
    sqlite_total = median(times('sqlite', TOTAL))
    fractions = [(by_key[polar, CONSTRUCT, i] + by_key[polar, REFRESH, i]) /
                 by_key[polar, TOTAL, i] for i in range(3)]
    stats['paired_fraction_median'] = median(fractions)
    assert all(0 < x < 1 for x in fractions)
    return stats, sqlite_open, sqlite_total


def main():
    print('Note: three samples per scenario; descriptive diagnostics only.')
    for name, (rows, run_id) in RUNS.items():
        stats, sqlite_open, sqlite_total = check(name, rows, run_id)
        print(f'{name} run={run_id}')
        print(f'  Polar.DB open={stats["open-only"]:.4f} ms, '
              f'query-ready={stats[TOTAL]:.4f} ms')
        print(f'  string construct={stats[CONSTRUCT]:.4f} ms, '
              f'string refresh={stats[REFRESH]:.4f} ms, '
              f'paired share={stats["paired_fraction_median"]*100:.2f}%')
        print(f'  SQLite open={sqlite_open:.4f} ms, query-ready={sqlite_total:.4f} ms')


if __name__ == '__main__':
    main()
