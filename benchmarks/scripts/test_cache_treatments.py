#!/usr/bin/env python3
"""Independent opt-in v32 cache experiments. No changes to legacy benchmarks."""
import json
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
BIN = REPO/'benchmarks/src/PkIntLookup/bin/Release/net10.0/PkIntLookup.dll'


def call(*args, ok=True):
    r = subprocess.run(['dotnet',str(BIN),*args],cwd=REPO,
        capture_output=True,text=True,timeout=130)
    if (r.returncode==0)!=ok:
        raise AssertionError(f'Unexpected result ({r.returncode}): {args}\n{r.stdout[-750:]}\n{r.stderr[-750:]}')


def main():
    sha = subprocess.check_output(['git','rev-parse','HEAD'],cwd=REPO,text=True).strip()
    with tempfile.TemporaryDirectory(prefix='polar-v32-') as t:
        root=Path(t)/'prepared'
        out=Path(t)/'results'
        seen={}
        for engine,conditions in (('sqlite',('none','data')),('polar-db',
                ('none','payload','hashes','offsets'))):
            args=(f'--prepared-root={root}',f'--benchmark-engine={engine}','--rows=20000')
            opts=('--prepared-plan=uniform-v32',f'--prepared-source-sha={sha}')
            call('--prepared-stage=prepare',*args)
            call('--prepared-stage=plan',*args,*opts)
            for treatment in conditions:
                tag=f'test-v32-{engine}-{treatment}'
                call('--prepared-stage=query',*args,*opts,
                    '--prepared-cache-treatment='+treatment,
                    f'--prepared-results-root={out}',f'--benchmark-run-id={tag}')
                result=json.loads((out/'pk-int-lookup'/(tag+'.json')).read_text())
                assert result['CacheTreatment']==treatment
                assert result['ResidencyBeforeTreatment']['Fraction']>=.995
                assert result['BatchRows']==10000 and result['LatencyRows']==2000
                assert result['BatchChecksum']==result['ExpectedBatch']['Checksum']
                assert result['LatencyChecksum']==result['ExpectedLatency']['Checksum']
                before={x['Name']:x['Fraction'] for x in result['ResidencyBeforeBatch']['Files']}
                if treatment=='none':assert min(before.values())>=.995
                else:
                    target=('data.sqlite' if engine=='sqlite' else
                            {'payload':'f0.bin','hashes':'f1.bin','offsets':'f2.bin'}[treatment])
                    assert before[target]<=.1
                    if engine=='polar-db':
                        assert all(before[x]>=.995 for x in ('f1.bin','f2.bin') if x!=target)
                seen[engine,treatment]=result
                call('--prepared-stage=query',*args,*opts,
                    '--prepared-cache-treatment='+treatment,
                    f'--prepared-results-root={out}',f'--benchmark-run-id={tag}',ok=False)
            call('--prepared-stage=verify',*args,*opts)
            # Engine-incompatible treatment and absent treatment must fail.
            call('--prepared-stage=query',*args,*opts,f'--prepared-results-root={out}',
                 '--benchmark-run-id=missing-treatment',ok=False)
            bad='hashes' if engine=='sqlite' else 'data'
            call('--prepared-stage=query',*args,*opts,
                 '--prepared-cache-treatment='+bad,
                 f'--prepared-results-root={out}', '--benchmark-run-id=bad-treatment',ok=False)
        a=seen['sqlite','none'];b=seen['polar-db','none']
        assert (a['BatchChecksum'],a['LatencyChecksum'])==(
            b['BatchChecksum'],b['LatencyChecksum'])
    print('PASS v32 targeted payload/hash/offset eviction, results, no overwrite, negative checks')


if __name__=='__main__':
    try:main()
    except Exception as e:
        print('FAIL',e,file=sys.stderr)
        raise
