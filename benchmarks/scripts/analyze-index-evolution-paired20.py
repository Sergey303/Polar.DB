#!/usr/bin/env python3
"""Audit the full preregistered 20×2 paired series and recompute its statistics."""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
from pathlib import Path
import random
import statistics
import sys
import tarfile

from index_evolution_support import OLD, NEW, COLUMNS

ROOT = Path(__file__).resolve().parents[2]
SCHEDULE = Path(__file__).with_name('index-evolution-paired20-schedule.json')
METRICS = ('load_ms','prerequisite_ms','index_build_ms','lookup_ms')


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def quantile(sorted_values: list[float], q: float) -> float:
    k=(len(sorted_values)-1)*q
    j=int(k)
    h=k-j
    return sorted_values[j]*(1-h)+sorted_values[min(j+1,len(sorted_values)-1)]*h


def summarize(logs: list[float], bootstrap_seed: int) -> dict:
    ratios=[math.exp(x) for x in logs]
    estimate=math.exp(statistics.mean(logs))
    randomizer=random.Random(bootstrap_seed)
    samples=[]
    for _ in range(20_000):
        samples.append(math.exp(sum(logs[randomizer.randrange(len(logs))]
                                    for _ in logs)/len(logs)))
    samples.sort()
    return {'ratioGeomean':estimate,'ratioMedian':statistics.median(ratios),
            'ratioRange':[min(ratios),max(ratios)],
            'bootstrap95':[quantile(samples,.025),quantile(samples,.975)],
            'oldFasterPairs':sum(r<1 for r in ratios),
            'newFasterPairs':sum(r>1 for r in ratios),
            'equalPairs':sum(r==1 for r in ratios),
            'pairRatios':ratios}


def analyze(directory: Path) -> dict:
    manifest=json.loads((directory/'manifest.json').read_text(encoding='utf-8'))
    schedule=json.loads(SCHEDULE.read_text(encoding='utf-8'))['pairs']
    if manifest['status']!='complete' or manifest['acceptedRuns']!=80 or manifest['completedPairs']!=40:
        raise ValueError('Incomplete counted series; refuse to calculate published statistics')
    if manifest['plannedPairs']!=40 or manifest['pairsPerSize']!=20 or manifest['probes']!=10_000 or manifest['seed']!=777:
        raise ValueError('Protocol params mismatch')
    if manifest['sourceSnapshots']!={'old':OLD,'new':NEW}:
        raise ValueError('Unexpected pinned versions')
    if manifest['scheduleSha256']!=digest(SCHEDULE) or manifest['resultsSha256']!=digest(directory/'results.csv'):
        raise ValueError('SHA-256 mismatch in schedule or results')
    if manifest['invocationsSha256']!=digest(directory/'invocations.csv'):
        raise ValueError('Invocation SHA-256 mismatch')
    with (directory/'results.csv').open(encoding='utf-8',newline='') as f:
        reader=csv.DictReader(f)
        if tuple(reader.fieldnames)!=COLUMNS: raise ValueError('Unexpected CSV schema')
        rows=list(reader)
    with (directory/'invocations.csv').open(encoding='utf-8',newline='') as f:
        walls=list(csv.DictReader(f))
    if len(rows)!=80 or len(walls)!=80:
        raise ValueError('Expected exactly 80 result and 80 wall-time records')
    archive = directory/'measurement-logs.tar.gz'
    archived_logs = None
    if archive.is_file():
        with tarfile.open(archive,mode='r:gz') as tar:
            entries=tar.getmembers()
            if len(entries)!=80 or any((not e.isfile() or '/' in e.name or
                                         not e.name.startswith('measure-') or
                                         not e.name.endswith('.log')) for e in entries):
                raise ValueError('Unsafe or incomplete measurement log archive')
            archived_logs={entry.name:tar.extractfile(entry).read().decode('utf-8') for entry in entries}
            if len(archived_logs)!=80:
                raise ValueError('Duplicate logs in archive')
    result={}
    for sequence,item in enumerate(schedule,1):
        pair=rows[2*(sequence-1):2*sequence]
        invocations=walls[2*(sequence-1):2*sequence]
        order=item['order'].split('-')
        for position,(engine,row,wall) in enumerate(zip(order,pair,invocations),1):
            if (row['engine']!=engine or row['sha']!= (OLD if engine=='old' else NEW)
                or int(row['rows'])!=item['rows'] or int(row['block'])!=item['block']
                or int(row['probes'])!=10_000 or int(row['seed'])!=777 or row['verified']!='true'):
                raise ValueError(f'Wrong identity or validation flag in pair {sequence}')
            if (int(wall['pair_sequence'])!=sequence or int(wall['rows'])!=item['rows']
                or wall['engine']!=engine or int(wall['block'])!=item['block']
                or int(wall['position'])!=position or not float(wall['wall_sec'])>0):
                raise ValueError(f'Incorrect wall-time entry in pair {sequence}')
            if any(not math.isfinite(float(row[x])) or float(row[x])<=0 for x in METRICS):
                raise ValueError('Non-finite or nonpositive phase')
            name=f'measure-{sequence:02}-{item["rows"]}-{item["block"]}-{engine}.log'
            if archived_logs is not None:
                content=archived_logs.get(name,'')
            else:
                log=directory/name
                content=log.read_text(encoding='utf-8') if log.is_file() else ''
            result_lines=[x[7:] for x in content.splitlines() if x.startswith('RESULT ')]
            if len(result_lines)!=1:
                raise ValueError('Missing or duplicate RESULT in process log '+name)
            fields=dict(x.split('=',1) for x in result_lines[0].split())
            for label in ('rows','probes','seed','load_ms','prerequisite_ms',
                          'index_build_ms','lookup_ms','checksum','verified'):
                if fields.get(label)!=row[label]:
                    raise ValueError('Log/CSV discrepancy for '+name+' field '+label)
            if fields.get('engine')!=row['sha']:
                raise ValueError('Log/CSV source identity discrepancy in '+name)
        if pair[0]['checksum']!=pair[1]['checksum']:
            raise ValueError(f'Mismatching checksums in pair {sequence}')
        result[item['rows'],item['block']]={r['engine']:r for r in pair}
    for size in (50_000,1_000_000):
        checksums={result[size,b][engine]['checksum'] for b in range(1,21) for engine in ('old','new')}
        if len(checksums)!=1: raise ValueError(f'Dataset checksum drift at {size}')
    final={'status':'complete','experiment':'2021-vs-2026-paired20-10000probes',
           'rawCsvSha256':digest(directory/'results.csv'),
           'scheduleSha256':digest(SCHEDULE),'manifestGitHead':manifest['gitHead'],
           'host':manifest['environment'], 'bySize':{}}
    for size in (50_000,1_000_000):
        data={engine:[result[size,b][engine] for b in range(1,21)] for engine in ('old','new')}
        logs=[math.log(float(data['old'][i]['lookup_ms'])/float(data['new'][i]['lookup_ms']))
              for i in range(20)]
        stats=summarize(logs,bootstrap_seed=20261109+size)
        stats['ratioByOrder']={}
        for name, wanted in (('old-new',('old','new')),('new-old',('new','old'))):
            applicable=[]
            for seq,item in enumerate(schedule,1):
                if item['rows']==size and item['order']==name:
                    pair=result[size,item['block']]
                    applicable.append(math.log(float(pair['old']['lookup_ms'])/
                                               float(pair['new']['lookup_ms'])))
            if len(applicable)!=10: raise ValueError('Unbalanced order strata')
            stats['ratioByOrder'][name]={'n':10,'geomean':math.exp(statistics.mean(applicable)),
                                          'median':statistics.median(math.exp(x) for x in applicable)}
        for metric in ('lookup_ms','index_build_ms','load_ms','prerequisite_ms'):
            stats[metric]={engine:{'median':statistics.median(float(x[metric]) for x in data[engine]),
                                   'min':min(float(x[metric]) for x in data[engine]),
                                   'max':max(float(x[metric]) for x in data[engine])}
                           for engine in ('old','new')}
        stats['checksums']=list({r['checksum'] for values in data.values() for r in values})
        final['bySize'][str(size)]=stats
    return final


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory',type=Path)
    parser.add_argument('--out',type=Path,help='Optional JSON report path')
    args=parser.parse_args()
    try:
        report=analyze(args.directory)
        result=json.dumps(report,ensure_ascii=False,indent=2)+'\n'
        if args.out: args.out.write_text(result,encoding='utf-8')
        print(result)
    except (ValueError,OSError,KeyError,RuntimeError) as exc:
        print('ANALYSIS FAILED: '+str(exc),file=sys.stderr)
        sys.exit(2)
