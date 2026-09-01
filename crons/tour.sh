#!/usr/bin/env bash
# Un cron, un rôle. Si la boîte est vide : exit 0, pas d'agent.
# Personne n'appelle le cron suivant.
set -euo pipefail

ROLE="${1:?usage: tour.sh <briefer|planifier|coder|relire>}"
PROJET="${ATELIER_PROJET:-/srv/ForgeHistory}"
ATELIER="${ATELIER_ROOT:-$(cd "$(dirname "$0")/.." && pwd)}"
export PYTHONPATH="${ATELIER}${PYTHONPATH:+:$PYTHONPATH}"

cd "$PROJET"
carte="$(python3 -m atelier prochain --projet "$PROJET" --role "$ROLE")"
if [[ "$carte" == "RIEN" ]]; then
    exit 0
fi

echo "carte $ROLE : $carte"

# Sans ATELIER_INVOQUER=1 on n'appelle aucun modèle : on imprime
# l'invocation. C'est le mode qui n'a pas brûlé le lot 035.
case "$ROLE" in
    briefer)
        echo "claude -p \"Écris le brief de \$carte. Ne code rien.\""
        ;;
    planifier)
        echo "agent -p \"Planifie \$carte. N'écris pas le code.\" --model cursor-grok-4.6"
        ;;
    coder)
        echo "agent -p \"Exécute le brief de \$carte. Seule source d'instruction.\" --model composer-2.5"
        ;;
    relire)
        echo "claude -p \"Relis la PR de \$carte. Tu n'as pas écrit ce code, tu ne le corriges pas.\""
        ;;
esac

if [[ "${ATELIER_INVOQUER:-0}" != "1" ]]; then
    echo "ATELIER_INVOQUER n'est pas posé : aucun agent lancé."
    exit 0
fi

echo "invocation réelle : à brancher sur le binaire une fois le VPS authentifié." >&2
exit 0
