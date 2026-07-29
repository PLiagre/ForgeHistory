#!/usr/bin/env bash
# harness/backends/run_cursor_generator.sh -- delegate the Générateur role to
# Cursor's headless CLI (cursor-agent). Modeled on the subprocess + prompt
# file + handoff/status shape of ECC's scripts/orchestrate-codex-worker.sh.
#
# Usage: bash harness/backends/run_cursor_generator.sh <brief_dir>
#
# Contract (see harness/backends/README.md): writes
# <brief_dir>/deliverables/manifest.json and
# <brief_dir>/deliverables/generator-log.md (Author: forge-generateur-cursor).
# Never writes verdict.md -- the Générateur does not judge its own work.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

BRIEF_DIR="${1:-}"
if [ -z "$BRIEF_DIR" ]; then
  echo "usage: bash harness/backends/run_cursor_generator.sh <brief_dir>" >&2
  exit 2
fi
if [ ! -d "$BRIEF_DIR" ]; then
  echo "ERROR: $BRIEF_DIR is not a directory" >&2
  exit 2
fi
BRIEF_DIR="$(cd "$BRIEF_DIR" && pwd)"

# --- Hard-won rule 9: an impossibility is tested before being invoked ---
# (a command + an error message, never a silent skip)

if ! command -v cursor-agent >/dev/null 2>&1; then
  echo "ERROR: cursor-agent not found on PATH." >&2
  echo "Install it: curl https://cursor.com/install -fsSL | bash" >&2
  echo "(or, on Windows: irm 'https://cursor.com/install?win32=true' | iex)" >&2
  exit 2
fi

if [ -z "${CURSOR_API_KEY:-}" ]; then
  echo "WARNING: CURSOR_API_KEY is not set. If 'cursor-agent login' has not" >&2
  echo "been run in this environment, the invocation below will fail with" >&2
  echo "an auth error rather than silently skipping." >&2
fi

# --- Build the prompt: brief + rubric + rules + role instructions ---

PROMPT_FILE="$BRIEF_DIR/cursor-prompt.md"
{
  echo "You are the Générateur in ForgeHistory's three-role harness."
  echo "Read the following brief, rubric, and rules, then implement the"
  echo "brief. Write deliverables/manifest.json and"
  echo "deliverables/generator-log.md with \`**Author**: forge-generateur-cursor\`."
  echo "Do NOT write verdict.md -- you do not judge your own work; a"
  echo "separate Évaluateur does that independently."
  echo
  echo "## Brief"
  cat "$BRIEF_DIR/brief.md"
  echo
  echo "## Evaluation Rubric"
  cat "$BRIEF_DIR/eval-rubric.md"
  echo
  echo "## Hard-Won Rules (must obey all of these)"
  cat "$REPO_ROOT/docs/rules/hard-won-rules.md"
  echo
  echo "## Simulation Principles"
  cat "$REPO_ROOT/docs/rules/simulation-principles.md"
} > "$PROMPT_FILE"

# --- Invoke cursor-agent headless ---

OUT_JSON="$BRIEF_DIR/cursor-run.json"
ERR_LOG="$BRIEF_DIR/cursor-run.err"
STATUS_FILE="$BRIEF_DIR/deliverables/backend-status.md"
mkdir -p "$BRIEF_DIR/deliverables"

set +e
cursor-agent -p "$(cat "$PROMPT_FILE")" \
  --workspace "$BRIEF_DIR" \
  --force \
  --output-format json \
  --model "${CURSOR_MODEL:-auto}" \
  > "$OUT_JSON" 2> "$ERR_LOG"
CURSOR_EXIT=$?
set -e

IS_ERROR="unknown"
if command -v py >/dev/null 2>&1 && [ -s "$OUT_JSON" ]; then
  IS_ERROR="$(py -c "import json,sys; d=json.load(open(sys.argv[1])); print(d.get('is_error', 'unknown'))" "$OUT_JSON" 2>/dev/null || echo "unparseable")"
fi

if [ "$CURSOR_EXIT" -eq 0 ] && [ "$IS_ERROR" != "True" ] && [ "$IS_ERROR" != "true" ]; then
  {
    echo "# Backend Status -- Cursor"
    echo
    echo "**Status**: completed"
    echo "**Exit code**: $CURSOR_EXIT"
    echo "**stdout (JSON)**: $(basename "$OUT_JSON")"
    echo "**stderr**: $(basename "$ERR_LOG")"
  } > "$STATUS_FILE"
  py "$REPO_ROOT/harness/backends/ledger.py" append --backend cursor --brief "$BRIEF_DIR" --event generator-run || true
  echo "Cursor run completed. See $OUT_JSON and $STATUS_FILE"
  exit 0
else
  {
    echo "# Backend Status -- Cursor"
    echo
    echo "**Status**: failed"
    echo "**Exit code**: $CURSOR_EXIT"
    echo "**is_error**: $IS_ERROR"
    echo "**stdout (JSON)**: $(basename "$OUT_JSON")"
    echo "**stderr**: $(basename "$ERR_LOG")"
  } > "$STATUS_FILE"
  echo "ERROR: cursor-agent run failed (exit=$CURSOR_EXIT, is_error=$IS_ERROR)." >&2
  echo "See $ERR_LOG and $STATUS_FILE" >&2
  exit 1
fi
