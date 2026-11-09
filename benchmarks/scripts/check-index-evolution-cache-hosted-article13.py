#!/usr/bin/env python3
"""Recompute all 8 numeric rows in Article Table 13 and R2 hosted report Table 1."""
from __future__ import annotations
import json,re
from decimal import Decimal,ROUND_HALF_UP
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
EVIDENCE=ROOT/'benchmarks/evidence'
DOCS=ROOT/'benchmarks/docs'
SOURCES=(
 ('RDC / WSL2','20261109-index-evolution-cache-r2/summary.json'),
 ('GitHub Ubuntu 22.04 / x64','20261109-index-evolution-cache-hosted-42593558005/ubuntu-22-x64/summary.json'),
 ('GitHub Ubuntu 24.04 / x64','20261109-index-evolution-cache-hosted-42593558005/ubuntu-24-x64/summary.json'),
 ('GitHub Ubuntu 24.04 / ARM64','20261109-index-evolution-cache-hosted-42593558005/ubuntu-24-arm64/summary.json'),
)

def f(x):
    return str(Decimal(str(x)).quantize(Decimal('0.001'),rounding=ROUND_HALF_UP)).replace('.',',')

def ratio(x):
    return f(x['geomean'])+' ['+f(x['ci95'][0])+'; '+f(x['ci95'][1])+']'

def extract(path,caption=None):
    s=path.read_text(encoding='utf-8')
    if caption:
        heading='Таблица '+str(caption)+' —'
        p=s.index(heading)
        s=s[p:]
    m=re.search(r'^\| Среда \| Записей \|[^\n]+\n((?:\|[^\n]+\n)+)',s,re.M)
    if not m:raise RuntimeError('R2 result table not found: '+str(path))
    entries=m.group(1).splitlines()
    rows=[[c.strip() for c in line.strip().strip('|').split('|')] for line in entries[1:]]
    if len(rows)!=8 or any(len(row)!=5 for row in rows):
        raise RuntimeError('Expected 8 rows, 5 cols: '+str(path))
    return rows

def main():
    expected=[]
    for name,rel in SOURCES:
        raw=json.loads((EVIDENCE/rel).read_text(encoding='utf-8'))
        if raw['status']!='complete' or raw['blocks']!=40 or raw['processes']!=160:
            raise RuntimeError('Incomplete evidence '+rel)
        for size in (50000,1000000):
            data=raw['bySize'][str(size)]
            if (data['cacheBefore']['baseline']!=[1.,1.] or
                data['cacheAfter']['baseline']!=[1.,1.] or
                data['cacheAfter']['refresh']!=[1.,1.] or
                data['cacheBefore']['evict']!=[1.,1.] or
                data['cacheAfter']['evict']!=[0.,0.]):
                raise RuntimeError('Cache state not confirmed in '+rel+' size '+str(size))
            a=data['paired']['evict_over_baseline']['lookup_ms']
            b=data['paired']['baseline_over_modern']['lookup_ms']
            if b['pairsAbove1']+b['pairsBelow1']!=20:
                raise RuntimeError('Invalid paired-run accounting')
            expected.append([name,f'{size:,}'.replace(',',' '),ratio(a),ratio(b),
                             str(b['pairsAbove1'])+'/20'])
    paths=[(DOCS/'POLAR_DB_ARTICLE_DRAFT.md',13),
           (DOCS/'INDEX_EVOLUTION_CACHE_HOSTED_RESULTS_20261109.md',1),
           (EVIDENCE/'20261109-index-evolution-cache-hosted-42593558005/README.md',None)]
    for path,table in paths:
        actual=extract(path,table)
        if actual!=expected:
            first=next(((i,actual[i],expected[i]) for i in range(8) if actual[i]!=expected[i]),None)
            raise RuntimeError('Table values differ from source in '+str(path)+': '+str(first))
    print('PASS: article Table 13, hosted R2 report Table 1, evidence README; all 8 rows match 4 x 160 process summaries')

if __name__=='__main__':main()
