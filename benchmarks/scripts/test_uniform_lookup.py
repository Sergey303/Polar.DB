#!/usr/bin/env python3
"""Independent SplitMix64 parity + uniform v3 prepare/plan/query integration smoke."""
import hashlib
import json
import struct
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
BIN = REPO/'benchmarks/src/PkIntLookup/bin/Release/net10.0/PkIntLookup.dll'
MASK = (1 << 64)-1
SEED = 0x6A09E667F3BCC909


def sample(n):
    state = SEED
    found = set()
    parts = []
    for size in (500,10000,2000):
        values = []
        while len(values) < size:
            state = (state + 0x9E3779B97F4A7C15)&MASK
            z = state
            z = ((z^(z>>30))*0xBF58476D1CE4E5B9)&MASK
            z = ((z^(z>>27))*0x94D049BB133111EB)&MASK
            z ^= z>>31
            threshold = ((-n)&MASK)%n
            if z < threshold: continue
            id_ = z%n+1
            if id_ in found: continue
            found.add(id_)
            values.append(id_)
        parts.append(values)
    stream = [value for group in parts for value in group]
    digest = hashlib.sha256(b''.join(struct.pack('<i',x) for x in stream)).hexdigest()
    assert len(stream)==len(found)==12500
    assert not set(parts[0]).intersection(parts[1],parts[2])
    assert not set(parts[1]).intersection(parts[2])
    return digest, len({(x-1)//50000 for x in stream}),len({(x-1)//1000000 for x in stream})


def run(*args, success=True):
    p = subprocess.run(['dotnet',str(BIN),*args],cwd=REPO,
                       text=True, capture_output=True,timeout=90)
    if (p.returncode==0)!=success:
        raise AssertionError((args,p.returncode,p.stdout[-1000:],p.stderr[-1000:]))
    return p


def main():
    # The Python implementation is independent of the C# plan generator.
    for rows in (20_000,5_000_000,20_000_000):
        digest,bins,regions=sample(rows)
        print('EXPECTED',rows,'SHA256',digest,'50k bins',bins,'1m regions',regions)
    sha=subprocess.check_output(['git','rev-parse','HEAD'],cwd=REPO,text=True).strip()
    with tempfile.TemporaryDirectory(prefix='uniform-v3-') as temp:
        root=Path(temp)/'prepared'
        output=Path(temp)/'results'
        records={}
        for engine in ('sqlite','polar-db'):
            args=(f'--prepared-root={root}',f'--benchmark-engine={engine}',
                  '--rows=20000')
            v3=('--prepared-plan=uniform-v3',f'--prepared-source-sha={sha}')
            run('--prepared-stage=prepare',*args)
            run('--prepared-stage=plan',*args,*v3)
            p=root/'pk-int-lookup/rows-20000'/(engine+'.uniform-v3.json')
            sidecar=json.loads(p.read_text());expect,bins,regions=sample(20000)
            assert sidecar['KeyStreamSha256']==expect
            assert sidecar['Coverage']['UniqueKeys']==12500
            assert sidecar['Coverage']['BinsTouched']==bins
            assert sidecar['Coverage']['MillionRowRegionsTouched']==regions
            assert sidecar['DatasetSourceSha']==sha
            run('--prepared-stage=query',*args,*v3,
                f'--prepared-results-root={output}',
                f'--benchmark-run-id=test-{engine}-v3')
            result=json.loads((output/'pk-int-lookup'/f'test-{engine}-v3.json').read_text())
            assert result['BatchRows']==10000 and result['LatencyRows']==2000
            assert result['BatchChecksum']==sidecar['BatchChecksum']
            assert result['LatencyChecksum']==sidecar['LatencyChecksum']
            assert result['ResidencyBeforeBatch']['Pages']>0
            assert result['ResidencyAfterBatch']['Pages']>0
            records[engine]=result
            run('--prepared-stage=verify',*args,*v3)
            run('--prepared-stage=query',*args,*v3,
                f'--prepared-results-root={output}',
                f'--benchmark-run-id=test-{engine}-v3',success=False)
            # Source SHA mismatch must be rejected, not silently accepted.
            wrong=('0'*40) if sha!='0'*40 else ('1'*40)
            run('--prepared-stage=query',*args,
                '--prepared-plan=uniform-v3',f'--prepared-source-sha={wrong}',
                f'--prepared-results-root={output}',
                f'--benchmark-run-id=wrong-{engine}',success=False)
            # Tamper only with sidecar metadata; original DB must remain intact.
            sidecar['KeyStreamSha256']='0'*64
            p.write_text(json.dumps(sidecar))
            run('--prepared-stage=verify',*args,*v3,success=False)
        a,b=records['sqlite'],records['polar-db']
        assert a['PlanKeyStreamSha256']==b['PlanKeyStreamSha256']
        assert (a['BatchChecksum'],a['LatencyChecksum'])==(
            b['BatchChecksum'],b['LatencyChecksum'])
        print('PASS uniform-v3 independent PRNG, disjoint sets, Oracle parity, immutable DB and tamper rejections')


if __name__=='__main__':
    try:main()
    except Exception as ex:
        print('FAIL',ex,file=sys.stderr)
        raise
