#!/usr/bin/env python3
"""Inspect or evict only the old index file on Linux/ext4; do not drop global cache."""
from __future__ import annotations
import ctypes
import json
import mmap
import os
from pathlib import Path
import sys

libc=ctypes.CDLL(None,use_errno=True)
libc.mmap.argtypes=(ctypes.c_void_p,ctypes.c_size_t,ctypes.c_int,ctypes.c_int,ctypes.c_int,ctypes.c_long)
libc.mmap.restype=ctypes.c_void_p
libc.mincore.argtypes=(ctypes.c_void_p,ctypes.c_size_t,ctypes.c_void_p)
libc.mincore.restype=ctypes.c_int
libc.munmap.argtypes=(ctypes.c_void_p,ctypes.c_size_t)
libc.munmap.restype=ctypes.c_int

def cached(fd):
    size=os.fstat(fd).st_size
    if size<=0:raise RuntimeError('Empty index')
    pages=(size+os.sysconf('SC_PAGE_SIZE')-1)//os.sysconf('SC_PAGE_SIZE')
    addr=libc.mmap(None,size,0,mmap.MAP_PRIVATE,fd,0)
    if addr==ctypes.c_void_p(-1).value:raise OSError(ctypes.get_errno(),'mmap failed')
    try:
        vec=(ctypes.c_ubyte*pages)()
        if libc.mincore(addr,size,vec)!=0:raise OSError(ctypes.get_errno(),'mincore failed')
        return sum(1 for b in vec if b&1),pages,size
    finally:
        if libc.munmap(addr,size)!=0:raise OSError(ctypes.get_errno(),'munmap failed')

def filesystem(p):
    candidates=[]
    for line in Path('/proc/self/mountinfo').read_text().splitlines():
        pair=line.split(' - ',1)
        if len(pair)!=2:continue
        a,b=pair[0].split(),pair[1].split()
        if len(a)<5 or not b:continue
        point=a[4].replace('\\040',' ')
        if str(p)==point or str(p).startswith(point.rstrip('/')+'/'):
            candidates.append((len(point),b[0]))
    if not candidates:raise RuntimeError('Could not determine filesystem')
    return max(candidates)[1]

def main():
    if len(sys.argv)!=3 or sys.argv[2] not in ('inspect','evict'):
        raise ValueError('Usage: controller.py path inspect|evict')
    path=Path(sys.argv[1]).resolve()
    if not path.is_file():raise FileNotFoundError(path)
    fs=filesystem(path)
    if fs!='ext4':raise RuntimeError('Data must be on ext4, found '+fs)
    fd=os.open(path,os.O_RDONLY)
    try:
        before=cached(fd)
        if sys.argv[2]=='evict':
            os.fsync(fd)
            os.posix_fadvise(fd,0,0,os.POSIX_FADV_DONTNEED)
        after=cached(fd)
    finally:os.close(fd)
    print(json.dumps({'filesystem':fs,'file_bytes':after[2],
        'pages_total':after[1],'before_resident_pages':before[0],
        'after_resident_pages':after[0],
        'before_resident_fraction':round(before[0]/before[1],6),
        'after_resident_fraction':round(after[0]/after[1],6)},
        sort_keys=True,separators=(',',':')))

if __name__=='__main__':
    try:main()
    except Exception as e:print(type(e).__name__,str(e),file=sys.stderr);sys.exit(2)
