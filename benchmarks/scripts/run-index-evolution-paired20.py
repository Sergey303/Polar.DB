#!/usr/bin/env python3
"""Frozen 20 paired blocks per row-count: 2021 versus 2026, 10k probes each."""
from __future__ import annotations

import argparse
import csv
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import platform
import shutil
import sys
import tempfile
import time
import uuid

from index_evolution_support import (
    OLD, NEW, FILES, COLUMNS, checked, disk_guard, file_sha256,
    logged, parse_log, require_tools, write_snapshot,
)

ROOT = Path(__file__).resolve().parents[2]
SCHEDULE = Path(__file__).with_name('index-evolution-paired20-schedule.json')
ROWS = (50_000, 1_000_000)
PROBES = 10_000
SEED = 777
PAIRS_PER_SIZE = 20
MIN_FREE_BYTES = 2 * 2**30


def ensure_schedule() -> list[dict]:
    payload = json.loads(SCHEDULE.read_text(encoding='utf-8'))
    pairs = payload['pairs']
    assert payload['version'] == 1 and payload['seed'] == 20261109
    assert len(pairs) == len(ROWS) * PAIRS_PER_SIZE
    for size in ROWS:
        matching = [p for p in pairs if p['rows'] == size]
        assert len(matching) == PAIRS_PER_SIZE
        assert sorted(p['block'] for p in matching) == list(range(1, PAIRS_PER_SIZE+1))
        assert [p['order'] for p in matching].count('old-new') == 10
        assert [p['order'] for p in matching].count('new-old') == 10
    assert all(set(p)=={'rows','block','order'} for p in pairs)
    return pairs


def run() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, help='New destination; never overwrite existing')
    args = parser.parse_args()
    schedule = ensure_schedule()
    target = args.output.expanduser().absolute() if args.output else (
        ROOT/'benchmarks/results/index-evolution'/('paired20-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')+'-'+uuid.uuid4().hex[:6]))
    if target.exists() or target.is_symlink():
        raise RuntimeError('Refusing to touch an existing directory: '+str(target))
    disk_guard(target, MIN_FREE_BYTES)
    sdk = require_tools(ROOT)
    selected_sdk = next(x.split()[0] for x in sdk['sdk'] if x.startswith('10.'))
    source = ROOT/'benchmarks/src/IndexEvolutionDirect'
    if any(not (source/name).is_file() for name in FILES):
        raise RuntimeError('Missing index measurement adapter')
    target.mkdir(parents=True, exist_ok=False)
    print('EXPERIMENT_DIR='+str(target),flush=True)
    manifest_path = target/'manifest.json'
    results_path = target/'results.csv'
    walls_path = target/'invocations.csv'
    manifest = {
        'schema':1,'status':'running','experiment':'paired20-2021-2026',
        'sourceSnapshots':{'old':OLD,'new':NEW},'probes':PROBES,'seed':SEED,
        'sizes':ROWS,'pairsPerSize':PAIRS_PER_SIZE,'plannedPairs':len(schedule),
        'completedPairs':0,'acceptedRuns':0,
        'scheduleSha256':file_sha256(SCHEDULE),'runnerSha256':file_sha256(Path(__file__)),
        'supportSha256':file_sha256(Path(__file__).with_name('index_evolution_support.py')),
        'adapterSha256':{name:file_sha256(source/name) for name in FILES},
        'gitHead':checked(['git','-C',str(ROOT),'rev-parse','HEAD']).strip(),
        'gitTrackedDirty':bool(checked(['git','-C',str(ROOT),'status','--porcelain','-uno']).strip()),
        'environment':{'platform':platform.platform(),'python':platform.python_version(),
                       'sdk':selected_sdk,'sdks':sdk['sdk'],'runtimes':sdk['runtime'],
                       'cpus':os.cpu_count(),'machine':platform.machine()},
    }
    def save():
        temp = manifest_path.with_suffix('.tmp')
        temp.write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
        temp.replace(manifest_path)
    save()
    with results_path.open('w',encoding='utf-8',newline='') as out:
        csv.DictWriter(out,fieldnames=COLUMNS).writeheader()
    wall_header=['pair_sequence','block','rows','engine','position','started_utc','wall_sec']
    with walls_path.open('w',encoding='utf-8',newline='') as out:
        csv.DictWriter(out,fieldnames=wall_header).writeheader()
    try:
        builds={}
        for engine,sha in (('old',OLD),('new',NEW)):
            disk_guard(target,MIN_FREE_BYTES)
            checkout=target/'_build'/engine
            write_snapshot(ROOT,sha,checkout,legacy=engine=='old')
            (checkout/'global.json').write_text(json.dumps({'sdk':{'version':selected_sdk,'rollForward':'latestPatch'}}),encoding='utf-8')
            project=checkout/'benchmarks/src/IndexEvolutionDirect/IndexEvolutionDirect.csproj'
            flags=['-p:DefineConstants=LEGACY2021'] if engine=='old' else ['-p:TargetFrameworks=net10.0']
            logged(['dotnet','build',str(project),'-c','Release','-v','quiet','--nologo',
                    '-p:GeneratePackageOnBuild=false',*flags],target/f'build-{engine}.log',checkout,600)
            dll=project.parent/'bin/Release/net10.0/IndexEvolutionDirect.dll'
            if not dll.is_file(): raise RuntimeError('Expected build DLL missing: '+str(dll))
            builds[engine]=dll
            print(f'BUILT {engine}',flush=True)
        for sequence,item in enumerate(schedule,1):
            size,block,order=item['rows'],item['block'],item['order'].split('-')
            pair_records=[]
            for position,engine in enumerate(order,1):
                disk_guard(target,MIN_FREE_BYTES)
                sha=OLD if engine=='old' else NEW
                log=target/f'measure-{sequence:02}-{size}-{block}-{engine}.log'
                started=datetime.now(timezone.utc).isoformat()
                then=time.monotonic()
                with tempfile.TemporaryDirectory(prefix='_measured-',dir=target) as root:
                    logged(['dotnet',str(builds[engine]),str(Path(root)/'database'),str(size),
                            str(PROBES),str(SEED)],log,builds[engine].parent,900,
                           {**os.environ,'INDEX_EVOLUTION_SHA':sha})
                wall=time.monotonic()-then
                row=parse_log(log,engine,sha,size,PROBES,SEED,block)
                pair_records.append(row)
                with walls_path.open('a',encoding='utf-8',newline='') as out:
                    csv.DictWriter(out,fieldnames=wall_header).writerow({
                        'pair_sequence':sequence,'block':block,'rows':size,
                        'engine':engine,'position':position,'started_utc':started,
                        'wall_sec':f'{wall:.5f}'})
            if pair_records[0]['checksum']!=pair_records[1]['checksum']:
                raise RuntimeError('Pair checksum mismatch: '+str(item))
            with results_path.open('a',encoding='utf-8',newline='') as out:
                writer=csv.DictWriter(out,fieldnames=COLUMNS)
                writer.writerows(pair_records)
            manifest['acceptedRuns']+=2
            manifest['completedPairs']=sequence
            save()
            print(f'PASS pair={sequence:02}/40 rows={size} block={block} order={item["order"]}',flush=True)
        manifest['status']='complete'
        manifest['resultsSha256']=file_sha256(results_path)
        manifest['invocationsSha256']=file_sha256(walls_path)
        save()
        print('COMPLETE 40/40 pairs, 80/80 processes',flush=True)
        print('RESULTS_CSV='+str(results_path),flush=True)
        return 0
    except Exception as error:
        manifest['status']='failed';manifest['failure']=str(error)
        save()
        print(f'FAILED: {error}; partial logs preserved in {target}',file=sys.stderr)
        return 1


if __name__=='__main__':
    try: sys.exit(run())
    except (RuntimeError,AssertionError,OSError,ValueError) as error:
        print('PRE-FLIGHT FAILED: '+str(error),file=sys.stderr);sys.exit(2)
