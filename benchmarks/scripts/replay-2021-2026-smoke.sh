#!/bin/sh
# Portable POSIX entrypoint; Windows: python benchmarks/scripts/run-legacy2021-replay.py
set -eu
script_dir=$(CDPATH= cd "$(dirname "$0")" && pwd)
exec python3 "$script_dir/run-legacy2021-replay.py" "$@"
