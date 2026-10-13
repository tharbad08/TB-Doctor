#!/usr/bin/env bash
set -euo pipefail

GAME_ROOT="${TIMBERBORN_GAME_ROOT:-$HOME/.local/share/Steam/steamapps/common/Timberborn}"
WORKSHOP_ROOT="${TIMBERBORN_WORKSHOP_ROOT:-$HOME/.local/share/Steam/steamapps/workshop/content/1062090}"
MANAGED="$GAME_ROOT/Timberborn_Data/Managed"

if [[ ! -d "$MANAGED" ]]; then
  echo "Timberborn Managed folder not found: $MANAGED" >&2
  exit 1
fi

HARMONY_DLL="$(find "$WORKSHOP_ROOT/3284904751" -type f -iname '0Harmony.dll' -print -quit 2>/dev/null || true)"
if [[ -z "$HARMONY_DLL" ]]; then
  HARMONY_DLL="$(find "$WORKSHOP_ROOT" -type f -iname '0Harmony.dll' -print -quit 2>/dev/null || true)"
fi
if [[ -z "$HARMONY_DLL" ]]; then
  echo "0Harmony.dll not found under $WORKSHOP_ROOT" >&2
  exit 1
fi

MODDING_DLL="$(find "$MANAGED" -maxdepth 1 -type f \( -iname 'Timberborn.Modding.dll' -o -iname 'Timberborn.ModManagerScene.dll' \) -print -quit)"
if [[ -z "$MODDING_DLL" ]]; then
  echo "Could not locate the Timberborn assembly that defines IModStarter in $MANAGED" >&2
  echo "Run: grep -ril 'IModStarter' '$MANAGED'  # or inspect with ilspy" >&2
  exit 1
fi

ROOT="$(cd "$(dirname "$0")" && pwd)"
OUT="$ROOT/Code.dll"
SRC="$ROOT/Source/DemolitionJobDoctor.cs"

if command -v csc >/dev/null 2>&1; then
  csc -nologo -target:library -optimize+ -out:"$OUT" \
    -reference:"$HARMONY_DLL" \
    -reference:"$MODDING_DLL" \
    "$SRC"
elif command -v mcs >/dev/null 2>&1; then
  mcs -target:library -optimize+ -out:"$OUT" \
    -r:"$HARMONY_DLL" \
    -r:"$MODDING_DLL" \
    "$SRC"
else
  echo "No C# compiler found. Install dotnet-sdk (csc) or mono (mcs), then rerun." >&2
  exit 1
fi

echo "Built: $OUT"
