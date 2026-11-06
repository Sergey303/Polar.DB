#!/usr/bin/env python3
"""Integration/negative tests for opt-in Linux uniform-v4 pinned-page cache."""
import json
import resource
import subprocess
import sys
import tempfile
from pathlib import Path

REPO=Path(__file__).resolve().parents[2]
DLL=REPO/'benchmarks/src/PkIntLookup/bin/Release/net10.0/PkIntLookup.dll'


def run(*args,success=True,preexec=None):
    p=subprocess.run(['dotnet',str(DLL),*args],cwd=REPO,text=True,
        stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=120,
        preexec_fn=preexec)
    if (p.returncode==0)!=success:
        raise AssertionError(f'exit={p.returncode} args={args}\n{p.stdout[-1200:]}')
    return p.stdout


def main():
    if not sys.platform.startswith('linux'):
        print('SKIP Linux-only pinned-cache integration');return
    sha=subprocess.check_output(['git','rev-parse','HEAD'],cwd=REPO,text=True).strip()
    with tempfile.TemporaryDirectory(prefix='polar-pin-v4-') as tmp:
        base=Path(tmp);root=base/'prepared';results=base/'result'
        checks={}
        for engine,conditions in (('sqlite',('none','data','data-half')),
                ('polar-db',('none','hashes','offsets','payload','payload-half'))):
            common=(f'--prepared-root={root}',f'--benchmark-engine={engine}','--rows=20000')
            v4=('--prepared-plan=uniform-v4',f'--prepared-source-sha={sha}')
            run('--prepared-stage=prepare',*common)
            run('--prepared-stage=plan',*common,*v4)
            for treatment in conditions:
                tag=f'pin-v4-{engine}-{treatment}'
                args=('--prepared-stage=query',*common,*v4,
                    f'--prepared-results-root={results}',
                    f'--prepared-cache-treatment={treatment}',f'--benchmark-run-id={tag}')
                run(*args)
                obj=json.loads((results/'pk-int-lookup'/(tag+'.json')).read_text())
                assert obj['PinnedCacheProtocol']=='mlock-v1'
                total=obj['PinnedInitiallyBytes'];during=obj['PinnedDuringBatchBytes']
                assert total>0 and during>=0
                files={f['Name']:f['Fraction'] for f in obj['ResidencyBeforeBatch']['Files']}
                after={f['Name']:f['Fraction'] for f in obj['ResidencyAfterBatch']['Files']}
                assert obj['BatchRows']==10000 and obj['LatencyRows']==2000
                assert obj['BatchChecksum']==obj['ExpectedBatch']['Checksum']
                assert obj['LatencyChecksum']==obj['ExpectedLatency']['Checksum']
                if treatment=='none':
                    assert during==total and min(files.values())==1 and min(after.values())==1
                else:
                    selected='data.sqlite' if engine=='sqlite' else {
                        'hashes':'f1.bin','offsets':'f2.bin',
                        'payload':'f0.bin','payload-half':'f0.bin'}[treatment]
                    expected=Path(root/'pk-int-lookup'/'rows-20000'/engine/selected).stat().st_size
                    assert during==total-expected,(tag,during,total,expected)
                    if treatment.endswith('-half'): assert .35<=files[selected]<=.65
                    else: assert files[selected]<=.1
                    for name in files:
                        if name!=selected:assert files[name]==1 and after[name]==1
                checks[(engine,treatment)]=(obj['BatchChecksum'],obj['LatencyChecksum'])
                run(*args,success=False) # result is immutable
            run('--prepared-stage=verify',*common,*v4)
            # No silent use of the v4 cache treatment under the legacy v3 plan.
            run('--prepared-stage=query',*common,
                '--prepared-plan=uniform-v3',f'--prepared-source-sha={sha}',
                f'--prepared-results-root={results}',
                '--prepared-cache-treatment=none','--benchmark-run-id=invalid-v3',
                success=False)
        assert checks['sqlite','none']==checks['polar-db','none']
        # Resource limitation must cause an explicit failure, never a measured run.
        def tiny_limit():resource.setrlimit(resource.RLIMIT_MEMLOCK,(65536,65536))
        args=('--prepared-stage=query',f'--prepared-root={root}',
              '--benchmark-engine=polar-db','--rows=20000',
              '--prepared-plan=uniform-v4',f'--prepared-source-sha={sha}',
              f'--prepared-results-root={results}',
              '--prepared-cache-treatment=none','--benchmark-run-id=insufficient-memlock')
        output=run(*args,success=False,preexec=tiny_limit)
        assert 'mlock(' in output and ('errno=' in output),output[-1000:]
        assert not (results/'pk-int-lookup/insufficient-memlock.json').exists()
        print('PASS v4 pin/unpin, data and index-only eviction, 50% treatments, '
              'unchanged checksums, no overwrite, legacy isolation, low memlock fail-fast')

if __name__=='__main__':main()
