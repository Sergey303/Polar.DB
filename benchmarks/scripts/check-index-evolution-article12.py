#!/usr/bin/env python3
"""Recompute every value of manuscript Table 12 from 4 sealed datasets."""
from __future__ import annotations

import json
from pathlib import Path
import re

ROOT=Path(__file__).resolve().parents[2]
ARTICLE=ROOT/'benchmarks/docs/POLAR_DB_ARTICLE_DRAFT.md'
LOCAL=ROOT/'benchmarks/evidence/20261109-index-evolution-paired20/summary.json'
HOSTED=ROOT/'benchmarks/evidence/20261109-index-evolution-hosted-42573634646'


def f(n):
    return f'{n:.3f}'.replace('.',',')


def check():
    text=ARTICLE.read_text(encoding='utf-8')
    match=re.search(r'^Таблица 12 —[^\n]*\n\n((?:\|[^\n]*\n)+)',text,re.M)
    if not match:
        raise AssertionError('Table 12 not found')
    rows=[[cell.strip() for cell in line.strip().strip('|').split('|')]
          for line in match.group(1).splitlines()]
    if len(rows)!=10 or any(len(r)!=5 for r in rows):
        raise AssertionError(f'Expected 5 columns, 1 header, 1 separator, 8 values: {len(rows)}')
    sources=[('RDC / WSL2',LOCAL),('GitHub Ubuntu / x64',HOSTED/'ubuntu/summary.json'),
             ('GitHub Windows / x64',HOSTED/'windows/summary.json'),
             ('GitHub macOS / ARM64',HOSTED/'macos/summary.json')]
    expected=[]
    for name,path in sources:
        data=json.loads(path.read_text(encoding='utf-8'))
        if data['status']!='complete':raise AssertionError('Incomplete source '+str(path))
        for size in (50_000,1_000_000):
            block=data['bySize'][str(size)]
            if block['newFasterPairs']+block['oldFasterPairs']+block['equalPairs']!=20:
                raise AssertionError('Incorrect pair count '+str(path))
            expected.append([name,f'{size:,}'.replace(',',' '),f(block['ratioGeomean']),
                             f(block['bootstrap95'][0])+'–'+f(block['bootstrap95'][1]),
                             str(block['newFasterPairs'])+'/20'])
    if rows[2:]!=expected:
        for i,(actual,wanted) in enumerate(zip(rows[2:],expected),1):
            if actual!=wanted:
                raise AssertionError(f'Mismatch row {i}: article={actual}, source={wanted}')
        raise AssertionError('Missing/duplicate rows in Table 12')
    print('PASS: Table 12 all 8 rows x 5 columns exactly match 4 independent evidence summaries')


if __name__=='__main__':check()
