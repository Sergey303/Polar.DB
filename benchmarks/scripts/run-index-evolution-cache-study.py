#!/usr/bin/env python3
"""Preregistered 20-block x 2 sizes x 4 modes study, only on Linux/ext4."""
from __future__ import annotations
import argparse,csv,json,os,platform,shutil,subprocess,sys,tempfile,time
from pathlib import Path
from datetime import datetime,timezone
from index_evolution_support import OLD,NEW,write_snapshot,file_sha256,disk_guard,require_tools,logged

ROOT=Path(__file__).resolve().parents[2]
SUITE='IndexEvolutionCacheDirect'
FILES=(SUITE+'.csproj','Program.cs','IndexAdapter.cs','LegacyAdapter.cs','ModernAdapter.cs')
SCHEDULE=Path(__file__).with_name('index-evolution-cache-schedule.json')
CONTROLLER=Path(__file__).with_name('index-evolution-cache-pages.py')
PROTOCOL=ROOT/'benchmarks/docs/INDEX_EVOLUTION_CACHE_CONTROL_PROTOCOL_20261109.md'
FIELDS=('sequence','rows','block','position','mode','sha','probes','seed',
        'first100_ms','tail_ms','lookup_ms','load_ms','prerequisite_ms','index_build_ms',
        'checksum','verified','index_bytes','before_resident_fraction',
        'after_resident_fraction','wall_sec')

def save(path,payload):
    temp=path.with_suffix('.tmp')
    temp.write_text(json.dumps(payload,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    temp.replace(path)

def record(log,when,rows,block,seq,pos,wall):
    lines=[s[7:].strip() for s in log.read_text().splitlines() if s.startswith('RESULT ')]
    if len(lines)!=1:raise RuntimeError('Expected exactly one RESULT from '+str(log))
    values=dict(s.split('=',1) for s in lines[0].split())
    if (int(values['rows'])!=rows or values['mode']!=when or
        values['verified']!='true' or values['probes']!='10000' or values['seed']!='777'):
        raise RuntimeError('Wrong run identity in '+str(log))
    engine=NEW if when=='modern' else OLD
    if values['engine']!=engine:raise RuntimeError('Wrong source SHA in '+str(log))
    first=float(values['first100_ms']);tail=float(values['tail_ms']);full=float(values['lookup_ms'])
    if first<=0 or tail<=0 or abs(first+tail-full)>0.005:
        raise RuntimeError('Invalid Stopwatch stages in '+str(log))
    cache=json.loads(values['cache_json'])
    if when!='modern':
        if (cache['filesystem']!='ext4' or cache['file_bytes']!=12*rows+8):
            raise RuntimeError('Wrong index file or filesystem in '+str(log))
        fraction=cache['after_resident_fraction']
        if when=='evict' and fraction>0.1:raise RuntimeError('Failed eviction')
        if when=='refresh' and fraction<0.9:raise RuntimeError('Failed warmup')
        before=cache['before_resident_fraction']
        indexbytes=cache['file_bytes']
    else:indexbytes='';before='';fraction=''
    result={'sequence':seq,'rows':rows,'block':block,'position':pos,'mode':when,
      'sha':engine,'probes':values['probes'],'seed':values['seed'],
      'first100_ms':values['first100_ms'],'tail_ms':values['tail_ms'],
      'lookup_ms':values['lookup_ms'],'load_ms':values['load_ms'],
      'prerequisite_ms':values['prerequisite_ms'],
      'index_build_ms':values['index_build_ms'],'checksum':values['checksum'],
      'verified':values['verified'],'index_bytes':indexbytes,
      'before_resident_fraction':before,'after_resident_fraction':fraction,
      'wall_sec':f'{wall:.6f}'}
    return result

def main():
    args=argparse.ArgumentParser(description=__doc__)
    args.add_argument('--output',type=Path,required=True)
    opts=args.parse_args()
    out=opts.output.resolve()
    if out.exists() or out.is_symlink():raise RuntimeError('Output exists: '+str(out))
    if not str(out).startswith('/root/'):
        raise RuntimeError('Use explicit ext4 /root/ output; never /workspace/9p')
    disk_guard(out,4*2**30)
    require_tools(ROOT)
    raw=json.loads(SCHEDULE.read_text());plan=raw['schedule']
    if (len(plan)!=40 or raw['seed']!=20261109 or raw['probes']!=10000 or raw['query_seed']!=777):
        raise RuntimeError('Schedule mismatch')
    for n in (50000,1000000):
        sample=[p for p in plan if p['rows']==n]
        if len(sample)!=20 or sorted(p['block'] for p in sample)!=list(range(1,21)):
            raise RuntimeError('Incorrect blocks')
        if any(sorted(p['modes'])!=sorted(('baseline','refresh','evict','modern')) for p in sample):
            raise RuntimeError('Invalid mode permutation')
    out.mkdir(parents=True)
    print('EXPERIMENT_OUTPUT',out,flush=True)
    manifest={'status':'running','completedBlocks':0,'acceptedProcesses':0,'plannedBlocks':40,
        'plannedProcesses':160,'protocolSha256':file_sha256(PROTOCOL),
        'scheduleSha256':file_sha256(SCHEDULE),'controllerSha256':file_sha256(CONTROLLER),
        'runnerSha256':file_sha256(Path(__file__)),
        'sourceSha256':{f:file_sha256(ROOT/'benchmarks/src'/SUITE/f) for f in FILES},
        'sourceSnapshots':{'old':OLD,'new':NEW},'gitHead':subprocess.check_output(
        ['git','-C',str(ROOT),'rev-parse','HEAD'],text=True).strip(),
        'system':{'platform':platform.platform(),'kernel':platform.release(),
                  'cpus':os.cpu_count()},'startedUtc':datetime.now(timezone.utc).isoformat()}
    save(out/'manifest.json',manifest)
    with (out/'results.csv').open('w',newline='',encoding='utf-8') as f:
        csv.DictWriter(f,fieldnames=FIELDS).writeheader()
    try:
        dlls={}
        for engine,sha in (('old',OLD),('new',NEW)):
            checkout=out/'_build'/engine
            write_snapshot(ROOT,sha,checkout,legacy=engine=='old',suite=SUITE,files=FILES)
            (checkout/'global.json').write_text(json.dumps({'sdk':{'version':'10.0.112','rollForward':'latestPatch'}}))
            project=checkout/'benchmarks/src'/SUITE/(SUITE+'.csproj')
            extra=['-p:DefineConstants=LEGACY2021'] if engine=='old' else ['-p:TargetFrameworks=net10.0']
            logged(['dotnet','build',str(project),'-c','Release','-v','quiet','--nologo',
                    '-p:GeneratePackageOnBuild=false',*extra],out/f'build-{engine}.log',checkout,600)
            dll=project.parent/'bin/Release/net10.0'/(SUITE+'.dll')
            if not dll.is_file():raise RuntimeError('Build missing '+str(dll))
            dlls[engine]=dll
            print('BUILT',engine,flush=True)
        for seq,pair in enumerate(plan,1):
            rows=[]
            for position,mode in enumerate(pair['modes'],1):
                disk_guard(out,4*2**30)
                engine='new' if mode=='modern' else 'old'
                log=out/f'measure-{seq:02}-{pair["rows"]}-{pair["block"]}-{mode}.log'
                started=time.monotonic()
                with tempfile.TemporaryDirectory(prefix='_db_',dir=out) as dbroot:
                    logged(['dotnet',str(dlls[engine]),str(Path(dbroot)/'database'),
                            str(pair['rows']),'10000','777',mode,str(CONTROLLER)],
                           log,dlls[engine].parent,240,
                           {**os.environ,'INDEX_EVOLUTION_SHA':NEW if engine=='new' else OLD})
                rec=record(log,mode,pair['rows'],pair['block'],seq,position,time.monotonic()-started)
                rows.append(rec)
            if len({r['checksum'] for r in rows})!=1:
                raise RuntimeError('Cross-mode checksum mismatch, block '+str(seq))
            with (out/'results.csv').open('a',newline='',encoding='utf-8') as f:
                csv.DictWriter(f,fieldnames=FIELDS).writerows(rows)
            manifest['completedBlocks']=seq;manifest['acceptedProcesses']=seq*4
            save(out/'manifest.json',manifest)
            print(f'PASS block={seq:02}/40 rows={pair["rows"]} order={",".join(pair["modes"])}',flush=True)
        manifest['resultsSha256']=file_sha256(out/'results.csv')
        manifest['status']='complete';manifest['finishedUtc']=datetime.now(timezone.utc).isoformat()
        save(out/'manifest.json',manifest)
        print('COMPLETE 40/40 blocks; 160/160 processes',flush=True)
    except Exception as error:
        manifest['status']='failed';manifest['error']=str(error)
        save(out/'manifest.json',manifest)
        print('FAILED',str(error),file=sys.stderr)
        raise

if __name__=='__main__':
    try:main()
    except (ValueError,RuntimeError,OSError) as e:print('ERROR',e,file=sys.stderr);sys.exit(2)
