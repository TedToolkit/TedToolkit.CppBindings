#!/usr/bin/env bash
set -euo pipefail

if [[ $# != 1 || $(uname -s) != Linux || $(uname -m) != x86_64 ]]; then
    echo "Usage on Linux x64: bash Build/VerifyManifoldLinux.sh <Manifold 3.5.2 install prefix>" >&2
    exit 2
fi

repository=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)
prefix=$(realpath -e -- "$1")
if [[ ! -f "$repository/.editorconfig" ]]; then
    cp -- "$repository/externals/TedToolkit/.editorconfig" "$repository/.editorconfig"
fi
mkdir -p -- "$repository/output"
work=$(mktemp -d "$repository/output/manifold-linux.XXXXXXXX")
fixtures="$repository/tests/TedToolkit.CppBindings.Manifold.Linux.Tests/Fixtures"
generated="$work/generated"
native_build="$work/native-build"
bridge="$native_build/libted_toolkit_cpp_bindings_manifold.so"
result="$work/consumer-result.json"
printf 'Manifold installation: %s\nVerification output: %s\n' "$prefix" "$work"

unset VCPKG_ROOT CMAKE_TOOLCHAIN_FILE LD_LIBRARY_PATH
dotnet run --project "$fixtures/Generate" -c Release \
    -p:GeneratePackageOnBuild=false -p:NuGetAudit=false -- "$prefix" "$generated"
cmake -S "$generated/cpp" -B "$native_build" -G Ninja -DCMAKE_BUILD_TYPE=Release \
    "-DCPPBINDINGS_MANIFOLD_PREFIX=$prefix" "-DCMAKE_PREFIX_PATH=$prefix"
cmake --build "$native_build" --parallel 2
cat "$native_build/compiler-identity.txt"
printf '\n'

exports=$(nm -D --defined-only "$bridge" | awk '$2 == "T" {print $3}')
if [[ $exports != NativeApi_GetFunctionTable ]]; then
    printf 'Unexpected bridge exports: %s\n' "$exports" >&2
    exit 1
fi

dependencies=$(ldd "$bridge")
manifold_library=$(sed -nE 's/^[[:space:]]*libmanifold\.so[^[:space:]]* => (.*) \(0x[[:xdigit:]]+\)$/\1/p' <<< "$dependencies")
if [[ -z $manifold_library || $(realpath -e -- "$manifold_library") != "$prefix/"* ]]; then
    printf 'The bridge did not resolve libmanifold from the selected installation.\n%s\n' "$dependencies" >&2
    exit 1
fi
printf 'Resolved native dependency: %s\n' "$manifold_library"

dotnet build "$fixtures/Consumer" -c Release -o "$work/consumer" \
    -p:GeneratePackageOnBuild=false -p:NuGetAudit=false "-p:GeneratedRoot=$generated" "-p:NativeBridge=$bridge"
dotnet "$work/consumer/Consumer.dll" "$result"
python3 - "$result" <<'PYTHON'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as stream:
    result = json.load(stream)

expected = {
    "Status": "NoError",
    "TriangleCount": 4,
    "TranslatedStatus": "NoError",
    "UnionStatus": "NoError",
    "UnionTriangleCount": 14,
    "IntersectionStatus": "NoError",
    "IntersectionTriangleCount": 4,
    "DifferenceStatus": "NoError",
    "DifferenceTriangleCount": 8,
    "MeshVertexCoordinateCount": 12,
    "MeshTriangleIndexCount": 12,
    "MeshMinX": 0.125,
    "TetraOraclePassed": True,
    "NonFiniteStatus": "NonFiniteVertex",
    "OutOfRangeStatus": "VertexOutOfBounds",
    "NonManifoldStatus": "NotManifold",
    "InvalidVertexLengthRejected": True,
    "InvalidIndexLengthRejected": True,
    "ConcurrentDisposeRejectedLaterUse": True,
}
failures = {key: result.get(key) for key, value in expected.items() if result.get(key) != value}
if failures:
    raise SystemExit(f"Manifold linux-x64 assertions failed: {failures}")
print(f"Manifold linux-x64 passed {len(expected)} native call assertions.")
print(json.dumps(result, sort_keys=True))
PYTHON
