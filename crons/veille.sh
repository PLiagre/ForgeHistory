#!/usr/bin/env bash
# Veille : aucun modèle. Une fumée, ou le silence.
# Un échec ici n'empêche pas les autres crons : ils ne s'appellent pas.
set -u
PROJET="${ATELIER_PROJET:-/srv/ForgeHistory}"
cd "$PROJET" || exit 0
python3 -m sim --ticks 0 --json >/dev/null
