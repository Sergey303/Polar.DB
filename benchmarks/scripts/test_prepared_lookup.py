#!/usr/bin/env python3
"""Small, deterministic integration smoke for opt-in prepare/verify/query stages.

Build the requested benchmark executable in Release before running. This smoke
uses temporary files and intentionally does not test OS-cache coldness or speed.
"""
import json
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
EXE = REPO / 'benchmarks/src/PkIntLookup/bin/Release/net10.0/PkIntLookup.dll'


def call(*arguments, success=True):
    r = subprocess.run(['dotnet', str(EXE), *arguments], cwd=REPO,
                       text=True, capture_output=True, timeout=90)
    if (r.returncode == 0) != success:
        raise AssertionError(f'expected success={success}, exit={r.returncode}: '
                             f'{arguments}\nstdout={r.stdout[-1200:]}\nstderr={r.stderr[-1200:]}')
    return r


def run():
    if not EXE.is_file():
        raise RuntimeError('Build PkIntLookup -c Release first.')
    with tempfile.TemporaryDirectory(prefix='polar-prepared-') as temp:
        root = Path(temp) / 'prepared'
        results = Path(temp) / 'results'
        outputs = {}
        for engine in ('sqlite', 'polar-db'):
            common = (f'--prepared-root={root}', f'--benchmark-engine={engine}', '--rows=500')
            call('--prepared-stage=prepare', *common)
            manifest = json.loads((root / 'pk-int-lookup' / 'rows-500' /
                                   (engine + '.ready.json')).read_text())
            assert manifest['Schema'] == 2
            assert manifest['LookupsPerSample'] > 0
            assert len(manifest['ExpectedPhases']) == 2
            call('--prepared-stage=verify', *common)
            # Re-preparing would overwrite a sealed dataset, so it must fail.
            call('--prepared-stage=prepare', *common, success=False)
            for phase in ('cold', 'hot'):
                runid = f'test-{engine}-{phase}'
                call('--prepared-stage=query', *common,
                     f'--prepared-results-root={results}',
                     f'--prepared-phase={phase}', f'--benchmark-run-id={runid}')
                out = results / 'pk-int-lookup' / (runid + '.json')
                value = json.loads(out.read_text())
                assert value['Rows'] == 500
                assert value['BatchRows'] == value['ExpectedBatch']['Rows']
                assert value['BatchChecksum'] == value['ExpectedBatch']['Checksum']
                assert value['LatencyChecksum'] == value['ExpectedLatency']['Checksum']
                assert value['BatchAverageMs'] and value['LatencySamplesMs']
                assert value['Environment']['BuildConfiguration'] == 'Release'
                stages = value['Snapshots']
                for key in ('BeforeFileWarmup', 'AfterFileWarmup', 'AfterOpen',
                            'AfterKeyWarmup', 'AfterBatch', 'AfterLatency', 'AfterClose'):
                    assert 'ProcessIo' in stages[key] and 'MemoryCgroup' in stages[key]
                    assert 'CgroupCurrentBytes' in stages[key]
                order = [stages[key]['StopwatchTicks'] for key in
                         ('BeforeFileWarmup', 'AfterFileWarmup', 'AfterOpen',
                          'AfterKeyWarmup', 'AfterBatch', 'AfterLatency', 'AfterClose')]
                assert order == sorted(order)
                outputs[engine, phase] = value
                # Duplicate run ID must not overwrite successful results.
                call('--prepared-stage=query', *common,
                     f'--prepared-results-root={results}',
                     f'--prepared-phase={phase}', f'--benchmark-run-id={runid}',
                     success=False)
            call('--prepared-stage=verify', *common)

        for phase in ('cold', 'hot'):
            a, b = outputs['sqlite', phase], outputs['polar-db', phase]
            for key in ('BatchRows', 'BatchChecksum', 'LatencyRows', 'LatencyChecksum'):
                assert a[key] == b[key], (key, phase)

        # Tampering with one byte of the permanent SQLite data file must fail.
        db = root / 'pk-int-lookup/rows-500/sqlite/data.sqlite'
        with db.open('r+b') as f:
            f.seek(150)
            original = f.read(1)
            f.seek(150)
            f.write(bytes([original[0] ^ 1]))
        call('--prepared-stage=verify', f'--prepared-root={root}',
             '--benchmark-engine=sqlite', '--rows=500', success=False)
    print('PASS prepared lookup integration: build/verify/query/correctness/immutability/tamper')


if __name__ == '__main__':
    try:
        run()
    except Exception as error:
        print('FAIL', error, file=sys.stderr)
        raise
