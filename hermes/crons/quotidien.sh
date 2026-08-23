#!/usr/bin/env bash
# Cron quotidien Hermes — script seul, aucune invocation de modèle.
# Contrat : hermes/crons/README.md | ADR-0016
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PYTHON="${ROOT}/.venv/bin/python"
if [[ ! -x "$PYTHON" ]]; then
    PYTHON="$(command -v python3)"
fi

# veille.py ne produit rien quand tout va bien. Une erreur ou une alerte est
# la seule sortie destinée au journal cron / relais Discord du contrôleur.
exec "$PYTHON" "$ROOT/hermes/crons/veille.py" \
    --repo "$ROOT" \
    --output "hermes/propositions/DERNIERE-VEILLE.md"
