#!/usr/bin/env python3
"""Archive verified R2 cache-control data with byte-exact provenance."""
from __future__ import annotations
import argparse,hashlib,json,shutil,sys,tarfile
from importlib.util import spec_from_file_location,module_from_spec
from pathlib import Path
SCRIPTS=Path(__file__).resolve().parent
spec=spec_from_file_location('cache_r2_analysis',SCRIPTS/'analyze-index-evolution-cache-study.py')
module=module_from_spec(spec)
spec.loader.exec_module(module)

def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as file:
        for block in iter(lambda:file.read(1<<20),b''):h.update(block)
    return h.hexdigest()

def publish(source,dest):
    if dest.exists() or dest.is_symlink():raise RuntimeError('Destination exists: '+str(dest))
    original=module.analyze(source)
    logs=sorted(source.glob('measure-*.log'))
    if len(logs)!=160:raise RuntimeError(f'Expected 160 logs, got {len(logs)}')
    builds=sorted(source.glob('build-*.log'))
    if len(builds)!=2:raise RuntimeError('Expected both build logs')
    dest.mkdir(parents=True)
    for name in ('manifest.json','results.csv'):
        shutil.copyfile(source/name,dest/name)
    for filename,files in [('measurement-logs.tar.gz',logs),('build-logs.tar.gz',builds)]:
        with tarfile.open(dest/filename,'w:gz') as archive:
            for path in files:archive.add(path,arcname=path.name,recursive=False)
    if module.analyze(dest)!=original:
        raise RuntimeError('New archive does not match source measurements')
    (dest/'summary.json').write_text(json.dumps(original,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    files=sorted(p for p in dest.iterdir() if p.is_file())
    (dest/'SHA256SUMS').write_text(''.join(sha(p)+'  '+p.name+'\n' for p in files),encoding='utf-8')
    print('SEALED',dest,'PROCESS_LOGS',len(logs),'SHA_RESULTS',sha(dest/'results.csv'))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('source',type=Path)
    p.add_argument('dest',type=Path)
    args=p.parse_args()
    try:publish(args.source,args.dest)
    except Exception as exc:print('ERROR',exc,file=sys.stderr);sys.exit(2)
