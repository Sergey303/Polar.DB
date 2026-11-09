#!/usr/bin/env python3
"""Verify all 160 original runs and compute paired R2 cache-control statistics."""
from __future__ import annotations
import argparse,csv,hashlib,json,math,random,statistics,sys,tarfile
from pathlib import Path
from index_evolution_support import OLD,NEW,file_sha256
ROOT=Path(__file__).resolve().parents[2]
SCHEDULE=Path(__file__).with_name('index-evolution-cache-schedule.json')
FIELDS=('sequence','rows','block','position','mode','sha','probes','seed','first100_ms','tail_ms','lookup_ms','load_ms','prerequisite_ms','index_build_ms','checksum','verified','index_bytes','before_resident_fraction','after_resident_fraction','wall_sec')
MEASURES=('first100_ms','tail_ms','lookup_ms')
MODES=('baseline','refresh','evict','modern')

def q(values,p):
    arr=sorted(values);ind=(len(arr)-1)*p;i=int(ind);h=ind-i
    return arr[i]*(1-h)+arr[min(i+1,len(arr)-1)]*h

def ratio(a,b,size,metric):
    pairs=[math.log(float(a[i][metric])/float(b[i][metric])) for i in range(20)]
    rnd=random.Random(20261109+size+sum(ord(x) for x in metric)+sum(ord(x) for x in a[0]['mode']))
    boots=[]
    for _ in range(20000):
        boots.append(math.exp(sum(pairs[rnd.randrange(20)] for _ in range(20))/20))
    return {'geomean':math.exp(statistics.mean(pairs)),
            'ci95':[q(boots,.025),q(boots,.975)],
            'medianPairRatio':statistics.median(math.exp(x) for x in pairs),
            'pairsAbove1':sum(x>0 for x in pairs),'pairsBelow1':sum(x<0 for x in pairs),
            'rawRatios':[math.exp(x) for x in pairs]}

def analyze(directory):
    manifest=json.loads((directory/'manifest.json').read_text())
    schedule=json.loads(SCHEDULE.read_text())['schedule']
    if (manifest['status']!='complete' or manifest['completedBlocks']!=40 or
            manifest['acceptedProcesses']!=160 or manifest['plannedProcesses']!=160):
        raise RuntimeError('Incomplete experiment')
    if manifest['scheduleSha256']!=file_sha256(SCHEDULE):raise RuntimeError('Schedule SHA mismatch')
    for key,path in (
        ('protocolSha256',ROOT/'benchmarks/docs/INDEX_EVOLUTION_CACHE_CONTROL_PROTOCOL_20261109.md'),
        ('controllerSha256',Path(__file__).with_name('index-evolution-cache-pages.py')),
        ('runnerSha256',Path(__file__).with_name('run-index-evolution-cache-study.py'))):
        if manifest[key]!=file_sha256(path):raise RuntimeError('Pinned '+key+' mismatch')
    for path,hashvalue in manifest['sourceSha256'].items():
        source=ROOT/'benchmarks/src/IndexEvolutionCacheDirect'/path
        if file_sha256(source)!=hashvalue:raise RuntimeError('Adapter source SHA mismatch: '+path)
    if manifest['resultsSha256']!=file_sha256(directory/'results.csv'):
        raise RuntimeError('Results SHA mismatch')
    if manifest['sourceSnapshots']!={'old':OLD,'new':NEW}:raise RuntimeError('Sources drifted')
    with (directory/'results.csv').open(newline='',encoding='utf-8') as f:
        reader=csv.DictReader(f)
        if tuple(reader.fieldnames)!=FIELDS:raise RuntimeError('Wrong field order')
        data=list(reader)
    if len(data)!=160:raise RuntimeError('Expected 160 records')
    archive=directory/'measurement-logs.tar.gz'
    if archive.is_file():
        with tarfile.open(archive,'r:gz') as tar:
            members=tar.getmembers()
            if len(members)!=160 or any(not m.isfile() or '/' in m.name for m in members):
                raise RuntimeError('Wrong or unsafe log archive')
            logs={m.name:tar.extractfile(m).read().decode() for m in members}
            if len(logs)!=160:raise RuntimeError('Duplicate archive member')
    else:
        logs={p.name:p.read_text() for p in directory.glob('measure-*.log')}
    bysize={}
    for seq,plan in enumerate(schedule,1):
        blockdata=data[4*(seq-1):4*seq]
        for pos,(mode,row) in enumerate(zip(plan['modes'],blockdata),1):
            if (int(row['sequence'])!=seq or int(row['rows'])!=plan['rows'] or
                int(row['block'])!=plan['block'] or int(row['position'])!=pos or
                row['mode']!=mode or row['sha']!=(NEW if mode=='modern' else OLD) or
                row['verified']!='true' or row['probes']!='10000' or row['seed']!='777'):
                raise RuntimeError(f'Identity mismatch in block {seq}')
            name=f'measure-{seq:02}-{plan["rows"]}-{plan["block"]}-{mode}.log'
            if name not in logs:raise RuntimeError('Missing log '+name)
            results=[s[7:] for s in logs[name].splitlines() if s.startswith('RESULT ')]
            if len(results)!=1:raise RuntimeError('Wrong RESULT count '+name)
            fields=dict(t.split('=',1) for t in results[0].split())
            for f in ('mode','rows','probes','seed','first100_ms','tail_ms','lookup_ms',
                      'load_ms','prerequisite_ms','index_build_ms','checksum','verified'):
                if fields[f]!=row[f]:raise RuntimeError('Log differs in '+name+' field '+f)
            if row['sha']!=fields['engine']:raise RuntimeError('Source SHA differs '+name)
            if mode!='modern':
                cache=json.loads(fields['cache_json'])
                if str(cache['file_bytes'])!=str(row['index_bytes']):raise RuntimeError('Index size mismatch')
                if str(cache['after_resident_fraction'])!=row['after_resident_fraction']:
                    raise RuntimeError('Cache fraction mismatch')
                if str(cache['before_resident_fraction'])!=row['before_resident_fraction']:
                    raise RuntimeError('Cache pre-resident fraction mismatch')
                after=float(row['after_resident_fraction'])
                if mode=='evict' and after>0.1:raise RuntimeError('Eviction verification failed')
                if mode=='refresh' and after<0.9:raise RuntimeError('Refresh verification failed')
            for m in MEASURES:
                if not (0<float(row[m])<1e8):raise RuntimeError('Invalid positive time')
        if len({r['checksum'] for r in blockdata})!=1:
            raise RuntimeError('Checksum drift in block '+str(seq))
        bysize.setdefault(plan['rows'],{})[plan['block']]={r['mode']:r for r in blockdata}
    result={'status':'complete','sourceSha':manifest['gitHead'],'scheduleSha256':file_sha256(SCHEDULE),
      'resultsSha256':file_sha256(directory/'results.csv'),'host':manifest['system'],
      'blocks':40,'processes':160,'bySize':{}}
    for size,blocks in sorted(bysize.items()):
        assert sorted(blocks)==list(range(1,21))
        ordered={mode:[blocks[i][mode] for i in range(1,21)] for mode in MODES}
        out={'medianMs':{},'cacheBefore':{},'cacheAfter':{},'paired':{}}
        for mode,rows in ordered.items():
            out['medianMs'][mode]={m:statistics.median(float(r[m]) for r in rows) for m in MEASURES}
            if mode!='modern':
                out['cacheBefore'][mode]=[min(float(r['before_resident_fraction']) for r in rows),
                                          max(float(r['before_resident_fraction']) for r in rows)]
                out['cacheAfter'][mode]=[min(float(r['after_resident_fraction']) for r in rows),
                                         max(float(r['after_resident_fraction']) for r in rows)]
        for a,b in [('evict','baseline'),('refresh','baseline'),('baseline','modern'),('refresh','modern')]:
            out['paired'][a+'_over_'+b]={metric:ratio(ordered[a],ordered[b],size,metric) for metric in MEASURES}
        out['indexFileBytes']=int(ordered['baseline'][0]['index_bytes'])
        out['checksum']=ordered['baseline'][0]['checksum']
        result['bySize'][str(size)]=out
    return result

if __name__=='__main__':
    arg=argparse.ArgumentParser(description=__doc__)
    arg.add_argument('directory',type=Path)
    arg.add_argument('--out',type=Path)
    args=arg.parse_args()
    try:
        summary=analyze(args.directory)
        output=json.dumps(summary,indent=2,ensure_ascii=False)+'\n'
        if args.out:args.out.write_text(output,encoding='utf-8')
        print(output)
    except Exception as e:print('ANALYSIS FAILED:',e,file=sys.stderr);sys.exit(2)
