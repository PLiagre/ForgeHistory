#!/usr/bin/env bash
# harness/backends/run_codex_generator.sh -- delegate the Générateur role to
# Codex CLI's stable non-interactive `codex exec` command.
#
# Usage: bash harness/backends/run_codex_generator.sh <brief_dir> [extra_dirs_colon_separated]
#
# Contract: writes <brief_dir>/deliverables/manifest.json and
# <brief_dir>/deliverables/generator-log.md with Author:
# forge-generateur-codex. Never writes verdict.md.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

BRIEF_DIR="${1:-}"
if [ -z "$BRIEF_DIR" ]; then
  echo "usage: bash harness/backends/run_codex_generator.sh <brief_dir> [extra_dirs_colon_separated]" >&2
  exit 2
fi
CODEX_EXTRA_DIRS="${2:-${CODEX_EXTRA_DIRS:-}}"
if [ ! -d "$BRIEF_DIR" ]; then
  echo "ERROR: $BRIEF_DIR is not a directory" >&2
  exit 2
fi
BRIEF_DIR="$(cd "$BRIEF_DIR" && pwd)"

# Cross-platform launcher. `py` is mandatory on the project's Windows host;
# `python3` is the real interpreter name on Linux/WSL runners and is not the
# forbidden Microsoft Store `python` alias.
if command -v py >/dev/null 2>&1; then
  PY_LAUNCHER="py"
elif command -v python3 >/dev/null 2>&1; then
  PY_LAUNCHER="python3"
else
  echo "ERROR: neither py nor python3 is available." >&2
  exit 2
fi

# SC11: first action capable of refusing, before mktemp, mkdir or any write in
# the repository. This calls the exact check corrected by Lot 010a; it does
# not reimplement actor comparison in shell.
"$PY_LAUNCHER" "$REPO_ROOT/harness/backends/codex_preflight.py" "$BRIEF_DIR"

CODEX_BIN=""
# Windows desktop bundles both an extensionless Linux ELF and a native PE.
# Prefer the PE there; Unix hosts naturally fall through to `codex`.
for candidate in codex.exe codex codex.cmd; do
  if command -v "$candidate" >/dev/null 2>&1; then
    CODEX_BIN="$candidate"
    break
  fi
done
if [ -z "$CODEX_BIN" ]; then
  echo "ERROR: Codex CLI not found on PATH (checked codex.exe, codex, codex.cmd)." >&2
  echo "Official reference: https://developers.openai.com/codex/cli/reference/" >&2
  exit 2
fi

CODEX_ADD_DIR_ARGS=()
if [ -n "${CODEX_EXTRA_DIRS:-}" ]; then
  IFS=':' read -ra _codex_extra_dirs <<< "$CODEX_EXTRA_DIRS"
  for _dir in "${_codex_extra_dirs[@]}"; do
    CODEX_ADD_DIR_ARGS+=(--add-dir "$_dir")
  done
fi

mkdir -p "$BRIEF_DIR/deliverables"
PROMPT_FILE="$(mktemp)"
cleanup_prompt() {
  rm -f "$PROMPT_FILE"
}
trap cleanup_prompt EXIT INT TERM HUP QUIT

{
  echo "You are the Générateur in ForgeHistory's three-role harness."
  echo "Implement the brief and write deliverables/manifest.json plus"
  echo "deliverables/generator-log.md with **Author**: forge-generateur-codex."
  echo "Never write verdict.md and never judge your own work."
  echo
  echo "Workspace root: $REPO_ROOT"
  echo "Brief directory: $BRIEF_DIR"
  echo
  echo "## Brief"
  cat "$BRIEF_DIR/brief.md"
  echo
  echo "## Evaluation Rubric"
  cat "$BRIEF_DIR/eval-rubric.md"
  echo
  echo "## Hard-Won Rules"
  cat "$REPO_ROOT/docs/rules/hard-won-rules.md"
  echo
  echo "## Simulation Principles"
  cat "$REPO_ROOT/docs/rules/simulation-principles.md"
} > "$PROMPT_FILE"

OUT_JSONL="$BRIEF_DIR/codex-run.jsonl"
ERR_LOG="$BRIEF_DIR/codex-run.err"
STATUS_FILE="$BRIEF_DIR/deliverables/backend-status-codex.md"

set +e
# Official CLI reference: `codex exec` is the stable non-interactive command;
# `-` reads the prompt from stdin, `--json` emits JSONL, and workspace-write
# grants repository edits without bypassing the sandbox.
"$CODEX_BIN" exec \
  --cd "$REPO_ROOT" \
  --sandbox workspace-write \
  --json \
  "${CODEX_ADD_DIR_ARGS[@]}" \
  - < "$PROMPT_FILE" > "$OUT_JSONL" 2> "$ERR_LOG"
CODEX_EXIT=$?
set -e

if [ "$CODEX_EXIT" -eq 0 ] \
  && [ -s "$BRIEF_DIR/deliverables/manifest.json" ] \
  && grep -q '^\*\*Author\*\*: forge-generateur-codex$' "$BRIEF_DIR/deliverables/generator-log.md"; then
  {
    echo "# Backend Status -- Codex"
    echo
    echo "**Status**: completed"
    echo "**Exit code**: $CODEX_EXIT"
    echo "**stdout (JSONL)**: $(basename "$OUT_JSONL")"
    echo "**stderr**: $(basename "$ERR_LOG")"
  } > "$STATUS_FILE"
  "$PY_LAUNCHER" "$REPO_ROOT/harness/backends/ledger.py" append \
    --backend codex --brief "$BRIEF_DIR" --event generator-run
  echo "Codex run completed. See $OUT_JSONL and $STATUS_FILE"
  exit 0
fi

{
  echo "# Backend Status -- Codex"
  echo
  echo "**Status**: failed"
  echo "**Exit code**: $CODEX_EXIT"
  echo "**stdout (JSONL)**: $(basename "$OUT_JSONL")"
  echo "**stderr**: $(basename "$ERR_LOG")"
} > "$STATUS_FILE"
"$PY_LAUNCHER" "$REPO_ROOT/harness/backends/ledger.py" append \
  --backend codex --brief "$BRIEF_DIR" --event generator-run-failed || true
echo "ERROR: Codex run failed or did not produce the required deliverables (exit=$CODEX_EXIT)." >&2
echo "See $ERR_LOG and $STATUS_FILE" >&2
exit 1
