#!/usr/bin/env python3
"""Publish verified 20-pair evidence without modifying the source run or past data."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import sys
import tarfile

from importlib.util import module_from_spec, spec_from_file_location

ANALYZER = Path(__file__).with_name('analyze-index-evolution-paired20.py')
_spec=spec_from_file_location('paired_analysis',ANALYZER)
_module=module_from_spec(_spec)
_spec.loader.exec_module(_module)
analyze=_module.analyze


def sha256(path: Path) -> str:
    h=hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda:f.read(1<<20),b''): h.update(block)
    return h.hexdigest()


def package(source: Path,target: Path) -> None:
    if target.exists():
        raise RuntimeError('Refusing to overwrite publication directory '+str(target))
    result=analyze(source)
    logs=sorted(source.glob('measure-*.log'))
    if len(logs)!=80: raise RuntimeError('Expected exactly 80 raw process logs')
    builds=sorted(source.glob('build-*.log'))
    if len(builds)!=2: raise RuntimeError('Expected both build logs')
    target.mkdir(parents=True)
    for name in ('results.csv','invocations.csv','manifest.json'):
        shutil.copyfile(source/name,target/name)
    with tarfile.open(target/'measurement-logs.tar.gz',mode='w:gz') as archive:
        for path in logs:
            archive.add(path,arcname=path.name,recursive=False)
    with tarfile.open(target/'build-logs.tar.gz',mode='w:gz') as archive:
        for path in builds:
            archive.add(path,arcname=path.name,recursive=False)
    # Validate again against the packaged form, including its archived logs.
    repackaged=analyze(target)
    if repackaged!=result:raise RuntimeError('Packaged evidence differs from source run')
    (target/'summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    files=sorted(x for x in target.iterdir() if x.is_file())
    (target/'SHA256SUMS').write_text(''.join(sha256(x)+'  '+x.name+'\n' for x in files),encoding='utf-8')
    print('PUBLISHED',target,'files',len(files)+1,'pairs',result['status'],flush=True)
    for size,v in result['bySize'].items():
        print('SIZE',size,'RATIO',round(v['ratioGeomean'],5),'CI',v['bootstrap95'],flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source',type=Path)
    parser.add_argument('target',type=Path)
    args=parser.parse_args()
    try:package(args.source,args.target)
    except (OSError,RuntimeError,ValueError,KeyError) as error:
        print('PUBLISH FAILED:',str(error),file=sys.stderr);sys.exit(2)
