#!/usr/bin/env bash
# One-pass, small-data experiment using the SAME measurement source on both versions.
set -euo pipefail
repo=$(git rev-parse --show-toplevel)
old=39cefe13db8697cf5f8e15edaa4677ba52a2cdea
new=09ec0703f476f25ec79baff338843df8a8e83b35
rows=${1:-10000}; probes=${2:-1000}; seed=${3:-777}
[[ "$rows" =~ ^[0-9]+$ && "$probes" =~ ^[0-9]+$ && "$seed" =~ ^-?[0-9]+$ ]]
((rows >= 2 && rows <= 100000 && probes >= 1 && probes <= 100000)) || {
    echo 'Safety limit: 2..100000 rows, 1..100000 probes' >&2; exit 2;
}
[[ $(df -B1 --output=avail /workspace | tail -1) -ge 2147483648 ]] || {
    echo 'Refusing to run: Windows D: has less than 2 GiB free' >&2; exit 75;
}
dotnet --list-sdks | grep -q '^10\.' || { echo '.NET 10 SDK required' >&2;exit 2; }
sandbox=$(mktemp -d /root/polar2021-2026.XXXXXXXX)
for version in old new; do
    if [[ "$version" == old ]]; then sha=$old;else sha=$new;fi
    mkdir -p "$sandbox/$version/benchmarks/src/Legacy2021Replay"
    if [[ "$version" == old ]]; then
        git -C "$repo" archive "$sha" src/Polar.DB | tar -xf - -C "$sandbox/$version"
        # Target-framework-only adaptation, not a code modification.
        python3 - "$sandbox/$version/src/Polar.DB/Polar.DB.csproj" <<'PY'
from pathlib import Path
import sys
p=Path(sys.argv[1]);s=p.read_text();needle='<TargetFramework>netstandard2.0</TargetFramework>'
assert s.count(needle)==1
p.write_text(s.replace(needle,'<TargetFramework>net10.0</TargetFramework>'))
PY
    else
        git -C "$repo" archive "$sha" src/Polar.DB README.md LICENSE icon.png | tar -xf - -C "$sandbox/$version"
    fi
    cp "$repo/benchmarks/src/Legacy2021Replay/"*.cs "$sandbox/$version/benchmarks/src/Legacy2021Replay/"
    cp "$repo/benchmarks/src/Legacy2021Replay/Legacy2021Replay.csproj" \
        "$sandbox/$version/benchmarks/src/Legacy2021Replay/"
    pushd "$sandbox/$version" >/dev/null
    buildlog="$sandbox/${version}-build.log"
    if ! dotnet build "$sandbox/$version/benchmarks/src/Legacy2021Replay/Legacy2021Replay.csproj" \
        -c Release --nologo -v quiet -p:GeneratePackageOnBuild=false >"$buildlog" 2>&1; then
        tail -n 35 "$buildlog" >&2
        exit 1
    fi
    echo "$version: Release build succeeded (full log: $buildlog)"
    echo "=== $version: $sha ==="
    log="$sandbox/${version}-index.log"
    REPLAY_ENGINE_SHA="$sha" dotnet \
        "$sandbox/$version/benchmarks/src/Legacy2021Replay/bin/Release/net10.0/Legacy2021Replay.dll" \
        index "$sandbox/${version}-index" "$rows" "$probes" "$seed" | tee "$log"
    grep -qx "verified_lookup_count=$probes" "$log" || {
        echo "Missing verified result for $version" >&2;exit 72;
    }
    popd >/dev/null
done
printf 'Shared .NET Runtime: ';dotnet --list-runtimes | grep '^Microsoft.NETCore.App 10\.' | head -1
printf 'Evidence directory: %s\n' "$sandbox"
printf 'These are one-pass smoke timings, not proof of an algorithmic speedup.\n'
