#!/usr/bin/env python3
"""Check every numeric data cell of article tables against the pinned final-series archive."""
from pathlib import Path
from collections import defaultdict
from statistics import median
import re,csv,json,math
ROOT=Path(__file__).resolve().parents[2]
RESULTS=ROOT/'benchmarks/results/final-series/20261104T075733840Z-abbb03c-ext4/results'
ARTICLE=(ROOT/'benchmarks/docs/POLAR_DB_ARTICLE_DRAFT.md').read_text()
cache={};count=0
C='Cold after reopen'; H='Hot after file and lookup warmup'
def values(name,n,engine,metric,phase=''):
    if name not in cache:
        a=defaultdict(list)
        for r in csv.DictReader((RESULTS/(name+'.raw.csv')).open()):
            a[(int(r['setup_rows']),r['engine'],r['phase'],r['metric'])].append(float(r['value']))
        cache[name]=a
    v=cache[name][(n,'polar-db-current' if engine=='p' else 'sqlite',phase,metric)]
    assert v,(name,n,engine,phase,metric)
    return v
def md(name,n,engine,metric,phase=''):return median(values(name,n,engine,metric,phase))
def perc(v):
    v=sorted(v);i=(len(v)-1)*.95;j=math.floor(i)
    return v[j]*(1-(i-j))+v[min(j+1,len(v)-1)]*(i-j)
def tab(n):
    m=re.search(rf'^Таблица {n} —[^\n]*\n\n((?:\|[^\n]+\|\n)+)',ARTICLE,re.M)
    assert m, n
    ls=m.group(1).splitlines()
    return [[c.strip().strip('`') for c in line.strip('|').split('|')] for line in ls[2:]]
def check(cell,actual):
    global count
    s=cell.replace(' ','').replace(',','.')
    number=float(s)
    digits=len(s.split('.',1)[1]) if '.' in s else 0
    tol=0.501*10**(-digits)
    assert math.isfinite(actual) and abs(number-actual)<=tol,(cell,actual,number,tol)
    count+=1
def base(n):return int(n.replace(' ',''))
for no in (1,11):
    n=5000000 if no==1 else 50000
    for row in tab(no):
        key,mode=row[0],row[1];ph=C if mode=='без специального разогрева' else H
        name='pk-'+key.lower()+'-lookup'
        if no==11:
            for col,(engine,phase) in enumerate([('p',C),('s',C),('p',H),('s',H)],1):
                check(row[col],md(name,n,engine,'lookup-batch-average',phase)*1000)
        else:
            for col,eng in [(2,'p'),(3,'s')]: check(row[col],md(name,n,eng,'lookup-batch-average',ph)*1000)
            for col,eng in [(4,'p'),(5,'s')]:
                a,b=row[col].split('/')
                v=values(name,n,eng,'lookup-latency',ph)
                check(a,median(v)*1000);check(b,perc(v)*1000)
for no in (2,3,12,13):
    n=5000000 if no<11 else 50000
    pre='external-famous-' if no in (3,13) else 'external-'
    scale=.001 if no==3 else 1
    for row in tab(no):
        name=pre+row[0].lower()+'-lookup'
        for col,(eng,ph) in enumerate([('p',C),('s',C),('p',H),('s',H)],1):
            check(row[col],md(name,n,eng,'lookup-batch-average',ph)*scale)
for row in tab(4):
    n=base(row[0]);name='build-primary-int-only-id-only-experiment'
    check(row[1],md(name,n,'p','build'));check(row[2],md(name,n,'s','build'))
    check(row[3],md(name,n,'p','build-allocated-bytes')/1e6)
    assert row[4]=='/'.join(str(round(md(name,n,'p','build-gen'+str(i)+'-collections'))) for i in range(3))
    j=json.loads((RESULTS/(name+'.raw.json')).read_text())
    r=next(x for x in j['lifecycleRuns'] if x['setupRows']==n)
    e=next(x for x in r['engines'] if x['engine']=='polar-db-current')
    check(row[5],(e['resourcesAfter']['managedBytes']-e['resourcesBefore']['managedBytes'])/1e6)
for row in tab(5):
    n=base(row[0]);name='build-external-indexes-only'
    for col,eng,metric,scale in [(1,'p','build',1),(2,'s','build',1),(3,'p','external indexes build + flush',1),(4,'s','external indexes build + flush',1),(5,'p','build-allocated-bytes',1e-9)]:
        check(row[col],md(name,n,eng,metric)*scale)
    assert row[6]=='/'.join(str(round(md(name,n,'p','build-gen'+str(i)+'-collections'))) for i in range(3))
for row in tab(6):
    n=base(row[0])
    for col,name,eng,metric in [(1,'traversal-only','p','full logical traversal'),(2,'traversal-only','s','full logical traversal'),(3,'reopen-with-tail','p','fixed-int query-ready reopen with dynamic tail'),(4,'reopen-with-tail','s','fixed-int query-ready reopen with appended tail')]:
        check(row[col],md(name,n,eng,metric))
for row in tab(7):
    n=base(row[0]);name='reopen-only'
    for col,eng,metric in [(1,'p','open-only'),(2,'s','open-only'),(3,'p','query-ready reopen'),(4,'s','query-ready reopen')]:
        check(row[col],md(name,n,eng,metric))
for row in tab(9):
    n=base(row[0]);j=json.loads((RESULTS/'reopen-only.raw.json').read_text())
    r=next(x for x in j['lifecycleRuns'] if x['setupRows']==n)
    e=next(x for x in r['engines'] if x['engine']=='polar-db-current')
    bef=e['resourcesBefore'];aft=e['resourcesAfter']
    for col,value in [(1,aft['managedBytes']/1e9),(2,(aft['managedBytes']-bef['managedBytes'])/1e9),(3,aft['privateBytes']/1e9),(4,aft['workingSetBytes']/1e9)]:check(row[col],value)
for row in tab(10):
    kind='append-only' if row[0]=='добавление' else 'delete-only';n=base(row[1])
    for col,eng,metric in [(2,'p','volatile mutation'),(3,'s','volatile mutation'),(4,'p','durable-per-operation'),(5,'s','durable-per-operation')]:
        check(row[col],md(kind,n,eng,metric))
# Diagnostics in table 8 come from paired iterations in the ReopenOnly worker.
STAGES = [
 'reopen/raw-file-handles/open-close',
 'reopen/constructor-open-only/construct-external-3',
 'reopen/query-ready/construct-external-3',
 'reopen/query-ready/refresh-primary',
 'reopen/query-ready/refresh-external-0',
 'reopen/query-ready/refresh-external-1',
 'reopen/query-ready/refresh-external-2',
 'reopen/query-ready/refresh-external-3',
 'reopen/query-ready/first-primary-query-and-oracle',
]
assert len(tab(8))==len(STAGES)
for row,metric in zip(tab(8),STAGES):
    for n,cell in [(50000,row[1]),(5000000,row[2])]:
        check(cell,md('reopen-only',n,'p',metric))

# The 88.7% claim is the median of fractions paired by sample_index.
records=list(csv.DictReader((RESULTS/'reopen-only.raw.csv').open()))
for n,expected in ((50000,.754),(5000000,.887)):
    subset={(r['metric'],int(r['sample_index'])):float(r['value']) for r in records
            if r['engine']=='polar-db-current' and int(r['setup_rows'])==n}
    parts=('reopen/query-ready/construct-external-3',
           'reopen/query-ready/refresh-external-3')
    ratios=[sum(subset[(metric,i)] for metric in parts)/subset[('query-ready reopen',i)]
            for i in range(30)]
    assert abs(median(ratios)-expected)<.0006

print('OK: all 13 tables,',count,'numeric cells match archived CSV/JSON within stated rounding precision; paired reopen share checks pass')
