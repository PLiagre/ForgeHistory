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

# On Windows, the cursor-agent installer updates the user's PATH via the
# registry, which an already-running shell (like this one) never sees --
# append its known install directory here if present, so callers don't need
# to prefix every invocation with their own `export PATH=...` (which also
# makes the invoking command harder to allow-list by prefix).
if [ -d "$HOME/AppData/Local/cursor-agent" ]; then
  PATH="$PATH:$HOME/AppData/Local/cursor-agent"
fi

BRIEF_DIR="${1:-}"
if [ -z "$BRIEF_DIR" ]; then
  echo "usage: bash harness/backends/run_cursor_generator.sh <brief_dir> [extra_dirs_colon_separated]" >&2
  exit 2
fi
# CURSOR_EXTRA_DIRS may come from the environment (back-compat) or as a second
# positional argument -- the latter keeps the whole invocation a single plain
# command starting with this script's own path, which is what a Bash
# permission allow-rule prefix-matches against.
CURSOR_EXTRA_DIRS="${2:-${CURSOR_EXTRA_DIRS:-}}"
if [ ! -d "$BRIEF_DIR" ]; then
  echo "ERROR: $BRIEF_DIR is not a directory" >&2
  exit 2
fi
BRIEF_DIR="$(cd "$BRIEF_DIR" && pwd)"

# --- Hard-won rule 9: an impossibility is tested before being invoked ---
# (a command + an error message, never a silent skip)

# On Windows, the installer places cursor-agent.cmd/.ps1 shims, not a bare
# `cursor-agent` -- Git Bash's `command -v` (and direct exec) won't resolve
# the bare name to a .cmd file, so try each real variant explicitly.
CURSOR_AGENT_BIN=""
for candidate in cursor-agent cursor-agent.cmd cursor-agent.exe; do
  if command -v "$candidate" >/dev/null 2>&1; then
    CURSOR_AGENT_BIN="$candidate"
    break
  fi
done

if [ -z "$CURSOR_AGENT_BIN" ]; then
  echo "ERROR: cursor-agent not found on PATH (checked cursor-agent, cursor-agent.cmd, cursor-agent.exe)." >&2
  echo "Install it: curl https://cursor.com/install -fsSL | bash" >&2
  echo "(or, on Windows: irm 'https://cursor.com/install?win32=true' | iex)" >&2
  exit 2
fi

if [ -z "${CURSOR_API_KEY:-}" ]; then
  echo "WARNING: CURSOR_API_KEY is not set. If 'cursor-agent login' has not" >&2
  echo "been run in this environment, the invocation below will fail with" >&2
  echo "an auth error rather than silently skipping." >&2
fi

# A brief usually needs to write outside its own harness/queue/briefs/NNN-*/
# directory (e.g. pipeline/geo/, docs/adr/) -- so the workspace root is the
# repo, not the brief directory. A brief that also needs to READ from a
# directory outside this repo entirely (e.g. porting from a sibling project)
# can request that via CURSOR_EXTRA_DIRS, a `:`-separated list of absolute
# paths, one --add-dir per entry -- not hardcoded here, since it's specific
# to whichever brief is running.
CURSOR_ADD_DIR_ARGS=()
if [ -n "${CURSOR_EXTRA_DIRS:-}" ]; then
  IFS=':' read -ra _cursor_extra_dirs <<< "$CURSOR_EXTRA_DIRS"
  for _d in "${_cursor_extra_dirs[@]}"; do
    CURSOR_ADD_DIR_ARGS+=(--add-dir "$_d")
  done
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
  echo "Your workspace root is the repository at: $REPO_ROOT"
  echo "This brief's own directory (brief.md, eval-rubric.md, deliverables/) is at:"
  echo "$BRIEF_DIR"
  echo "Write/read files anywhere under the workspace root using paths relative"
  echo "to it, or absolute paths -- you are not confined to the brief directory."
  if [ -n "${CURSOR_EXTRA_DIRS:-}" ]; then
    echo "You have additionally been granted read/write access to:"
    IFS=':' read -ra _cursor_extra_dirs_prompt <<< "$CURSOR_EXTRA_DIRS"
    for _d in "${_cursor_extra_dirs_prompt[@]}"; do
      echo "- $_d"
    done
  fi
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

# --- Confirmed cursor-agent bug (2026-07-29): every PreToolUse/Stop hook in
# .claude/settings.json fails under cursor-agent, not because of what the
# hook's own logic checks, but because cursor-agent wraps the hook command in
# a PowerShell-syntax string ($OutputEncoding=...; ... | & { $input | <cmd> })
# and then runs that string through a POSIX `eval`, which cannot parse
# PowerShell's `&` call operator: "eval: line 1: syntax error near unexpected
# token `&'". This blocks the agent's very first Bash/Write call, regardless
# of what that call is -- confirmed by testing an allowed command (`py
# --version`), which failed identically. The command text in settings.json is
# already POSIX-portable; the bug is entirely inside cursor-agent's own
# hook-invocation wrapper, not fixable from this repo.
#
# Per the project owner's explicit decision: hooks are temporarily moved
# aside for the duration of this invocation only, and restored unconditionally
# via `trap` (covers normal exit, error exit, and interruption) -- never left
# disabled. The mechanical gate (verdict_audit.py) and the Évaluateur are the
# safety net for this run instead of the hooks.
SETTINGS_JSON="$REPO_ROOT/.claude/settings.json"
SETTINGS_JSON_PARKED="$REPO_ROOT/.claude/settings.json.cursor-hook-bug-disabled"
_settings_restored=0
restore_settings_json() {
  if [ "$_settings_restored" -eq 0 ] && [ -f "$SETTINGS_JSON_PARKED" ]; then
    mv "$SETTINGS_JSON_PARKED" "$SETTINGS_JSON"
    _settings_restored=1
    echo "Restored .claude/settings.json (hooks re-enabled)." >&2
  fi
}
trap restore_settings_json EXIT INT TERM
if [ -f "$SETTINGS_JSON" ]; then
  mv "$SETTINGS_JSON" "$SETTINGS_JSON_PARKED"
  echo "Parked .claude/settings.json for this Cursor invocation (hooks break under cursor-agent -- see comment above); will restore on exit." >&2
fi

set +e
# Prompt is piped via stdin, not passed as a positional argument -- on
# Windows, a prompt this size (brief + rubric + both rule docs) blows past
# cmd.exe's command-line length limit ("La ligne de commande est trop
# longue") when passed as argv, since the .cmd shim re-invokes through
# cmd.exe. Confirmed working via stdin instead.
"$CURSOR_AGENT_BIN" -p \
  --workspace "$REPO_ROOT" \
  "${CURSOR_ADD_DIR_ARGS[@]}" \
  --force \
  --output-format json \
  --model "${CURSOR_MODEL:-auto}" \
  < "$PROMPT_FILE" \
  > "$OUT_JSON" 2> "$ERR_LOG"
CURSOR_EXIT=$?
set -e

# PROMPT_FILE embeds brief.md's raw content (headings included) so cursor-agent
# can read it -- but a file lingering in the brief directory with those same
# headings outside brief.md itself trips
# test_no_paraphrased_brief_headings_outside_brief_md (single source of
# instruction). It's purely a transmission artifact, fully redundant with
# brief.md + eval-rubric.md which remain on disk; delete it once sent.
rm -f "$PROMPT_FILE"

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
