#!/bin/sh
# POSIX convenience entrypoint; Windows users invoke the .py file directly.
set -eu
script_dir=$(CDPATH= cd "$(dirname "$0")" && pwd)
if [ "$#" -eq 1 ] && [ "$1" = "3" ]; then
    exec python3 "$script_dir/run-index-evolution-direct.py" --mode full
fi
exec python3 "$script_dir/run-index-evolution-direct.py" "$@"
