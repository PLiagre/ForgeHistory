#!/usr/bin/env bash
# Veille : aucun modèle. Une fumée, ou le silence.
# L'atelier ne sait pas ce que le produit fabrique : la commande de
# fumée vient de son atelier.toml, pas d'ici.
# Un échec ici n'empêche pas les autres crons : ils ne s'appellent pas.
set -u
PROJET="${ATELIER_PROJET:-/srv/ForgeHistory}"
ATELIER="${ATELIER_ROOT:-$(cd "$(dirname "$0")/.." && pwd)}"
export PYTHONPATH="${ATELIER}${PYTHONPATH:+:$PYTHONPATH}"
cd "$PROJET" || exit 0

# Une absence se déclare. Un branchement introuvable n'est pas un
# succès : sans lui, la veille ne mesure rien du tout.
if ! fumee="$(python3 -m atelier fumee --projet "$PROJET")"; then
    echo "veille : branchement illisible ($PROJET/atelier.toml) — rien n'a été mesuré." >&2
    exit 1
fi
eval "$fumee" >/dev/null
