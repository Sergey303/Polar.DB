#!/usr/bin/env python3
"""Recompute all three paper tables against sealed R2 cache evidence summary."""
from __future__ import annotations
import json,re
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'benchmarks/docs/INDEX_EVOLUTION_CACHE_CONTROL_RESULTS_20261109.md'
SOURCE=ROOT/'benchmarks/evidence/20261109-index-evolution-cache-r2/summary.json'

def f(x):return str(Decimal(str(x)).quantize(Decimal('0.001'),rounding=ROUND_HALF_UP)).replace('.',',')

def rows_between(text,from_id,to_id):
    start=text.index('Таблица '+str(from_id)+' —')
    end=text.index('Таблица '+str(to_id)+' —',start) if to_id else text.index('## Значение для R1 и R2',start)
    values=[s for s in text[start:end].splitlines() if s.startswith('|')]
    return [[c.strip() for c in s.strip().strip('|').split('|')] for s in values[2:]]

def check():
    t=DOC.read_text(encoding='utf-8');src=json.loads(SOURCE.read_text(encoding='utf-8'))
    one=rows_between(t,1,2)
    two=rows_between(t,2,3)
    three=rows_between(t,3,None)
    if len(one)!=4 or len(two)!=3 or len(three)!=2:
        raise AssertionError('Unexpected scientific table row counts')
    modes=['baseline','refresh','evict','modern']
    for mode,row in zip(modes,one):
        for col,n in [(1,50000),(2,1000000)]:
            expected=f(src['bySize'][str(n)]['medianMs'][mode]['lookup_ms'])
            if row[col]!=expected:raise AssertionError('Medians mismatch '+mode+' '+str(n))
    modes=['evict_over_baseline','refresh_over_baseline','baseline_over_modern']
    for name,row in zip(modes,two):
        for col,n in [(1,50000),(2,1000000)]:
            v=src['bySize'][str(n)]['paired'][name]['lookup_ms']
            expected=f(v['geomean'])+' ['+f(v['ci95'][0])+'; '+f(v['ci95'][1])+']'
            normalized=row[col].replace('**','')
            if normalized!=expected:raise AssertionError('Paired ratio mismatch '+name+' '+str(n)+': '+normalized+' vs '+expected)
    for row,n in zip(three,[50000,1000000]):
        v=src['bySize'][str(n)]['paired']['evict_over_baseline']['first100_ms']
        if (row[1].replace('**','')!=f(v['geomean']) or
            row[2]!='['+f(v['ci95'][0])+'; '+f(v['ci95'][1])+']' or
            row[3]!=str(v['pairsAbove1'])+'/20'):
            raise AssertionError('Cold first100 mismatch '+str(n))
    print('PASS: 3 R2 scientific tables, 4 median rows, 6 paired ratio rows, 2 early-query rows verified')

if __name__=='__main__':check()
