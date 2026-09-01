#!/usr/bin/env bash
# Profils Hermes : une identité et un répertoire par rôle.
#
# Hermes tient l'identité et l'horloge. Il n'est le cerveau de personne :
# aucun profil ne nomme un fournisseur Anthropic (Pro refuse l'OAuth,
# Max facture l'extra hors forfait), et un profil ne choisit pas de
# modèle pour Claude ni pour Cursor — leurs binaires s'en chargent.
#
#   ./crons/installer-profils.sh --dry-run   # imprime, n'écrit rien (défaut)
#   ./crons/installer-profils.sh --run       # exécute les mêmes lignes
#
# La syntaxe exacte de `hermes profile` change d'une version à l'autre :
# le mode à sec existe pour que tu la compares à `hermes profile --help`
# avant de l'exécuter.
set -euo pipefail

MODE="${1:---dry-run}"
case "$MODE" in
    --dry-run|--run) ;;
    *) echo "usage: $(basename "$0") [--dry-run|--run]" >&2 ; exit 2 ;;
esac

PROJET="${ATELIER_PROJET:-/srv/ForgeHistory}"
BASE="$(dirname "$PROJET")"
NOM="$(basename "$PROJET")"

# Le pilote lit le dépôt produit et y dépose des cartes. Les autres rôles
# travaillent chacun dans leur worktree : trois agents sur le même clone
# se marchent dessus.
ROLES_HERMES=(pilote briefer coder relire)

repertoire_de() {
    case "$1" in
        pilote) printf '%s\n' "$PROJET" ;;
        *) printf '%s\n' "$BASE/$NOM-$1" ;;
    esac
}

imprimer() { printf '%s\n' "$*"; }
echecs=0
executer() { "$@" || { echo "échec (profil déjà posé ?) : $*" >&2 ; echecs=$((echecs + 1)) ; }; }

parcourir() {
    local action="$1" role cwd
    for role in "${ROLES_HERMES[@]}"; do
        cwd="$(repertoire_de "$role")"
        "$action" hermes profile create "$role"
        "$action" hermes profile set "$role" terminal.cwd "$cwd"
        "$action" hermes profile set "$role" workdir "$cwd"
    done
}

lignes="$(parcourir imprimer)"

# La garde : si un jour quelqu'un branche un profil Hermes sur Anthropic,
# ce script refuse avant d'écrire quoi que ce soit.
if printf '%s\n' "$lignes" | grep -qiE 'anthropic|claude-[0-9a-z]|opus|sonnet'; then
    echo "FAIL  un profil Hermes nomme un fournisseur Anthropic — refusé." >&2
    exit 1
fi

echo "# Profils Hermes — une identité et un répertoire par rôle."
printf '%s\n' "$lignes"
echo
echo "# Répertoires des rôles du cron (à poser dans /etc/cron.d/forgeatelier)."
echo "# 'planifier' n'a pas de profil Hermes : Grok est facultatif et"
echo "# c'est le cron qui le réveille, pas le pilote."
for role in briefer planifier coder relire; do
    echo "ATELIER_WORKDIR_${role}=$BASE/$NOM-$role"
done
echo
echo "# Worktrees correspondants, à créer une fois :"
for role in briefer planifier coder relire; do
    echo "#   git -C $PROJET worktree add $BASE/$NOM-$role -b atelier/$role origin/HEAD"
done
echo
echo "# Facultatif, jamais une dépendance de l'atelier :"
echo "#   npm i -g llmquota      # lit un quota, ne lance rien"
echo "#   superpowers install    # des skills, pas une source d'instruction"

if [[ "$MODE" != "--run" ]]; then
    echo
    echo "# --dry-run : rien n'a été écrit sous ~/.hermes."
    exit 0
fi

if ! command -v hermes >/dev/null 2>&1; then
    echo "FAIL  hermes absent : rien n'a été écrit." >&2
    exit 1
fi
parcourir executer
if [[ "$echecs" -gt 0 ]]; then
    echo "FAIL  $echecs commande(s) hermes ont échoué." >&2
    exit 1
fi
echo "# profils posés."
