#!/usr/bin/env bash
# Cron quotidien Hermes — lecture, mesure, proposition. Jamais de fusion.
# Contrat : hermes/crons/README.md  |  ADR-0016
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

if [[ ! -f sim/__main__.py ]] || [[ ! -d hermes/crons ]]; then
    echo "refus : ce n'est pas la racine ForgeHistory ($ROOT)" >&2
    exit 2
fi

PYTHON="${ROOT}/.venv/bin/python"
if [[ ! -x "$PYTHON" ]]; then
    PYTHON="$(command -v python3)"
fi

STAMP="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
OUT="hermes/propositions/DERNIERE-VEILLE.md"
mkdir -p hermes/propositions

{
    echo "---"
    echo "author: hermes"
    echo "kind: proposition"
    echo "created_at: ${STAMP}"
    echo "concerns: projet"
    echo "status: OPEN"
    echo "---"
    echo "# Veille quotidienne — ${STAMP}"
    echo
    echo "Run automatique. Pas une instruction. Pas une fusion."
    echo
    echo "## Git"
    echo
    echo "- branche : \`$(git branch --show-current)\`"
    echo "- HEAD : \`$(git log -1 --oneline)\`"
    echo "- porcelain : \`$(git status --porcelain | wc -l) fichier(s)\`"
    echo
    echo "## Simulation sans Unity"
    echo
    echo '```'
    "$PYTHON" -m sim --json || echo "ECHEC python -m sim (code $?)"
    echo '```'
    echo
    echo "## Tests sim/"
    echo
    echo '```'
    if "$PYTHON" -m pytest sim/tests/ -q --tb=no; then
        echo "(pytest sim/ : OK)"
    else
        echo "ECHEC pytest sim/"
    fi
    echo '```'
    echo
    echo "## Vue projet"
    echo
    if [[ -f hermes/DASHBOARD.md ]]; then
        grep -m1 'Générée le' hermes/DASHBOARD.md || echo "date de génération introuvable"
    else
        echo "hermes/DASHBOARD.md absent"
    fi
    echo
    echo "Une proposition nommée \`PROPOSITION-*.md\` n'est ouverte que si un"
    echo "humain (Hermes en session) confirme un constat nouveau. Ce fichier"
    echo "est seulement la veille du jour."
} > "$OUT"

echo "veille écrite : $OUT"
