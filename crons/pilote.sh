#!/usr/bin/env bash
# Hermes : dépose une carte ou s'arrête. N'invoque pas Claude ni Cursor.
set -euo pipefail
PROJET="${ATELIER_PROJET:-/srv/ForgeHistory}"
cd "$PROJET"

if ! command -v hermes >/dev/null 2>&1; then
    echo "hermes absent — RIEN" >&2
    exit 0
fi

hermes -p "Tu es le pilote de ForgeHistory. Tu ne codes pas, tu ne fusionnes pas, tu n'invoques ni claude ni agent. Lis ROADMAP.md. S'il manque un brief pour un lot au périmètre libre, exécute python3 -m atelier deposer --projet $PROJET --etat a-briefer --lot NNN-slug --brief briefs/NNN-slug.md et arrête-toi. Sinon écris RIEN et arrête-toi."
