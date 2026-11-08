#!/usr/bin/env bash
# Builds PersonalBalancePatch in RELEASE mode (without deploying anywhere) and zips a Thunderstore package.
# Output: dist/<name>-<version>.zip  (files at the zip root, DLL under plugins/)
set -euo pipefail
cd "$(dirname "$0")"

NAME=$(python3 -I -c 'import json;print(json.load(open("package/manifest.json"))["name"])')
VER=$(python3 -I -c 'import json;print(json.load(open("package/manifest.json"))["version_number"])')
CODE_VER=$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' PersonalBalancePatch.cs)

# The mod's compatibility check compares PluginVersion, so it must equal the published version.
if [ "$VER" != "$CODE_VER" ]; then
    echo "Version mismatch: package/manifest.json says $VER but PluginVersion in the code is $CODE_VER." >&2
    exit 1
fi

for f in manifest.json README.md icon.png; do
    [ -f "package/$f" ] || { echo "Missing package/$f" >&2; exit 1; }
done

# R2Profile=NoSuchProfile makes the deploy step skip, so packaging never touches an r2modman profile.
./build.sh -c Release -p:R2Profile=NoSuchProfile

DLL="bin/Release/netstandard2.1/$NAME.dll"
[ -f "$DLL" ] || { echo "Build output not found: $DLL" >&2; exit 1; }

STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT
cp package/manifest.json package/README.md package/icon.png "$STAGE/"
[ -f package/CHANGELOG.md ] && cp package/CHANGELOG.md "$STAGE/"
mkdir -p "$STAGE/plugins"
cp "$DLL" "$STAGE/plugins/"

mkdir -p dist
OUT="$PWD/dist/$NAME-$VER.zip"
rm -f "$OUT"
(cd "$STAGE" && zip -r -q "$OUT" .)

echo
echo "Built: $OUT"
unzip -l "$OUT"
