#!/usr/bin/env bash
# Builds PersonalBalancePatch against the NuGet reference package (build mode) and, if the r2modman
# 'Dev' profile has BepInEx installed, copies the DLL into it. Extra args go to dotnet build.
set -euo pipefail
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
cd "$(dirname "$0")"
dotnet build PersonalBalancePatch.csproj -p:R2_BUILD=true "$@"
