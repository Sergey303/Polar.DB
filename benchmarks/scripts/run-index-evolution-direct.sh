#!/usr/bin/env bash
# Pin source, build both versions with one measurement driver, run small paired trials.
set -euo pipefail
repo=$(git rev-parse --show-toplevel)
old=39cefe13db8697cf5f8e15edaa4677ba52a2cdea
new=09ec0703f476f25ec79baff338843df8a8e83b35
pairs=${1:-2}
[[ "$pairs" =~ ^[0-9]+$ ]] && (( pairs >= 1 && pairs <= 3 )) || {
    echo 'Allowed paired blocks: 1..3' >&2; exit 2;
}
dotnet --list-sdks | grep -q '^10\.' || { echo 'Need .NET 10 SDK' >&2; exit 2; }
# Conservatively require 2 GiB available on the physical volume before each run.
check_disk() {
    free=$(df -B1 --output=avail /workspace | tail -1)
    (( free >= 2147483648 )) || { echo 'Disk guard: fewer than 2 GiB on D:' >&2; exit 75; }
}
check_disk
sandbox=$(mktemp -d /root/index-evolution-2021-2026.XXXXXXXX)
printf 'EXPERIMENT_DIR=%s\n' "$sandbox"
mkdir -p "$sandbox/old/benchmarks/src/IndexEvolutionDirect" \
         "$sandbox/new/benchmarks/src/IndexEvolutionDirect"
for label in old new; do
    if [[ "$label" == old ]]; then sha=$old; else sha=$new; fi
    if [[ "$label" == old ]]; then
        git -C "$repo" archive "$sha" src/Polar.DB | tar -xf - -C "$sandbox/$label"
        python3 - "$sandbox/$label/src/Polar.DB/Polar.DB.csproj" <<'PY'
import sys
from pathlib import Path
p=Path(sys.argv[1]);s=p.read_text();needle='<TargetFramework>netstandard2.0</TargetFramework>'
assert s.count(needle)==1
p.write_text(s.replace(needle, '<TargetFramework>net10.0</TargetFramework>'))
PY
    else
        git -C "$repo" archive "$sha" src/Polar.DB README.md LICENSE icon.png | tar -xf - -C "$sandbox/$label"
    fi
    cp "$repo/benchmarks/src/IndexEvolutionDirect/"*.cs* "$sandbox/$label/benchmarks/src/IndexEvolutionDirect/"
    target="$sandbox/$label/benchmarks/src/IndexEvolutionDirect/IndexEvolutionDirect.csproj"
    pushd "$sandbox/$label" >/dev/null
    if [[ "$label" == old ]]; then
        properties=(-p:DefineConstants=LEGACY2021)
    else
        properties=(-p:TargetFrameworks=net10.0)
    fi
    if ! dotnet build "$target" -c Release -v quiet --nologo \
        -p:GeneratePackageOnBuild=false "${properties[@]}" \
        > "$sandbox/$label-build.log" 2>&1; then
        echo "Build failed for $label:" >&2
        tail -70 "$sandbox/$label-build.log" >&2
        exit 1
    fi
    popd >/dev/null
    echo "COMPILED $label $sha"
done
printf 'block,engine,sha,rows,probes,seed,load_ms,prerequisite_ms,index_build_ms,lookup_ms,checksum,verified\n' > "$sandbox/results.csv"
for rows in 50000 1000000; do
    for ((block=1; block<=pairs; block++)); do
        if (( block%2 == 1 )); then order=(old new); else order=(new old); fi
        for label in "${order[@]}"; do
            check_disk
            if [[ "$label" == old ]]; then sha=$old;else sha=$new;fi
            dir="$sandbox/d-$rows-$block-$label"
            log="$sandbox/measurement-$rows-$block-$label.log"
            INDEX_EVOLUTION_SHA="$sha" dotnet \
                "$sandbox/$label/benchmarks/src/IndexEvolutionDirect/bin/Release/net10.0/IndexEvolutionDirect.dll" \
                "$dir" "$rows" 1000 777 > "$log" 2>&1 || {
                    cat "$log" >&2;echo "FAILED $label rows=$rows block=$block" >&2;exit 1;
                }
            # Reject incomplete or spurious success, record only validated results.
            python3 - "$log" "$sandbox/results.csv" "$block" "$label" "$rows" <<'PY'
import sys,re,csv
from pathlib import Path
log,output,block,engine,rows=sys.argv[1:]
lines=[line for line in Path(log).read_text().splitlines() if line.startswith('RESULT ')]
assert len(lines)==1,(log,lines)
fields=dict(token.split('=',1) for token in lines[0][7:].split())
assert fields['verified']=='true' and int(fields['rows'])==int(rows) and int(fields['probes'])==1000
with open(output,'a',newline='') as out:
    csv.writer(out).writerow([block,engine,fields['engine'],rows,1000,fields['seed'],
        fields['load_ms'],fields['prerequisite_ms'],fields['index_build_ms'],
        fields['lookup_ms'],fields['checksum'],fields['verified']])
print('COMPLETE',engine,'rows='+rows,'block='+block, 'build_ms='+fields['index_build_ms'],
      'lookup_ms='+fields['lookup_ms'], 'checksum='+fields['checksum'])
PY
            # Remove only the current script-created data directory, preserving logs.
            rm -rf -- "$dir"
        done
    done
done
printf 'DOTNET_RUNTIME: ';dotnet --list-runtimes | grep '^Microsoft.NETCore.App 10\.' | head -1
printf 'EXPERIMENT_DIR=%s\n' "$sandbox"
printf 'RESULTS_CSV=%s\n' "$sandbox/results.csv"
