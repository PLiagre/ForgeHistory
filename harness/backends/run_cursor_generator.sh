#!/usr/bin/env bash
# Wrapper facultatif pour exécuter un brief avec le CLI Cursor.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BRIEF_DIR="${1:-}"

if [[ -z "$BRIEF_DIR" || ! -d "$BRIEF_DIR" ]]; then
  echo "usage: bash harness/backends/run_cursor_generator.sh <dossier_du_brief>" >&2
  exit 2
fi

BRIEF_DIR="$(cd "$BRIEF_DIR" && pwd)"
CURSOR_AGENT_BIN=""
for candidate in cursor-agent cursor-agent.cmd cursor-agent.exe; do
  if command -v "$candidate" >/dev/null 2>&1; then
    CURSOR_AGENT_BIN="$candidate"
    break
  fi
done

if [[ -z "$CURSOR_AGENT_BIN" ]]; then
  echo "ERREUR : cursor-agent est absent du PATH." >&2
  exit 2
fi

PROMPT_FILE="$(mktemp)"
trap 'rm -f "$PROMPT_FILE"' EXIT

{
  echo "Réalise la tâche décrite ci-dessous dans le dépôt ForgeHistory."
  echo "Tu peux planifier, modifier, tester, documenter et relire le résultat."
  echo "Respecte AGENTS.md, préserve les changements locaux sans rapport et"
  echo "termine par un résumé des fichiers modifiés et des tests exécutés."
  echo "Les clauses historiques de rôle, d'identité, de relecture séparée ou"
  echo "de porte présentes dans un ancien lot sont obsolètes ; AGENTS.md prime."
  echo
  echo "## Règles communes"
  sed -n '1,240p' "$REPO_ROOT/AGENTS.md"
  echo
  echo "## Tâche"
  sed -n '1,400p' "$BRIEF_DIR/brief.md"
  if [[ -f "$BRIEF_DIR/eval-rubric.md" ]]; then
    echo
    echo "## Critères historiques disponibles"
    sed -n '1,300p' "$BRIEF_DIR/eval-rubric.md"
  fi
} > "$PROMPT_FILE"

mkdir -p "$BRIEF_DIR/deliverables"
OUT_JSON="$BRIEF_DIR/cursor-run.json"
ERR_LOG="$BRIEF_DIR/cursor-run.err"

"$CURSOR_AGENT_BIN" -p \
  --workspace "$REPO_ROOT" \
  --force \
  --output-format json \
  --model "${CURSOR_MODEL:-auto}" \
  < "$PROMPT_FILE" \
  > "$OUT_JSON" 2> "$ERR_LOG"

echo "Exécution terminée : $OUT_JSON"
